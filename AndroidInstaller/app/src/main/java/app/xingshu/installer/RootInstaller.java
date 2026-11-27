package app.xingshu.installer;

import android.content.Context;
import android.content.pm.PackageManager;
import java.io.ByteArrayOutputStream;
import java.io.Closeable;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Base64;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

/**
 * Local superuser (`su`) backend for rooted phones and Android emulators.
 * Invokes the same {@link LocalInstallerMain} allowlisted operations without needing ADB or Shizuku.
 */
final class RootInstaller implements Closeable {
    private static final String[] SU_PATHS = {
        "/system/bin/su",
        "/system/xbin/su",
        "/sbin/su",
        "/su/bin/su",
        "/vendor/bin/su",
        "/data/adb/ksu/bin/su",
        "/data/adb/ap/bin/su",
    };

    private final Context context;
    private final ScheduledExecutorService timer = Executors.newSingleThreadScheduledExecutor();
    private volatile boolean rootConnected;
    private volatile String resolvedSuBinary;
    private String lastSyncedSpecJson;

    RootInstaller(Context context) {
        this.context = context.getApplicationContext();
    }

    boolean connected() {
        return rootConnected;
    }

    String resolvedBinary() {
        return resolvedSuBinary;
    }

    static String findSuBinary() {
        for (String candidate : SU_PATHS) {
            try {
                File f = new File(candidate);
                if (f.exists() && f.isFile()) {
                    return candidate;
                }
            } catch (Throwable ignored) { }
        }
        String pathEnv = System.getenv("PATH");
        if (pathEnv != null && !pathEnv.isEmpty()) {
            for (String dir : pathEnv.split(File.pathSeparator)) {
                if (dir == null || dir.trim().isEmpty()) continue;
                try {
                    File f = new File(dir.trim(), "su");
                    if (f.exists() && f.isFile()) {
                        return f.getAbsolutePath();
                    }
                } catch (Throwable ignored) { }
            }
        }
        return null;
    }

    static final class RootManagerInfo {
        final String packageName;
        final String label;
        final boolean kernelSuLike;

        RootManagerInfo(String packageName, String label, boolean kernelSuLike) {
            this.packageName = packageName;
            this.label = label;
            this.kernelSuLike = kernelSuLike;
        }
    }

    private static final String[][] ROOT_MANAGERS = {
        {"me.weishu.kernelsu", "KernelSU", "1"},
        {"com.rifsxd.ksunext", "KernelSU Next", "1"},
        {"com.sukisu.ultra", "SukiSU", "1"},
        {"me.bmax.apatch", "APatch", "1"},
        {"com.topjohnwu.magisk", "Magisk", "0"},
    };

    static RootManagerInfo detectRootManager(Context context) {
        if (context == null) return null;
        PackageManager pm = context.getPackageManager();
        if (pm == null) return null;
        for (String[] entry : ROOT_MANAGERS) {
            try {
                pm.getPackageInfo(entry[0], 0);
                return new RootManagerInfo(entry[0], entry[1], "1".equals(entry[2]));
            } catch (Throwable ignored) { }
        }
        return null;
    }

    static List<String> suCandidateCommands() {
        Set<String> seen = new LinkedHashSet<>();
        String detected = findSuBinary();
        if (detected != null && !detected.isEmpty()) {
            seen.add(detected);
        }
        seen.add("su");
        for (String candidate : SU_PATHS) {
            seen.add(candidate);
        }
        return new ArrayList<>(seen);
    }

    static boolean isSuBinaryPresent() {
        return findSuBinary() != null;
    }

