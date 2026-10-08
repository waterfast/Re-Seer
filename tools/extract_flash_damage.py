"""Extract the Flash HpMC digit frames without executing ActionScript."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import uuid

from PIL import Image
from flash_asset_utils import decode_swf

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/Art/UI/DamageNumbers/Critical"
WORK = ROOT / "Temp/DamageDisplay"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--java", default="C:/Program Files/Java/jdk-19/bin/java.exe")
    parser.add_argument("--ffdec", default="C:/Program Files (x86)/FFDec/ffdec.jar")
    parser.add_argument("--source", type=Path, default=ROOT / "docs/OfficialReference/20261006/dll/PetFightDLL_201308.swf")
    args = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    original = args.source.read_bytes()
    decoded = WORK / "fight.swf"
    decoded.write_bytes(decode_swf(original))
    subprocess.run([args.java, "-Xmx1024m", "-Djava.awt.headless=true", "-jar", args.ffdec,
                    "-selectid", "184,205,207", "-zoom", "3", "-ignorebackground",
                    "-export", "sprite", str(WORK / "rendered"), str(decoded)], check=True, timeout=150)
    template = (ROOT / "Assets/Art/Battle/Flash/TestCollection/UI/Parts/hp-fill-normal.png.meta").read_text()
    records = []
    for name, character, frame in [(f"{i}", 205, i + 1) for i in range(10)] + [("minus", 207, 1), ("critical-flame", 184, 1)]:
        source = next((WORK / "rendered").glob(f"DefineSprite_{character}*/{frame}.png"))
        image = Image.open(source).convert("RGBA")
        bounds = image.getchannel("A").getbbox()
        if bounds is None:
            raise ValueError(f"Empty Flash frame: {character}/{frame}")
        # Keep the shared digit baseline; only remove horizontal transparent margins.
        crop = (bounds[0], 0, bounds[2], image.height) if character == 205 else bounds
        destination = OUTPUT / f"{name}.png"
        image.crop(crop).save(destination)
        meta = destination.with_suffix(".png.meta")
        if not meta.exists():
            import re
            contents = re.sub(r"(?m)^guid: .*", "guid: " + uuid.uuid4().hex, template)
            contents = re.sub(r"spriteID: \w+", "spriteID: " + uuid.uuid4().hex, contents)
            contents = contents.replace("spritePixelsToUnits: 100", "spritePixelsToUnits: 600")
            contents = contents.replace("textureCompression: 1", "textureCompression: 0")
            meta.write_text(contents, encoding="utf-8")
        records.append({"file": destination.relative_to(ROOT).as_posix(), "characterId": character,
                        "frame": frame, "renderScale": 3, "crop": crop,
                        "sha256": hashlib.sha256(destination.read_bytes()).hexdigest()})
    provenance = {"sourceUrl": "https://seer.61.com/dll/PetFightDLL_201308.swf",
                  "localSource": args.source.relative_to(ROOT).as_posix(),
                  "sourceSha256": hashlib.sha256(original).hexdigest(),
                  "notes": "HpMC (209): numbers 205 frames 1–10 map to 0–9, negative 207, movie 184. Critical display reuses these digits with larger scale and the Flash flame decoration; no separate critical digit set was extracted.",
                  "assets": records}
    (OUTPUT / "sources.json").write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
