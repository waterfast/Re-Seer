# 当前头像体力槽的动态显示

`Assets/Scripts/Battle/UI/HpBar.cs` 挂在 `HpBar` 根对象，绑定当前直接子对象：

- `mainbar`：框体的 SpriteRenderer。
- `avatar`：精灵头像的 SpriteRenderer；也支持在其下放一个 `Pet` 图片子对象，兼容旧名称 `avatar` / `avator`。
- `pet_level`：等级 TMP_Text。
- `pet_name`：名字 TMP_Text。
- `hp` / `hp_fill`：体力填充 SpriteRenderer。
- `hp_text`：可选的当前／最大 HP 文字。
- `element`：属性 SpriteRenderer，支持放在子层级中；沿用手动绑定和已有布局。

挂载时自动查找这些引用，也可在 Inspector 手动拖入。查找只认 TMP_Text，不绑定 TMP 自动生成的 SubMesh；不更改子对象位置、缩放、字体、遮罩或镜像。

外部显示数据就绪后调用：

```csharp
hpBar.BindPet("4500", "武心婵", 60, 200, 300);
// 战斗逻辑确认扣血之后，将新数值交给界面；运行时平滑缩短血条。
hpBar.SetHealth(150, 300);
```

常规入口使用 `BindPet`，根据精灵编号自动从资源模块取得头像。如需直接指定图片或更换框体，仍可使用 `Bind` 的 Sprite 参数入口。默认素材目录为 `Assets/Art/Pet/avatar/编号.png`；自定义目录和资源索引见 [精灵资源与界面更新](精灵资源与界面更新.md)。

`HpBarTest.cs` 只用于主动点击 ContextMenu 的显示测试，不在 Start 中自动加载或覆盖精灵。接入正式战斗后停用或移除该组件即可。

测试菜单集中在 HpBarTest：**同步测试：切换60级武心婵（200／300 HP）**、**同步测试：还原100级武心婵（300／300 满血）**、**同步测试：当前体力减少50**。前两个当场同步更新所有显示；扣血在编辑模式立即更新，运行模式平滑过渡。HpBar 不再转发测试菜单。

先把 `hp` 图片按满血宽度摆好，再绑定组件。脚本记录其完整位置与缩放，横向缩放到 `CurrentHp / MaxHp`，并依据 Sprite 边界补偿中心位置，固定左端；无需修改素材轴心。`Fill From Right` 固定右端，供对方血条使用，不会镜像文字。200/300 对应满宽的三分之二。切换精灵直接显示新比例，连续扣血从当前显示比例继续动画；最大 HP 必须大于零，当前 HP 限制在 0 到最大值。

2026-10-07 当前血条的 `Position Y=0.04`、`Scale Y=1.08` 沿用用户布局。脚本只缓存满血 X 基准；每次更新都保留当前 Y/Z 的位置和缩放，避免旧缓存把厚度改回 1。以后继续调 Y 无需重新记录满血宽度。

HpBar 的“镜像”区域勾选 `Mirrored`，或调用 `SetMirrored(true)`，围绕框体中心换边。镜像只改变直接子组的位置与各组自身的 SpriteRenderer.flipX，不对父层使用负缩放；`pet_level/lv-icon` 和 `pet_level/level` 保持正常朝向，`pet_name/information` 也不反转。血条同步切换固定端，取消镜像恢复布局。建议保留当前的分组层级。

需要分别手工调整两侧时，直接编辑 `Assets/Prefabs/BattleUI/EnemyHpBar.prefab`。这是从当前 HpBar 派生的反向预制体，镜像已完成，Lv 和文字保留正常朝向。保持 `Mirrored` 勾选，直接调整子对象 Transform；不需要两套布局缓存或额外布局脚本。调整完成后再替换场景中的敌方槽，当前场景不会自动切换到这个新预制体。

头像字段已统一命名为 `avatar`，通过 FormerlySerializedAs 保留原 `avatar` 字段的组件引用。字段引用优先于自动查找，因此重新在 Inspector 绑定的文字组件不会被替换。

Unity 编译刷新后，本次的一次性装配请求会尝试配置当前场景中唯一的 `HpBar`，兼容现有的 `avator` 拼写，绑定编号 4500 的武心婵头像并显示 60 级、200/300 HP。若缺少 `hp_text`，配置菜单会复用已有等级文字的字体与材质添加体力数字。也可使用菜单 **Tools → ReSeer → 配置当前HpBar武心婵预览**。装配支持 Undo，标记场景修改，不自动保存场景；遇到多个同名根对象或缺少组件时明确报错。

HpBar 只显示上层传入的数值，不计算伤害或修改战斗状态。显示中的 `CurrentHp` / `MaxHp` 在调用时立即更新，缩放动画独立于实际数值。

`pet_name` 下的 TMP SubMesh 是 TMP 管理的渲染子对象，可能由中文回退字体或其他材质生成。编辑文字请修改父对象的 TMP 组件，勿手工给 SubMesh 绑定文字数据。

## 属性图标同步

素材集中在 `Assets/Resources/UI/Types`，139 张原图保留原 GUID。运行时按 `Resources.Load<Sprite>("UI/Types/88")` 加载，`catalog.json` 保存属性名称、双属性组成和来源。

`BindPet(BattlePetState)` 同步使用 `Elements`；五参数的简单预览按 `PetTypes.json` 查默认属性，武心婵 4500 对应 88。该映射来自已有的本地官方 XML。清空精灵或传入未知属性会清空旧图标，扣血保持图标不变。

```csharp
hpBar.SetElements(new[] { "fire" });
hpBar.SetElements(new[] { "nature", "saint" }); // 双属性对应合成图标 88。
hpBar.SetElements(new[] { "88" }); // 也可以直接使用官方编号。
```

仅更换 Sprite，不修改属性图标的位置、缩放或朝向。
