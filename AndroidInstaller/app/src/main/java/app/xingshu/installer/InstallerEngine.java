package app.xingshu.installer;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.RandomAccessFile;
import java.nio.channels.FileLock;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Deque;
import java.util.Enumeration;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.TimeUnit;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.zip.ZipEntry;
import java.util.zip.ZipException;
import java.util.zip.ZipFile;

/** Multi-channel privileged installer engine running under ADB (UID 2000) or Shizuku. */
final class InstallerEngine {
    private volatile CoreRuntimeSpec activeSpec = CoreRuntimeSpec.embedded();

    synchronized void setActiveSpec(CoreRuntimeSpec spec) {
        if (spec != null) {
            this.activeSpec = spec;
        }
    }

    synchronized String updateCoreSpec(String specJson) {
        try {
            CoreRuntimeSpec parsed = CoreRuntimeSpec.parseAndValidate(specJson, "ipc");
            this.activeSpec = parsed;
            Map<String, Object> ok = new LinkedHashMap<>();
            ok.put("ok", Boolean.TRUE);
            ok.put("runtimeVersion", parsed.runtimeVersion);
            ok.put("gameVersion", parsed.gameVersion);
            return MiniJson.stringify(ok);
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    String scanPackages() {
        try {
            final CoreRuntimeSpec spec = this.activeSpec;
            String pmOut = command("/system/bin/pm", "list", "packages", "-3");
            List<String> candidates = GamePackageCatalog.parseThirdPartyPackages(pmOut);
            String psOut = "";
            try { psOut = command("/system/bin/ps", "-A"); } catch (Exception ignored) { }

            List<Map<String, Object>> items = new ArrayList<>();
            int limit = Math.min(candidates.size(), 32);
            for (int i = 0; i < limit; i++) {
                String pkg = candidates.get(i);
                String dump;
                try {
                    dump = command("/system/bin/dumpsys", "package", pkg);
                } catch (Exception ignored) {
                    continue;
                }
                String ver = GamePackageCatalog.extractVersionName(dump);
                if (ver.isEmpty()) continue;

                GamePackageCatalog.ChannelMeta meta = GamePackageCatalog.classifyPackage(pkg);
                File filesRoot = GamePackageCatalog.filesRoot(pkg);
                File catalog = new File(filesRoot, spec.catalogRelativePath);
                boolean hasCatalog = catalog.isFile();
                boolean isRunning = Pattern.compile("(?m)\\s" + Pattern.quote(pkg) + "(?::[^\\s]+)?\\s*$")
                    .matcher(psOut).find();
                String installState = "version_mismatch";
                String resolvedRel = "";

                if (spec.gameVersion.equals(ver)) {
                    if (!hasCatalog) {
                        installState = "no_catalog";
                    } else {
                        try {
                            resolvedRel = AddressablesCatalog.resolveManagedRuntimeRelativePath(
                                filesRoot, catalog, spec.bundleRelativePath);
                            File target = new File(filesRoot, resolvedRel);
                            if (!target.isFile()) {
                                installState = "no_bundle";
                            } else {
                                String hash = hash(target);
                                List<File> companions = CacheConflict.findCompanions(filesRoot, target);
                                if (hash.equals(spec.patchSha256) && companions.isEmpty()) {
                                    installState = "installed";
                                } else if (hash.equals(spec.vanillaSha256) && companions.isEmpty()) {
                                    installState = "vanilla";
                                } else if (spec.isKnownHash(hash) && companions.isEmpty()) {
                                    installState = "upgrade";
                                } else if (CacheConflict.replaceableTarget(target, catalog, spec.catalogSha256, resolvedRel, spec.bundleRelativePath)) {
                                    installState = "conflict";
                                } else {
                                    installState = "incompatible";
                                }
                            }
                        } catch (Exception ignored) {
                            installState = "bad_catalog";
                        }
                    }
                }

                Map<String, Object> item = new LinkedHashMap<>();
                item.put("packageName", pkg);
                item.put("label", meta.label);
                item.put("platform", meta.platform);
                item.put("versionName", ver);
                item.put("hasCatalog", hasCatalog);
                item.put("running", isRunning);
                item.put("installState", installState);
                item.put("resolvedRel", resolvedRel);
                items.add(item);
            }
            return MiniJson.stringify(Collections.singletonMap("packages", items));
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    String inspect(String packageName) {
        try {
            final CoreRuntimeSpec spec = this.activeSpec;
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            GamePackageCatalog.ChannelMeta meta = GamePackageCatalog.classifyPackage(pkg);
            File filesRoot = GamePackageCatalog.filesRoot(pkg);
            File catalog = new File(filesRoot, spec.catalogRelativePath);

            checkGame(pkg, meta, spec);
            if (!catalog.isFile()) {
                throw new IOException("未找到 " + meta.label + "（" + pkg + "）的 " + spec.catalogFileName() + "。\n请先打开该渠道游戏更新资源并进入大厅一次，再正常退出后检查。");
            }
            String resolvedRel = AddressablesCatalog.resolveManagedRuntimeRelativePath(filesRoot, catalog, spec.bundleRelativePath);
            File target = new File(filesRoot, resolvedRel);
            String hash = hash(target);
            probe(target);

            List<File> companions = CacheConflict.findCompanions(filesRoot, target);
            boolean targetConflict = !spec.isKnownHash(hash);
            String state;
            if (targetConflict || !companions.isEmpty()) {
                boolean replaceable = CacheConflict.replaceableTarget(
                    target, catalog, spec.catalogSha256, resolvedRel, spec.bundleRelativePath);
                if (!replaceable) {
                    String catHash = catalog.isFile() ? CacheConflict.hash(catalog) : "缺失";
                    state = "未知版本，禁止替换；资源清单或容器与本版不匹配（" + meta.label
                        + "，catalog SHA256=" + catHash + "，target SHA256=" + hash
                        + "，bundle=" + resolvedRel + "），请先用官方资源修复。";
                } else {
                    String pluginName = CacheConflict.identifyPlugin(target, companions);
                    StringBuilder detail = new StringBuilder();
                    detail.append("同位置覆盖冲突（").append(pluginName).append("），可备份整理并删除替换\n");
                    detail.append("冲突文件SHA256：").append(hash);
                    if (!companions.isEmpty()) {
                        detail.append("\n检测到 ").append(companions.size()).append(" 个关联冲突文件：");
                        String prefix = filesRoot.getPath() + "/";
                        for (int i = 0; i < Math.min(companions.size(), 5); i++) {
                            String p = companions.get(i).getPath();
                            detail.append("\n  - ").append(p.startsWith(prefix) ? p.substring(prefix.length()) : companions.get(i).getName());
                        }
                    }
                    detail.append("\n清理并替换后原插件将停止生效，其他目录的 MOD 不会移动。");
                    state = detail.toString();
                }
            } else {
                state = hash.equals(spec.patchSha256) ? "本版星枢"
                    : hash.equals(spec.vanillaSha256) ? "官方原版"
                    : "可升级的旧版星枢";
            }
            return "游戏版本匹配，目录可读取并写入。\n目标渠道：" + meta.label + "（" + pkg + "）\n定位资源：" + resolvedRel
                + "\n当前缓存：" + state + "\n"
                + (running(pkg) ? "游戏仍在运行，请正常退出后安装。" : "游戏已退出，可以安装或还原。");
        } catch (Exception e) {
            return "检查未通过：" + friendly(e);
        }
    }

    synchronized String apply(String packageName, boolean restore, String conflictHash,
            InputStream candidate, InputStream vanilla) {
        try {
            final CoreRuntimeSpec spec = this.activeSpec;
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            File backups = GamePackageCatalog.backupRoot(pkg, spec.vanillaSha256);
            if (!backups.isDirectory() && !backups.mkdirs()) {
                throw new IOException("无法创建备份目录");
            }
            try (RandomAccessFile guard = new RandomAccessFile(new File(backups, "transaction.lock"), "rw");
                 FileLock lock = guard.getChannel().tryLock()) {
                if (lock == null) throw new IOException("另一个安装操作正在进行，请稍后重试");
                return applyLocked(pkg, backups, spec, restore, conflictHash, candidate, vanilla);
            }
        } catch (Exception e) {
            return "未完成：" + friendly(e);
        }
    }

    private String applyLocked(String pkg, File backups, CoreRuntimeSpec spec, boolean restore, String conflictHash,
            InputStream candidate, InputStream vanilla) {
        File staged = null, original = null, snapshot = null;
        try {
            GamePackageCatalog.ChannelMeta meta = GamePackageCatalog.classifyPackage(pkg);
            File filesRoot = GamePackageCatalog.filesRoot(pkg);
            File catalog = new File(filesRoot, spec.catalogRelativePath);

            checkGame(pkg, meta, spec);
            if (running(pkg)) throw new IOException("游戏仍在运行，请正常退出后重试，不会中断对局");
            String resolvedRel = AddressablesCatalog.resolveManagedRuntimeRelativePath(filesRoot, catalog, spec.bundleRelativePath);
            File target = new File(filesRoot, resolvedRel);

            String before = hash(target);
            if (conflictHash != null && !conflictHash.equals(before)) {
                throw new IOException("冲突文件在确认后发生变化，请重新检查，未修改游戏");
            }
            List<File> companions = restore ? Collections.emptyList() : CacheConflict.findCompanions(filesRoot, target);
            boolean conflict = !spec.isKnownHash(before) || !companions.isEmpty();
            boolean replaceable = CacheConflict.replaceableTarget(
                target, catalog, spec.catalogSha256, resolvedRel, spec.bundleRelativePath);
            if (conflict && (restore || conflictHash == null || !replaceable)) {
                throw new IOException("目标是其他覆盖文件、残留冲突插件或未知资源。请先检查冲突；仅资源清单匹配且确认备份删除冲突后才能安装，未修改文件");
            }
            probe(target);
            if (!backups.isDirectory() && !backups.mkdirs()) throw new IOException("无法创建备份目录");

            File backup = new File(backups, "official.__data");
            if (restore) {
                original = new File(backups, "vanilla.staging");
                if (vanilla != null) {
                    receive(vanilla, original, spec.vanillaSha256);
                    if (!backup.exists() || !hash(backup).equals(spec.vanillaSha256)) copy(original, backup);
                } else if (backup.isFile() && hash(backup).equals(spec.vanillaSha256)) {
                    copy(backup, original);
                } else {
                    throw new IOException("缺少官方原版备份，请联网下载后重试还原");
                }
                if (!hash(backup).equals(spec.vanillaSha256)) throw new IOException("原版备份校验失败，已停止");
            } else {
                staged = new File(backups, "candidate.staging");
                receive(candidate, staged, spec.patchSha256);
                if (vanilla != null) {
                    original = new File(backups, "vanilla.staging");
                    receive(vanilla, original, spec.vanillaSha256);
                    if (!backup.exists() || !hash(backup).equals(spec.vanillaSha256)) copy(original, backup);
                } else if (before.equals(spec.vanillaSha256) && (!backup.isFile() || !hash(backup).equals(spec.vanillaSha256))) {
                    copy(target, backup);
                }
            }

            snapshot = CacheConflict.archive(target, backups, before);
            if (!hash(snapshot).equals(before)) throw new IOException("备份期间缓存发生变化，未安装");
            if (!companions.isEmpty()) CacheConflict.archiveAndDeleteCompanions(filesRoot, companions, snapshot.getParentFile());
            if (running(pkg) || !hash(target).equals(before)) throw new IOException("游戏已启动或缓存已变化，请退出后重试");

            String recheckedRel = AddressablesCatalog.resolveManagedRuntimeRelativePath(filesRoot, catalog, spec.bundleRelativePath);
            if (!recheckedRel.equals(resolvedRel) || !CacheConflict.replaceableTarget(
                    target, catalog, spec.catalogSha256, recheckedRel, spec.bundleRelativePath)) {
                throw new IOException("官方资源清单已变化，请重新检查，未修改游戏");
            }

            File source = restore ? original : staged;
            String expected = restore ? spec.vanillaSha256 : spec.patchSha256;
            if (!before.equals(expected)) {
                try {
                    // Overwrite existing inode to preserve game UID ownership and SELinux context.
                    copy(source, target);
                    if (!hash(target).equals(expected)) throw new IOException("安装后 SHA256 校验失败");
                } catch (Exception writeError) {
                    copy(snapshot, target);
                    if (!hash(target).equals(before)) {
                        throw new IOException("写入和自动恢复均未通过校验，请保留备份并使用官方资源修复");
                    }
                    throw new IOException("写入失败，已恢复安装前缓存：" + writeError.getMessage());
                }
            }
            return (restore ? "已还原官方原版" : "已安装 / 更新星枢 " + spec.runtimeVersion)
                + "（" + meta.label + "），SHA256 校验通过。\n"
                + (conflict ? "同位置冲突插件及关联文件（共 " + companions.size() + " 个附加项）已备份删除并替换；其他模组未移动。\n" : "")
                + "安装前文件与记录：" + snapshot.getParent() + "\n现在可以从该渠道入口启动游戏。";
        } catch (Exception e) {
            return "未完成：" + friendly(e);
        } finally {
            if (staged != null) staged.delete();
            if (original != null) original.delete();
        }
    }

    private static final Pattern PS_LINE_PATTERN = Pattern.compile(
        "^\\S+\\s+([0-9]{1,7})\\s+.*\\s(\\S+)\\s*$"
    );
    private static final Pattern APPID_IN_NAME_PATTERN = Pattern.compile("^(?:java|native)_(1100[0-9]{5})\\.log$");

    String readChannelTokensJson() {
        try {
            StringBuilder combined = new StringBuilder();

            // Source 1: query full logcat ring buffer for BnSdk tags without -t 4000 / Unity:V noise.
            String tagLogs = commandBestEffort(1500 * 1024,
                "/system/bin/logcat", "-d", "-b", "main", "-s",
                "BN_DEBUG:V", "BN_UNITY:V", "BN_WARN:V", "Ev_CRASH_OS:V");
            if (!hasSdkSignalLine(tagLogs)) {
                String defaultBufLogs = commandBestEffort(1500 * 1024,
                    "/system/bin/logcat", "-d", "-s",
                    "BN_DEBUG:V", "BN_UNITY:V", "BN_WARN:V", "Ev_CRASH_OS:V");
                if (!defaultBufLogs.isEmpty()) {
                    combined.append(defaultBufLogs).append('\n');
                }
            } else {
                combined.append(tagLogs).append('\n');
            }

            // Source 2: scan running candidate game PIDs with --pid filtering (fallback for ROMs with tag quirks).
            String psOut = commandBestEffort(512 * 1024, "/system/bin/ps", "-A");
            if (psOut.trim().isEmpty()) {
                psOut = commandBestEffort(512 * 1024, "/system/bin/ps");
            }
            Map<String, String> runningPidToPkg = findRunningCandidatePids(psOut);
            Set<String> runningAppIds = new LinkedHashSet<>();
            for (Map.Entry<String, String> entry : runningPidToPkg.entrySet()) {
                String pid = entry.getKey();
                String pkg = entry.getValue();
                String appId = BnSdkTokenExtractor.appIdForPackage(pkg);
                if (!appId.isEmpty()) runningAppIds.add(appId);
                combined.append("D/XINGSHU_PS(").append(pid).append("): package=").append(pkg).append('\n');
                String pidLogs = readPidRelevantLogcat(pid);
                if (!pidLogs.isEmpty()) {
                    combined.append(pidLogs).append('\n');
                }
            }

            // Source 3: installed package list + on-disk BnSdk/ibingniao hints & logs.
            String pmOut = commandBestEffort(128 * 1024, "/system/bin/pm", "list", "packages", "-3");
            List<String> installedPkgs = GamePackageCatalog.parseThirdPartyPackages(pmOut);
            Set<String> installedNativeAppIds = new LinkedHashSet<>();
            for (String pkg : installedPkgs) {
                String appId = BnSdkTokenExtractor.appIdForPackage(pkg);
                if (!appId.isEmpty()) installedNativeAppIds.add(appId);
                appendOnDiskBnSdkEvidence(pkg, combined);
            }
            appendIbingniaoPeriodHints(combined);

            String fallbackAppId = "";
            if (runningAppIds.size() == 1) {
                fallbackAppId = runningAppIds.iterator().next();
            } else if (installedNativeAppIds.size() == 1) {
                fallbackAppId = installedNativeAppIds.iterator().next();
            }

            List<BnSdkTokenExtractor.ChannelToken> tokens =
                BnSdkTokenExtractor.extractFromLogcat(combined.toString(), fallbackAppId);
            List<Map<String, Object>> out = new ArrayList<>(tokens.size());
            for (BnSdkTokenExtractor.ChannelToken tok : tokens) {
                Map<String, Object> item = new LinkedHashMap<>();
                item.put("platform", tok.platform);
                item.put("appId", tok.appId);
                item.put("label", tok.label);
                item.put("userId", tok.userId);
                item.put("sdkToken", tok.sdkToken);
                item.put("nativeSupported", tok.nativeSupported);
                out.add(item);
            }
            return MiniJson.stringify(Collections.singletonMap("tokens", out));
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    private static boolean hasSdkSignalLine(String text) {
        if (text == null || text.isEmpty()) return false;
        return text.contains("BN_DEBUG")
            || text.contains("BN_UNITY")
            || text.contains("BN_WARN")
            || text.contains("accessToken")
            || text.contains("userSqlEntity");
    }

    private static boolean isRelevantSdkLogLine(String line) {
        if (line == null || line.isEmpty()) return false;
        return line.contains("BN_")
            || line.contains("accessToken")
            || line.contains("access_token")
            || line.contains("userSqlEntity")
            || line.contains("[LoginSDK]")
            || line.contains("RoleInfo")
            || line.contains("11000")
            || line.contains("feimogames")
            || line.contains("com.feimo.");
    }

    private static Map<String, String> findRunningCandidatePids(String psOut) {
        Map<String, String> result = new LinkedHashMap<>();
        if (psOut == null || psOut.isEmpty()) return result;
        for (String rawLine : psOut.split("[\\r\\n]+")) {
            Matcher m = PS_LINE_PATTERN.matcher(rawLine.trim());
            if (!m.matches()) continue;
            String pid = m.group(1);
            String procName = m.group(2);
            int colon = procName.indexOf(':');
            String basePkg = colon > 0 ? procName.substring(0, colon) : procName;
            if (GamePackageCatalog.isCandidatePackageName(basePkg) && colon < 0) {
                result.put(pid, basePkg);
            }
        }
        return result;
    }

    private static String readPidRelevantLogcat(String pid) {
        if (pid == null || !pid.matches("[0-9]{1,7}")) return "";
        StringBuilder out = new StringBuilder();
        try {
            Process p = new ProcessBuilder("/system/bin/logcat", "-d", "--pid=" + pid)
                .redirectErrorStream(true)
                .start();
            final int maxChars = 256 * 1024;
            Thread reader = new Thread(() -> {
                try (BufferedReader br = new BufferedReader(
                        new InputStreamReader(p.getInputStream(), StandardCharsets.UTF_8))) {
                    String line;
                    while ((line = br.readLine()) != null) {
                        if (isRelevantSdkLogLine(line) && out.length() < maxChars) {
                            out.append(line).append('\n');
                        }
                    }
                } catch (IOException ignored) { }
            });
            reader.start();
            if (!p.waitFor(10, TimeUnit.SECONDS)) {
                p.destroyForcibly();
            }
            reader.join(2000);
        } catch (Exception ignored) { }
        return out.toString();
    }

    private static void appendOnDiskBnSdkEvidence(String pkg, StringBuilder combined) {
        try {
            File filesRoot = GamePackageCatalog.filesRoot(pkg);
            if (!filesRoot.isDirectory()) return;
            File[] rootEntries = filesRoot.listFiles();
            if (rootEntries != null) {
                for (File f : rootEntries) {
                    if (!f.isFile()) continue;
                    Matcher m = APPID_IN_NAME_PATTERN.matcher(f.getName());
                    if (m.matches()) {
                        combined.append("D/XINGSHU_DISK: ")
                            .append(f.getPath())
                            .append(" package=").append(pkg)
                            .append('\n');
                    }
                }
            }
            File bnSdkDir = new File(filesRoot, "BnSdk");
            if (!bnSdkDir.isDirectory()) return;
            List<File> logFiles = new ArrayList<>();
            collectBnSdkLogs(bnSdkDir, 0, logFiles);
            logFiles.sort((a, b) -> Long.compare(b.lastModified(), a.lastModified()));
            int scanned = 0;
            for (File logFile : logFiles) {
                if (scanned >= 8) break;
                if (logFile.length() <= 0 || logFile.length() > 512 * 1024) continue;
                scanned++;
                appendFilteredLogFile(logFile, combined);
            }
        } catch (Exception ignored) { }
    }

    private static void collectBnSdkLogs(File dir, int depth, List<File> out) {
        if (dir == null || depth > 3 || out.size() >= 32) return;
        File[] children = dir.listFiles();
        if (children == null) return;
        for (File child : children) {
            if (child.isDirectory()) {
                collectBnSdkLogs(child, depth + 1, out);
            } else if (child.isFile() && child.getName().toLowerCase(Locale.ROOT).endsWith(".log")) {
                out.add(child);
                if (out.size() >= 32) return;
            }
        }
    }

    private static void appendFilteredLogFile(File file, StringBuilder combined) {
        combined.append("D/XINGSHU_DISK_FILE: ").append(file.getPath()).append('\n');
        try (BufferedReader br = new BufferedReader(
                new InputStreamReader(new FileInputStream(file), StandardCharsets.UTF_8))) {
            String line;
            int budget = 128 * 1024;
            while ((line = br.readLine()) != null && budget > 0) {
                if (isRelevantSdkLogLine(line)) {
                    combined.append(line).append('\n');
                    budget -= line.length() + 1;
                }
            }
        } catch (IOException ignored) { }
    }

    private static void appendIbingniaoPeriodHints(StringBuilder combined) {
        try {
            File periodRoot = new File("/sdcard/ibingniao/period");
            if (!periodRoot.isDirectory()) return;
            File[] appDirs = periodRoot.listFiles();
            if (appDirs == null) return;
            for (File appDir : appDirs) {
                if (!appDir.isDirectory() || !appDir.getName().matches("1100[0-9]{5}")) continue;
                File[] uidEntries = appDir.listFiles();
                if (uidEntries != null && uidEntries.length > 0) {
                    combined.append("D/XINGSHU_DISK: /period/")
                        .append(appDir.getName()).append('/').append(uidEntries[0].getName()).append('\n');
                }
            }
        } catch (Exception ignored) { }
    }

    String launchGame(String packageName) {
        try {
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            GamePackageCatalog.ChannelMeta meta = GamePackageCatalog.classifyPackage(pkg);
            try {
                String resolved = command("/system/bin/cmd", "package", "resolve-activity", "--brief", pkg);
                String component = "";
                for (String line : resolved.split("[\\r\\n]+")) {
                    String t = line.trim();
                    if (t.startsWith(pkg + "/") && t.length() <= 200 && !t.contains(" ")) {
                        component = t;
                    }
                }
                if (!component.isEmpty()) {
                    command("/system/bin/am", "start", "--user", "0", "-n", component);
                    return "已启动 " + meta.label + "（" + pkg + "）";
                }
            } catch (Exception ignored) { }
            command("/system/bin/monkey", "-p", pkg, "-c", "android.intent.category.LAUNCHER", "1");
            return "已启动 " + meta.label + "（" + pkg + "）";
        } catch (Exception e) {
            return "启动游戏失败：" + friendly(e);
        }
    }

    String writeGameHandoff(String packageName, String payloadJson) {
        try {
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            if (payloadJson == null || payloadJson.isEmpty() || payloadJson.length() > 256 || payloadJson.indexOf('\0') >= 0) {
                throw new IllegalArgumentException("同步凭据无效");
            }
            File filesRoot = GamePackageCatalog.filesRoot(pkg);
            if (!filesRoot.isDirectory()) {
                return "SKIP:NO_DIR";
            }
            File tmp = new File(filesRoot, "xingshu_handoff.json.tmp");
            File target = new File(filesRoot, "xingshu_handoff.json");
            try (FileOutputStream out = new FileOutputStream(tmp)) {
                out.write(payloadJson.getBytes(StandardCharsets.UTF_8));
                out.getFD().sync();
            }
            if (!tmp.renameTo(target)) {
                copy(tmp, target);
                tmp.delete();
            }
            return "OK";
        } catch (Exception e) {
            return "ERR:" + friendly(e);
        }
    }

    private static final int MAX_MOD_ENTRIES = 4000;
    private static final long MAX_MOD_UNPACKED_BYTES = 2L * 1024L * 1024L * 1024L;
    private static final Pattern XSPKG_MODULE_TARGET_PATTERN = Pattern.compile(
        "^xingshu/plugins/xingshu\\.launcher\\.[a-z0-9-]{1,64}\\.module$"
    );

    String installModPack(String packageName, String metaJson, InputStream modZip) {
        File stagedZip = null;
        try {
            if (modZip == null) throw new IOException("缺少 Mod 压缩包输入流");
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            Map<String, Object> metaMap = MiniJson.parseObject(metaJson);
            String modId = MiniJson.stringField(metaMap, "id");
            if (!AndroidWorkshopClient.isSafeModId(modId)) throw new IllegalArgumentException("模组 ID 无效");
            String expectedSha = MiniJson.stringField(metaMap, "sha256").toLowerCase(Locale.ROOT);
            if (!PayloadDownloadManager.isValidSha256(expectedSha)) throw new IllegalArgumentException("缺少有效的 Mod SHA-256");

            File backups = GamePackageCatalog.backupRoot(pkg);
            if (!backups.isDirectory() && !backups.mkdirs()) throw new IOException("无法创建模组备份目录");
            try (RandomAccessFile lockFile = new RandomAccessFile(new File(backups, "install.lock"), "rw");
                 FileLock ignored = lockFile.getChannel().lock()) {
                File filesRoot = GamePackageCatalog.filesRoot(pkg);
                if (!filesRoot.isDirectory()) {
                    throw new IOException("未找到该渠道游戏数据目录，请先打开一次游戏后再安装 Mod。");
                }
                stagedZip = new File(backups, "mod.staging.zip");
                receive(modZip, stagedZip, expectedSha);

                String requestedKind = MiniJson.stringField(metaMap, "installKind");
                boolean isXspkg = "xspkg".equalsIgnoreCase(requestedKind) || hasXspkgManifest(stagedZip);
                String actualKind = isXspkg ? "xspkg" : "modimg";

                List<String> installedFiles;
                String targetDirDisplay;
                if (isXspkg) {
                    installedFiles = installXspkgArchive(stagedZip, filesRoot);
                    targetDirDisplay = new File(filesRoot, "xingshu/plugins").getAbsolutePath();
                } else {
                    File modImgDir = new File(filesRoot, "modimg");
                    if (!modImgDir.isDirectory() && !modImgDir.mkdirs()) {
                        throw new IOException("无法创建游戏 modimg 目录：" + modImgDir.getAbsolutePath());
                    }
                    installedFiles = extractModPackArchive(stagedZip, modImgDir);
                    ensureStaticPortraitsModuleEnabled(filesRoot);
                    targetDirDisplay = modImgDir.getAbsolutePath();
                }

                File receiptsDir = new File(backups, "mods");
                if (!receiptsDir.isDirectory() && !receiptsDir.mkdirs()) {
                    throw new IOException("无法创建模组记录目录");
                }
                Map<String, Object> receipt = new LinkedHashMap<>();
                receipt.put("id", modId);
                receipt.put("name", MiniJson.stringField(metaMap, "name"));
                receipt.put("version", MiniJson.stringField(metaMap, "version"));
                receipt.put("sha256", expectedSha);
                receipt.put("installKind", actualKind);
                receipt.put("installedAt", System.currentTimeMillis());
                receipt.put("files", installedFiles);
                writeTextFileAtomic(new File(receiptsDir, modId + ".json"), MiniJson.stringify(receipt));

                Map<String, Object> resp = new LinkedHashMap<>();
                resp.put("ok", Boolean.TRUE);
                resp.put("modId", modId);
                resp.put("installKind", actualKind);
                resp.put("fileCount", installedFiles.size());
                resp.put("modImgDir", targetDirDisplay);
                return MiniJson.stringify(resp);
            }
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        } finally {
            if (stagedZip != null) stagedZip.delete();
        }
    }

    String listInstalledMods(String packageName) {
        try {
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            File filesRoot = GamePackageCatalog.filesRoot(pkg);
            File modImgDir = new File(filesRoot, "modimg");
            File pluginsDir = new File(filesRoot, "xingshu/plugins");
            File backups = GamePackageCatalog.backupRoot(pkg);
            File receiptsDir = new File(backups, "mods");

            List<Map<String, Object>> mods = new ArrayList<>();
            if (receiptsDir.isDirectory()) {
                File[] entries = receiptsDir.listFiles();
                if (entries != null) {
                    Arrays.sort(entries, (a, b) -> a.getName().compareToIgnoreCase(b.getName()));
                    for (File f : entries) {
                        if (!f.isFile() || !f.getName().endsWith(".json")) continue;
                        try {
                            Map<String, Object> rec = MiniJson.parseObject(readUtf8File(f, 1024 * 1024));
                            String modId = MiniJson.stringField(rec, "id");
                            if (!AndroidWorkshopClient.isSafeModId(modId)) continue;
                            String kind = MiniJson.stringField(rec, "installKind");
                            boolean isXspkg = "xspkg".equalsIgnoreCase(kind);
                            Object rawFiles = rec.get("files");
                            int declared = 0;
                            int present = 0;
                            if (rawFiles instanceof List<?>) {
                                for (Object rf : (List<?>) rawFiles) {
                                    if (rf == null) continue;
                                    declared++;
                                    String rel = String.valueOf(rf).replace('\\', '/');
                                    if (isXspkg) {
                                        if (isSafeXspkgTarget(rel) && new File(filesRoot, rel).isFile()) {
                                            present++;
                                        }
                                    } else {
                                        String safeMod = sanitizeModImgRelative(rel);
                                        if (safeMod != null && new File(modImgDir, safeMod).isFile()) {
                                            present++;
                                        }
                                    }
                                }
                            }
                            Map<String, Object> summary = new LinkedHashMap<>();
                            summary.put("id", modId);
                            summary.put("name", MiniJson.stringField(rec, "name"));
                            summary.put("version", MiniJson.stringField(rec, "version"));
                            summary.put("sha256", MiniJson.stringField(rec, "sha256"));
                            summary.put("installKind", isXspkg ? "xspkg" : "modimg");
                            summary.put("declaredCount", declared);
                            summary.put("presentCount", present);
                            mods.add(summary);
                        } catch (Exception ignored) { }
                    }
                }
            }

            File disabledDir = new File(filesRoot, ".xingshu-mods/disabled-modules");
            Map<String, String> officialModules = scanOfficialModulesStatus(pluginsDir, disabledDir);

            int totalPng = countPngFiles(modImgDir);
            int activeModules = countModuleFiles(pluginsDir);
            Map<String, Object> out = new LinkedHashMap<>();
            out.put("ok", Boolean.TRUE);
            out.put("mods", mods);
            out.put("officialModules", officialModules);
            out.put("totalPngCount", totalPng);
            out.put("activeModuleCount", activeModules);
            out.put("modImgDir", modImgDir.getAbsolutePath());
            return MiniJson.stringify(out);
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    String removeModPack(String packageName, String modId) {
        try {
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            if (!AndroidWorkshopClient.isSafeModId(modId)) throw new IllegalArgumentException("模组标识无效");
            File backups = GamePackageCatalog.backupRoot(pkg);
            if (!backups.isDirectory() && !backups.mkdirs()) throw new IOException("无法访问备份目录");
            try (RandomAccessFile lockFile = new RandomAccessFile(new File(backups, "install.lock"), "rw");
                 FileLock ignored = lockFile.getChannel().lock()) {
                File filesRoot = GamePackageCatalog.filesRoot(pkg);
                File modImgDir = new File(filesRoot, "modimg");
                File receiptsDir = new File(backups, "mods");
                File targetReceipt = new File(receiptsDir, modId + ".json");
                if (!targetReceipt.isFile()) {
                    return MiniJson.stringify(Collections.singletonMap("error", "未找到该模组的安装记录"));
                }

                Map<String, Object> rec = MiniJson.parseObject(readUtf8File(targetReceipt, 1024 * 1024));
                boolean isXspkg = "xspkg".equalsIgnoreCase(MiniJson.stringField(rec, "installKind"));

                Set<String> keepByOthers = new LinkedHashSet<>();
                File[] all = receiptsDir.listFiles();
                if (all != null) {
                    for (File other : all) {
                        if (!other.isFile() || !other.getName().endsWith(".json") || other.equals(targetReceipt)) continue;
                        try {
                            Map<String, Object> oRec = MiniJson.parseObject(readUtf8File(other, 1024 * 1024));
                            boolean oXspkg = "xspkg".equalsIgnoreCase(MiniJson.stringField(oRec, "installKind"));
                            if (oXspkg != isXspkg) continue;
                            Object oFiles = oRec.get("files");
                            if (oFiles instanceof List<?>) {
                                for (Object rf : (List<?>) oFiles) {
                                    String raw = rf == null ? "" : String.valueOf(rf).replace('\\', '/');
                                    String safe = isXspkg ? (isSafeXspkgTarget(raw) ? raw : null) : sanitizeModImgRelative(raw);
                                    if (safe != null) keepByOthers.add(safe);
                                }
                            }
                        } catch (Exception ignoredEx) { }
                    }
                }

                Object rawFiles = rec.get("files");
                int removed = 0;
                File baseDir = isXspkg ? filesRoot : modImgDir;
                if (rawFiles instanceof List<?> && baseDir.isDirectory()) {
                    String canonRoot = baseDir.getCanonicalPath() + File.separator;
                    for (Object rf : (List<?>) rawFiles) {
                        String raw = rf == null ? "" : String.valueOf(rf).replace('\\', '/');
                        String safe = isXspkg ? (isSafeXspkgTarget(raw) ? raw : null) : sanitizeModImgRelative(raw);
                        if (safe == null || keepByOthers.contains(safe)) continue;
                        File disk = new File(baseDir, safe).getCanonicalFile();
                        if (disk.getPath().startsWith(canonRoot) && disk.isFile() && disk.delete()) {
                            removed++;
                        }
                    }
                }
                targetReceipt.delete();

                Map<String, Object> out = new LinkedHashMap<>();
                out.put("ok", Boolean.TRUE);
                out.put("modId", modId);
                out.put("installKind", isXspkg ? "xspkg" : "modimg");
                out.put("removedFiles", removed);
                return MiniJson.stringify(out);
            }
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    static final Set<String> RETIRED_OFFICIAL_MODULE_IDS = Collections.unmodifiableSet(new LinkedHashSet<>(Arrays.asList(
            "monster-targeting",
            "monster-cooldown",
            "battle-card-points",
            "card-points",
            "quick-thanks",
            "avatar-praise",
            "star-coin-shop",
            "settlement-capture",
            "settlement-screenshot",
            "custom-lobby-bg"
    )));

    static List<String> legacyAliasesForCanonical(String id) {
        if ("monster-action".equals(id)) {
            return Arrays.asList("monster-targeting", "monster-cooldown");
        }
        if ("defense-hint".equals(id)) {
            return Arrays.asList("battle-card-points", "card-points");
        }
        if ("auto-social".equals(id)) {
            return Arrays.asList("quick-thanks", "avatar-praise", "star-coin-shop", "settlement-capture", "settlement-screenshot");
        }
        return Collections.emptyList();
    }

    String setOfficialModules(String packageName, String moduleIdsCsv, boolean enable, boolean remove) {
        try {
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            if (moduleIdsCsv == null || moduleIdsCsv.trim().isEmpty()) {
                throw new IllegalArgumentException("请选择要操作的官方功能模块");
            }
            File backups = GamePackageCatalog.backupRoot(pkg);
            if (!backups.isDirectory() && !backups.mkdirs()) throw new IOException("无法访问备份目录");
            try (RandomAccessFile lockFile = new RandomAccessFile(new File(backups, "install.lock"), "rw");
                 FileLock ignored = lockFile.getChannel().lock()) {
                if (running(pkg)) {
                    throw new IOException("游戏仍在运行，请先退出游戏再调整官方模组。");
                }
                File filesRoot = GamePackageCatalog.filesRoot(pkg);
                File pluginsDir = new File(filesRoot, "xingshu/plugins");
                File disabledDir = new File(filesRoot, ".xingshu-mods/disabled-modules");
                return applyOfficialModulesToDirs(pluginsDir, disabledDir, moduleIdsCsv, enable, remove);
            }
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    static String applyOfficialModulesToDirs(
            File pluginsDir, File disabledDir, String moduleIdsCsv, boolean enable, boolean remove) throws IOException {
        if (moduleIdsCsv == null || moduleIdsCsv.trim().isEmpty()) {
            throw new IllegalArgumentException("请选择要操作的官方功能模块");
        }
        String trimmedCsv = moduleIdsCsv.trim().toLowerCase(Locale.ROOT);
        boolean wipeAll = remove && ("all".equals(trimmedCsv) || "*".equals(trimmedCsv));
        boolean purgeLegacyOnly = remove && "legacy".equals(trimmedCsv);

        List<String> ids = new ArrayList<>();
        if (!wipeAll && !purgeLegacyOnly) {
            for (String raw : moduleIdsCsv.split(",")) {
                String clean = raw == null ? "" : raw.trim().toLowerCase(Locale.ROOT);
                if (clean.isEmpty()) continue;
                if (!clean.matches("[a-z0-9-]{2,48}")) {
                    throw new IllegalArgumentException("包含无效的模块标识：" + clean);
                }
                if (!ids.contains(clean)) ids.add(clean);
            }
            if (ids.isEmpty()) {
                throw new IllegalArgumentException("未指定有效的功能模块");
            }
        }

        if (!remove) {
            if (enable && !pluginsDir.isDirectory() && !pluginsDir.mkdirs()) {
                throw new IOException("无法创建插件目录：" + pluginsDir.getAbsolutePath());
            }
            if (!enable && !disabledDir.isDirectory() && !disabledDir.mkdirs()) {
                throw new IOException("无法创建停用缓存目录：" + disabledDir.getAbsolutePath());
            }
        }

        int changed = 0;
        if (wipeAll) {
            changed += deleteAllLauncherModuleFiles(pluginsDir);
            changed += deleteAllLauncherModuleFiles(disabledDir);
        } else if (purgeLegacyOnly) {
            for (String retiredId : RETIRED_OFFICIAL_MODULE_IDS) {
                changed += deleteModuleDescriptor(pluginsDir, disabledDir, retiredId);
            }
        } else {
            for (String id : ids) {
                String fileName = "xingshu.launcher." + id + ".module";
                File activeFile = new File(pluginsDir, fileName);
                File disabledFile = new File(disabledDir, fileName);
                String expectedContent = "xingshu-module-v1:" + id + "\n";

                if (remove) {
                    if (activeFile.isFile() && activeFile.delete()) changed++;
                    if (disabledFile.isFile() && disabledFile.delete()) changed++;
                } else if (enable) {
                    boolean needsWrite = !activeFile.isFile()
                        || !expectedContent.equals(safeReadSmallUtf8(activeFile));
                    if (needsWrite) {
                        writeTextFileAtomic(activeFile, expectedContent);
                        changed++;
                    }
                    if (disabledFile.isFile()) {
                        disabledFile.delete();
                    }
                } else {
                    boolean needsWrite = !disabledFile.isFile()
                        || !expectedContent.equals(safeReadSmallUtf8(disabledFile));
                    if (needsWrite) {
                        writeTextFileAtomic(disabledFile, expectedContent);
                        changed++;
                    }
                    if (activeFile.isFile()) {
                        activeFile.delete();
                        if (!needsWrite) changed++;
                    }
                }
                for (String legacyId : legacyAliasesForCanonical(id)) {
                    changed += deleteModuleDescriptor(pluginsDir, disabledDir, legacyId);
                }
            }
            if (enable && ids.size() >= 10) {
                for (String retiredId : RETIRED_OFFICIAL_MODULE_IDS) {
                    changed += deleteModuleDescriptor(pluginsDir, disabledDir, retiredId);
                }
            }
        }

        Map<String, String> statusMap = scanOfficialModulesStatus(pluginsDir, disabledDir);
        List<String> enabledIds = new ArrayList<>();
        List<String> disabledIds = new ArrayList<>();
        for (Map.Entry<String, String> entry : statusMap.entrySet()) {
            if ("enabled".equals(entry.getValue())) enabledIds.add(entry.getKey());
            else disabledIds.add(entry.getKey());
        }

        Map<String, Object> out = new LinkedHashMap<>();
        out.put("ok", Boolean.TRUE);
        out.put("changedCount", changed);
        out.put("activeModuleCount", countModuleFiles(pluginsDir));
        out.put("officialModules", statusMap);
        out.put("enabledIds", enabledIds);
        out.put("disabledIds", disabledIds);
        return MiniJson.stringify(out);
    }

    private static int deleteModuleDescriptor(File pluginsDir, File disabledDir, String id) {
        int deleted = 0;
        String fileName = "xingshu.launcher." + id + ".module";
        File a = new File(pluginsDir, fileName);
        if (a.isFile() && a.delete()) deleted++;
        File d = new File(disabledDir, fileName);
        if (d.isFile() && d.delete()) deleted++;
        return deleted;
    }

    private static int deleteAllLauncherModuleFiles(File dir) {
        if (dir == null || !dir.isDirectory()) return 0;
        File[] files = dir.listFiles();
        if (files == null) return 0;
        int deleted = 0;
        for (File f : files) {
            if (!f.isFile()) continue;
            String name = f.getName();
            if (name.startsWith("xingshu.launcher.") && name.endsWith(".module") && f.delete()) {
                deleted++;
            }
        }
        return deleted;
    }

    private static String safeReadSmallUtf8(File file) {
        try {
            if (!file.isFile() || file.length() > 4096) return "";
            return readUtf8File(file, 4096);
        } catch (Exception ignored) {
            return "";
        }
    }

    private static Map<String, String> scanOfficialModulesStatus(File pluginsDir, File disabledDir) {
        Map<String, String> status = new LinkedHashMap<>();
        collectModuleDir(disabledDir, "disabled", status);
        collectModuleDir(pluginsDir, "enabled", status);
        return status;
    }

    private static void collectModuleDir(File dir, String state, Map<String, String> out) {
        if (dir == null || !dir.isDirectory()) return;
        File[] files = dir.listFiles();
        if (files == null) return;
        Arrays.sort(files, (a, b) -> a.getName().compareToIgnoreCase(b.getName()));
        String prefix = "xingshu.launcher.";
        String suffix = ".module";
        for (File f : files) {
            if (!f.isFile()) continue;
            String name = f.getName();
            if (name.startsWith(prefix) && name.endsWith(suffix) && name.length() > prefix.length() + suffix.length()) {
                String id = name.substring(prefix.length(), name.length() - suffix.length());
                if (id.matches("[a-z0-9-]{2,48}")) {
                    out.put(id, state);
                }
            }
        }
    }

    static boolean isSafeXspkgTarget(String target) {
        if (target == null || target.isEmpty()) return false;
        String norm = target.replace('\\', '/');
        return XSPKG_MODULE_TARGET_PATTERN.matcher(norm).matches();
    }

    private static boolean hasXspkgManifest(File zipFile) {
        try (ZipFile zip = openModZip(zipFile)) {
            return zip.getEntry("manifest.json") != null;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static void ensureStaticPortraitsModuleEnabled(File filesRoot) {
        try {
            File target = new File(filesRoot, "xingshu/plugins/xingshu.launcher.static-portraits.module");
            if (target.isFile()) return;
            File dir = target.getParentFile();
            if (dir != null && !dir.isDirectory()) dir.mkdirs();
            writeTextFileAtomic(target, "xingshu-module-v1:static-portraits\n");
        } catch (Exception ignored) { }
    }

    /**
     * Verifies manifest.json inside an official .xspkg optimization archive (schemaVersion, target allowlist,
     * declared file size, and SHA-256 digest) and atomically writes validated .module descriptors to filesRoot.
     */
    static List<String> installXspkgArchive(File xspkgFile, File filesRoot) throws Exception {
        if (!filesRoot.isDirectory() && !filesRoot.mkdirs()) {
            throw new IOException("无法创建游戏数据根目录：" + filesRoot.getAbsolutePath());
        }
        String canonPluginsRoot = new File(filesRoot, "xingshu/plugins").getCanonicalPath() + File.separator;
        List<String> writtenTargets = new ArrayList<>();
        try (ZipFile zip = openModZip(xspkgFile)) {
            ZipEntry manifestEntry = zip.getEntry("manifest.json");
            if (manifestEntry == null || manifestEntry.isDirectory()) {
                throw new IOException(".xspkg 缺少 manifest.json");
            }
            byte[] manifestBytes = readZipEntryBounded(zip, manifestEntry, 256 * 1024);
            Map<String, Object> manifest = MiniJson.parseObject(new String(manifestBytes, StandardCharsets.UTF_8));
            if (MiniJson.longField(manifest, "schemaVersion", 0L) != 1L) {
                throw new IOException("不支持的 .xspkg 清单版本");
            }
            Object rawFiles = manifest.get("files");
            if (!(rawFiles instanceof List<?>) || ((List<?>) rawFiles).isEmpty()) {
                throw new IOException(".xspkg 清单缺少 files 条目");
            }
            List<?> filesList = (List<?>) rawFiles;
            if (filesList.size() > 32) {
                throw new IOException(".xspkg 包含文件数过多");
            }
            for (Object obj : filesList) {
                if (!(obj instanceof Map<?, ?>)) throw new IOException(".xspkg files 条目格式无效");
                @SuppressWarnings("unchecked")
                Map<String, Object> entry = (Map<String, Object>) obj;
                String srcRel = MiniJson.stringField(entry, "source").replace('\\', '/').replaceFirst("^\\./", "");
                String targetRel = MiniJson.stringField(entry, "target").replace('\\', '/');
                String sha = MiniJson.stringField(entry, "sha256").toLowerCase(Locale.ROOT);
                long declaredSize = MiniJson.longField(entry, "size", 0L);

                if (srcRel.isEmpty() || srcRel.contains("..") || srcRel.startsWith("/")) {
                    throw new SecurityException("非法的 .xspkg source 路径：" + srcRel);
                }
                if (!isSafeXspkgTarget(targetRel)) {
                    throw new SecurityException("非法的 .xspkg 目标路径（仅允许官方模块白名单）：" + targetRel);
                }
                if (!PayloadDownloadManager.isValidSha256(sha)) {
                    throw new IOException(".xspkg 缺少有效的目标 SHA-256");
                }
                if (declaredSize <= 0L || declaredSize > 16384L) {
                    throw new IOException(".xspkg 文件声明大小超出限制：" + declaredSize);
                }

                ZipEntry payloadEntry = zip.getEntry(srcRel);
                if (payloadEntry == null || payloadEntry.isDirectory()) {
                    throw new IOException(".xspkg 缺少源载荷：" + srcRel);
                }
                byte[] payloadBytes = readZipEntryBounded(zip, payloadEntry, 16384);
                if (payloadBytes.length != declaredSize) {
                    throw new IOException(".xspkg 文件实际大小与清单不符");
                }
                String actualSha = PayloadDownloadManager.hex(MessageDigest.getInstance("SHA-256").digest(payloadBytes));
                if (!sha.equals(actualSha)) {
                    throw new SecurityException(".xspkg 载荷 SHA-256 校验未通过");
                }

                File outFile = new File(filesRoot, targetRel).getCanonicalFile();
                if (!outFile.getPath().startsWith(canonPluginsRoot)) {
                    throw new SecurityException("目标路径越界：" + targetRel);
                }
                File parent = outFile.getParentFile();
                if (parent != null && !parent.isDirectory() && !parent.mkdirs()) {
                    throw new IOException("无法创建插件目录：" + parent.getAbsolutePath());
                }
                writeBytesAtomic(outFile, payloadBytes);
                writtenTargets.add(targetRel);
            }
        }
        return writtenTargets;
    }

    private static byte[] readZipEntryBounded(ZipFile zip, ZipEntry entry, int maxBytes) throws IOException {
        try (InputStream in = zip.getInputStream(entry)) {
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int total = 0;
            int n;
            while ((n = in.read(buf)) != -1) {
                total += n;
                if (total > maxBytes) throw new IOException("压缩包内条目超出安全大小上限");
                out.write(buf, 0, n);
            }
            return out.toByteArray();
        }
    }

    private static int countModuleFiles(File pluginsDir) {
        if (pluginsDir == null || !pluginsDir.isDirectory()) return 0;
        File[] files = pluginsDir.listFiles();
        if (files == null) return 0;
        int n = 0;
        for (File f : files) {
            if (f.isFile() && f.getName().endsWith(".module")) n++;
        }
        return n;
    }

    private static void writeBytesAtomic(File target, byte[] bytes) throws IOException {
        File tmp = new File(target.getParentFile(), target.getName() + ".tmp");
        try (FileOutputStream out = new FileOutputStream(tmp)) {
            out.write(bytes);
            out.flush();
            try { out.getFD().sync(); } catch (IOException ignored) { }
        }
        if (target.exists()) target.delete();
        if (!tmp.renameTo(target)) {
            copy(tmp, target);
            tmp.delete();
        }
    }

    static ZipFile openModZip(File zipFile) throws IOException {
        ZipFile utf8 = null;
        boolean needsGbk = false;
        try {
            utf8 = new ZipFile(zipFile, StandardCharsets.UTF_8);
            Enumeration<? extends ZipEntry> en = utf8.entries();
            while (en.hasMoreElements()) {
                ZipEntry e = en.nextElement();
                if (e.getName().indexOf('\uFFFD') >= 0) {
                    needsGbk = true;
                    break;
                }
            }
            if (!needsGbk) return utf8;
        } catch (ZipException | IllegalArgumentException malformed) {
            needsGbk = true;
        }
        if (utf8 != null) {
            try { utf8.close(); } catch (IOException ignored) { }
        }
        return new ZipFile(zipFile, Charset.forName("GBK"));
    }

    /**
     * Normalizes an archive entry name such as "PE手机端(星枢)/modimg/UT_Event_30002.png"
     * to a safe path relative to modimg/ ("UT_Event_30002.png"). Rejects Zip-Slip traversal,
     * macOS metadata, and entries outside a modimg/ directory.
     */
    static String sanitizeModImgEntry(String rawEntryName) {
        if (rawEntryName == null || rawEntryName.isEmpty()) return null;
        if (rawEntryName.indexOf('\0') >= 0 || rawEntryName.indexOf(':') >= 0
                || rawEntryName.indexOf('\r') >= 0 || rawEntryName.indexOf('\n') >= 0) {
            return null;
        }
        String norm = rawEntryName.replace('\\', '/');
        if (norm.endsWith("/")) return null;
        String[] parts = norm.split("/");
        int modimgIdx = -1;
        for (int i = 0; i < parts.length; i++) {
            String seg = parts[i];
            if (seg.equals("..") || "__MACOSX".equalsIgnoreCase(seg)) return null;
            if (seg.isEmpty() || seg.equals(".")) continue;
            if ("modimg".equalsIgnoreCase(seg)) {
                modimgIdx = i;
            }
        }
        if (modimgIdx < 0 || modimgIdx >= parts.length - 1) return null;
        StringBuilder rel = new StringBuilder();
        int count = 0;
        for (int i = modimgIdx + 1; i < parts.length; i++) {
            String seg = parts[i];
            if (seg.isEmpty() || seg.equals(".")) continue;
            if (seg.equals("..") || seg.startsWith("._")) return null;
            if (rel.length() > 0) rel.append('/');
            rel.append(seg);
            count++;
        }
        if (count == 0 || count > 4 || rel.length() > 200) return null;
        String res = rel.toString();
        if (!res.toLowerCase(Locale.ROOT).endsWith(".png")) return null;
        return res;
    }

    static String sanitizeModImgRootFallback(String rawEntryName) {
        if (rawEntryName == null || rawEntryName.isEmpty()) return null;
        if (rawEntryName.indexOf('\0') >= 0 || rawEntryName.indexOf(':') >= 0
                || rawEntryName.indexOf('\r') >= 0 || rawEntryName.indexOf('\n') >= 0) {
            return null;
        }
        String norm = rawEntryName.replace('\\', '/');
        if (norm.endsWith("/")) return null;
        String[] parts = norm.split("/");
        List<String> clean = new ArrayList<>();
        for (String seg : parts) {
            if (seg.equals("..") || "__MACOSX".equalsIgnoreCase(seg)) return null;
            if (seg.isEmpty() || seg.equals(".")) continue;
            if (seg.startsWith("._")) return null;
            clean.add(seg);
        }
        if (clean.isEmpty() || clean.size() > 2) return null;
        String leaf = clean.get(clean.size() - 1);
        if (!leaf.toLowerCase(Locale.ROOT).endsWith(".png") || leaf.length() > 160) return null;
        return leaf;
    }

    private static String sanitizeModImgRelative(String rel) {
        if (rel == null || rel.isEmpty() || rel.indexOf('\0') >= 0 || rel.indexOf(':') >= 0) return null;
        String norm = rel.replace('\\', '/');
        String[] parts = norm.split("/");
        if (parts.length == 0 || parts.length > 4) return null;
        for (String seg : parts) {
            if (seg.isEmpty() || seg.equals(".") || seg.equals("..") || seg.startsWith("._")) return null;
        }
        return norm.toLowerCase(Locale.ROOT).endsWith(".png") ? norm : null;
    }

    static List<String> extractModPackArchive(File zipFile, File modImgDir) throws IOException {
        if (!modImgDir.isDirectory() && !modImgDir.mkdirs()) {
            throw new IOException("无法创建目标目录：" + modImgDir.getAbsolutePath());
        }
        String canonRoot = modImgDir.getCanonicalPath() + File.separator;
        List<String> extracted = new ArrayList<>();
        try (ZipFile zip = openModZip(zipFile)) {
            List<Map.Entry<ZipEntry, String>> chosen = new ArrayList<>();
            Enumeration<? extends ZipEntry> scan = zip.entries();
            int seen = 0;
            while (scan.hasMoreElements()) {
                seen++;
                if (seen > MAX_MOD_ENTRIES * 2) throw new IOException("压缩包内文件过多");
                ZipEntry entry = scan.nextElement();
                String rel = sanitizeModImgEntry(entry.getName());
                if (rel != null) {
                    chosen.add(new java.util.AbstractMap.SimpleEntry<>(entry, rel));
                }
            }
            if (chosen.isEmpty()) {
                Enumeration<? extends ZipEntry> fb = zip.entries();
                while (fb.hasMoreElements()) {
                    ZipEntry entry = fb.nextElement();
                    String rel = sanitizeModImgRootFallback(entry.getName());
                    if (rel != null) {
                        chosen.add(new java.util.AbstractMap.SimpleEntry<>(entry, rel));
                    }
                }
            }
            if (chosen.isEmpty()) {
                throw new IOException("压缩包中未检测到可用的 modimg/*.png 贴图");
            }
            if (chosen.size() > MAX_MOD_ENTRIES) {
                throw new IOException("Mod 贴图数量超出上限（" + MAX_MOD_ENTRIES + "）");
            }

            long totalBytes = 0L;
            byte[] buf = new byte[32768];
            for (Map.Entry<ZipEntry, String> item : chosen) {
                ZipEntry entry = item.getKey();
                String rel = item.getValue();
                File outFile = new File(modImgDir, rel).getCanonicalFile();
                if (!outFile.getPath().startsWith(canonRoot)) {
                    throw new SecurityException("拦截非法压缩包路径：" + entry.getName());
                }
                File parent = outFile.getParentFile();
                if (parent != null && !parent.isDirectory() && !parent.mkdirs()) {
                    throw new IOException("无法创建子目录：" + parent.getAbsolutePath());
                }
                try (InputStream in = zip.getInputStream(entry);
                     FileOutputStream out = new FileOutputStream(outFile)) {
                    int n;
                    while ((n = in.read(buf)) != -1) {
                        totalBytes += n;
                        if (totalBytes > MAX_MOD_UNPACKED_BYTES) {
                            throw new IOException("Mod 解压总大小超出 256 MB 安全上限");
                        }
                        out.write(buf, 0, n);
                    }
                    out.flush();
                }
                extracted.add(rel);
            }
        }
        return extracted;
    }

    // ---- Game Customize (lobbybg & xingshu_customize_debug.json) ----

    private static final Set<String> LOBBY_BG_IMAGE_EXTS = new LinkedHashSet<>(Arrays.asList(
            ".png", ".jpg", ".jpeg", ".bmp", ".webp"));
    private static final Set<String> LOBBY_BG_VIDEO_EXTS = new LinkedHashSet<>(Arrays.asList(
            ".mp4", ".mov", ".webm", ".mkv", ".avi", ".m4v"));
    private static final Set<String> LOBBY_BG_USM_EXTS = new LinkedHashSet<>(Arrays.asList(
            ".usm"));
    private static final Set<String> CUSTOM_FONT_EXTS = new LinkedHashSet<>(Arrays.asList(
            ".ttf", ".otf", ".ttc"));

    String manageCustomize(String packageName, String op, String payloadJson) {
        try {
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            return manageCustomizeForRoot(GamePackageCatalog.filesRoot(pkg), op, payloadJson);
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    String importCustomizeFile(String packageName, String targetKind, String fileName, InputStream dataIn) {
        try {
            String pkg = GamePackageCatalog.requireValidPackageName(packageName);
            return importCustomizeFileForRoot(GamePackageCatalog.filesRoot(pkg), targetKind, fileName, dataIn);
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    String manageCustomizeForRoot(File filesRoot, String op, String payloadJson) {
        try {
            if (filesRoot == null) throw new IllegalArgumentException("游戏目录无效");
            String action = op == null ? "" : op.trim();
            Map<String, Object> payload = parseOptionalJsonObject(payloadJson);
            switch (action) {
                case "lobbybg_state":
                    return MiniJson.stringify(buildLobbyBgStateMap(filesRoot, null));
                case "lobbybg_save":
                    return MiniJson.stringify(saveLobbyBgConfigMap(filesRoot, payload));
                case "lobbybg_delete":
                    return MiniJson.stringify(deleteLobbyBgFileMap(filesRoot, payload));
                case "tuning_get":
                    return MiniJson.stringify(buildCustomizeTuningStateMap(filesRoot));
                case "tuning_save":
                    return MiniJson.stringify(saveCustomizeTuningMap(filesRoot, payload));
                default:
                    throw new IllegalArgumentException("未知的自定义操作：" + action);
            }
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    String importCustomizeFileForRoot(File filesRoot, String targetKind, String fileName, InputStream dataIn) {
        try {
            if (filesRoot == null) throw new IllegalArgumentException("游戏目录无效");
            if (dataIn == null) throw new IOException("缺少待导入文件数据");
            String kind = targetKind == null ? "" : targetKind.trim().toLowerCase(Locale.ROOT);
            String safeName = sanitizeSingleSegmentName(fileName);
            if (safeName == null) {
                throw new IllegalArgumentException("文件名非法，不允许包含路径分隔符或特殊控制字符");
            }
            File destDir;
            long maxBytes;
            if ("lobbybg".equals(kind)) {
                if (classifyLobbyBgMediaKind(safeName) == null) {
                    throw new IllegalArgumentException("不支持的大厅背景格式（仅支持 png/jpg/webp/bmp 图片或 mp4/mov/webm/mkv/usm 视频）");
                }
                destDir = new File(filesRoot, "lobbybg");
                maxBytes = 300L * 1024L * 1024L;
            } else if ("fonts".equals(kind)) {
                if (!hasAllowedExtension(safeName, CUSTOM_FONT_EXTS)) {
                    throw new IllegalArgumentException("不支持的字体格式（仅支持 .ttf / .otf / .ttc）");
                }
                destDir = new File(new File(filesRoot, "xingshu"), "fonts");
                maxBytes = 32L * 1024L * 1024L;
            } else {
                throw new IllegalArgumentException("不支持的导入目标：" + kind);
            }

            if (!destDir.isDirectory() && !destDir.mkdirs()) {
                throw new IOException("无法创建目录：" + destDir.getAbsolutePath());
            }
            String canonRoot = destDir.getCanonicalPath() + File.separator;
            File target = new File(destDir, safeName).getCanonicalFile();
            if (!target.getPath().startsWith(canonRoot)) {
                throw new SecurityException("非法目标路径");
            }
            File tmp = new File(destDir, safeName + ".tmp").getCanonicalFile();
            if (!tmp.getPath().startsWith(canonRoot)) {
                throw new SecurityException("非法临时文件路径");
            }

            long written = 0L;
            try (InputStream in = dataIn; FileOutputStream out = new FileOutputStream(tmp)) {
                byte[] buf = new byte[65536];
                int n;
                while ((n = in.read(buf)) != -1) {
                    written += n;
                    if (written > maxBytes) {
                        throw new IOException("导入文件超出安全大小上限（" + (maxBytes / (1024L * 1024L)) + " MB）");
                    }
                    out.write(buf, 0, n);
                }
                out.flush();
                try { out.getFD().sync(); } catch (IOException ignored) { }
            } catch (Exception ex) {
                tmp.delete();
                throw ex;
            }
            if (written == 0L) {
                tmp.delete();
                throw new IOException("导入文件为空");
            }
            if (target.exists()) target.delete();
            if (!tmp.renameTo(target)) {
                copy(tmp, target);
                tmp.delete();
            }

            Map<String, Object> out = new LinkedHashMap<>();
            out.put("ok", Boolean.TRUE);
            out.put("targetKind", kind);
            out.put("fileName", safeName);
            out.put("sizeBytes", written);
            if ("lobbybg".equals(kind)) {
                out.put("mediaKind", classifyLobbyBgMediaKind(safeName));
                out.put("state", buildLobbyBgStateMap(filesRoot, null));
            } else {
                out.put("tuningState", buildCustomizeTuningStateMap(filesRoot));
            }
            return MiniJson.stringify(out);
        } catch (Exception e) {
            return MiniJson.stringify(Collections.singletonMap("error", friendly(e)));
        }
    }

    private Map<String, Object> buildLobbyBgStateMap(File filesRoot, String deletedFile) throws IOException {
        File lobbyDir = new File(filesRoot, "lobbybg");
        Map<String, Object> cfg = readLobbyBgConfigMap(lobbyDir);
        List<Map<String, Object>> files = scanLobbyBgFiles(lobbyDir);
        Map<String, Object> out = new LinkedHashMap<>();
        out.put("ok", Boolean.TRUE);
        out.put("lobbyBgDir", lobbyDir.getAbsolutePath());
        out.put("config", cfg);
        out.put("files", files);
        if (deletedFile != null && !deletedFile.isEmpty()) {
            out.put("deletedFile", deletedFile);
        }
        return out;
    }

    private Map<String, Object> saveLobbyBgConfigMap(File filesRoot, Map<String, Object> raw) throws IOException {
        File lobbyDir = new File(filesRoot, "lobbybg");
        if (!lobbyDir.isDirectory() && !lobbyDir.mkdirs()) {
            throw new IOException("无法创建大厅背景目录：" + lobbyDir.getAbsolutePath());
        }
        Map<String, Object> normalized = normalizeLobbyBgConfig(raw);
        writeTextFileAtomic(new File(lobbyDir, "xingshu_lobby_bg.json"), MiniJson.stringify(normalized));
        return buildLobbyBgStateMap(filesRoot, null);
    }

    private Map<String, Object> deleteLobbyBgFileMap(File filesRoot, Map<String, Object> payload) throws IOException {
        String safeName = sanitizeSingleSegmentName(MiniJson.stringField(payload, "fileName"));
        if (safeName == null || classifyLobbyBgMediaKind(safeName) == null) {
            throw new IllegalArgumentException("要删除的媒体文件名无效");
        }
        File lobbyDir = new File(filesRoot, "lobbybg");
        if (lobbyDir.isDirectory()) {
            String canonRoot = lobbyDir.getCanonicalPath() + File.separator;
            File target = new File(lobbyDir, safeName).getCanonicalFile();
            if (!target.getPath().startsWith(canonRoot)) {
                throw new SecurityException("拦截越界删除请求");
            }
            if (target.isFile() && !target.delete()) {
                throw new IOException("无法删除背景文件：" + safeName);
            }
        }
        Map<String, Object> cfg = readLobbyBgConfigMap(lobbyDir);
        if (safeName.equals(MiniJson.stringField(cfg, "selectedFile"))) {
            cfg.put("selectedFile", "");
            cfg.put("enabled", Boolean.FALSE);
            cfg.put("updatedAt", System.currentTimeMillis());
            if (lobbyDir.isDirectory()) {
                writeTextFileAtomic(new File(lobbyDir, "xingshu_lobby_bg.json"), MiniJson.stringify(cfg));
            }
        }
        return buildLobbyBgStateMap(filesRoot, safeName);
    }

    private static Map<String, Object> readLobbyBgConfigMap(File lobbyDir) {
        File cfgFile = new File(lobbyDir, "xingshu_lobby_bg.json");
        if (cfgFile.isFile() && cfgFile.length() > 0L && cfgFile.length() <= 256L * 1024L) {
            try {
                Map<String, Object> parsed = MiniJson.parseObject(readUtf8File(cfgFile, 256 * 1024));
                return normalizeLobbyBgConfig(parsed);
            } catch (Exception ignored) { }
        }
        return normalizeLobbyBgConfig(Collections.emptyMap());
    }

    private static Map<String, Object> normalizeLobbyBgConfig(Map<String, Object> raw) {
        String selectedRaw = MiniJson.stringField(raw, "selectedFile");
        String selected = sanitizeSingleSegmentName(selectedRaw);
        if (selected == null || classifyLobbyBgMediaKind(selected) == null) {
            selected = "";
        }
        String fillMode = MiniJson.stringField(raw, "fillMode").trim().toLowerCase(Locale.ROOT);
        if (!"contain".equals(fillMode) && !"stretch".equals(fillMode)) {
            fillMode = "cover";
        }
        double brightness = clampDouble(doubleField(raw, "brightness", 1.0), 0.30, 1.00);
        double speed = clampDouble(doubleField(raw, "speed", 1.0), 0.50, 2.00);
        double volume = clampDouble(doubleField(raw, "volume", 0.70), 0.00, 1.00);

        Map<String, Object> out = new LinkedHashMap<>();
        out.put("enabled", MiniJson.boolField(raw, "enabled", false));
        out.put("selectedFile", selected);
        out.put("syncSubPanels", MiniJson.boolField(raw, "syncSubPanels", false));
        out.put("randomOnLobby", MiniJson.boolField(raw, "randomOnLobby", false));
        out.put("fillMode", fillMode);
        out.put("brightness", Math.round(brightness * 100.0) / 100.0);
        out.put("speed", Math.round(speed * 100.0) / 100.0);
        out.put("muted", MiniJson.boolField(raw, "muted", true));
        out.put("volume", Math.round(volume * 100.0) / 100.0);
        out.put("muteVanillaBgm", MiniJson.boolField(raw, "muteVanillaBgm", false));
        out.put("updatedAt", System.currentTimeMillis());
        return out;
    }

    private static List<Map<String, Object>> scanLobbyBgFiles(File lobbyDir) {
        if (lobbyDir == null || !lobbyDir.isDirectory()) return Collections.emptyList();
        File[] children = lobbyDir.listFiles();
        if (children == null || children.length == 0) return Collections.emptyList();
        Arrays.sort(children, (a, b) -> a.getName().compareToIgnoreCase(b.getName()));
        List<Map<String, Object>> out = new ArrayList<>();
        for (File f : children) {
            if (!f.isFile()) continue;
            String mediaKind = classifyLobbyBgMediaKind(f.getName());
            if (mediaKind == null) continue;
            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("name", f.getName());
            entry.put("kind", mediaKind);
            entry.put("sizeBytes", f.length());
            entry.put("modifiedAt", f.lastModified());
            out.add(entry);
        }
        return out;
    }

    private Map<String, Object> buildCustomizeTuningStateMap(File filesRoot) {
        File cfgFile = new File(filesRoot, "xingshu_customize_debug.json");
        Map<String, Object> parsed = Collections.emptyMap();
        if (cfgFile.isFile() && cfgFile.length() > 0L && cfgFile.length() <= 512L * 1024L) {
            try {
                parsed = MiniJson.parseObject(readUtf8File(cfgFile, 512 * 1024));
            } catch (Exception ignored) { }
        }
        Map<String, Object> tuning = normalizeCustomizeTuning(parsed);
        File fontsDir = new File(new File(filesRoot, "xingshu"), "fonts");
        List<Map<String, Object>> fontFiles = scanCustomFontFiles(fontsDir);
        Map<String, Object> out = new LinkedHashMap<>();
        out.put("ok", Boolean.TRUE);
        out.put("tuning", tuning);
        out.put("fontsDir", fontsDir.getAbsolutePath());
        out.put("fontFiles", fontFiles);
        return out;
    }

    private Map<String, Object> saveCustomizeTuningMap(File filesRoot, Map<String, Object> raw) throws IOException {
        if (!filesRoot.isDirectory() && !filesRoot.mkdirs()) {
            throw new IOException("无法访问游戏目录：" + filesRoot.getAbsolutePath());
        }
        Map<String, Object> normalized = normalizeCustomizeTuning(raw);
        writeTextFileAtomic(new File(filesRoot, "xingshu_customize_debug.json"), MiniJson.stringify(normalized));
        return buildCustomizeTuningStateMap(filesRoot);
    }

    private static Map<String, Object> normalizeCustomizeTuning(Map<String, Object> raw) {
        Map<String, Object> rawDebug = MiniJson.objectField(raw, "homeUiDebug");
        Map<String, Object> debugOut = new LinkedHashMap<>();
        debugOut.put("hideVanillaBgAnim", MiniJson.boolField(rawDebug, "hideVanillaBgAnim", false));
        debugOut.put("compactLobbyBadges", MiniJson.boolField(rawDebug, "compactLobbyBadges", false));
        debugOut.put("showDebugOutline", MiniJson.boolField(rawDebug, "showDebugOutline", false));
        long opacity = MiniJson.longField(rawDebug, "customHudOpacity", 100L);
        debugOut.put("customHudOpacity", Math.max(40L, Math.min(100L, opacity)));
        debugOut.put("hideLobbyAds", MiniJson.boolField(rawDebug, "hideLobbyAds", false));

        String fontFile = sanitizeSingleSegmentName(MiniJson.stringField(raw, "selectedFontFile"));
        if (fontFile == null || !hasAllowedExtension(fontFile, CUSTOM_FONT_EXTS)) {
            fontFile = null;
        }
        String fontFamily = MiniJson.stringField(raw, "selectedFontFamily").trim();
        if (fontFamily.length() > 120) fontFamily = fontFamily.substring(0, 120);

        Map<String, Object> out = new LinkedHashMap<>();
        out.put("activeCardProfile", nullableTrimmed(MiniJson.stringField(raw, "activeCardProfile")));
        out.put("cardProfiles", copyStringList(MiniJson.listField(raw, "cardProfiles")));
        out.put("activeAudioProfile", nullableTrimmed(MiniJson.stringField(raw, "activeAudioProfile")));
        out.put("audioProfiles", copyStringList(MiniJson.listField(raw, "audioProfiles")));
        out.put("activeCharacterProfile", nullableTrimmed(MiniJson.stringField(raw, "activeCharacterProfile")));
        out.put("characterProfiles", copyStringList(MiniJson.listField(raw, "characterProfiles")));
        out.put("selectedFontFamily", fontFamily.isEmpty() ? null : fontFamily);
        out.put("selectedFontFile", fontFile);
        out.put("fontCjkOnly", MiniJson.boolField(raw, "fontCjkOnly", false));
        out.put("fontReplaceHeadings", MiniJson.boolField(raw, "fontReplaceHeadings", true));
        out.put("homeUiDebug", debugOut);
        out.put("updatedAt", System.currentTimeMillis());
        return out;
    }

    private static List<Map<String, Object>> scanCustomFontFiles(File fontsDir) {
        if (fontsDir == null || !fontsDir.isDirectory()) return Collections.emptyList();
        File[] children = fontsDir.listFiles();
        if (children == null || children.length == 0) return Collections.emptyList();
        Arrays.sort(children, (a, b) -> a.getName().compareToIgnoreCase(b.getName()));
        List<Map<String, Object>> out = new ArrayList<>();
        for (File f : children) {
            if (!f.isFile() || !hasAllowedExtension(f.getName(), CUSTOM_FONT_EXTS)) continue;
            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("name", f.getName());
            entry.put("sizeBytes", f.length());
            out.add(entry);
        }
        return out;
    }

    private static Map<String, Object> parseOptionalJsonObject(String json) throws IOException {
        if (json == null || json.trim().isEmpty()) return Collections.emptyMap();
        return MiniJson.parseObject(json);
    }

    private static String nullableTrimmed(String s) {
        if (s == null) return null;
        String t = s.trim();
        return t.isEmpty() ? null : t;
    }

    private static List<String> copyStringList(List<?> raw) {
        if (raw == null || raw.isEmpty()) return Collections.emptyList();
        List<String> out = new ArrayList<>();
        for (Object item : raw) {
            if (item == null) continue;
            String s = String.valueOf(item).trim();
            if (!s.isEmpty() && out.size() < 64) out.add(s);
        }
        return out;
    }

    static String sanitizeSingleSegmentName(String raw) {
        if (raw == null) return null;
        String trimmed = raw.trim();
        if (trimmed.isEmpty() || trimmed.length() > 180) return null;
        if (trimmed.equals(".") || trimmed.equals("..")) return null;
        for (int i = 0; i < trimmed.length(); i++) {
            char c = trimmed.charAt(i);
            if (c < 0x20 || c == '/' || c == '\\' || c == ':' || c == '\0') {
                return null;
            }
        }
        if (trimmed.contains("..")) return null;
        return trimmed;
    }

    static String classifyLobbyBgMediaKind(String fileName) {
        if (hasAllowedExtension(fileName, LOBBY_BG_IMAGE_EXTS)) return "image";
        if (hasAllowedExtension(fileName, LOBBY_BG_VIDEO_EXTS)) return "video";
        if (hasAllowedExtension(fileName, LOBBY_BG_USM_EXTS)) return "usm";
        return null;
    }

    private static boolean hasAllowedExtension(String fileName, Set<String> allowed) {
        if (fileName == null) return false;
        int dot = fileName.lastIndexOf('.');
        if (dot < 0) return false;
        return allowed.contains(fileName.substring(dot).toLowerCase(Locale.ROOT));
    }

    private static double doubleField(Map<String, Object> map, String key, double fallback) {
        if (map == null || key == null) return fallback;
        Object val = map.get(key);
        if (val instanceof Number) return ((Number) val).doubleValue();
        if (val instanceof String) {
            try { return Double.parseDouble((String) val); } catch (NumberFormatException ignored) { }
        }
        return fallback;
    }

    private static double clampDouble(double val, double min, double max) {
        if (Double.isNaN(val) || Double.isInfinite(val)) return min;
        return Math.max(min, Math.min(max, val));
    }

    private static int countPngFiles(File dir) {
        if (dir == null || !dir.isDirectory()) return 0;
        File[] children = dir.listFiles();
        if (children == null) return 0;
        int count = 0;
        for (File c : children) {
            if (c.isDirectory()) count += countPngFiles(c);
            else if (c.isFile() && c.getName().toLowerCase(Locale.ROOT).endsWith(".png")) count++;
        }
        return count;
    }

    private static String readUtf8File(File file, int maxBytes) throws IOException {
        if (file.length() > maxBytes) throw new IOException("文件过大");
        try (InputStream in = new FileInputStream(file)) {
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int n;
            while ((n = in.read(buf)) != -1) out.write(buf, 0, n);
            return new String(out.toByteArray(), StandardCharsets.UTF_8);
        }
    }

    private static void writeTextFileAtomic(File target, String content) throws IOException {
        File tmp = new File(target.getParentFile(), target.getName() + ".tmp");
        try (FileOutputStream out = new FileOutputStream(tmp)) {
            out.write(content.getBytes(StandardCharsets.UTF_8));
            out.flush();
            try { out.getFD().sync(); } catch (IOException ignored) { }
        }
        if (target.exists()) target.delete();
        if (!tmp.renameTo(target)) {
            copy(tmp, target);
            tmp.delete();
        }
    }

    private void checkGame(String pkg, GamePackageCatalog.ChannelMeta meta, CoreRuntimeSpec spec) throws Exception {
        String dump = command("/system/bin/dumpsys", "package", pkg);
        String ver = GamePackageCatalog.extractVersionName(dump);
        if (!spec.gameVersion.equals(ver)) {
            throw new IOException("仅适配 " + spec.gameVersion + " 版本（" + meta.label + " " + pkg + " 当前为 "
                + (ver.isEmpty() ? "未安装/未检测到版本" : ver) + "），请勿使用于其他版本或分身目录");
        }
    }

    private void probe(File target) throws Exception {
        try (RandomAccessFile test = new RandomAccessFile(target, "rw")) {
            test.length();
        }
    }

    private boolean running(String pkg) throws Exception {
        String ps = command("/system/bin/ps", "-A");
        return Pattern.compile("(?m)\\s" + Pattern.quote(pkg) + "(?::[^\\s]+)?\\s*$").matcher(ps).find();
    }

    private static String command(String... args) throws Exception {
        Process p = new ProcessBuilder(args).redirectErrorStream(true).start();
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        Thread reader = new Thread(() -> {
            try {
                byte[] b = new byte[8192];
                int n;
                while ((n = p.getInputStream().read(b)) != -1) bytes.write(b, 0, n);
            } catch (IOException ignored) { }
        });
        reader.start();
        if (!p.waitFor(15, TimeUnit.SECONDS)) {
            p.destroyForcibly();
            throw new IOException("系统检查超时");
        }
        reader.join(2000);
        if (p.exitValue() != 0) throw new IOException("系统拒绝检查游戏权限");
        return bytes.toString("UTF-8");
    }

    private static String commandBestEffort(int maxTailBytes, String... args) {
        try {
            Process p = new ProcessBuilder(args).redirectErrorStream(true).start();
            Deque<byte[]> chunks = new ArrayDeque<>();
            final int[] total = { 0 };
            Thread reader = new Thread(() -> {
                try {
                    byte[] b = new byte[8192];
                    int n;
                    while ((n = p.getInputStream().read(b)) != -1) {
                        chunks.addLast(Arrays.copyOf(b, n));
                        total[0] += n;
                        while (total[0] > maxTailBytes && chunks.size() > 1) {
                            total[0] -= chunks.removeFirst().length;
                        }
                    }
                } catch (IOException ignored) { }
            });
            reader.start();
            if (!p.waitFor(12, TimeUnit.SECONDS)) {
                p.destroyForcibly();
            }
            reader.join(2000);
            if (reader.isAlive()) return "";
            ByteArrayOutputStream out = new ByteArrayOutputStream(Math.max(256, total[0]));
            for (byte[] chunk : chunks) {
                out.write(chunk, 0, chunk.length);
            }
            return out.toString("UTF-8");
        } catch (Exception ignored) {
            return "";
        }
    }

    private static void receive(InputStream fd, File target, String sha) throws Exception {
        if (fd == null) throw new IOException("缺少安装文件");
        try (InputStream in = fd; FileOutputStream out = new FileOutputStream(target)) {
            transfer(in, out);
            out.getFD().sync();
        }
        if (!hash(target).equals(sha)) throw new IOException("安装包损坏，SHA256 不匹配，未修改游戏");
    }

    private static void copy(File source, File destination) throws IOException {
        try (InputStream in = new FileInputStream(source); FileOutputStream out = new FileOutputStream(destination)) {
            transfer(in, out);
            out.getFD().sync();
        }
    }

    private static void transfer(InputStream in, OutputStream out) throws IOException {
        byte[] b = new byte[65536];
        int n;
        while ((n = in.read(b)) != -1) out.write(b, 0, n);
    }

    private static String hash(File file) throws Exception {
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        try (InputStream in = new FileInputStream(file)) {
            byte[] b = new byte[65536];
            int n;
            while ((n = in.read(b)) != -1) digest.update(b, 0, n);
        }
        StringBuilder hex = new StringBuilder();
        for (byte b : digest.digest()) hex.append(String.format(Locale.ROOT, "%02x", b & 255));
        return hex.toString();
    }

    private static String friendly(Exception error) {
        String message = String.valueOf(error.getMessage());
        if (message.contains("Permission denied") || message.contains("EACCES") || message.contains("Operation not permitted")) {
            return "系统仍拒绝目录读写。请检查无线调试授权；小米可检查开发者选项的“USB 调试（安全设置）”。未修改游戏，请保留检查结果反馈。\n" + message;
        }
        return message;
    }
}
