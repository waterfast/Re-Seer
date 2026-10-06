# 官方战斗 UI 图元

2026-10-01 从淘米公开战斗 UI 包提取全部 **296 个 Sprite**，保留透明度及原始像素尺寸。包括头像框、血条与护盾、技能底板、第五技能、战斗/精灵/道具/撤退按钮、回合和计时装饰、战报、状态与详情面板。

这是匹配项目已有素材的战斗 UI 包的完整 Sprite 集合，不是整个赛尔号所有界面和所有历史皮肤。包中还含官方运行代码及 Prefab，这里只提取静态美术，本项目 Prefab 由自己的组件重新装配。

- 官网：[赛尔号](https://seer.61.com/)
- 公开资源包：[battle bundle](https://newseer.61.com/Assets/WebGL/DefaultPackage/1e67a21076d1bff49bbd5386a72dfec8)
- 当日公开站点版本：20260928155626。这个资源包最初记录于 20260904173604，重新下载后与原缓存逐字节一致；没有将老包冒称为最新版全部游戏资源。
- 原始图片名称、尺寸及 SHA-256：`sources.json`。文件名中的空格转换为下划线。
- 同风格 Flash 蓝色机械框：相邻 `../Flash/`；来自同日重新核对的 [PetFightDLL_201308.swf](https://seer.61.com/dll/PetFightDLL_201308.swf)。头像和角色立绘复用 `Assets/Art/Pet/avatar/70.png`、`261.png` 和 `Assets/Art/Pet/pets/70.png`、`261.png`。
- 重点素材总览：`docs/BattleUI/official-ui-contact.png`。

运行 `python tools/collect_battle_ui.py` 从已校验缓存重建；加 `--download` 重新下载并核对 MD5。开发解码依赖安装在 `Temp/AssetTools`，不影响 Unity 运行。来源与版权沿用本项目官方素材记录。
