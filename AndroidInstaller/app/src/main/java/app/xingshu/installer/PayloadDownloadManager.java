package app.xingshu.installer;

import android.content.Context;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.RandomAccessFile;
import java.net.HttpURLConnection;
import java.net.URL;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;
import javax.net.ssl.HttpsURLConnection;

/**
 * Downloads verified XingShu payloads and PE workshop archives on demand from official HTTPS routes.
 * Stores verified files in app-internal filesDir/payload-cache keyed by lowercase SHA-256.
 */
final class PayloadDownloadManager {
    static final String OFFICIAL_HOST = "astralparty-xingshu.lanxichaxu.cn";
    static final long MAX_PAYLOAD_BYTES = 1024L * 1024L * 1024L;
    static final long PARALLEL_THRESHOLD_BYTES = 2L * 1024L * 1024L;
    static final int PARALLEL_THREADS = 4;
    private static final long PROGRESS_INTERVAL_NS = 80_000_000L;
    private static final long PROGRESS_STEP_BYTES = 64L * 1024L;

    interface ProgressListener {
        void onProgress(long receivedBytes, long totalBytes, long speedBytesPerSec, String stageText);
    }

    private PayloadDownloadManager() { }

    static File cacheDir(Context context) {
        return new File(context.getFilesDir(), "payload-cache");
    }

    static boolean isValidSha256(String sha256) {
        return sha256 != null && sha256.matches("[0-9a-fA-F]{64}");
    }

    static URL validateOfficialUrl(String urlStr) throws IOException {
        if (urlStr == null || urlStr.trim().isEmpty()) {
            throw new IOException("下载地址为空");
        }
        URL url = new URL(urlStr.trim());
        if (!"https".equalsIgnoreCase(url.getProtocol())
                || !OFFICIAL_HOST.equalsIgnoreCase(url.getHost())) {
            throw new SecurityException("拒绝从非官方 HTTPS 域名下载资源：" + url.getHost());
        }
        return url;
    }

    static File cacheFileForHash(File cacheDir, String expectedSha256) {
        if (!isValidSha256(expectedSha256)) {
            throw new IllegalArgumentException("无效的 SHA-256 校验值");
        }
        return new File(cacheDir, expectedSha256.toLowerCase(Locale.ROOT) + ".bin");
    }

    /** Fast existence & size check safe for UI refresh paths. */
    static boolean hasCachedFile(File cacheDir, String expectedSha256, long expectedSize) {
        if (cacheDir == null || !isValidSha256(expectedSha256)) return false;
        File target = cacheFileForHash(cacheDir, expectedSha256);
        if (!target.isFile()) return false;
        return expectedSize <= 0 || target.length() == expectedSize;
    }

    /** Full cryptographic verification of a cached file; deletes corrupted entries. */
    static File getCachedIfValid(File cacheDir, String expectedSha256, long expectedSize) {
        if (cacheDir == null || !isValidSha256(expectedSha256)) return null;
        File target = cacheFileForHash(cacheDir, expectedSha256);
        if (!target.isFile()) return null;
        if (expectedSize > 0 && target.length() != expectedSize) {
            target.delete();
            return null;
        }
        try {
            String actual = sha256File(target);
            if (expectedSha256.toLowerCase(Locale.ROOT).equals(actual)) {
                return target;
            }
        } catch (Exception ignored) { }
        target.delete();
        return null;
    }

