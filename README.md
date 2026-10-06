# Re-Seer

Unity 赛尔号客户端，当前集中制作战斗页面。客户端负责显示服务器提供的数据、收集玩家操作和播放表现；战斗结算由服务端负责。

- 场景：`Assets/Scenes/Battle.unity`；布局参考：`Assets/Scenes/BattleLayout.unity`。[布局说明](docs/战斗场景布局.md)
- 页面组件：`Assets/Scripts/Battle/UI/` 与 `Assets/Prefabs/BattleUI/`，包含技能按钮、操作菜单、血条和界面导航。
- 页面数据：`Assets/Scripts/Battle/Data/`，保存双方、当前精灵、技能 PP、道具和回合等显示状态。[前后端边界](docs/基础骨架与前后端边界.md)
- 技能展示库：`Assets/Game Data/SkillDatabase.asset`，通过 `Tools > Re-Seer > Skill Editor` 编辑。保留名称和描述的翻译键、中文回退文案、图标和表现资源索引。[工作流](docs/Skills/技能数据工作流.md)
- 精灵图片加载：`Assets/Scripts/Pets/Art/`，仅按素材编号读取图片，不定义精灵物种或养成规则。
- 参考素材：`Assets/Art/Battle/Flash/` 与 `Assets/Art/Battle/OfficialUI/`。[来源说明](docs/官方资源来源.md)

旧的本地战斗内核和物种/养成定义已删除。技能库的效果参数、翻译字段和服务器规则导出保留，供后续描述翻译与服务器数据库使用。按钮点击不在本地扣 PP。服务器连接尚未接通，页面预览使用示例数据。
