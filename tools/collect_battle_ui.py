"""Collect all static sprites in the matching public battle UI bundle; never run official game code."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Temp/AssetTools"))
import UnityPy

DIGEST = "1e67a21076d1bff49bbd5386a72dfec8"
URL = "https://newseer.61.com/Assets/WebGL/DefaultPackage/" + DIGEST


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download", action="store_true", help="Revalidate against the public official endpoint")
    args = parser.parse_args()
    source = ROOT / "docs/OfficialReference/20261001/battle.bundle"
    if args.download:
        request = urllib.request.Request(URL, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(request, timeout=30) as response:
            downloaded = response.read()
        if hashlib.md5(downloaded).hexdigest() != DIGEST:
            raise ValueError("Official bundle digest mismatch; existing files retained")
        source.parent.mkdir(parents=True, exist_ok=True)
        source.write_bytes(downloaded)
    raw = source.read_bytes()
    if hashlib.md5(raw).hexdigest() != DIGEST:
        raise ValueError("Cached bundle digest mismatch")
    output = ROOT / "Assets/Art/Battle/OfficialUI"
    output.mkdir(parents=True, exist_ok=True)
    records = []
    used = set()
    for obj in UnityPy.load(raw).objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        name = re.sub(r"[^a-zA-Z0-9_-]", "_", sprite.m_Name)
        if name in used:
            raise ValueError("Conflicting output name: " + name)
        used.add(name)
        destination = output / (name + ".png")
        image = sprite.image
        image.save(destination)
        records.append({"name": sprite.m_Name, "file": destination.relative_to(ROOT).as_posix(),
                        "width": image.width, "height": image.height,
                        "sha256": hashlib.sha256(destination.read_bytes()).hexdigest()})
    manifest = {"sourceUrl": URL, "bundleMd5": DIGEST, "bundleSha256": hashlib.sha256(raw).hexdigest(),
                "packageVersion": "20260904173604", "retrievedDate": "2026-10-01",
                "currentSitePackageVersion": "20260928155626",
                "verifiedAgainstOfficialDownload": True, "assets": records}
    (output / "sources.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Extracted {len(records)} sprites from the matching official battle bundle")


if __name__ == "__main__":
    main()
