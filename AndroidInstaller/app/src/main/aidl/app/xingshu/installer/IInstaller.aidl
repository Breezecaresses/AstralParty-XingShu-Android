package app.xingshu.installer;

import android.os.ParcelFileDescriptor;

interface IInstaller {
    void destroy() = 16777114;
    String scanPackages() = 1;
    String inspect(String packageName) = 2;
    String apply(String packageName, boolean restore, String conflictHash, in ParcelFileDescriptor candidate, in ParcelFileDescriptor vanilla) = 3;
    String readChannelLogcat() = 4;
    String launchGame(String packageName) = 5;
    String writeGameHandoff(String packageName, String payloadJson) = 6;
    String installModPack(String packageName, String metaJson, in ParcelFileDescriptor modZip) = 7;
    String listInstalledMods(String packageName) = 8;
    String removeModPack(String packageName, String modId) = 9;
    String setOfficialModules(String packageName, String moduleIdsCsv, boolean enable, boolean remove) = 10;
    String setCoreSpec(String specJson) = 11;
    String manageCustomize(String packageName, String op, String payloadJson) = 12;
    String importCustomizeFile(String packageName, String targetKind, String fileName, in ParcelFileDescriptor dataFd) = 13;
}
