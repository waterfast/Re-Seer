using System;
using System.Collections.Generic;
using System.Linq;
using ReSeer.Pets;
using ReSeer.Combat.Contracts;

namespace ReSeer.Combat
{
    /// <summary>
    /// 权威单挑战斗：双方提交命令后推进一回合，结算与 Unity、网络传输无关。
    /// 服务端每局使用单线程队列调用；本对象不是并发容器。
    /// </summary>
    public sealed class Battle
    {
        private readonly string leftParticipant;
        private readonly string rightParticipant;
        private readonly Dictionary<string, BattleCommand> pending = new Dictionary<string, BattleCommand>();
        private readonly List<EventRecord> records = new List<EventRecord>();
        private uint randomState;
        private long nextEventId;
        private int eventCount;
        /// <summary>对局编号，由服务端分配。</summary>
        public string Id { get; }
        /// <summary>规则标识；这里仅实现固定伤害骨架。</summary>
        public const string RulesVersion = "combat-skeleton-1";
        /// <summary>左侧独立战斗精灵。</summary>
        public BattlePet Left { get; }
        /// <summary>右侧独立战斗精灵。</summary>
        public BattlePet Right { get; }
        /// <summary>已经开始结算的回合数。</summary>
        public int Round { get; private set; }
        /// <summary>当前阶段。</summary>
        public BattlePhase Phase { get; private set; } = BattlePhase.WaitingForCommands;
        /// <summary>当前结果。</summary>
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        /// <summary>胜方参与者编号；尚未结束时为空。</summary>
        public string WinnerId { get; private set; } = "";
        /// <summary>仅服务端诊断可读取的异常，公共快照不暴露堆栈。</summary>
        public Exception Fault { get; private set; }

        /// <summary>
        /// 从服务器读取的可信个体及内容配置建立独立战斗状态。
        /// participantId 必须来自已认证会话，不可直接采用请求体里声称的身份。
        /// </summary>
        public Battle(string id, string leftParticipant, Pet left, string rightParticipant, Pet right,
            IReadOnlyDictionary<string, Skill> skills, IPetStatCalculator calculator, uint seed,
            IInscriptionRules inscriptionRules = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(leftParticipant) ||
                string.IsNullOrWhiteSpace(rightParticipant) || leftParticipant == rightParticipant)
                throw new ArgumentException("对局和双方身份必须有效且不同");
            if (left == null || right == null || calculator == null || skills == null) throw new ArgumentNullException(nameof(left));
            // 内容表先复制，避免调用者在两侧装配之间修改字典。
            var catalog = skills.ToDictionary(p => p.Key, p => p.Value);
            Left = new BattlePet("left", left, CalculateStats(left, calculator, inscriptionRules), catalog);
            Right = new BattlePet("right", right, CalculateStats(right, calculator, inscriptionRules), catalog);
            Id = id; this.leftParticipant = leftParticipant; this.rightParticipant = rightParticipant;
            randomState = seed == 0 ? 0x9E3779B9u : seed;
        }

        /// <summary>收集本回合意图。双方齐备后同步结算；过期、重复或非法命令没有状态副作用。</summary>
        public SubmitResult Submit(string authenticatedParticipantId, BattleCommand command)
        {
            int start = records.Count;
            string error = ValidateCommand(authenticatedParticipantId, command);
            if (error != "") return Reply(false, error, start);
            pending.Add(authenticatedParticipantId, command);
            if (pending.Count < 2) return Reply(true, "", start);
            Phase = BattlePhase.Resolving;
            try
            {
                Round++; eventCount = 0;
                RunEvent(new RoundEvent(this, pending[leftParticipant], pending[rightParticipant]));
                Phase = Outcome == BattleOutcome.Ongoing ? BattlePhase.WaitingForCommands : BattlePhase.Finished;
            }
            catch (Exception errorDuringResolution)
            {
                Fault = errorDuringResolution; Outcome = BattleOutcome.Faulted; Phase = BattlePhase.Faulted;
                Record(null, "BattleFaulted");
            }
            finally { pending.Clear(); }
            return Reply(true, "", start);
        }

