<!-- UI 参考、资源出处、装配入口和表现边界；与纯 C# 战斗规则分开维护。 -->
# 战斗 UI 参考与装配

本轮于 2026-09-09 读取两端公开资源，以 Flash 的蓝色机械边框为主，搭配官方 Unity 端的背景、头像、属性图标和位图伤害数字，重新组装 `BattleTest`。这里核对的是资源原件及面板结构，未登录正式服；静态导出的 SWF 首帧会包含设计时的隐藏弹窗，不能称为正式服实战截图。

## 两端参考与取舍

| 部位 | Flash 公开战斗模块 | Unity WebGL 战斗资源 | 本项目采用 |
|---|---|---|---|
| 技能卡 | 明亮蓝色渐变、金属切角、白色高光 | 更平整的蓝色卡片，左上图标槽 | Flash 纯底板；技能名、PP 和说明由本项目绘制 |
| 生命面板 | 头像与长血条连成一体，两侧镜像 | 横向资料板、独立头像槽与条形血量 | Flash 边框，独立数字与填充图元 |
| 第五技能 | 独立圆形技能盘，动画、名称、次数各为子元件 | 独立环形底图 | Flash 空白圆盘；保留雷神天明闪的真实 PP |
| 伤害数字 | `HpMC`、`delataNumMc` 等多帧显示元件 | `hp_damage_num` 位图 Font，附有字形坐标 | Unity 原始字形，避免携带 Flash 运行代码 |
| 属性 | 官方 XML 编号与精灵类型 | `PetType.spriteatlas` 中的编号 Sprite | 电 5、普通 8、战斗 11 |

Flash 模块为官网加载器引用的 [PetFightDLL_201308.swf](https://seer.61.com/dll/PetFightDLL_201308.swf)。文件名中的 `201308` 是模块名，不能当成当前内容的更新时间。Unity 资源以采集时的公开包版本 `20260904173604` 为准。它们不代表同一年代的界面设计，本页主动选用 Flash 的视觉语言。

### 原件对照

Flash 技能底板：

![Flash 技能底板](screenshots/ui-reference/flash-skill.png)

Unity 技能底板：

![Unity 技能底板](screenshots/ui-reference/unity-skill.png)

Flash 头像血条框：

![Flash 头像血条框](screenshots/ui-reference/flash-health.png)

Unity 资料面板：

![Unity 资料面板](screenshots/ui-reference/unity-health.png)

## 资源在哪

```text
Assets/Art/Battle/
  arena.png、rey.png、gaia.png      背景与站立图
  rey-head.png、gaia-head.png       原始头像小图
  Flash/                          血条框、控制板、技能卡、第五技能圆盘
  Types/                          electric、normal、fighting
  Fonts/                          原始 hp_damage_num 图集、度量及 0–9 / 减号字形
  UI/                             Unity 端面板参考、血条等原件
```

`sources.json`、`ui-sources.json`、`Flash/sources.json` 分别保存立绘头像、Unity UI、Flash 装饰的原始 URL 和校验值。原包保留在 `docs/OfficialReference`，提取工具缓存放 `Temp`。字体是位图字形，不是可安装的 TTF；切片保留原始透明度和彩色像素。

属性编号经过官网 XML 对照：雷伊 ID 70 的类型为 5 / 电；盖亚 ID 261 为 11 / 战斗；悠悠 ID 91 为 8 / 普通。极光刃用普通图标，其余雷伊招式用电图标；“属性技能”是招式类别，不是新的精灵元素。

## 从哪开始读代码

1. `Assets/Scripts/Battle/Unity/Editor/BattlePageBuilder.cs` 是编辑期装配入口。菜单 **Tools → Re-Seer → 创建测试战斗页** 导入图片、生成场景、连接全部依赖并保存。已有未保存场景时拒绝覆盖。
2. `BattlePageController.cs` 接收按钮，调用已有 `TrainingSession`；结算期间锁定重复输入。
3. `BattlePageView.cs` 从状态和战报更新姓名、血量、强化、麻痹、技能 PP 与最近三条记录。
4. `BattleDamageNumber.cs` 只接收整数，复用字形图元实现上浮与淡出。实际伤害取战报的 `damage.Amount`，以攻击者反向定位受击一侧；同轮同侧伤害合并显示。无伤害不显示 `-0`，重开立即隐藏旧数字。

依赖仍是 `场景 → Controller → TrainingSession → 核心`，`View → DamageNumber` 仅负责显示。没有在按钮、动画、贴图或字体中计算命中与伤害。人物暂时使用静态立绘和短距离位移，不是官方精灵逐帧战斗动画。

画布采用 1440 × 900 的居中设计区域，按完整视口等比缩放；边框镜像不影响文字方向。装饰和飘字均不接收射线，只有五招、自动与重开按钮可点击。

## 复现与验证

本地已缓存资源时可运行：

```powershell
python tools/extract_official_art.py
python tools/extract_battle_ui.py
python tools/extract_flash_ui.py
.\battle.ps1 test
```

前两个提取器需要 `Temp/AssetTools` 中的 UnityPy 1.25.3；第三个需要 Java 和 `Temp/FFDec/ffdec.jar`（JPEXS 26.2.1）。只静态解码资源，提取工具不主动联网。Unity 运行和内核测试不需要这些工具。

PlayMode 集成测试在 `Assets/Scripts/Battle/Unity/Tests/BattlePageTests.cs`。本次先新增飘字测试并验证旧场景因缺失数字视图而失败，再实现视图并重新装配。测试覆盖实际扣血数字、无伤害不显示、重开清理、动效结束隐藏，并保留按钮射线、回合互斥、自动对战与重开验证。

通过 **Tools → Re-Seer → 保存战斗页截图** 在运行模式离屏渲染，不需要占用桌面输入。最新画面保存到 `docs/screenshots/battle-test.png`。
