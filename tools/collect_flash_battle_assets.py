"""Collect traceable Flash UI parts, type icons and a batch of pet test art.

Requires requests, Pillow, Java and JPEXS at Temp/FFDec/ffdec.jar.
Raw downloads and renderer output stay outside Assets. Only PNG and catalogs
enter Unity; the source SWFs and scripts are never executed.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess
import uuid
import xml.etree.ElementTree as ET

import requests
from PIL import Image, ImageDraw, ImageFont

from flash_asset_utils import decode_swf, embedded_data, symbols

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "docs/OfficialReference/20261006"
WORK = ROOT / "Temp/BattleAssetCollection"
OUTPUT = ROOT / "Assets/Art/Battle/Flash/TestCollection"
TYPE_OUTPUT = ROOT / "Assets/Art/UI/Types"
FFDEC = ROOT / "Temp/FFDec/ffdec.jar"
DATE = "2026-10-06"
BASE = "https://seer.61.com/"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def download(relative):
    path = CACHE / relative
    if not path.exists():
        response = requests.get(BASE + relative, timeout=(10, 45))
        response.raise_for_status()
        decode_swf(response.content)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(response.content)
    data = path.read_bytes()
    decode_swf(data)
    return path, {"sourceUrl": BASE + relative, "sourceSha256": digest(data),
                  "retrievedDate": DATE, "sourceBytes": len(data)}


def export(source, destination, kind, options=()):
    destination.mkdir(parents=True, exist_ok=True)
    # Each job owns its output/log. A timeout or renderer failure remains a
    # recorded failure, never a substitute picture from another pet.
    with (destination / "export.log").open("w", encoding="utf-8") as log:
        subprocess.run(["java", "-Xmx1024m", "-Djava.awt.headless=true", "-jar", str(FFDEC),
                        *options, "-ignorebackground", "-export", kind,
                        str(destination), str(source)], stdout=log, stderr=log,
                       check=True, timeout=150, cwd=ROOT)


def save_png(source, relative, provenance, crop=False):
    with Image.open(source) as original:
        image = original.convert("RGBA")
    canvas = list(image.size)
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Empty rendered image: " + str(source))
    if crop:
        image = image.crop(bounds)
    if relative.startswith("Pets/Heads/"):
        destination = ROOT / "Assets/Art/Pet/avatar" / Path(relative).name
    elif relative.startswith("Pets/Fight/"):
        destination = ROOT / "Assets/Art/Pet/pets" / Path(relative).name
    elif relative.startswith("Types/"):
        destination = TYPE_OUTPUT / Path(relative).name
    else:
        destination = OUTPUT / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination)
    return {**provenance, "file": destination.relative_to(ROOT).as_posix(),
            "width": image.width, "height": image.height, "sourceCanvas": canvas,
            "alphaBounds": list(bounds), "cropped": crop,
            "pngSha256": digest(destination.read_bytes())}


def character_frame(directory, character, frame):
    matches = list(directory.glob(f"*_{character}_*/{frame}.png"))
    matches += list(directory.glob(f"*_{character}/{frame}.png"))
    matches += list(directory.glob(f"*_{character}_*/{frame}_*.png"))
    if len(matches) != 1:
        raise ValueError(f"Expected one render for character {character}, frame {frame}: {matches}")
    return matches[0]


def extract_configuration():
    source, info = download("dll/RobotCoreDLL.swf")
    data = decode_swf(source.read_bytes())
    binaries = embedded_data(data)
    extracted = {}
    for name, number in symbols(data).items():
        if name in ("com.robot.core.config.xml.PetXMLInfo_xmlClass",
                    "com.robot.core.config.xml.SkillXMLInfo_typeClass"):
            content = binaries[number]
            path = CACHE / (name.rsplit(".", 1)[1] + ".xml")
            path.write_bytes(content)
            extracted[name.rsplit(".", 1)[1]] = ET.fromstring(content)
    return extracted, info


def collect_types(configuration):
    source, info = download("dll/UI.swf")
    mappings = {name.split("_")[-1]: number for name, number in symbols(decode_swf(source.read_bytes())).items()
                if name.startswith("Icon_PetType_")}
    rendered = WORK / "types-rendered"
    if not (rendered / "complete.json").exists():
        export(source, rendered, "button", ["-selectid", ",".join(map(str, mappings.values())), "-zoom", "3"])
        write_json(rendered / "complete.json", info)
    names = {item.get("id"): item.attrib for item in configuration["SkillXMLInfo_typeClass"].findall("item")}
    records = []
    for key, number in sorted(mappings.items(), key=lambda pair: int(pair[0]) if pair[0].isdigit() else 999):
        attributes = names.get(key, {"cn": "属性技能", "en": "attribute_skill"})
        records.append(save_png(character_frame(rendered, number, 1), f"Types/{key}.png",
                                {**info, "typeId": key, "name": attributes["cn"],
                                 "englishName": attributes["en"], "components": attributes.get("att", "").split(),
                                 "characterId": number, "state": "up", "renderScale": 3}))
    missing = sorted(set(names) - set(mappings))
    write_json(TYPE_OUTPUT / "catalog.json", {"assets": records, "missingTypeIds": missing})
    entries = [{"petId": pet.get("ID"), "typeId": pet.get("Type")}
               for pet in configuration["PetXMLInfo_xmlClass"] if pet.get("ID") and pet.get("Type")]
    write_json(TYPE_OUTPUT / "PetTypes.json", {"entries": entries})
    return records


UI_SHAPES = {
    269: "pet-card-ring-blue", 271: "pet-card-info-shadow", 272: "pet-card-hp-fill",
    274: "pet-card-type-background", 317: "pet-card-ring-gold-hover",
    318: "pet-card-info-hover", 319: "pet-card-ring-gold-active",
    320: "pet-card-info-active", 321: "pet-card-fight-button", 324: "pet-card-hp-track",
    476: "health-head-frame-blue", 477: "head-name-background-blue",
    479: "head-type-background-blue", 481: "level-label-blue",
    483: "health-head-frame-special", 484: "health-special-crystal",
    486: "health-head-frame-special-overlay", 487: "health-head-frame-gold",
    489: "head-type-background-gold", 490: "level-label-gold", 492: "head-name-background-gold",
    745: "portrait-mask-reference", 753: "hp-fill-normal", 757: "hp-fill-special-primary",
    760: "hp-fill-special-secondary", 781: "health-enemy-special-overlay",
    782: "head-name-and-type-gold",
}
UI_SPRITES = {493: ("self-head-slot", 3), 783: ("enemy-head-slot", 3),
              325: ("pet-card-reference", 3), 778: ("self-info-reference", 1),
              793: ("enemy-info-reference", 1)}


def collect_ui():
    source, info = download("dll/PetFightDLL_201308.swf")
    decoded = WORK / "fight-current.swf"
    decoded.write_bytes(decode_swf(source.read_bytes()))
    shapes, sprites = WORK / "ui-final-shapes", WORK / "ui-final-sprites"
    export(decoded, shapes, "shape", ["-selectid", ",".join(map(str, UI_SHAPES)), "-zoom", "3", "-format", "shape:png"])
    selection = ",".join(f"{number}:1-{count}" for number, (_, count) in UI_SPRITES.items())
    export(decoded, sprites, "sprite", ["-selectid", ",".join(map(str, UI_SPRITES)), "-select", selection, "-zoom", "3"])
    records = []
    for number, name in UI_SHAPES.items():
        records.append(save_png(shapes / f"{number}.png", f"UI/Parts/{name}.png",
                                {**info, "characterId": number, "renderScale": 3}))
    for number, (name, count) in UI_SPRITES.items():
        for frame in range(1, count + 1):
            records.append(save_png(character_frame(sprites, number, frame), f"UI/{name}-{frame}.png",
                                    {**info, "characterId": number, "frame": frame, "renderScale": 3,
                                     "referenceOnly": "reference" in name}))
    write_json(OUTPUT / "UI/sources.json", {"assets": records})
    return records


def select_pets(configuration):
    monsters = [item.attrib for item in configuration["PetXMLInfo_xmlClass"].findall("Monster")]
    # Sample actual species IDs from the newer Flash era, avoiding aliases.
    candidates = [m for m in monsters if 4200 <= int(m["ID"]) <= 5020 and not m.get("RealId")]
    selected = [candidates[round(i * (len(candidates) - 1) / 63)] for i in range(64)]
    latest = sorted([m for m in monsters if 5600 <= int(m["ID"]) < 10000 and not m.get("RealId")],
                    key=lambda m: int(m["ID"]))[-32:]
    return selected, latest


def collect_pet(monster, include_fight):
    number = monster["ID"]
    records, failures = [], []
    for kind, directory in [("Heads", "pet/head"), *([("Fight", "fightResource/pet/swf")] if include_fight else [])]:
        relative = f"resource/{directory}/{number}.swf"
        try:
            source, info = download(relative)
            rendered = WORK / "pet-rendered" / kind / number
            if kind == "Heads":
                export(source, rendered, "frame", ["-select", "1"])
                image = rendered / "1.png"
                character = None
            else:
                names = symbols(decode_swf(source.read_bytes()))
                character = names.get("pet")
                if character is None:
                    raise ValueError("No exported pet symbol; manual inspection required")
                # Root frames clip off-stage artwork. Export the pet MovieClip
                # itself, whose full authored bounds include both sides.
                export(source, rendered, "sprite", ["-selectid", str(character), "-select", f"{character}:1"])
                image = character_frame(rendered, character, 1)
            records.append(save_png(image, f"Pets/{kind}/{number}.png",
                                    {**info, "petId": int(number), "name": monster["DefName"],
                                     "typeId": int(monster["Type"]), "category": kind,
                                     "characterId": character, "frame": 1, "renderScale": 1}, crop=kind == "Fight"))
        except (requests.RequestException, ValueError, subprocess.SubprocessError, OSError) as error:
            failures.append({"petId": int(number), "category": kind, "sourceUrl": BASE + relative,
                             "reason": str(error)})
    return records, failures


def collect_pets(configuration):
    selected, latest = select_pets(configuration)
    jobs = [(m, True) for m in selected] + [(m, False) for m in latest]
    records, failures = [], []
    with ThreadPoolExecutor(max_workers=3) as pool:
        for index, (assets, errors) in enumerate(pool.map(lambda job: collect_pet(*job), jobs), 1):
            records.extend(assets)
            failures.extend(errors)
            print(f"Pets {index}/{len(jobs)}: {len(assets)} images, {len(errors)} failures", flush=True)
    latest_ids = {int(m["ID"]) for m in latest}
    successful_latest = [r["petId"] for r in records if r["category"] == "Heads" and r["petId"] in latest_ids]
    fallback = sorted([m.attrib for m in configuration["PetXMLInfo_xmlClass"].findall("Monster")
                       if 5600 <= int(m.get("ID")) < min(latest_ids) and not m.get("RealId")],
                      key=lambda m: int(m["ID"]), reverse=True)
    for monster in fallback:
        if len(successful_latest) >= 32:
            break
        assets, errors = collect_pet(monster, False)
        records.extend(assets)
        failures.extend(errors)
        if assets:
            successful_latest.append(int(monster["ID"]))
    by_pet = {}
    for record in records:
        row = by_pet.setdefault(record["petId"], {"petId": record["petId"], "name": record["name"], "typeId": record["typeId"]})
        row[record["category"].lower()] = record["file"]
    write_json(OUTPUT / "Pets/catalog.json", {"pets": list(by_pet.values()), "assets": records,
                                              "failures": failures, "latestHeadsOnlyIds": successful_latest,
                                              "latestRequestedIds": [int(m["ID"]) for m in latest]})
    with (OUTPUT / "Pets/catalog.csv").open("w", newline="", encoding="utf-8-sig") as file:
        writer = csv.DictWriter(file, fieldnames=["petId", "name", "typeId", "heads", "fight"])
        writer.writeheader()
        writer.writerows(by_pet.values())
    return records, failures


def preview(records, path, columns, cell_size):
    width, height = cell_size
    canvas = Image.new("RGB", (columns * width, ((len(records) + columns - 1) // columns) * height), (25, 34, 47))
    draw = ImageDraw.Draw(canvas)
    font_path = Path("C:/Windows/Fonts/msyh.ttc")
    font = ImageFont.truetype(str(font_path), 14) if font_path.exists() else ImageFont.load_default()
    for index, record in enumerate(records):
        x, y = index % columns * width, index // columns * height
        with Image.open(ROOT / record["file"]) as image:
            image = image.convert("RGBA")
            image.thumbnail((width - 16, height - 44))
            canvas.paste(image, (x + (width - image.width) // 2, y + 5 + (height - 44 - image.height) // 2), image)
        label = str(record.get("petId", record.get("typeId", ""))) + " " + record.get("name", Path(record["file"]).stem)
        draw.text((x + 7, y + height - 34), label[:25], fill="white", font=font)
        draw.text((x + 7, y + height - 17), f'{record["width"]} x {record["height"]}', fill=(153, 181, 205), font=font)
    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path, quality=90)


def unity_metadata():
    template = (ROOT / "Assets/Art/Pet/avatar/70.png.meta").read_text("utf-8")
    pet_art = ROOT / "Assets/Art/Pet"
    for path in [OUTPUT, *sorted(OUTPUT.rglob("*")), pet_art, *sorted(pet_art.rglob("*")),
                 TYPE_OUTPUT, *sorted(TYPE_OUTPUT.rglob("*"))]:
        # Unity metadata belongs to an asset; it must never become an asset
        # itself on a later run of the collector.
        if path.suffix.lower() == ".meta":
            continue
        meta = Path(str(path) + ".meta")
        if meta.exists():
            continue
        guid = uuid.uuid4().hex
        if path.is_dir():
            content = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        elif path.suffix.lower() in (".png", ".jpg"):
            content = re.sub(r"(?m)^guid: .*", "guid: " + guid, template, count=1)
        else:
            content = f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        meta.write_text(content, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--skip-pets", action="store_true")
    args = parser.parse_args()
    CACHE.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    configuration, config_source = extract_configuration()
    ui, types = collect_ui(), collect_types(configuration)
    pets, failures = ([], []) if args.skip_pets else collect_pets(configuration)
    write_json(OUTPUT / "sources.json", {"retrievedDate": DATE, "renderer": "JPEXS 26.3.0",
                                         "configurationSource": config_source,
                                         "assets": ui + types + pets, "failures": failures})
    preview(ui, OUTPUT / "UI/preview.jpg", 4, (350, 180))
    preview(types, TYPE_OUTPUT / "preview.jpg", 10, (130, 120))
    for kind in ["Heads", "Fight"]:
        group = [r for r in pets if r["category"] == kind]
        if group:
            preview(group, OUTPUT / f"Pets/{kind.lower()}-preview.jpg", 8, (180, 160 if kind == "Heads" else 240))
    unity_metadata()
    print(f"Complete: UI={len(ui)}, types={len(types)}, pet images={len(pets)}, failures={len(failures)}")


if __name__ == "__main__":
    main()
