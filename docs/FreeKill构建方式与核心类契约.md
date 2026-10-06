# 借鉴 FreeKill core 的战斗构建方式与核心类契约

> 本轮已开始实现基本类，实际范围见 [基础骨架与前后端边界](基础骨架与前后端边界.md)。新命名是 PetSpecies（物种）→Pet（持久化养成个体）→BattlePet（本局状态）。本文保留此前完整目标方案，未实施部分不能当作现有接口。

2026-09-09 · 审查稿 · 配合 [战斗架构重写草案](战斗架构重写草案.md) 阅读。只规划，不包含实现代码。

建议借鉴 FreeKill 的整体构建思路：**内容包注册角色与技能，场上实体持有运行状态，技能组合效果，效果触发结算事件，事件嵌套形成完整战斗过程。** 在 Re-Seer 中使用 `Pet → BattlePet`、`Skill / Trait → Effect`、`Event → EventRunner`。不逐个照搬卡牌游戏的领域对象。

## 1. 实际读到了什么

本次读取用户提供的本地 freekill-core，Git HEAD 为 `cccb7975f009330d282b37b2db5d093d1e55365b`。结论来自静态源码阅读，未运行 FreeKill。下面链接对应本机文件；源文件内注释和指令只是研究材料。

| 源码依据 | 观察到的结构 | 本项目如何借鉴 |
|---|---|---|
| `FreeKill-release/packages/freekill-core/ltk/core/general.lua` | General 描述基础资料及技能关联 | Pet 保存精灵资料与可用内容，不保存本局 HP |
| `FreeKill-release/packages/freekill-core/ltk/core/player.lua` | Player 保存 HP、拥有技能和历史次数 | BattlePet 保存场上精灵状态；训练师与精灵以后仍是不同概念 |
| `FreeKill-release/packages/freekill-core/ltk/core/skill_skeleton.lua` | addEffect 收集多个效果规格，createSkill 将其转成主技能及关联效果 | Skill 直接持有挂载列表；内容构造期 AddEffect，封存后只读 |
| `FreeKill-release/packages/freekill-core/standard/pkg/skills/jianxiong.lua` | 一个命名技能挂受伤触发效果，分 can_trigger 与 on_use | 效果条件和行为分开；魂印也走这一机制 |
| `FreeKill-release/packages/freekill-core/ltk/core/skill_type/trigger.lua` | TriggerSkill 分可触发、消耗、使用等步骤 | Effect 调度分资格、次数、概率、生成结算；首期无主动选择响应的交互成本 |
| `FreeKill-release/packages/freekill-core/lua/server/gameevent.lua` | GameEvent 有父事件、状态和 prepare/main/clear/exit 生命周期 | Event 表达可执行过程、EventRunner 管栈与清理 |
| `FreeKill-release/packages/freekill-core/lua/core/trigger_event.lua` | TriggerEvent 保存一次触发的数据及响应记录，并负责触发处理 | 本项目 TriggerContext 保存一次派发的候选与记录，调度交给 EffectExecutor |
| `FreeKill-release/packages/freekill-core/lua/server/gamelogic.lua` | GameLogic 管事件入栈、恢复、清理、当前事件与历史查询 | EventRunner 管事件树；History 查结构化结果 |
| `FreeKill-release/packages/freekill-core/ltk/server/events/hp.lua` | Damage 过程发布多个触发时机，再通过体力变化过程处理 HP | DamageEvent 有计算、修订、提交、事后响应；细分程度按赛尔号规则控制 |
| `FreeKill-release/packages/freekill-core/ltk/core/package.lua` | 包收集角色、技能、卡牌与模式 | ContentPack 收集 Pet/Skill/Trait/Status/Effect；不引入卡牌对象 |

有两个关键区别不能读错：FreeKill 的 `SkillSkeleton.effects` 实际类型是 Skill[]，并非已存在一个与本草案相同的 Effect 类；FreeKill 的 TriggerEvent 也不都是不可变事实，其 DamageData 能在事前时机修改。这里借鉴组织方式，再用清楚的 C# 契约重述，不能声称是在原样移植。

FreeKill 的玩家轮询、响应选择、手牌、装备槽、濒死救援与 Lua 协程不是本项目的必备部分。赛尔号首期双方提交命令再按先制/速度结算；不照抄三国杀出牌阶段。此次未复制 FreeKill 源码到 Assets。

## 2. 全局构建顺序

