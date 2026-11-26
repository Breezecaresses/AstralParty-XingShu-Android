"""Build a streamlined pure-launcher Android ZIP (APK + README/License) plus direct APK."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import tempfile
import zipfile
from android_payload import previous_hashes
from project_paths import publish_directory

ROOT = Path(__file__).resolve().parents[1]
APK_NAME = '星枢启动器.apk'
PACKAGE_FILES = {APK_NAME, '先读我.txt', 'LICENSE.txt', 'SHA256SUMS.txt'}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def verify_apk_payload_binding(phone_apk, candidate_sha256, original_sha256):
    """Verify the APK pins the exact candidate and vanilla hashes via bundled assets or DEX constants."""
    with zipfile.ZipFile(phone_apk) as apk:
        names = set(apk.namelist())
        if 'assets/candidate.__data' in names or 'assets/vanilla.__data' in names:
            assert 'assets/candidate.__data' in names and 'assets/vanilla.__data' in names, 'Incomplete bundled APK payloads'
            assert hashlib.sha256(apk.read('assets/candidate.__data')).hexdigest() == candidate_sha256, 'APK bundled candidate mismatch'
            assert hashlib.sha256(apk.read('assets/vanilla.__data')).hexdigest() == original_sha256, 'APK bundled vanilla mismatch'
            return
        dex_names = sorted(n for n in names if '/' not in n and n.endswith('.dex'))
        assert dex_names, 'APK lacks both bundled payloads and DEX bytecode'
        dex_bytes = b''.join(apk.read(n) for n in dex_names)
        assert candidate_sha256.lower().encode('ascii') in dex_bytes, 'Pure launcher APK lacks candidate SHA-256 binding'
        assert original_sha256.lower().encode('ascii') in dex_bytes, 'Pure launcher APK lacks vanilla SHA-256 binding'


def verify_android_archive(archive, phone_apk, manifest=None):
    """Bind the streamlined distribution ZIP, APK, and SHA256SUMS without extracting anything."""
    with zipfile.ZipFile(archive) as package:
        names = package.namelist()
        assert names and len(names) == len(set(names)), 'Duplicate Android ZIP entries'
        roots = {name.split('/')[0] for name in names}
        assert len(roots) == 1, 'Android ZIP must have one root directory'
        root = roots.pop()
        assert re.fullmatch(r'XingShu_Android_[0-9A-Za-z][0-9A-Za-z.\-]{0,79}', root), 'Invalid Android ZIP root'
        assert set(names) == {root + '/' + name for name in PACKAGE_FILES}, 'Unexpected or missing Android ZIP files'
        assert sum(entry.file_size for entry in package.infolist()) <= 1024**3, 'Android ZIP is too large'
        assert package.testzip() is None, 'Corrupt Android ZIP'
        read = lambda name: package.read(root + '/' + name)
        assert hashlib.sha256(read(APK_NAME)).hexdigest() == sha(phone_apk), 'ZIP APK differs from updater APK'
        if manifest is not None:
            verify_apk_payload_binding(phone_apk, manifest['candidateSha256'], manifest['originalSha256'])
        checksums = {}
        for line in read('SHA256SUMS.txt').decode('utf8').splitlines():
            digest, name = line.split('  ', 1)
            assert name not in checksums, 'Duplicate checksum entry'
            checksums[name] = digest
        assert set(checksums) == PACKAGE_FILES - {'SHA256SUMS.txt'}, 'Incomplete Android ZIP checksums'
        for name, digest in checksums.items():
            assert hashlib.sha256(read(name)).hexdigest() == digest, 'Android ZIP checksum mismatch: ' + name
    return {'releaseVersion': root[len('XingShu_Android_'):]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--version', required=True)
    parser.add_argument('--source-commit', required=True)
    parser.add_argument('--phone-apk', type=Path, required=True)
    parser.add_argument('--vanilla', type=Path, required=True)
    parser.add_argument('--previous-manifest', type=Path, action='append', default=[])
    args = parser.parse_args()
    if not re.fullmatch('[a-f0-9]{40}', args.source_commit): raise SystemExit('Expected full source commit')
    if not re.fullmatch(r'[0-9A-Za-z][0-9A-Za-z.\-]{0,79}', args.version): raise SystemExit('Invalid version')
    manifest = json.loads(args.manifest.read_text('utf8'))
    candidate = Path(manifest['candidate'])
    assert sha(candidate) == manifest['candidateSha256'] and sha(args.vanilla) == manifest['originalSha256']
    assert manifest['platform'] == 'ANDROID_CN' and manifest['nativeLibraries'] == []
    verify_apk_payload_binding(args.phone_apk, manifest['candidateSha256'], manifest['originalSha256'])
    _ = previous_hashes(manifest, args.previous_manifest)
    name = 'XingShu_Android_' + args.version
    publish = publish_directory(ROOT)
    publish.mkdir(exist_ok=True)
    destination, archive, phone_asset = publish / name, publish / (name + '.zip'), publish / (name + '.apk')
    outputs = [destination, archive, phone_asset, Path(str(archive) + '.sha256'), Path(str(phone_asset) + '.sha256')]
    if any(path.exists() for path in outputs): raise SystemExit('This phone package already exists; choose a new version')
    with tempfile.TemporaryDirectory(prefix='.building-' + name + '-', dir=publish) as temp:
        stage = Path(temp).resolve()
        assert stage.parent == publish
        package = stage / name
        package.mkdir(parents=True)
        shutil.copy2(args.phone_apk, package / APK_NAME)
        shutil.copy2(ROOT / 'LICENSE', package / 'LICENSE.txt')
        (package / '先读我.txt').write_text(f'''星枢安卓启动器 {args.version}

适配游戏：{manifest['package']} {manifest['appVersion']}（资源 160）；星枢官方插件：{manifest['runtimeVersion']}。

使用说明（轻量纯启动器）
1. 安装包内「{APK_NAME}」（或直接使用同 Release 的 XingShu_Android_{args.version}.apk 覆盖安装）。
2. 先在官方《吉星派对》更新游戏资源、进入一次大厅并正常退出。
3. 打开「星枢启动器」，按界面引导通过 Shizuku 或无线/USB 调试完成权限授权。
4. 默认可直接点击启动官方纯净原版游戏；如需使用局内功能，请前往「模组」或「工坊」页按需在线拉取官方插件与 PE 模组（自动校验 SHA-256，支持一键备份还原官方原版）。

本版已彻底移除冗余桌面辅助工具与重复手替大包；软件自更新仅更新「星枢启动器」本体，更新后请先退出游戏再前往「模组」页核验或升级局内插件。
项目源码：https://github.com/Breezecaresses/AstralParty-XingShu
提交基线：{args.source_commit}
''', encoding='utf-8-sig')
        entries = sorted(path for path in package.rglob('*') if path.is_file())
        (package / 'SHA256SUMS.txt').write_text(''.join(f'{sha(path)}  {path.relative_to(package).as_posix()}\n' for path in entries), encoding='utf8')
        staged_archive = stage / archive.name
        with zipfile.ZipFile(staged_archive, 'x', zipfile.ZIP_DEFLATED, compresslevel=9) as output:
            for path in sorted(package.rglob('*')):
                if path.is_file(): output.write(path, Path(name) / path.relative_to(package))
        verify_android_archive(staged_archive, args.phone_apk, manifest)
        package.rename(destination)
        staged_archive.rename(archive)
        shutil.copy2(destination / APK_NAME, phone_asset)
        for asset in (archive, phone_asset):
            Path(str(asset) + '.sha256').write_text(sha(asset) + '  ' + asset.name + '\n', encoding='utf8')
    print(f'PHONE ZIP: {archive}\nSIZE: {archive.stat().st_size / 1024 / 1024:.2f} MiB\nSHA256: {sha(archive)}')
    print(f'UPDATER APK: {phone_asset}\nSHA256: {sha(phone_asset)}')


if __name__ == '__main__':
    main()
