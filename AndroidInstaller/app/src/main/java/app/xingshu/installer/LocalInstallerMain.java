package app.xingshu.installer;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.Base64;

/** Short-lived shell entry point under UID 2000 for multi-channel install, package discovery and BnSdk scan. */
public final class LocalInstallerMain {
    static final String STAGING = "/data/local/tmp/xingshu-phone/current";

    public static void main(String[] args) {
        String result;
        try {
            int uid = android.os.Process.myUid();
            if (uid != 2000 && uid != 0) throw new SecurityException("需要本机无线调试授权");
            if (args.length < 1 || args.length > 4) throw new IllegalArgumentException("操作不匹配");

            InstallerEngine engine = new InstallerEngine();
            File specFile = new File(STAGING, "core-spec.json");
            if (specFile.isFile() && specFile.length() > 0L) {
                if (specFile.length() > CoreRuntimeSpec.MAX_MANIFEST_BYTES) {
                    throw new IllegalArgumentException("核心清单同步文件过大");
                }
                String specJson = CoreRuntimeSpec.readUtf8File(specFile, CoreRuntimeSpec.MAX_MANIFEST_BYTES);
                engine.setActiveSpec(CoreRuntimeSpec.parseAndValidate(specJson, "staged"));
            }
            String operation = args[0];
            String packageName = args.length >= 2 ? GamePackageCatalog.requireValidPackageName(args[1]) : GamePackageCatalog.DEFAULT_PACKAGE;
            String conflictHash = null;
            if (operation.startsWith("install-conflict:")) {
                conflictHash = operation.substring("install-conflict:".length());
                if (!conflictHash.matches("[a-f0-9]{64}")) throw new IllegalArgumentException("冲突校验值无效");
                operation = "install";
            }

            switch (operation) {
                case "scan":
                    result = engine.scanPackages();
                    break;
                case "channel-logcat":
                    result = engine.readChannelTokensJson();
                    break;
                case "launch":
                    result = engine.launchGame(packageName);
                    break;
                case "handoff":
                    if (args.length != 3 || !args[2].matches("[A-Za-z0-9+/=]{8,512}")) {
                        throw new IllegalArgumentException("同步凭据格式无效");
                    }
                    String json = new String(Base64.getDecoder().decode(args[2]), StandardCharsets.UTF_8);
                    result = engine.writeGameHandoff(packageName, json);
                    break;
                case "inspect":
                    result = engine.inspect(packageName);
                    break;
                case "install":
                case "restore":
                    boolean restore = operation.equals("restore");
                    File vanillaStaged = new File(STAGING, "vanilla.__data");
                    try (InputStream p = restore ? null : new FileInputStream(new File(STAGING, "candidate.__data"));
                         InputStream v = vanillaStaged.isFile() ? new FileInputStream(vanillaStaged) : null) {
                        result = engine.apply(packageName, restore, conflictHash, p, v);
                    }
                    break;
                case "install-mod":
                    if (args.length != 3 || !args[2].matches("[A-Za-z0-9+/=]{4,32768}")) {
                        throw new IllegalArgumentException("模组元数据无效");
                    }
                    String modMetaJson = new String(Base64.getDecoder().decode(args[2]), StandardCharsets.UTF_8);
                    try (InputStream z = new FileInputStream(new File(STAGING, "mod.zip"))) {
                        result = engine.installModPack(packageName, modMetaJson, z);
                    }
                    break;
                case "list-mods":
                    result = engine.listInstalledMods(packageName);
                    break;
                case "remove-mod":
                    if (args.length != 3 || !args[2].matches("[A-Za-z0-9._-]{1,120}")) {
                        throw new IllegalArgumentException("模组标识无效");
                    }
                    result = engine.removeModPack(packageName, args[2]);
                    break;
                case "set-official-modules":
                    if (args.length != 4 || !args[3].matches("[a-z0-9,-]{1,512}")) {
                        throw new IllegalArgumentException("官方模块参数无效");
                    }
                    String mode = args[2];
                    if ("enable".equals(mode)) {
                        result = engine.setOfficialModules(packageName, args[3], true, false);
                    } else if ("disable".equals(mode)) {
                        result = engine.setOfficialModules(packageName, args[3], false, false);
                    } else if ("remove".equals(mode)) {
                        result = engine.setOfficialModules(packageName, args[3], false, true);
                    } else {
                        throw new IllegalArgumentException("官方模块操作模式无效");
                    }
                    break;
                case "customize-op":
                    if (args.length != 4 || !args[2].matches("[a-z0-9_]{1,40}")
                            || !args[3].matches("[A-Za-z0-9+/=]{2,131072}")) {
                        throw new IllegalArgumentException("自定义参数无效");
                    }
                    String customizePayload = new String(Base64.getDecoder().decode(args[3]), StandardCharsets.UTF_8);
                    result = engine.manageCustomize(packageName, args[2], customizePayload);
                    break;
                case "customize-import":
                    if (args.length != 4 || !args[2].matches("[a-z0-9_]{1,32}")
                            || !args[3].matches("[A-Za-z0-9+/=]{4,1024}")) {
                        throw new IllegalArgumentException("自定义导入参数无效");
                    }
                    String importName = new String(Base64.getDecoder().decode(args[3]), StandardCharsets.UTF_8);
                    try (InputStream cin = new FileInputStream(new File(STAGING, "customize_import.bin"))) {
                        result = engine.importCustomizeFile(packageName, args[2], importName, cin);
                    }
                    break;
                default:
                    throw new IllegalArgumentException("操作不匹配");
            }
        } catch (Throwable e) {
            result = "未完成：" + e.getClass().getSimpleName() + " " + e.getMessage();
        }
        System.out.println("XINGSHU_RESULT:" + Base64.getEncoder().encodeToString(result.getBytes(StandardCharsets.UTF_8)));
        System.out.flush();
        System.exit(0);
    }
}