    static File ensureDownloaded(
            File cacheDir,
            String urlStr,
            String expectedSha256,
            long expectedSize,
            String displayLabel,
            ProgressListener listener) throws Exception {
        if (!isValidSha256(expectedSha256)) {
            throw new IllegalArgumentException("清单缺少有效的 SHA-256");
        }
        if (expectedSize < 0 || expectedSize > MAX_PAYLOAD_BYTES) {
            throw new IOException("声明文件体积超出安全上限");
        }
        String label = (displayLabel == null || displayLabel.isEmpty()) ? "资源包" : displayLabel;
        File cached = getCachedIfValid(cacheDir, expectedSha256, expectedSize);
        if (cached != null) {
            if (listener != null) {
                long sz = cached.length();
                listener.onProgress(sz, sz, 0L, label + " 已命中本地缓存");
            }
            return cached;
        }

        if (!cacheDir.isDirectory() && !cacheDir.mkdirs()) {
            throw new IOException("无法创建缓存目录：" + cacheDir.getAbsolutePath());
        }

        URL url = validateOfficialUrl(urlStr);
        File target = cacheFileForHash(cacheDir, expectedSha256);
        if (listener != null) {
            listener.onProgress(0L, Math.max(0L, expectedSize), 0L, "正在连接 EdgeOne CDN 下载 " + label + "…");
        }

        long headLength = probeRangeLength(url, expectedSize);
        if (headLength >= PARALLEL_THRESHOLD_BYTES && headLength <= MAX_PAYLOAD_BYTES) {
            File parallelTmp = new File(cacheDir, expectedSha256.toLowerCase(Locale.ROOT) + "." + System.nanoTime() + ".part");
            try {
                if (tryDownloadParallelRanges(url, parallelTmp, headLength, label, listener)) {
                    if (listener != null) {
                        listener.onProgress(headLength, headLength, 0L, "正在校验 " + label + " SHA-256…");
                    }
                    String actualSha = sha256File(parallelTmp);
                    if (!expectedSha256.toLowerCase(Locale.ROOT).equals(actualSha)) {
                        throw new SecurityException("文件 SHA-256 校验未通过，已丢弃（期望 " + expectedSha256.substring(0, 8) + "，实际 " + actualSha.substring(0, 8) + "）");
                    }
                    if (target.exists() && !target.delete()) {
                        throw new IOException("无法替换旧缓存文件");
                    }
                    if (!parallelTmp.renameTo(target)) {
                        copyFile(parallelTmp, target);
                    }
                    if (listener != null) {
                        listener.onProgress(headLength, headLength, 0L, label + " 下载并校验完成");
                    }
                    return target;
                }
            } finally {
                if (parallelTmp.exists()) {
                    parallelTmp.delete();
                }
            }
        }

        IOException lastIo = null;
        for (int attempt = 0; attempt < 2; attempt++) {
            HttpsURLConnection conn = (HttpsURLConnection) url.openConnection();
            conn.setInstanceFollowRedirects(false);
            conn.setConnectTimeout(15000);
            conn.setReadTimeout(30000);
            conn.setRequestMethod("GET");
            conn.setRequestProperty("User-Agent", "XingShu-Android-Launcher/2");
            conn.setRequestProperty("Accept-Encoding", "identity");
            try {
                int code = conn.getResponseCode();
                if (code != HttpURLConnection.HTTP_OK) {
                    throw new IOException("下载 " + label + " 失败（HTTP " + code + "）");
                }
                long headerLen = conn.getContentLengthLong();
                if (headerLen > MAX_PAYLOAD_BYTES) {
                    throw new IOException("远端文件大小超过上限（1 GB）");
                }
                if (expectedSize > 0 && headerLen > 0 && headerLen != expectedSize) {
                    throw new IOException("远端文件大小与清单不一致（声明 " + expectedSize + " B，实际 " + headerLen + " B）");
                }
                long totalExpected = expectedSize > 0 ? expectedSize : headerLen;
                try (InputStream in = conn.getInputStream()) {
                    return downloadStreamToCache(in, cacheDir, target, expectedSha256, totalExpected, label, listener);
                }
            } catch (IOException ioEx) {
                lastIo = ioEx;
                if (attempt == 1) throw ioEx;
            } finally {
                conn.disconnect();
            }
        }
        throw lastIo != null ? lastIo : new IOException("下载 " + label + " 失败");
    }

    static long[][] computeRanges(long totalBytes, int parts) {
        if (totalBytes <= 0 || parts <= 0) {
            return new long[0][0];
        }
        int count = (int) Math.min(parts, totalBytes);
        long[][] ranges = new long[count][2];
        long chunk = totalBytes / count;
        long rem = totalBytes % count;
        long cursor = 0L;
        for (int i = 0; i < count; i++) {
            long len = chunk + (i < rem ? 1L : 0L);
            ranges[i][0] = cursor;
            ranges[i][1] = cursor + len - 1L;
            cursor += len;
        }
        return ranges;
    }