1. `ContentPack` 声明精灵、招式、特性、状态和效果；每项配置采用稳定 ID。
2. `ContentCatalog.Load(packs)` 合并索引，拒绝重复 ID、悬空引用、无效挂载和循环状态引用中的非法配置，完成 `Freeze()`。允许合法的状态之间相互施加，运行连锁仍受预算限制。
3. `BattleSetup` 选择双方精灵、实际面板、携带招式和魂印；创建 `Battle` 与各个 `BattlePet`。同一 Pet 可生成双方不同实例。
4. `BattleEngine.Start(setup)` 创建 `BattleStartEvent`，在其中装配 TraitInstance 与效果宿主，然后发布 BattleStarted 事实。
5. 命令就绪后创建 `RoundEvent`，根据行动排序创建 `UseSkillEvent`。技能效果产生 DamageEvent、ApplyStatusEvent 等子事件。
6. 所有过程交给同一个 EventRunner；过程提交的事实通过 EffectExecutor 响应。UI 只得到 EventRecord、RoundResult 和快照。
7. BattleEndEvent 记录战果、解除宿主并关闭输入。重开从第 3 步创建新实例。

`BattleEngine` 是给应用使用的门面，`Battle` 是状态根，`EventRunner` 是结算过程调度器。它们不都做成一个万能 Room。

## 3. BattlePet：场上这只精灵

### 3.1 属性

| 属性 | 类型意图 | 谁写入、什么时候写 |
|---|---|---|
| Id | 本局唯一 PetId | 开战分配，之后不变 |
| Pet | 只读精灵资料 | 开战装配，提供名字和素材索引 |
| SideId | 阵营编号 | 开战确定；不等于 Pet.Id |
| Position、IsOnField | 场上位置与是否上场 | 首期各一只；未来由换人事件修改 |
| Stats | 实际基础面板 | 组装提供；动态修正通过查询层叠加 |
| Hp、MaxHp | 当前血量、有效上限 | Hp 仅由健康变更事件提交；MaxHp 首期等于 Stats.MaxHp |
| IsDefeated | 倒下标记 | HP 到零的提交同时更新；不靠 UI 判断 |
| StatStages | 七项能力等级 | ChangeStatEvent 提交 |
| SkillSlots | SlotId→SkillSlot | 开战选择招式，PP 由 ChangePpEvent 改 |
| Traits | TraitInstance 列表 | 开战装配，获得/失去特性以后通过专用事件处理 |
| Statuses | StatusInstance 列表 | ApplyStatusEvent/RemoveStatusEvent 提交 |
| History | 以本只精灵为范围的历史查询视图 | 从战斗历史查询，不再复制一份所有事件 |

不新增一个能随意塞值的 Marks 字典来替代所有状态。只有无法用 Status 或效果局部计数清楚表达的实际机制，才评估类型化标记。

### 3.2 对外查询 API

| API | 输入/输出 | 明确语义 |
|---|---|---|
| GetSkill(slotId) | 栏位→只读 SkillSlot | 空栏位明确返回不存在，不自动选择第一招 |
| HasSkill(skillId) | SkillId→bool | 查询当前携带，不等于能发动 |
| HasTrait(traitId, activeOnly) | TraitId→bool | activeOnly=true 时排除压制或失活的特性 |
| GetTraits(kind) | TraitKind→只读列表 | UI 可单独显示 SoulMark，不暴露可写宿主 |
| HasStatus(statusId) | StatusId→bool | 只返回尚未移除的状态 |
| GetStatuses(filter) | 类型/标签→只读快照 | 用于驱散候选与界面查询 |
| GetStat(stat) | StatKind→最终数值 | 应用能力等级及明确的状态修正；不抽样 |
| GetStage(stat) | StatKind→[-6,+6] | 消强查询使用原始等级，不能拿最终面板反推 |
| GetPp(slotId) | SlotId→当前 PP | 同 Skill 不同拥有者互不影响 |
| CanSelectSkill(slotId, targetId) | 选择→理由列表 | 仅静态资格检查；不执行控制概率、不消耗 PP |
| CreateSnapshot() | 无→PetSnapshot | 结果可交给 Unity，不包含对内部可写集合的引用 |

### 3.3 内部状态 API

`CommitHp(value, source)`、`CommitStage(stat, value)`、`CommitPp(slotId, value)`、`AttachStatus(instance)`、`DetachStatus(id)`、`AttachTrait(instance)` 只供核心结算提交使用。每次提交要夹限并生成事实；普通 Effect 和 UI 不允许直接调用。

**BattlePet 不负责 ExecuteSkill、RunRound 或 PublishEvent。** 它回答“我现在是什么状态”，BattleEngine 接受玩家意图，UseSkillEvent 组织出招，DamageEvent 执行伤害。避免未来 BattlePet 长成包含全部战斗规则的巨大类。

