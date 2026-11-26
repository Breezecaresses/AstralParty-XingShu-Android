package app.xingshu.installer;

import android.content.Context;
import android.os.Binder;
import android.os.ParcelFileDescriptor;
import java.io.InputStream;

/** Optional Shizuku transport; shares the same multi-channel transaction engine. */
public final class InstallerService extends IInstaller.Stub {
    private final int ownerUid;
    private final InstallerEngine engine = new InstallerEngine();

    public InstallerService(Context context) {
        ownerUid = context.getApplicationInfo().uid;
    }

    private void caller() {
        if (Binder.getCallingUid() != ownerUid) throw new SecurityException("调用方不匹配");
    }

    @Override
    public void destroy() {
        int uid = Binder.getCallingUid();
        if (uid != ownerUid && uid != 2000 && uid != 0) throw new SecurityException("调用方不匹配");
        System.exit(0);
    }

    @Override
    public synchronized String scanPackages() {
        caller();
        return engine.scanPackages();
    }

    @Override
    public synchronized String inspect(String packageName) {
        caller();
        return engine.inspect(packageName);
    }

    @Override
    public synchronized String apply(String packageName, boolean restore, String conflictHash,
            ParcelFileDescriptor candidate, ParcelFileDescriptor vanilla) {
        caller();
        try (InputStream p = candidate == null ? null : new ParcelFileDescriptor.AutoCloseInputStream(candidate);
             InputStream v = vanilla == null ? null : new ParcelFileDescriptor.AutoCloseInputStream(vanilla)) {
            return engine.apply(packageName, restore, conflictHash, p, v);
        } catch (Exception e) {
            return "未完成：" + e.getMessage();
        }
    }

    @Override
    public synchronized String readChannelLogcat() {
        caller();
        return engine.readChannelTokensJson();
    }

    @Override
    public synchronized String launchGame(String packageName) {
        caller();
        return engine.launchGame(packageName);
    }

    @Override
    public synchronized String writeGameHandoff(String packageName, String payloadJson) {
        caller();
        return engine.writeGameHandoff(packageName, payloadJson);
    }

    @Override
    public synchronized String installModPack(String packageName, String metaJson, ParcelFileDescriptor modZip) {
        caller();
        try (InputStream z = modZip == null ? null : new ParcelFileDescriptor.AutoCloseInputStream(modZip)) {
            return engine.installModPack(packageName, metaJson, z);
        } catch (Exception e) {
            return MiniJson.stringify(java.util.Collections.singletonMap("error", e.getMessage() == null ? "安装模组失败" : e.getMessage()));
        }
    }

    @Override
    public synchronized String listInstalledMods(String packageName) {
        caller();
        return engine.listInstalledMods(packageName);
    }

    @Override
    public synchronized String removeModPack(String packageName, String modId) {
        caller();
        return engine.removeModPack(packageName, modId);
    }

    @Override
    public synchronized String setOfficialModules(String packageName, String moduleIdsCsv, boolean enable, boolean remove) {
        caller();
        return engine.setOfficialModules(packageName, moduleIdsCsv, enable, remove);
    }

    @Override
    public synchronized String setCoreSpec(String specJson) {
        caller();
        return engine.updateCoreSpec(specJson);
    }

    @Override
    public synchronized String manageCustomize(String packageName, String op, String payloadJson) {
        caller();
        return engine.manageCustomize(packageName, op, payloadJson);
    }

    @Override
    public synchronized String importCustomizeFile(String packageName, String targetKind, String fileName, ParcelFileDescriptor dataFd) {
        caller();
        try (InputStream in = dataFd == null ? null : new ParcelFileDescriptor.AutoCloseInputStream(dataFd)) {
            return engine.importCustomizeFile(packageName, targetKind, fileName, in);
        } catch (Exception e) {
            return MiniJson.stringify(java.util.Collections.singletonMap("error", e.getMessage() == null ? "导入自定义文件失败" : e.getMessage()));
        }
    }
}