    static long probeRangeLength(URL url, long expectedSize) {
        if (url == null || !"https".equalsIgnoreCase(url.getProtocol())) return -1L;
        HttpsURLConnection conn = null;
        try {
            conn = (HttpsURLConnection) url.openConnection();
            conn.setInstanceFollowRedirects(false);
            conn.setConnectTimeout(8000);
            conn.setReadTimeout(10000);
            conn.setRequestMethod("HEAD");
            conn.setRequestProperty("User-Agent", "XingShu-Android-Launcher/2");
            conn.setRequestProperty("Accept-Encoding", "identity");
            if (conn.getResponseCode() != HttpURLConnection.HTTP_OK) return -1L;
            long len = conn.getContentLengthLong();
            if (len < PARALLEL_THRESHOLD_BYTES || len > MAX_PAYLOAD_BYTES) return -1L;
            if (expectedSize > 0 && len != expectedSize) return -1L;
            String ar = conn.getHeaderField("Accept-Ranges");
            if (ar == null || !ar.toLowerCase(Locale.ROOT).contains("bytes")) return -1L;
            return len;
        } catch (Exception ignored) {
            return -1L;
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    static boolean tryDownloadParallelRanges(
            URL url,
            File tmpFile,
            long totalSize,
            String displayLabel,
            ProgressListener listener) {
        if (totalSize < PARALLEL_THRESHOLD_BYTES || totalSize > MAX_PAYLOAD_BYTES) return false;
        long[][] ranges = computeRanges(totalSize, PARALLEL_THREADS);
        if (ranges.length <= 1) return false;
        String label = (displayLabel == null || displayLabel.isEmpty()) ? "资源包" : displayLabel;
        try {
            try (RandomAccessFile pre = new RandomAccessFile(tmpFile, "rw")) {
                pre.setLength(totalSize);
            }
            ExecutorService pool = Executors.newFixedThreadPool(ranges.length);
            AtomicLong totalReceived = new AtomicLong(0L);
            AtomicLong lastEmitNs = new AtomicLong(System.nanoTime());
            AtomicBoolean cancelled = new AtomicBoolean(false);
            long startedNs = System.nanoTime();
            try {
                List<Callable<Void>> tasks = new ArrayList<>(ranges.length);
                for (long[] slice : ranges) {
                    final long sliceStart = slice[0];
                    final long sliceEnd = slice[1];
                    tasks.add(() -> {
                        long sliceWritten = 0L;
                        long expectedSliceLen = sliceEnd - sliceStart + 1L;
                        for (int attempt = 0; attempt < 3; attempt++) {
                            if (cancelled.get()) throw new IOException("并发分片已取消");
                            long cursor = sliceStart + sliceWritten;
                            if (cursor > sliceEnd) break;
                            HttpsURLConnection c = (HttpsURLConnection) url.openConnection();
                            c.setInstanceFollowRedirects(false);
                            c.setConnectTimeout(10000);
                            c.setReadTimeout(20000);
                            c.setRequestMethod("GET");
                            c.setRequestProperty("User-Agent", "XingShu-Android-Launcher/2");
                            c.setRequestProperty("Accept-Encoding", "identity");
                            c.setRequestProperty("Range", "bytes=" + cursor + "-" + sliceEnd);
                            try {
                                int status = c.getResponseCode();
                                if (status != HttpURLConnection.HTTP_PARTIAL) {
                                    throw new IOException("分片响应码非 206：" + status);
                                }
                                long need = expectedSliceLen - sliceWritten;
                                byte[] buf = new byte[65536];
                                try (InputStream in = c.getInputStream();
                                     RandomAccessFile raf = new RandomAccessFile(tmpFile, "rw")) {
                                    raf.seek(cursor);
                                    int n;
                                    while (sliceWritten < expectedSliceLen && (n = in.read(buf, 0, (int) Math.min(buf.length, need))) != -1) {
                                        raf.write(buf, 0, n);
                                        sliceWritten += n;
                                        need -= n;
                                        long rx = totalReceived.addAndGet(n);
                                        if (listener != null) {
                                            long now = System.nanoTime();
                                            long prev = lastEmitNs.get();
                                            if (rx >= totalSize || (now - prev >= PROGRESS_INTERVAL_NS && lastEmitNs.compareAndSet(prev, now))) {
                                                long elapsed = Math.max(1L, now - startedNs);
                                                long spd = (rx * 1_000_000_000L) / elapsed;
                                                listener.onProgress(Math.min(rx, totalSize), totalSize, spd, "正在 4 路并发高速下载 " + label + "…");
                                            }
                                        }
                                    }
                                }
                                if (sliceWritten == expectedSliceLen) break;
                            } catch (Exception ex) {
                                if (attempt == 2) {
                                    cancelled.set(true);
                                    throw ex;
                                }
                            } finally {
                                c.disconnect();
                            }
                        }
                        if (sliceWritten != expectedSliceLen) {
                            cancelled.set(true);
                            throw new IOException("分片未下载完整");
                        }
                        return null;
                    });
                }
                List<Future<Void>> futures = pool.invokeAll(tasks);
                for (Future<Void> f : futures) {
                    f.get();
                }
                return totalReceived.get() == totalSize;
            } finally {
                pool.shutdownNow();
            }
        } catch (Exception ignored) {
            return false;
        }
    }

    /** Pure stream-to-cache verification helper usable in both device runtime and JVM unit tests. */
    static File downloadStreamToCache(
            InputStream in,
            File cacheDir,
            File target,
            String expectedSha256,
            long expectedSize,
            String displayLabel,
            ProgressListener listener) throws Exception {
        if (!isValidSha256(expectedSha256)) {
            throw new IllegalArgumentException("清单缺少有效的 SHA-256");
        }
        if (expectedSize > MAX_PAYLOAD_BYTES) {
            throw new IOException("声明文件体积超出安全上限");
        }
        if (!cacheDir.isDirectory() && !cacheDir.mkdirs()) {
            throw new IOException("无法创建缓存目录");
        }
        String label = (displayLabel == null || displayLabel.isEmpty()) ? "资源包" : displayLabel;
        String expectedLower = expectedSha256.toLowerCase(Locale.ROOT);
        File tmp = new File(cacheDir, expectedLower + "." + System.nanoTime() + ".tmp");

        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] buf = new byte[65536];
            long received = 0L;
            long startedNs = System.nanoTime();
            long lastEmitNs = startedNs;
            long lastEmitBytes = 0L;
            if (listener != null) {
                listener.onProgress(0L, Math.max(0L, expectedSize), 0L, "正在下载 " + label + "…");
            }

            try (FileOutputStream out = new FileOutputStream(tmp)) {
                int n;
                while ((n = in.read(buf)) != -1) {
                    received += n;
                    if (received > MAX_PAYLOAD_BYTES) {
                        throw new IOException("下载体积超出安全上限（1 GB）");
                    }
                    if (expectedSize > 0 && received > expectedSize) {
                        throw new IOException("下载内容超出声明大小，已中断");
                    }
                    digest.update(buf, 0, n);
                    out.write(buf, 0, n);
                    if (listener != null) {
                        long now = System.nanoTime();
                        if (received - lastEmitBytes >= PROGRESS_STEP_BYTES || now - lastEmitNs >= PROGRESS_INTERVAL_NS) {
                            long elapsedNs = Math.max(1L, now - startedNs);
                            long speed = (received * 1_000_000_000L) / elapsedNs;
                            lastEmitNs = now;
                            lastEmitBytes = received;
                            listener.onProgress(received, Math.max(received, expectedSize), speed, "正在下载 " + label + "…");
                        }
                    }
                }
                out.flush();
                try {
                    out.getFD().sync();
                } catch (IOException ignored) { }
            }

            if (received <= 0L) {
                throw new IOException("下载返回空内容，请重试");
            }
            if (expectedSize > 0 && received != expectedSize) {
                throw new IOException("下载中断，未收到完整文件（" + received + " / " + expectedSize + " B）");
            }
            if (listener != null) {
                listener.onProgress(received, received, 0L, "正在校验 " + label + " SHA-256…");
            }
            String actualSha = hex(digest.digest());
            if (!expectedLower.equals(actualSha)) {
                throw new SecurityException("文件 SHA-256 校验未通过，已丢弃（期望 " + expectedLower.substring(0, 8) + "，实际 " + actualSha.substring(0, 8) + "）");
            }
            if (target.exists() && !target.delete()) {
                throw new IOException("无法替换旧缓存文件");
            }
            if (!tmp.renameTo(target)) {
                copyFile(tmp, target);
            }
            if (listener != null) {
                listener.onProgress(received, received, 0L, label + " 下载并校验完成");
            }
            return target;
        } finally {
            if (tmp.exists()) {
                tmp.delete();
            }
        }
    }