    synchronized void connect() throws Exception {
        lastSyncedSpecJson = null;
        rootConnected = false;
        RootManagerInfo manager = detectRootManager(context);
        List<String> candidates = suCandidateCommands();
        String lastDetail = "";
        int attempts = 0;

        for (String su : candidates) {
            attempts++;
            try {
                ShellResult res = execSu(su, "id -u", 15000L, null);
                String out = res.stdout.trim();
                if (res.exitCode == 0 && "0".equals(out)) {
                    resolvedSuBinary = su;
                    rootConnected = true;
                    return;
                }
                if (!out.isEmpty()) {
                    lastDetail = out.replace('\n', ' ');
                } else {
                    lastDetail = "退出码 " + res.exitCode;
                }
            } catch (IOException spawnError) {
                if (lastDetail.isEmpty()) {
                    lastDetail = spawnError.getMessage() == null ? "无法执行 su" : spawnError.getMessage();
                }
            }
        }

        if (manager != null && manager.kernelSuLike) {
            throw new IOException(
                "检测到已安装 " + manager.label + "，但当前应用尚未获得超级用户放行"
                    + (attempts > 0 && !lastDetail.isEmpty() ? "（" + lastDetail + "）" : "")
                    + "。请点击下方「打开 " + manager.label + " 授权」→ 进入「超级用户」页 → 开启「星枢启动器」开关后返回重试。"
            );
        }
        if (manager != null) {
            throw new IOException(
                "Root 权限请求未通过（" + (lastDetail.isEmpty() ? "未放行" : lastDetail)
                    + "），请在 " + manager.label + " 或模拟器设置中允许星枢获取超级用户权限后重试。"
            );
        }
        if (!isSuBinaryPresent()) {
            throw new IOException("当前设备未检测到可用 su（也没有检测到 KernelSU / Magisk）：电脑模拟器可在「设置 → 其他/性能 → 开启手机 Root 权限」打开后重试，或改用无线调试 / Shizuku。");
        }
        throw new IOException(
            "Root 权限请求未通过（" + (lastDetail.isEmpty() ? "未放行" : lastDetail)
                + "），请在超级用户管理器中允许星枢获取 Root 权限。"
        );
    }

    void disconnect() {
        rootConnected = false;
        lastSyncedSpecJson = null;
    }

    synchronized void syncCoreSpec(CoreRuntimeSpec spec) throws Exception {
        if (spec == null) return;
        String json = spec.toCanonicalJson();
        if (json.equals(lastSyncedSpecJson)) return;
        File cacheDir = context.getCacheDir();
        if (cacheDir != null && !cacheDir.isDirectory()) cacheDir.mkdirs();
        File tmp = File.createTempFile("xingshu-core-spec-", ".json", cacheDir);
        try {
            try (FileOutputStream out = new FileOutputStream(tmp)) {
                out.write(json.getBytes(StandardCharsets.UTF_8));
                out.flush();
            }
            shell("mkdir -p " + quote(LocalInstallerMain.STAGING), 20000);
            pushFile(tmp, "core-spec.json");
            lastSyncedSpecJson = json;
        } finally {
            tmp.delete();
        }
    }

    String scanPackages() throws Exception {
        return operation("scan", null);
    }

    String readChannelLogcat() throws Exception {
        return operation("channel-logcat", null);
    }

