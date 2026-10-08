"""Build the native Asio server and a private Lua 5.4 runtime (no global installs)."""
import os
from pathlib import Path
import subprocess
import tarfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1] / "Server" / "seer-asio"
DEPS = ROOT / ".deps"


def download(url, name):
    target = DEPS / name
    if not target.exists():
        print("Downloading", name, flush=True)
        with urllib.request.urlopen(url, timeout=45) as response:
            target.write_bytes(response.read())
    return target


def main():
    DEPS.mkdir(exist_ok=True)
    if not (DEPS / "asio-asio-1-30-2").exists():
        archive = download("https://codeload.github.com/chriskohlhoff/asio/zip/refs/tags/asio-1-30-2", "asio.zip")
        with zipfile.ZipFile(archive) as package:
            for member in package.namelist():
                if not (DEPS / member).resolve().is_relative_to(DEPS.resolve()):
                    raise ValueError("Archive path outside dependency directory")
            package.extractall(DEPS)
    if not (DEPS / "json.hpp").exists():
        download("https://raw.githubusercontent.com/nlohmann/json/v3.11.3/single_include/nlohmann/json.hpp", "json.hpp")
    lua = DEPS / ("lua.exe" if os.name == "nt" else "lua")
    if not lua.exists():
        archive = download("https://www.lua.org/ftp/lua-5.4.7.tar.gz", "lua.tar.gz")
        with tarfile.open(archive) as package:
            package.extractall(DEPS, filter="data")
        sources = sorted((DEPS / "lua-5.4.7" / "src").glob("*.c"))
        sources = [str(p) for p in sources if p.name != "luac.c"]
        flags = [] if os.name == "nt" else ["-DLUA_USE_LINUX", "-ldl"]
        subprocess.run(["gcc", "-O2", *sources, *flags, "-lm", "-o", str(lua)], check=True)
    generator = "MinGW Makefiles" if os.name == "nt" else "Unix Makefiles"
    subprocess.run(["cmake", "-S", str(ROOT), "-B", str(ROOT / "build"), "-G", generator,
                    "-DSEER_DEPS_DIR=" + str(DEPS), "-DCMAKE_BUILD_TYPE=Debug"], check=True)
    subprocess.run(["cmake", "--build", str(ROOT / "build"), "--target", "seer-battle-server", "-j", "4"], check=True)
    print("Built server. Start it with tools/start_battle_server.ps1", flush=True)


if __name__ == "__main__":
    main()
