param([int]$Port = 9527)
$ErrorActionPreference = 'Stop'
$serverRoot = Join-Path $PSScriptRoot '..\Server\seer-asio'
$serverExe = Join-Path $serverRoot 'build\seer-battle-server.exe'
$luaExe = Join-Path $serverRoot '.deps\lua.exe'
if (!(Test-Path -LiteralPath $serverExe) -or !(Test-Path -LiteralPath $luaExe)) {
    throw 'Build first: python tools/build_battle_server.py'
}
& $serverExe $luaExe (Join-Path $serverRoot 'packages\seer-core') $Port