    String launchGame(String packageName) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        return operation("launch", pkg);
    }

    String writeGameHandoff(String packageName, String payloadJson) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        String b64 = Base64.getEncoder().encodeToString(payloadJson.getBytes(StandardCharsets.UTF_8));
        return operation("handoff", pkg, b64);
    }

    String inspect(String packageName) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        return operation("inspect", pkg);
    }

    String apply(String packageName, boolean restore, String conflictHash, File candidateFile, File vanillaFile) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        String preflight = inspect(pkg);
        if (!preflight.startsWith("游戏版本匹配") || preflight.contains("游戏仍在运行") || preflight.contains("未知版本")) {
            return preflight;
        }
        if (preflight.contains("同位置覆盖冲突") && conflictHash == null) {
            return "检测到同位置覆盖冲突，请先确认备份整理。\n" + preflight;
        }
        if (!restore && (candidateFile == null || !candidateFile.isFile())) {
            throw new IOException("缺少星枢核心运行包，请先下载完成。");
        }
        shell("mkdir -p " + quote(LocalInstallerMain.STAGING), 20000);
        try {
            if (vanillaFile != null && vanillaFile.isFile()) {
                pushFile(vanillaFile, "vanilla.__data");
            }
            if (!restore) {
                pushFile(candidateFile, "candidate.__data");
            }
            String op = restore ? "restore" : (conflictHash == null ? "install" : "install-conflict:" + conflictHash);
            return operation(op, pkg);
        } finally {
            try {
                shell(
                    "rm -f " + quote(LocalInstallerMain.STAGING + "/vanilla.__data") + " "
                        + quote(LocalInstallerMain.STAGING + "/candidate.__data"),
                    20000
                );
            } catch (Exception ignored) { }
        }
    }

    String installModPack(String packageName, String metaJson, File modZipFile) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        if (modZipFile == null || !modZipFile.isFile()) {
            throw new IOException("缺少待安装的 Mod 压缩包。");
        }
        String b64 = Base64.getEncoder().encodeToString(metaJson.getBytes(StandardCharsets.UTF_8));
        shell("mkdir -p " + quote(LocalInstallerMain.STAGING), 20000);
        try {
            pushFile(modZipFile, "mod.zip");
            return operation("install-mod", pkg, b64);
        } finally {
            try {
                shell("rm -f " + quote(LocalInstallerMain.STAGING + "/mod.zip"), 20000);
            } catch (Exception ignored) { }
        }
    }

    String listInstalledMods(String packageName) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        return operation("list-mods", pkg);
    }

    String removeModPack(String packageName, String modId) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        if (modId == null || !modId.matches("[A-Za-z0-9._-]{1,120}")) {
            throw new IllegalArgumentException("模组标识无效");
        }
        return operation("remove-mod", pkg, modId);
    }

    String setOfficialModules(String packageName, String moduleIdsCsv, boolean enable, boolean remove) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        if (moduleIdsCsv == null || !moduleIdsCsv.matches("[a-z0-9,-]{1,512}")) {
            throw new IllegalArgumentException("官方模块参数无效");
        }
        String mode = remove ? "remove" : (enable ? "enable" : "disable");
        return operation("set-official-modules", pkg, mode, moduleIdsCsv);
    }

    String manageCustomize(String packageName, String op, String payloadJson) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        if (op == null || !op.matches("[a-z0-9_]{1,40}")) {
            throw new IllegalArgumentException("自定义操作名无效");
        }
        String raw = payloadJson == null || payloadJson.trim().isEmpty() ? "{}" : payloadJson;
        String b64 = Base64.getEncoder().encodeToString(raw.getBytes(StandardCharsets.UTF_8));
        return operation("customize-op", pkg, op, b64);
    }

    String importCustomizeFile(String packageName, String targetKind, String fileName, File stagedLocalFile) throws Exception {
        String pkg = GamePackageCatalog.requireValidPackageName(packageName);
        if (targetKind == null || !targetKind.matches("[a-z0-9_]{1,32}")) {
            throw new IllegalArgumentException("自定义目录类型无效");
        }
        if (stagedLocalFile == null || !stagedLocalFile.isFile()) {
            throw new IllegalArgumentException("待导入文件不存在");
        }
        String b64Name = Base64.getEncoder().encodeToString((fileName == null ? "" : fileName).getBytes(StandardCharsets.UTF_8));
        shell("mkdir -p " + quote(LocalInstallerMain.STAGING), 20000);
        try {
            pushFile(stagedLocalFile, "customize_import.bin");
            return operation("customize-import", pkg, targetKind, b64Name);
        } finally {
            try {
                shell("rm -f " + quote(LocalInstallerMain.STAGING + "/customize_import.bin"), 20000);
            } catch (Exception ignored) { }
        }
    }

    private String operation(String operation, String packageName) throws Exception {
        return operation(operation, packageName, null, null);
    }

    private String operation(String operation, String packageName, String extraArg) throws Exception {
        return operation(operation, packageName, extraArg, null);
    }

    private String operation(String operation, String packageName, String extraArg, String extraArg2) throws Exception {
        String cmd = "CLASSPATH=" + quote(context.getApplicationInfo().sourceDir)
            + " /system/bin/app_process / app.xingshu.installer.LocalInstallerMain " + quote(operation);
        if (packageName != null && !packageName.isEmpty()) {
            cmd += " " + quote(GamePackageCatalog.requireValidPackageName(packageName));
        }
        if (extraArg != null && !extraArg.isEmpty()) {
            cmd += " " + quote(extraArg);
        }
        if (extraArg2 != null && !extraArg2.isEmpty()) {
            cmd += " " + quote(extraArg2);
        }
        String output = shell(cmd, 180000L);
        String marker = "XINGSHU_RESULT:";
        for (String line : output.split("[\\r\\n]+")) {
            if (line.startsWith(marker)) {
                return new String(Base64.getDecoder().decode(line.substring(marker.length()).trim()), StandardCharsets.UTF_8);
            }
        }
        throw new IOException("Root 授权进程未返回结果：" + output.substring(0, Math.min(output.length(), 1200)));
    }

    private String shell(String command, long timeoutMs) throws Exception {
        if (!rootConnected) {
            throw new IOException("Root 授权未连接，请先在「配置授权」中授予 Root 权限");
        }
        String su = resolvedSuBinary != null ? resolvedSuBinary : (findSuBinary() != null ? findSuBinary() : "su");
        ShellResult res = execSu(su, command, timeoutMs, null);
        if (res.exitCode != 0 && res.stdout.trim().isEmpty()) {
            rootConnected = false;
            throw new IOException("Root 命令执行失败（退出码 " + res.exitCode + "），请确认 Root 授权仍有效");
        }
        return res.stdout;
    }

    private void pushFile(File localFile, String remoteName) throws Exception {
        validateRemoteFileName(remoteName);
        if (!rootConnected) {
            throw new IOException("Root 授权未连接，请先授予 Root 权限");
        }
        String su = resolvedSuBinary != null ? resolvedSuBinary : (findSuBinary() != null ? findSuBinary() : "su");
        String dest = LocalInstallerMain.STAGING + "/" + remoteName;
        String cmd = "mkdir -p " + quote(LocalInstallerMain.STAGING)
            + " && cat > " + quote(dest)
            + " && chmod 600 " + quote(dest);
        ShellResult res = execSu(su, cmd, 120000L, localFile);
        if (res.exitCode != 0) {
            throw new IOException("Root 写入暂存文件失败：" + res.stdout.trim());
        }
    }

    static void validateRemoteFileName(String remoteName) {
        if (remoteName == null || !remoteName.matches("[A-Za-z0-9._-]{1,64}") || remoteName.contains("..")) {
            throw new IllegalArgumentException("非法的暂存文件名");
        }
    }

    private static final class ShellResult {
        final int exitCode;
        final String stdout;

        ShellResult(int exitCode, String stdout) {
            this.exitCode = exitCode;
            this.stdout = stdout;
        }
    }

    private ShellResult execSu(String suBinary, String command, long timeoutMs, File stdinFile) throws Exception {
        Process p = new ProcessBuilder(suBinary, "-c", command)
            .redirectErrorStream(true)
            .start();
        ScheduledFuture<?> deadline = timer.schedule(() -> {
            try { p.destroyForcibly(); } catch (Exception ignored) { }
        }, timeoutMs, TimeUnit.MILLISECONDS);
        try {
            if (stdinFile != null) {
                try (InputStream fileIn = new FileInputStream(stdinFile);
                     OutputStream procOut = p.getOutputStream()) {
                    byte[] buf = new byte[16384];
                    int n;
                    while ((n = fileIn.read(buf)) != -1) {
                        procOut.write(buf, 0, n);
                    }
                    procOut.flush();
                }
            } else {
                try { p.getOutputStream().close(); } catch (Exception ignored) { }
            }
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            try (InputStream in = p.getInputStream()) {
                byte[] b = new byte[8192];
                int n;
                while ((n = in.read(b)) != -1) {
                    out.write(b, 0, n);
                    if (out.size() > 2 * 1024 * 1024) {
                        throw new IOException("Root 命令输出超出限制");
                    }
                }
            }
            int code = p.waitFor();
            return new ShellResult(code, out.toString(StandardCharsets.UTF_8.name()).replace("\r", ""));
        } finally {
            deadline.cancel(false);
            try { p.destroyForcibly(); } catch (Exception ignored) { }
        }
    }

    static String quote(String s) {
        return "'" + s.replace("'", "'\"'\"'") + "'";
    }

    @Override
    public void close() {
        disconnect();
        timer.shutdownNow();
    }
}
