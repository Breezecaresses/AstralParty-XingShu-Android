"""Generate standalone remote android-core-manifest.json for decoupled Android XingShu Runtime hot-updates."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import urlparse

from android_payload import previous_hashes, sha, validate_baseline_manifest

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MANIFEST = ROOT / "XingShu-Dev/build/android-r836/candidate.json"
DEFAULT_VANILLA = ROOT / "XingShu-Dev/build/android-160-official/__data"
DEFAULT_OUT = ROOT / "XingShu-Dev/build/android-r836/android-core-manifest.json"
DEFAULT_CANDIDATE_URL = (
    "https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-preview8.36/candidate.__data"
)
DEFAULT_VANILLA_URL = (
    "https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-preview8.36/vanilla.__data"
)

OFFICIAL_HOST = "astralparty-xingshu.lanxichaxu.cn"
RUNTIME_PATH_PREFIX = "/xingshu-hub/assets/launcher-runtime/"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
SAFE_VERSION_RE = re.compile(r"^[A-Za-z0-9._-]+$")
BUNDLE_REL_RE = re.compile(r"^com\.unity\.addressables/AssetBundles/[0-9a-f]{32}/[0-9a-f]{32}/__data$")
MAX_PAYLOAD_BYTES = 128 * 1024 * 1024


def validate_runtime_url(raw_url: str, label: str) -> str:
    url = (raw_url or "").strip()
    parsed = urlparse(url)
    if (
        parsed.scheme != "https"
        or parsed.netloc.lower() != OFFICIAL_HOST
        or not parsed.path.startswith(RUNTIME_PATH_PREFIX)
        or ".." in parsed.path
        or "//" in parsed.path
        or parsed.query
        or parsed.fragment
    ):
        raise ValueError(f"Invalid {label}: {raw_url!r}")
    return url


def build_android_core_manifest(
    manifest_path: Path,
    vanilla_path: Path,
    candidate_url: str,
    vanilla_url: str,
    previous_manifest_paths: list[Path],
) -> dict:
    m = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    validate_baseline_manifest(m)

    runtime_version = str(m.get("runtimeVersion") or "").strip()
    game_version = str(m.get("appVersion") or "").strip()
    resource_version = str(m.get("resourceVersion") or "160").strip()
    catalog_rel = f"com.unity.addressables/catalog_{game_version}.json"
    catalog_sha = str(m.get("catalogSha256") or "").lower()
    bundle_rel = str(m.get("bundleRelativePath") or "")
    patch_sha = str(m.get("candidateSha256") or "").lower()
    vanilla_sha = str(m.get("originalSha256") or "").lower()

    if not runtime_version or len(runtime_version) > 64 or not SAFE_VERSION_RE.match(runtime_version):
        raise ValueError(f"Invalid runtimeVersion: {runtime_version!r}")
    if not game_version or len(game_version) > 32 or not SAFE_VERSION_RE.match(game_version):
        raise ValueError(f"Invalid gameVersion: {game_version!r}")
    if len(resource_version) > 32 or (resource_version and not SAFE_VERSION_RE.match(resource_version)):
        raise ValueError(f"Invalid resourceVersion: {resource_version!r}")
    if not BUNDLE_REL_RE.match(bundle_rel):
        raise ValueError(f"Invalid bundleRelativePath: {bundle_rel!r}")
    for key, val in (("catalogSha256", catalog_sha), ("candidateSha256", patch_sha), ("originalSha256", vanilla_sha)):
        if not SHA256_RE.match(val):
            raise ValueError(f"Invalid {key}: {val!r}")

    cand_raw = Path(m["candidate"])
    candidate_path = cand_raw if cand_raw.is_absolute() else (manifest_path.parent / cand_raw)
    if not candidate_path.is_file():
        raise FileNotFoundError(f"Missing candidate payload: {candidate_path}")
    if not vanilla_path.is_file():
        raise FileNotFoundError(f"Missing vanilla payload: {vanilla_path}")

    if sha(candidate_path) != patch_sha:
        raise ValueError(f"Candidate SHA-256 mismatch on disk: {candidate_path}")
    if sha(vanilla_path) != vanilla_sha:
        raise ValueError(f"Vanilla SHA-256 mismatch on disk: {vanilla_path}")

    cand_size = candidate_path.stat().st_size
    van_size = vanilla_path.stat().st_size
    if not (1 <= cand_size <= MAX_PAYLOAD_BYTES) or not (1 <= van_size <= MAX_PAYLOAD_BYTES):
        raise ValueError(f"Payload size out of bounds: candidate={cand_size}, vanilla={van_size}")

    safe_cand_url = validate_runtime_url(candidate_url, "--candidate-url")
    safe_van_url = validate_runtime_url(vanilla_url, "--vanilla-url")
    known_prev = sorted(
        h.lower() for h in previous_hashes(m, previous_manifest_paths) if SHA256_RE.match(h.lower())
    )

    return {
        "formatVersion": 1,
        "runtimeVersion": runtime_version,
        "gameVersion": game_version,
        "resourceVersion": resource_version,
        "catalogRelativePath": catalog_rel,
        "catalogSha256": catalog_sha,
        "bundleRelativePath": bundle_rel,
        "patchSha256": patch_sha,
        "vanillaSha256": vanilla_sha,
        "candidateUrl": safe_cand_url,
        "vanillaUrl": safe_van_url,
        "candidateSize": cand_size,
        "vanillaSize": van_size,
        "previousHashes": known_prev,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--vanilla", type=Path, default=DEFAULT_VANILLA)
    parser.add_argument("--candidate-url", default=DEFAULT_CANDIDATE_URL)
    parser.add_argument("--vanilla-url", default=DEFAULT_VANILLA_URL)
    parser.add_argument("--previous-manifest", type=Path, action="append", default=[])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()

    bundle = build_android_core_manifest(
        args.manifest,
        args.vanilla,
        args.candidate_url,
        args.vanilla_url,
        args.previous_manifest,
    )
    encoded = json.dumps(bundle, ensure_ascii=False, indent=2) + "\n"
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(encoded, encoding="utf-8")
    digest = hashlib.sha256(encoded.encode("utf-8")).hexdigest()
    print(
        f"Wrote {args.out} ({len(encoded.encode('utf-8'))} B, sha256={digest}, "
        f"runtime={bundle['runtimeVersion']}, game={bundle['gameVersion']})"
    )


if __name__ == "__main__":
    main()
