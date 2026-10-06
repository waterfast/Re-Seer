# 战斗 UI 骨架

`Assets/Prefabs/BattleUI/` 中有九个可编辑的 uGUI Prefab：`BattleUIRoot`、`PortraitFrame`、`HealthBar`、`PetStatusPanel`、`SkillCard`、`ActionButton`、`ActionMenu`、`PanelHost`、`DamagePopup`。`BattleUIRoot` 嵌套了状态面板、菜单与面板容器；头像、血条、技能卡只提供基本节点和占位文字，留给手工排版。不要把它当作已完成的画面。

菜单按钮的 `actionId` 分别为 `skill`、`pet`、`item`、`escape`。`BattleUiController` 将点击转换为界面导航或 `ActionSubmitted` 意图；它不执行伤害、不扣 PP，也不发网络请求。选择技能、精灵或道具后，可由后续页面调用 `ChooseEntry(id, needsTarget)`；需要目标时再调用 `ChooseTarget(id)`。服务端结果到来后调用 `ServerAccepted()` / `ServerRejected()`，动画结束调用 `PresentationFinished()`。

`BattleUiStateMachine` 管导航状态。`BattleUiDataState` 接收确认后的 HP、技能槽等快照；`BattlePresentationEvents` 单独发送一次性的伤害飘字事件。目前这两种事件尚未与具体文本、血条和弹窗绑定，手工做 UI 时可按组件逐个接入。

如需从零重建，菜单 `Tools > Re-Seer > Battle UI > Create Prefab Skeletons` 只在 `BattleUIRoot.prefab` 不存在时生成，不覆盖已经手工修改的 Prefab。菜单 `Verify Skeleton and State` 检查导航、事件与 Prefab 层级。

已提取素材见 `Assets/Art/Battle/Flash/`、`Assets/Art/Battle/OfficialUI/`。其中官方 WebGL 素材是原始提取集合，尚未逐一确认哪张最适合当前布局。