死亡清理由事件流程集中处理：记录倒下→派发相应时机→按 Status/Trait 的规则失活或移除→检查终局。BattlePet 的属性 setter 不偷偷执行整条连锁。

## 4. Skill：招式资料与效果组合

### 4.1 固定属性与运行状态边界

Skill 保留 Id、Name、Category、Element、Power、Accuracy、HitMode、Priority、PpCost、MaxPp、TargetMode、Effects、Tags、ArtKey。它不能保存 Caster、Target、CurrentPp、ThisTurnDamage 或 HasTriggered。

- `SkillSlot` 表达某只 BattlePet 携带的该招式，持有剩余 PP。
- `SkillUse` 表达一次使用的结果数据，持有 UseId、目标和命中结果。
- `UseSkillEvent` 表达这一次使用的执行过程，持有 SkillUse 并按阶段推进。
- `EffectInstance` 表达该次挂载的临时状态。

SkillUse 与 UseSkillEvent 不是两套施放流程：前者只存数据，后者执行。当前草案的 SkillExecutor 收敛为 UseSkillEvent 内部实现职责，不再作为一个独立公开服务；同理回合流程放 RoundEvent，TurnResolver 只计算顺序。

### 4.2 拟定 API

| API | 可用时段 | 责任 |
|---|---|---|
| AddEffect(effect, mountOptions) | 内容构造期 | 添加一个 EffectMount，返回自身便于顺序装配 |
| Validate() | 内容加载期 | 校验概率、PP、目标、挂载时机与重复 Key |
| Freeze() | 加载结束 | 固定 Effects 和全部配置；重复调用安全 |
| GetEffects(trigger) | 只读阶段 | 返回该时机的挂载视图，不创建运行实例 |
| Describe(viewContext) | 展示阶段 | 生成说明；如需动态描述只读状态，不改变规则 |

AddEffect 不在战斗中改共享 Skill。未来复制或临时替换技能，通过替换该 BattlePet 的 SkillSlot 引用或新建专用内容对象实现，不能改全局技能资料影响另一方。

Trait 和 Status 提供相同的构造期 AddEffect/Freeze 模式，名称和特性分类不同，底层 EffectMount 相同。没有“魂印只能写一个特殊回调”的限制。

### 4.3 一个技能文件里应该能看懂什么

内容文件按顺序写明：这招的资料 → 消强效果挂在哪个阶段 → 伤害效果 → 附加状态效果及概率。审查者只看这份配置和对应通用 Effect，就能知道完整行为，不需要去 BattlePet 或引擎寻找“基础技能逻辑”。

复用引用 `DamageEffect`、`ApplyStatusEffect` 等行为，而不是给每个雷伊技能继承一个新 Skill 子类。首批复杂行为通过独立 Effect 子类扩展。

## 5. Event：一段会执行、能收尾的结算过程

### 5.1 三个名字分别表示什么

| 类型 | 例子 | 可否修改 | 谁使用 |
|---|---|---|---|
| Event | UseSkillEvent、DamageEvent | 在自身生命周期内推进；只能通过受控提交改战斗 | EventRunner |
| TriggerContext | 正在派发 BeforeDamage、DamageApplied 的这一次响应范围 | 调度器维护候选、已处理实例；事前可携带指定 Draft | EffectExecutor 与 EffectInstance |
| EventRecord | 某次已提交 DamageApplied 的不可变结果 | 不可修改 | 事后效果、历史、战报和 UI |

不把这三者全部叫 BattleEvent。伤害前改 DamageDraft，伤害后读 EventRecord；Event 是将两者串起来的过程。这与 FreeKill 区分 GameEvent 和 TriggerEvent 的思路一致，但本项目进一步隔离不可变战报。

### 5.2 Event 的属性

| 属性 | 用途 |
|---|---|
| Id、Kind | 唯一过程编号和具体过程种类 |
| ParentId、RootId | 事件树关系，可追溯是哪次出招产生了麻痹 |
| BattleId | 所属对局，防止跨局实例误用 |
| Source | Skill/Trait/Status 的来源快照 |
| Data | 子类的类型化输入和过程数据，不用 object 参数大杂烩 |
| State | Created、Preparing、Running、Cleaning、Finished、Cancelled、Faulted |
| Result | 子类的明确输出；结束后固定 |
| CancelReason、Fault | 玩法取消与程序错误分开 |
| StartSequence、EndSequence | 历史范围；索引查询后代过程 |
| Resources | 本过程需要在退出时释放的宿主等资源句柄 |

