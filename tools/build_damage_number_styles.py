"""Prepare shared number styles from cached official glyphs and Flash font metadata.

Use JPEXS to export FangZhengZongyi (font 105) from the decoded RobotCoreDLL
to Temp/DamageDisplay/official-fonts before running. No game scripts are executed.
"""
import hashlib
import json
from pathlib import Path
import re
import shutil
import uuid

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/Art/UI/DamageNumbers"


def ensure_meta(path, pixels_per_unit=None):
    target = Path(str(path) + ".meta")
    if target.exists():
        return re.search(r"^guid: (\w+)", target.read_text(), re.M)[1]
    guid = uuid.uuid4().hex
    if pixels_per_unit is not None:
        template = (ROOT / "Assets/Art/Battle/Fonts/0.png.meta").read_text()
        template = re.sub(r"(?m)^guid: .*", "guid: " + guid, template)
        template = re.sub(r"spriteID: \w+", "spriteID: " + uuid.uuid4().hex, template)
        template = re.sub(r"spritePixelsToUnits: \d+", f"spritePixelsToUnits: {pixels_per_unit}", template)
        template = template.replace("textureCompression: 1", "textureCompression: 0")
    else:
        template = "fileFormatVersion: 2\nguid: " + guid + "\n"
        if path.is_dir():
            template += "folderAsset: yes\n"
        template += "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    target.write_text(template, encoding="utf-8")
    return guid


def rasterize_font(folder, font_path, fill, outline, pixels_per_unit=100, characters="0123456789+-"):
    font = ImageFont.truetype(str(font_path), 96)
    top = min(font.getbbox(c)[1] for c in "0123456789+-")
    bottom = max(font.getbbox(c)[3] for c in "0123456789+-")
    records = []
    for char in characters:
        width = round(font.getlength(char))
        image = Image.new("RGBA", (width + 18, bottom - top + 18))
        draw = ImageDraw.Draw(image)
        # Bake a crisp outline in place of Flash's runtime GlowFilter.
        draw.text((9, 9 - top), char, font=font, fill=fill, stroke_width=6, stroke_fill=outline)
        image = image.resize((round(image.width / 3), round(image.height / 3)), Image.Resampling.LANCZOS)
        name = "plus" if char == "+" else "minus" if char == "-" else char
        path = folder / (name + ".png")
        image.save(path)
        ensure_meta(path, pixels_per_unit)
        records.append({"file": path.relative_to(ROOT).as_posix(), "character": char,
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    return records


def main():
    critical = OUTPUT / "Critical"
    critical.mkdir(exist_ok=True)
    # Keep GUIDs when relocating the previously extracted critical glyphs.
    for name in [str(i) for i in range(10)] + ["minus", "critical-flame"]:
        old = OUTPUT / (name + ".png")
        if old.exists():
            shutil.move(str(old), str(critical / old.name))
            shutil.move(str(old) + ".meta", str(critical / (old.name + ".meta")))
    ensure_meta(critical)
    normal = OUTPUT / "Normal"
    normal.mkdir(exist_ok=True)
    ensure_meta(normal)
    for name in [str(i) for i in range(10)] + ["minus"]:
        source = ROOT / "Assets/Art/Battle/Fonts" / (name + ".png")
        shutil.copyfile(source, normal / source.name)
        ensure_meta(normal / source.name, 90)
    font_path = next((ROOT / "Temp/DamageDisplay/official-fonts").glob("105_*.ttf"))
    symbols = OUTPUT / "Symbols"
    symbols.mkdir(exist_ok=True)
    ensure_meta(symbols)
    symbol_records = rasterize_font(symbols, font_path, "#990000", "#ffff33", 100, "+")
    # Pink, white and green are confirmed runtime colors in official Flash metadata.
    styles = {"FixedDamage": ("#ff32ff", "#ffecff"),
              "TrueDamage": ("#ffffff", "#000033"),
              "Healing": ("#00cc00", "#ffff33")}
    records = []
    for name, (fill, outline) in styles.items():
        folder = OUTPUT / name
        folder.mkdir(exist_ok=True)
        ensure_meta(folder)
        records += rasterize_font(folder, font_path, fill, outline)
    old_manifest = json.loads((OUTPUT / "sources.json").read_text(encoding="utf-8"))
    for item in old_manifest.get("assets", []):
        item["file"] = "Assets/Art/UI/DamageNumbers/Critical/" + Path(item["file"]).name
    old_manifest["notes"] = "205 is the critical digit set, not the normal damage set. Normal digits reuse existing H5 hp_damage_num glyphs unchanged."
    old_manifest["additionalStyles"] = {
        "normalSource": "https://newseer.61.com/Assets/WebGL/DefaultPackage/e6ae1b6a24e07173fddd3e9af2475c7e",
        "fontSource": "https://seer.61.com/dll/RobotCoreDLL.swf",
        "fontSymbol": "FangZhengZongyi", "fontId": 105,
        "fontSha256": hashlib.sha256(font_path.read_bytes()).hexdigest(),
        "styleSource": "https://seer.61.com/dll/PetFightDLL_201308.swf",
        "styleMetadata": "UseSkillController TextFormat + GlowFilter: fixed #ff32ff/#ffecff; true #ffffff/#000033; healing #00cc00/#ffff33.",
        "conversion": "Rasterized exported official font with confirmed colors; crisp 2px outlines approximate the runtime GlowFilter. These are baked renditions, not independent original PNG assets. Plus uses the same exported official font.",
        "assets": records + symbol_records}
    (OUTPUT / "sources.json").write_text(json.dumps(old_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