        /// <summary>公共状态可以广播；不包含双方尚未执行的命令、养成或隐藏招式。</summary>
        public BattleSnapshot GetSnapshot() => new BattleSnapshot(Id, Round, Phase, Outcome, WinnerId,
            new[] { Left.CreateSnapshot(), Right.CreateSnapshot() });
        /// <summary>只向认证后的本人发送自己的栏位和 PP；陌生身份拒绝访问。</summary>
        public IReadOnlyList<SkillSlotSnapshot> GetOwnSkills(string authenticatedParticipantId)
        {
            if (authenticatedParticipantId == leftParticipant) return Left.GetSkillSlots();
            if (authenticatedParticipantId == rightParticipant) return Right.GetSkillSlots();
            throw new ArgumentException("参与者不属于本局");
        }
        /// <summary>返回公开增量事实的副本，传输层可按序号去重并补发。</summary>
        public IReadOnlyList<EventRecord> ReadEvents(long afterSequence) => Array.AsReadOnly(records.Where(r => r.Sequence > afterSequence).ToArray());

        private string ValidateCommand(string participant, BattleCommand command)
        {
            if (participant != leftParticipant && participant != rightParticipant) return "UnknownParticipant";
            if (Phase != BattlePhase.WaitingForCommands) return "BattleNotWaiting";
            if (command == null || command.BattleId != Id) return "WrongBattle";
            if (command.Round != Round + 1) return "WrongRound";
            if (pending.ContainsKey(participant)) return "AlreadySubmitted";
            var actor = participant == leftParticipant ? Left : Right;
            if (command.SkillSlot.HasValue && !actor.CanUse(command.SkillSlot.Value)) return "SkillUnavailable";
            return "";
        }
        private SubmitResult Reply(bool accepted, string error, int start) =>
            new SubmitResult(accepted, error, records.Skip(start), GetSnapshot());
        // 有刻印却没有规则提供者时明确拒绝，避免悄悄漏算或相信客户端填写的加成。
        private static StatValues CalculateStats(Pet pet, IPetStatCalculator calculator, IInscriptionRules inscriptions)
        {
            if (pet.Inscriptions.Count > 0)
            {
                if (inscriptions == null) throw new ArgumentException("已装备刻印，必须提供服务器刻印规则");
                inscriptions.Validate(pet);
            }
            var basic = calculator.Calculate(pet);
            if (basic == null || basic.Hp < 1 || basic.Attack < 1 || basic.Defense < 1 ||
                basic.SpecialAttack < 1 || basic.SpecialDefense < 1 || basic.Speed < 1)
                throw new ArgumentException("基础面板必须为六项正数");
            if (pet.Inscriptions.Count == 0) return basic;
            var bonus = inscriptions.GetStatBonus(pet) ?? throw new InvalidOperationException("刻印加成不能为空");
            return new StatValues(checked(basic.Hp + bonus.Hp), checked(basic.Attack + bonus.Attack),
                checked(basic.Defense + bonus.Defense), checked(basic.SpecialAttack + bonus.SpecialAttack),
                checked(basic.SpecialDefense + bonus.SpecialDefense), checked(basic.Speed + bonus.Speed));
        }
        internal void ValidateMember(BattlePet pet)
        { if (pet != Left && pet != Right) throw new ArgumentException("事件不能修改其他对局的精灵"); }
        internal bool Roll(int chance) => chance == 10000 || (chance > 0 && Next(10000) < chance);
        // 明确的整数算法，使同种子/同命令在不同运行时保持一致；种子不发给客户端。
        private int Next(int max)
        { randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5; return (int)(randomState % (uint)max); }
        internal void Record(Event process, string kind, string source = "", string target = "", string content = "", int amount = 0, int? hpAfter = null)
        { records.Add(new EventRecord(records.Count + 1L, Round, kind, process?.Id ?? 0, process?.ParentId ?? 0, source, target, content, amount, hpAfter)); }