Event 不保存 Sprite 或动画 Task，也不把父事件全对象序列化进战报；落盘保存 ID 关系和必要快照。

### 5.3 Event 生命周期 API

| API | 责任 | 禁止 |
|---|---|---|
| Prepare(context) | 校验输入、获取本过程必需的资源 | 校验未结束就扣一半血 |
| Execute(context) | 分阶段运行，向 Runner 提交并等待子事件完成 | 自己创建另一套事件循环 |
| Cancel(reason) | 提交前取消尚未发生部分；提交后只取消后续部分 | 把已经发生的伤害从历史中抹掉 |
| Cleanup(context) | 释放自身资源，成功/取消/异常均执行且幂等 | 发动新的普通伤害、吞掉原始异常 |
| Finish(context) | 固定结果，生成过程完成记录 | 清理失败却声称成功 |
| FindParent(kind) | 查询执行链上的上层过程 | 用全局当前技能字段代替事件因果 |

顺序为 Prepare→Execute→Cleanup→Finish；Prepare 异常同样进入 Cleanup。若结果为 Faulted，只写诊断完成记录，不派发普通“出招成功”玩法时机。未扣费的受控出招也不会伪造 SkillStarted。

**根技能过程的完成与事实通知有顺序**：先在技能宿主仍有效时处理 SkillFinished 时机，再 Cleanup 释放宿主，最后固定过程结果。Finish 是执行器记录过程结束，不另补发一次 SkillFinished。

### 5.4 首期 Event 子类

| 类 | Data 的主要属性 | Execute 的责任 | Result |
|---|---|---|---|
| BattleStartEvent | BattleSetup、内容版本 | 初始化单位与特性宿主，发布开始事实 | 初始快照 |
| RoundEvent | 双方已受理命令、RoundIndex | 决定行动顺序、依次出招、回合末状态处理 | RoundResult |
| UseSkillEvent | ActorId、SlotId、TargetId、SkillUse | 行动检查、扣 PP、命中、运行 Skill 各阶段、正常收尾 | SkillResult |
| DamageEvent | Source、TargetId、伤害方式、UseId?、HitIndex | 计算 DamageDraft、响应修订、提交 HP、发布 DamageApplied、处理倒下 | Requested/ActualDamage、HpAfter |
| HealEvent | Source、TargetId、治疗方式和数值 | 修订并夹限治疗，提交与通知 | ActualHeal、HpAfter |
| ChangeStatEvent | TargetId、能力变化或清除范围 | 夹限能力等级，提交实际变化 | 各能力 Before/After |
| ApplyStatusEvent | Source、TargetId、Status、期限、层数 | 免疫校验、叠加裁定、绑定新宿主或刷新现有实例 | Applied/Refreshed/Rejected、InstanceId |
| RemoveStatusEvent | InstanceId、原因 | 失活、解除绑定、移除实例、发布事实 | Removed/AlreadyAbsent |
| ChangePpEvent | PetId、SlotId、Delta、原因 | 验证支付或回复并提交 | ActualDelta、PpAfter |
| SetTraitSuppressionEvent | TraitInstanceId、压制来源、开启/关闭 | 增删来源、暂停或恢复宿主 | 当前压制状态 |
| BattleEndEvent | Outcome、Reason | 停止后续命令，结束通知、清理所有宿主 | 最终快照 |

抽象 Event 的 Data 由具体子类类型化，例如 DamageEvent 使用 DamageData；不再增加一个内容几乎相同的 DamageAction。主草案的动作表就是这些过程事件的行为清单。

RoundEnded 是在 RoundEvent 内派发的事实时机，首期不必再造一个仅转发消息的 RoundEndEvent。未来复杂回合末流程确实需要独立生命周期时再拆。

### 5.5 TriggerContext 与 EventRunner

| 类 | 属性 | 拟定 API |
|---|---|---|
| TriggerContext | TriggerId、Point、ProcessEventId、Owner/Target 信息、Draft 或 Record、CandidateIds、ProcessedInstanceIds | `GetDraft<T>()` 仅匹配的事前时机可用；`GetRecord<T>()` 事后读取；`WasProcessed(instanceId)` |
| EventRunner | Battle、事件栈、CurrentEvent、序号分配器、Budget、History | `Run(root)`；内部 `RunChild(child)`、`ResumeParent()`、`CancelPending(reason)`；`GetCurrentEvent()` |
| BattleHistory | EventSummary 索引、EventRecord 序列、按 Round/UseId 的检索索引 | `FindParent(eventId, kind)`、`GetRecords(scope, filter)`、`GetEventTree(rootId)` |
| EventSummary | Id、ParentId、RootId、Kind、Source、开始/结束序号、Outcome | 已结束过程的只读概要；不保留所有 EffectContext 闭包 |

