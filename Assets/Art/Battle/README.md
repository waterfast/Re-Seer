<!-- 资源中文说明：运行场景引用静态立绘、界面元件和位图字形，原始来源和校验值见 sources.json。 -->
# 战斗测试美术

2026-10-06 新增 [Flash/TestCollection](Flash/TestCollection/README.md)：Flash 精灵卡片图框、我方／对方头像体力槽的三个版本与独立零件，完整 Flash 属性图标库，以及按编号整理的精灵头像和战斗首帧批量测试样本。附总览、CSV／JSON 索引、原始来源及校验；尚未接入场景。

2026-09-09 新增 [Flash/Extracted](Flash/Extracted/README.md)：18 张官方 Flash 战斗模块位图，含背景、电光、提示和面板；附总览与逐文件校验，尚未接入场景。

精灵素材已迁入 `Assets/Art/Pet`：`pets/70.png` 为经典雷伊，`pets/261.png` 为盖亚，头像在同根目录的 `avatar/`。`arena.png` 为官方战斗 UI 包中的 `battle_bg` 贴图。

战斗 UI 美术放在 `Assets/Art/Battle`，精灵图片放在 `Assets/Art/Pet`。场景原有 Sprite 引用继续有效，按精灵编号加载使用自动生成的资源索引。移动图片和脚本时保留其 `.meta` 文件。

这些文件由公开官方资源包的 Texture2D 直接解码，未重绘。版权归原权利人，用于本地同人原型研究；下载不代表获得对外发布授权。

来源、原对象名、版本、包 MD5 和 PNG SHA-256 记录在 `sources.json`。详细入口和复现步骤见项目 `docs/官方资源来源.md`。运行时直接引用本地 Sprite，不访问官网，也不依赖官方代码、登录服务或服务器。


新增 `Flash/` 存放公开 Flash 模块的装饰原件；`Types/` 存放电、普通、战斗图标；`Fonts/` 存放原版位图伤害数字及度量；`UI/` 存放 Unity 原版面板参考和血条。雷伊／盖亚原始头像迁入 `Assets/Art/Pet/avatar/70.png`、`261.png`。分别核对 `Flash/sources.json`、`ui-sources.json`、`sources.json`，装配说明见 `docs/战斗UI参考与装配.md`。
