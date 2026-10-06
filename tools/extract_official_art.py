"""从已缓存的淘米公开资源包提取原始立绘、头像与场景贴图，并记录来源；不执行游戏代码。"""
import hashlib
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
# 解码依赖只放在工程临时目录，避免改变用户全局 Python 环境。
sys.path.insert(0, str(ROOT / "Temp/AssetTools"))
import UnityPy

SOURCES = [
    ("rey", "657525ec6342e44f87d412f4c149bffe", "70", "Assets/Art/Ui/assets/pet/body/70.png"),
    ("gaia", "6bcbc0b21e5fc8161a3805ddd962c628", "261", "Assets/Art/Ui/assets/pet/body/261.png"),
    ("arena", "1e67a21076d1bff49bbd5386a72dfec8", "battle_bg", "Assets/Game/UI/battle：Texture2D battle_bg"),
    ("rey-head", "1400be6cc627e9577df0496cbaa52a90", "70", "Assets/Art/Ui/assets/pet/head/70.png"),
    ("gaia-head", "8e3841f3ba204c666887a058c1d0d176", "261", "Assets/Art/Ui/assets/pet/head/261.png"),
]

def main():
    """按精确对象名提取，不裁切、不重绘；Unity 场景负责图像的展示尺寸。"""
    output = ROOT / "Assets/Art/Battle"
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for name, digest, texture_name, source_path in SOURCES:
        source = ROOT / "docs/OfficialReference" / (digest + ".bundle")
        data = source.read_bytes()
        if hashlib.md5(data).hexdigest() != digest:
            raise ValueError("资源包校验失败：" + source.name)
        textures = [o.read() for o in UnityPy.load(data).objects if o.type.name == "Texture2D"]
        matches = [t for t in textures if t.m_Name == texture_name]
        if len(matches) != 1:
            raise ValueError("原始贴图名称不唯一或不存在：" + texture_name)
        texture = matches[0]
        pet_paths = {"rey": "pets/70.png", "gaia": "pets/261.png",
                     "rey-head": "avatar/70.png", "gaia-head": "avatar/261.png"}
        destination = ROOT / "Assets/Art/Pet" / pet_paths[name] if name in pet_paths else output / (name + ".png")
        destination.parent.mkdir(parents=True, exist_ok=True)
        texture.image.save(str(destination))
        records.append({
            "file": destination.relative_to(ROOT).as_posix(),
            "sourceUrl": "https://newseer.61.com/Assets/WebGL/DefaultPackage/" + digest,
            "sourceAsset": source_path, "textureName": texture_name,
            "bundleMd5": digest, "pngSha256": hashlib.sha256(destination.read_bytes()).hexdigest(),
            "width": texture.m_Width, "height": texture.m_Height,
        })
    manifest = {"_说明": "淘米公开资源的本地研究引用；版权归原权利人，提取不代表取得发布授权。",
                "retrievedDate": "2026-09-09", "packageVersion": "20260904173604",
                "extractor": "UnityPy 1.25.3 / 原始 Texture2D 解码", "assets": records}
    (output / "sources.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("已提取", len(records), "张原始贴图并保存 sources.json")

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
