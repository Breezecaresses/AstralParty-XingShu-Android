"""Generate a signed standalone Android update manifest after verifying the APK and ZIP."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
REPO = 'Breezecaresses/AstralParty-XingShu-Android'


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def parse_gradle_version(gradle_file: Path) -> tuple[str, int]:
    text = gradle_file.read_text('utf-8')
    m_code = re.search(r"versionCode\s+(\d+)", text)
    m_name = re.search(r"versionName\s+'([^']+)'", text)
    assert m_code and m_name, 'Could not parse versionCode / versionName from app/build.gradle'
    return m_name.group(1), int(m_code.group(1))


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--android', type=Path, required=True)
    p.add_argument('--android-package', type=Path, required=True)
    p.add_argument('--notes', type=Path, required=True)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--aapt', type=Path, default=Path('C:/Android/sdk/build-tools/35.0.0/aapt2.exe'))
    args = p.parse_args()

    version, sequence = parse_gradle_version(ROOT / 'AndroidInstaller/app/build.gradle')
    from package_android_preview import verify_android_archive
    phone_package = verify_android_archive(args.android_package, args.android)
    assert phone_package['releaseVersion'] == version, 'Android ZIP build identity differs'

    badging = subprocess.check_output([str(args.aapt), 'dump', 'badging', str(args.android)], encoding='utf-8')
    identity = re.search(r"package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'", badging)
    assert identity and identity.groups() == ('app.xingshu.installer', str(sequence), version), 'APK build identity differs'

    signer = Path(os.environ['LOCALAPPDATA']) / 'XingShu/Signing'
    public = subprocess.check_output(
        ['java', str(ROOT / 'tools/SignUpdate.java'), str(signer / 'android-installer.jks'), str(signer / 'android-installer.password')],
        text=True,
    ).strip()
    assert public == (ROOT / 'tools/update-public-cert.txt').read_text('ascii').strip(), 'Signing identity changed'
    cert_digest = hashlib.sha256(base64.b64decode(public)).hexdigest()
    certs = subprocess.check_output([str(args.aapt.parent / 'apksigner.bat'), 'verify', '--print-certs', str(args.android)], encoding='utf-8')
    assert 'Signer #1 certificate SHA-256 digest: ' + cert_digest in certs, 'APK signing identity differs'

    notes = args.notes.read_text('utf-8-sig').strip()
    assert 0 < len(notes) <= 6000
    m = {
        'schema': 1,
        'repository': REPO,
        'tag': 'v' + version,
        'version': version,
        'sequence': sequence,
        'notes': notes,
        'android': {
            'asset': args.android.name,
            'size': args.android.stat().st_size,
            'sha256': sha(args.android),
            'versionCode': sequence,
        },
    }
    with tempfile.TemporaryDirectory(prefix='xingshu-android-sign-') as temp:
        payload, signature = Path(temp) / 'payload.json', Path(temp) / 'signature.txt'
        payload.write_text(json.dumps(m, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
        subprocess.run(
            ['java', str(ROOT / 'tools/SignUpdate.java'), str(signer / 'android-installer.jks'), str(signer / 'android-installer.password'), str(payload), str(signature)],
            check=True,
        )
        envelope = {'payload': base64.b64encode(payload.read_bytes()).decode('ascii'), 'signature': signature.read_text('ascii')}
        subprocess.run(
            ['java', str(ROOT / 'tools/SignUpdate.java'), '--verify', str(ROOT / 'tools/update-public-cert.txt'), str(payload), str(signature)],
            check=True,
        )

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(envelope, separators=(',', ':')), encoding='utf-8')
    print('Verified signed standalone Android update metadata:', args.out)


if __name__ == '__main__':
    main()
