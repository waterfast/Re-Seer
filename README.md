# Re-Seer

这是仍在搭建中的 Unity 赛尔号客户端。当前战斗 UI 只有可手动编辑的 Prefab 骨架和导航状态代码，没有完成的战斗页面，也没有接入服务器。

- 技能编辑源：[单一数据库资产](Assets/Game%20Data/SkillDatabase.asset)。用 Unity 菜单 `Tools > Re-Seer > Skill Editor` 编辑；每条技能是数据库内的一条记录，不另建技能 SO。[工作流](docs/Skills/技能数据工作流.md)
- 战斗 UI：`Assets/Prefabs/BattleUI/` 下的九个空白骨架 Prefab。状态机和控制器在 `Assets/Scripts/Battle/UI/`；[装配说明](docs/BattleUI/Prefab装配说明.md)。最终排版、素材替换和数据绑定由场景制作时完成。
- 参考素材：`Assets/Art/Battle/Flash/` 和 `Assets/Art/Battle/OfficialUI/`。来源分别记录在对应目录的 `sources.json`；目前只是已提取素材，不表示已逐一筛选或复刻 Flash 战斗页面。
- 原有 `Assets/Scripts/Combat/`、`Assets/Scripts/Pets/` 是尚未接入当前 UI 的规则基础代码，保留供后续选择使用。旧的终端战斗演示工程已移至 `docs/Backups/LegacyBattleRunner-20261001/`。

`Assets/Scenes/BattleTest.unity` 和 `Assets/Prefabs/SkillButton.prefab` 是现存的旧测试内容；它们不代表新战斗 UI 已接通。早期完整战斗界面尝试及旧文档保存在 `docs/Backups/`，方便需要时查阅。
