# 共用数字显示器

`Assets/Prefabs/BattleUI/DamageDisplay.prefab` 只保存根对象、一个显示脚本和空 Content。数字 SpriteRenderer 按实际位数创建、居中排列并复用；编辑器预览对象不会写入 prefab 或场景。根对象的位置和缩放决定显示位置，动画仅移动 Content。

## 素材

| 文件夹 | 用途与来源 |
| --- | --- |
| Normal | 复用项目已有 Fonts 中的官网 H5 普通数字，PNG 内容保持原样；没有挪动原文件。 |
| Critical | Flash HpMC 数字元件 205 的 0–9、元件 207 的负号和 184 的火焰。之前把这套暴击数字误用于普通伤害，现已分开。 |
| FixedDamage | 粉伤：官网 Flash 导出的方正综艺字体，填色 #ff32ff、描边 #ffecff。 |
| TrueDamage | 真伤：同一字体，填色 #ffffff、描边 #000033。 |
| Healing | 回血：同一字体，填色 #00cc00、描边 #ffff33。 |
| Symbols | 普通和暴击共用的加号，从同一官方字体转成位图。 |

Flash 的粉伤、真伤与回血由运行时 TextFormat 和 GlowFilter 生成，并非独立的原始 PNG。这里静态转换官网 RobotCoreDLL 中的 FangZhengZongyi 字体，并依据 PetFightDLL 的颜色配置烘焙；2 像素清晰描边近似其运行时 GlowFilter。来源、校验值和转换方式记录在 sources.json。

重建：`tools/extract_flash_damage.py` 提取暴击字形；JPEXS 从解码后的 RobotCoreDLL 导出字体 105 至 Temp/DamageDisplay/official-fonts，然后运行 `tools/build_damage_number_styles.py`。仅静态检查官方字体和表现配置，不执行或移植官方游戏逻辑。

## 显示入口

`display.Show(amount, style, sign)` 动画显示，`SetValue` 静态显示，`Hide` 隐藏。amount 支持完整 long 范围；显示绝对值与所选符号。重复 Show 会重新播放当前实例，数字槽按最大已使用位数复用。

- style：Normal、Critical、FixedDamage、TrueDamage、Healing。
- sign：Auto、None、Minus、Plus。Auto 对伤害用减号、回血用加号，负值用减号；可显式覆盖，粉色回血也可以选 FixedDamage + Plus。
- 兼容 `Show(int amount, bool critical)`。

在 Inspector 中调整 previewDamage、previewStyle、previewSign 即可预览，运行模式按钮播放动画。数字使用 L3 排序层，排序 50，火焰排序 49。

## UIController

数字入口放在 `BattleUIController.Numbers.cs`，没有添加额外组件。控制器会解析同场景里已有的 DamageDisplay / EnemyDamageDisplay 作为敌方实例，沿用用户摆放的位置和缩放；我方实例可手动绑定，或命名为 SelfDamageDisplay。

```csharp
controller.ShowDamage(555);                     // 已摆放的敌方位置，普通伤害
controller.ShowDamage(555, true);               // 暴击
controller.ShowNumber(false, 150, DamageNumberStyle.FixedDamage);
controller.ShowNumber(false, 150, DamageNumberStyle.TrueDamage);
controller.ShowNumber(false, 200, DamageNumberStyle.Healing);
controller.ShowNumber(false, 200, DamageNumberStyle.FixedDamage, NumberSign.Plus);
```

数字入口接收已经确认的表现数据，不根据 HP 差值猜伤害或暴击。没有修改 Battle 场景、技能、PP 或状态数据，也没有给尚未摆放的我方显示器推断位置。