    static long getCacheTotalBytes(File cacheDir) {
        if (cacheDir == null || !cacheDir.isDirectory()) return 0L;
        File[] files = cacheDir.listFiles();
        if (files == null) return 0L;
        long total = 0L;
        for (File f : files) {
            if (f.isFile()) total += f.length();
        }
        return total;
    }

    static long clearCache(File cacheDir) {
        if (cacheDir == null || !cacheDir.isDirectory()) return 0L;
        File[] files = cacheDir.listFiles();
        if (files == null) return 0L;
        long freed = 0L;
        for (File f : files) {
            if (f.isFile()) {
                long len = f.length();
                if (f.delete()) freed += len;
            }
        }
        return freed;
    }

    static String sha256File(File file) throws Exception {
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        try (InputStream in = new FileInputStream(file)) {
            byte[] buf = new byte[65536];
            int n;
            while ((n = in.read(buf)) != -1) {
                digest.update(buf, 0, n);
            }
        }
        return hex(digest.digest());
    }

    private static void copyFile(File src, File dst) throws IOException {
        try (FileInputStream in = new FileInputStream(src); FileOutputStream out = new FileOutputStream(dst)) {
            byte[] buf = new byte[65536];
            int n;
            while ((n = in.read(buf)) != -1) {
                out.write(buf, 0, n);
            }
            out.flush();
        }
    }

    static String hex(byte[] bytes) {
        StringBuilder sb = new StringBuilder(bytes.length * 2);
        for (byte b : bytes) {
            sb.append(String.format(Locale.ROOT, "%02x", b & 255));
        }
        return sb.toString();
    }

    static String formatBytes(long bytes) {
        if (bytes < 1024L) return bytes + " B";
        if (bytes < 1024L * 1024L) return String.format(Locale.US, "%.1f KB", bytes / 1024.0);
        return String.format(Locale.US, "%.2f MB", bytes / 1048576.0);
    }
}
