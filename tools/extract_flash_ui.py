"""静态导出公开 Flash 战斗模块中的五个装饰元件，不启动 Flash 或执行 ActionScript。"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import zlib

ROOT = Path(__file__).resolve().parents[1]
PARTS = [(1640, "skill-card"), (1647, "skill-card-hover"), (476, "health-frame"),
         (624, "control-panel"), (358, "fifth-ring")]


def main():
    """JPEXS 和 Java 为离线提取工具；Unity 运行不依赖它们。"""
    source = ROOT / "docs/OfficialReference/PetFightDLL_201308.swf"
    original = source.read_bytes()
    data = original
    # 官方 FightdllLoader 对公开文件先跳过七字节，再做标准 zlib 解压。
    if data[:3] not in (b"FWS", b"CWS"):
        data = zlib.decompress(data[7:])
    if data[:3] == b"CWS":
        data = b"FWS" + data[3:8] + zlib.decompress(data[8:])
    if data[:3] != b"FWS":
        raise ValueError("资源不是支持的 SWF 格式")
    temporary = ROOT / "Temp/UIReference"
    temporary.mkdir(parents=True, exist_ok=True)
    decoded = temporary / "Fight-decoded.swf"
    decoded.write_bytes(data)
    profile = ROOT / "Temp/FFDec/profile"
    profile.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment["APPDATA"] = str(profile)
    subprocess.run(["java", "-Djava.awt.headless=true", "-jar", str(ROOT / "Temp/FFDec/ffdec.jar"),
                    "-selectid", ",".join(str(i) for i, _ in PARTS), "-zoom", "3", "-format", "shape:png",
                    "-ignorebackground", "-export", "shape", str(temporary / "FlashShapes"), str(decoded)],
                   env=environment, check=True, timeout=55, cwd=ROOT)
    output = ROOT / "Assets/Art/Battle/Flash"
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for number, name in PARTS:
        destination = output / (name + ".png")
        shutil.copyfile(temporary / "FlashShapes" / (str(number) + ".png"), destination)
        records.append({"file": destination.relative_to(ROOT).as_posix(), "shapeId": number, "scale": 3,
                        "sha256": hashlib.sha256(destination.read_bytes()).hexdigest()})
    manifest = {"_说明": "JPEXS 26.2.1 静态渲染 DefineShape 原件；仅引用装饰，不执行或移植官方代码。",
                "sourceUrl": "https://seer.61.com/dll/PetFightDLL_201308.swf", "retrievedDate": "2026-09-09",
                "swfSha256": hashlib.sha256(original).hexdigest(), "assets": records}
    (output / "sources.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
