namespace ReSeer.Combat.Contracts
{
    /// <summary>已经发生的公开事实，和服务端可执行的 Event 分开。</summary>
    public sealed class EventRecord
    {
        /// <summary>本局单调递增的记录序号，供客户端去重。</summary>
        public long Sequence { get; }
        /// <summary>发生的回合。</summary>
        public int Round { get; }
        /// <summary>事实类型，如 SkillStarted、DamageApplied。</summary>
        public string Kind { get; }
        /// <summary>产生事实的过程事件编号。</summary>
        public long EventId { get; }
        /// <summary>父过程编号；根过程为零。</summary>
        public long ParentEventId { get; }
        /// <summary>场上来源编号。</summary>
        public string SourceId { get; }
        /// <summary>场上目标编号。</summary>
        public string TargetId { get; }
        /// <summary>内容编号，例如技能编号。</summary>
        public string ContentId { get; }
        /// <summary>实际变化值；伤害按实际损失报告。</summary>
        public int Amount { get; }
        /// <summary>目标提交后的生命，未涉及生命则为空。</summary>
        public int? HpAfter { get; }
        /// <summary>固定事实数据，不暴露可写过程。</summary>
        public EventRecord(long sequence, int round, string kind, long eventId, long parentEventId,
            string sourceId, string targetId, string contentId, int amount, int? hpAfter)
        { Sequence = sequence; Round = round; Kind = kind; EventId = eventId; ParentEventId = parentEventId;
            SourceId = sourceId; TargetId = targetId; ContentId = contentId; Amount = amount; HpAfter = hpAfter; }
    }
}