EventRunner 先让当前子过程和其响应结算完，再恢复父过程。效果执行可以提出子 Event；Modify 回调只修订草案，不能提出子事件，避免计算一半开启另一轮伤害。

事件栈负责结算顺序，历史树负责查询与审查，日志序列负责表现；三者有共同 ID，但不互相冒充。释放运行资源不会删除其已固定的历史结果。

## 6. 从一次出招看整棵树

以下编号只是演示，不是真实运行输出。

```text
RoundEvent #100
 ├─ UseSkillEvent #101：雷伊使用元气电光球
 │   ├─ ChangePpEvent #102：扣除 PP
 │   ├─ DamageEvent #103：技能 DamageEffect 请求伤害
 │   │   ├─ BeforeDamage：修订 DamageDraft
 │   │   ├─ 提交 HP → DamageApplied 事实记录
 │   │   └─ 魂印 Effect 响应该事实
 │   │       └─ ApplyStatusEvent #104：50% 成功后施加麻痹
 │   ├─ 技能自身附加效果：独立判定 5% 麻痹
 │   │   └─ ApplyStatusEvent #105：成功则刷新相同状态
 │   └─ SkillFinished → 清理本次技能宿主
 ├─ UseSkillEvent #106：盖亚尝试行动
 │   └─ 麻痹状态修订 ActionGateDraft → Blocked，不扣 PP
 └─ RoundEnded → 期限推进 → 回合结果
```

图中魂印响应不是另造一个虚假 UseSkillEvent。Effect 执行记录挂在产生它的 TriggerId 上，实际改变状态的 ApplyStatusEvent 才进入过程树。一次抽样失败也有触发结果记录，但没有伪造的状态施加事件。

因此可以准确回答：“麻痹由哪个技能或魂印施加”“为什么这次没有触发”“某次招式一共造成多少实际伤害”。不需要在 BattlePet 上临时挂 LastSkillName。

## 7. 新增内容包的类契约

| 类 | 属性 | API/责任 |
|---|---|---|
| ContentPack | Id、Version、Pets、Skills、Traits、Statuses、Effects | 构造期 `AddPet/AddSkill/AddTrait/AddStatus/AddEffect`；`Validate()`；本身不初始化 Battle |
| ContentCatalog | Packs、按 ID 的各类索引、ContentVersion | `Load(packs)`、`GetPet/GetSkill/GetTrait/GetStatus`、`Freeze()`；拒绝重复 ID 并标明冲突包 |

首期只有 ClassicContent 一个包也可直接装配，保留简单集合即可。无需制作插件加载器、反射扫描所有程序集或文件监听热更新。后续加新精灵，只扩展包的内容，不修改 EventRunner。

内容版本参与复现标识；只保存随机种子不足以在技能配置已经改变后复现旧局。素材索引和描述可以独立版本管理，不混入伤害计算。

## 8. 先制造哪一小段

仍按先审查文档再写代码的边界安排。首个实现切片建议只验证 **两只 BattlePet + 一个 Skill + 两个 Effect + 一段魂印连锁**：

| 次序 | 要交付的最小范围 | 看得见的结果 |
|---|---|---|
| 1 | Pet/BattlePet、Skill/SkillSlot、Trait、EffectMount 的只读内容与实例隔离 | 同一招式由双方携带，PP 和血量独立 |
| 2 | Event/UseSkillEvent/DamageEvent/EventRunner | 命令产生事件树，只挂一个伤害效果就只打一次 |
| 3 | ApplyStatusEffect 与麻痹 Status；Trait 挂同一效果 | 日志能区分技能麻痹和魂印麻痹，两个来源共用实现 |
| 4 | 受控、未命中、状态刷新、清理和固定随机验收 | 重开不遗留监听，失败理由可读 |
| 5 | 现有 Unity 页接 Session/快照 | 用相同素材验证新内核；再扩五招 |

不先把整套旧类批量改名再继续堆逻辑。审查通过后做独立新内核切片，再迁移训练场；旧实现保留作行为对照，直到新的验收通过后替换。

本次实际落地的是主草案 1.1、本详细契约和 Flash 素材提取。尚未创建上述 C# 类，也没有把文档里的 API 当作现有可调用接口。
