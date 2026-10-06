"""从官网缓存包按对象名提取 UI 和位图数字；保留来源、原始字体度量与校验值。"""
import hashlib
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Temp/AssetTools"))
import UnityPy

OUTPUT = ROOT / "Assets/Art/Battle"
RECORDS = []


def save(image, relative, digest, object_name):
    """PNG 仅解码原始 Sprite / 字形区域，不重新绘制官方素材。"""
    path = OUTPUT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)
    RECORDS.append({"file": path.relative_to(ROOT).as_posix(), "object": object_name,
                    "sourceUrl": "https://newseer.61.com/Assets/WebGL/DefaultPackage/" + digest,
                    "bundleMd5": digest, "pngSha256": hashlib.sha256(path.read_bytes()).hexdigest()})


def bundle(digest):
    data = (ROOT / "docs/OfficialReference" / (digest + ".bundle")).read_bytes()
    if hashlib.md5(data).hexdigest() != digest:
        raise ValueError("缓存资源校验失败：" + digest)
    return UnityPy.load(data)


def main():
    digest = "1e67a21076d1bff49bbd5386a72dfec8"
    names = {"battle_skill_bg", "battle_skill_bar_bg", "battle_self_info_bg1", "battle_player_info_bg1",
             "battle_clock_bg", "battle_round_bg", "battle_blood", "battle_blood_bg", "diwubg"}
    for obj in bundle(digest).objects:
        if obj.type.name == "Sprite":
            sprite = obj.read()
            if sprite.m_Name in names:
                save(sprite.image, "UI/" + sprite.m_Name + ".png", digest, sprite.m_Name)
    digest = "53ee4cf1e8320361c4aa9b7114c1fffa"
    # 编号由官网精灵 XML 核对：雷伊 70→5；盖亚 261→11；悠悠 91→8。
    types = {"5": "electric", "8": "normal", "11": "fighting"}
    for obj in bundle(digest).objects:
        if obj.type.name == "Sprite":
            sprite = obj.read()
            if sprite.m_Name in types:
                save(sprite.image, "Types/" + types[sprite.m_Name] + ".png", digest, sprite.m_Name)
    digest = "e6ae1b6a24e07173fddd3e9af2475c7e"
    env = bundle(digest)
    atlas = next(o.read().image for o in env.objects if o.type.name == "Texture2D" and o.read().m_Name == "hp_damage_num_0")
    font = next(o.read_typetree() for o in env.objects if o.type.name == "Font" and o.read().m_Name == "hp_damage_num")
    save(atlas, "Fonts/hp_damage_num_0.png", digest, "hp_damage_num_0")
    (OUTPUT / "Fonts/hp_damage_num.json").write_text(json.dumps({"_说明": "官方位图字形度量，UV 原点在左下。", **font}, ensure_ascii=False, indent=2), encoding="utf-8")
    for glyph in font["m_CharacterRects"]:
        code = glyph["index"]
        if code != 45 and not 48 <= code <= 57:
            continue
        uv = glyph["uv"]
        x = round(uv["x"] * atlas.width)
        y = round((1 - uv["y"] - uv["height"]) * atlas.height)
        w, h = round(uv["width"] * atlas.width), round(uv["height"] * atlas.height)
        save(atlas.crop((x, y, x+w, y+h)), "Fonts/" + ("minus" if code == 45 else chr(code)) + ".png", digest, "hp_damage_num / " + str(code))
    (OUTPUT / "ui-sources.json").write_text(json.dumps({"_说明": "官网原始美术的本地研究引用。", "retrievedDate": "2026-09-09", "packageVersion": "20260904173604", "assets": RECORDS}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("已提取", len(RECORDS), "个 UI / 图标 / 字形资源")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
