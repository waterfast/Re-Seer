<!-- 资源中文说明：运行场景引用静态立绘、界面元件和位图字形，原始来源和校验值见 sources.json。 -->
# 战斗测试美术

2026-09-09 新增 [Flash/Extracted](Flash/Extracted/README.md)：18 张官方 Flash 战斗模块位图，含背景、电光、提示和面板；附总览与逐文件校验，尚未接入场景。

`rey.png` 为经典雷伊（资源编号 70），`gaia.png` 为盖亚（261），`arena.png` 为官方战斗 UI 包中的 `battle_bg` 贴图。

美术统一放在 `Assets/Art/Battle`，源码位于 `Assets/Scripts/Battle`。场景持有图片的 Sprite 引用，不依赖 Resources 路径。移动图片和脚本时保留其 `.meta` 文件。

这些文件由公开官方资源包的 Texture2D 直接解码，未重绘。版权归原权利人，用于本地同人原型研究；下载不代表获得对外发布授权。

来源、原对象名、版本、包 MD5 和 PNG SHA-256 记录在 `sources.json`。详细入口和复现步骤见项目 `docs/官方资源来源.md`。运行时直接引用本地 Sprite，不访问官网，也不依赖官方代码、登录服务或服务器。


新增 `Flash/` 存放公开 Flash 模块的装饰原件；`Types/` 存放电、普通、战斗图标；`Fonts/` 存放原版位图伤害数字及度量；`UI/` 存放 Unity 原版面板参考和血条。`rey-head.png` / `gaia-head.png` 为原始头像。分别核对 `Flash/sources.json`、`ui-sources.json`、`sources.json`，装配说明见 `docs/战斗UI参考与装配.md`。