        // 一个显式迭代器栈处理所有过程；结束或故障都会释放尚未退出的迭代器。
        private void RunEvent(Event root)
        {
            var stack = new Stack<Frame>();
            try
            {
                Push(root, 0);
                while (stack.Count > 0)
                {
                    var frame = stack.Peek();
                    if (frame.Iterator.MoveNext()) Push(frame.Iterator.Current, frame.Process.Id);
                    else
                    {
                        frame.Iterator.Dispose(); frame.Process.State = EventState.Finished; stack.Pop();
                    }
                }
            }
            finally
            {
                while (stack.Count > 0)
                {
                    var frame = stack.Pop(); frame.Process.State = EventState.Faulted;
                    // 清理失败不阻止其他帧释放，原始错误仍由外层记录。
                    try { frame.Iterator.Dispose(); } catch { }
                }
            }
            void Push(Event child, long parent)
            {
                if (child == null || child.State != EventState.Created) throw new InvalidOperationException("事件为空或已使用");
                if (++eventCount > 512 || stack.Count >= 32) throw new InvalidOperationException("结算事件超过预算");
                child.Id = ++nextEventId; child.ParentId = parent; child.State = EventState.Running;
                try { stack.Push(new Frame(child, child.Execute(this).GetEnumerator())); }
                catch { child.State = EventState.Faulted; throw; }
            }
        }
        /// <summary>迭代器栈的一帧，仅权威循环使用。</summary>
        private sealed class Frame
        {
            internal readonly Event Process;
            internal readonly IEnumerator<Event> Iterator;
            internal Frame(Event process, IEnumerator<Event> iterator) { Process = process; Iterator = iterator; }
        }
        /// <summary>一次回合过程，保存本回合固定排序，避免执行中速度变化重排。</summary>
        private sealed class RoundEvent : Event
        {
            private readonly Battle owner;
            private readonly BattleCommand left;
            private readonly BattleCommand right;
            internal RoundEvent(Battle owner, BattleCommand left, BattleCommand right) { this.owner = owner; this.left = left; this.right = right; }
            internal override IEnumerable<Event> Execute(Battle battle)
            {
                battle.Record(this, "RoundStarted");
                bool leftFirst = true;
                if (left.SkillSlot.HasValue && right.SkillSlot.HasValue)
                {
                    int priority = owner.Left.Skills[left.SkillSlot.Value].Priority.CompareTo(owner.Right.Skills[right.SkillSlot.Value].Priority);
                    int speed = owner.Left.Stats.Speed.CompareTo(owner.Right.Stats.Speed);
                    leftFirst = priority != 0 ? priority > 0 : speed != 0 ? speed > 0 : owner.Next(2) == 0;
                }
                foreach (bool isLeft in leftFirst ? new[] { true, false } : new[] { false, true })
                {
                    var actor = isLeft ? owner.Left : owner.Right;
                    var target = isLeft ? owner.Right : owner.Left;
                    var command = isLeft ? left : right;
                    if (command.SkillSlot.HasValue) yield return new UseSkillEvent(actor, target, command.SkillSlot.Value);
                    else battle.Record(this, "Waited", actor.Id);
                    if (!owner.Left.IsAlive || !owner.Right.IsAlive)
                    {
                        owner.Outcome = !owner.Left.IsAlive && !owner.Right.IsAlive ? BattleOutcome.Draw : BattleOutcome.Victory;
                        owner.WinnerId = owner.Outcome == BattleOutcome.Draw ? "" : owner.Left.IsAlive ? owner.leftParticipant : owner.rightParticipant;
                        battle.Record(this, "BattleEnded"); yield break;
                    }
                }
                battle.Record(this, "RoundEnded");
            }
        }
    }
}
