using System.Collections.Generic;
using System.Linq;

namespace ReSeer.Combat.Contracts
{
    /// <summary>一次提交的回执。只包含本次新增事实，不公开另一方尚未执行的选招。</summary>
    public sealed class SubmitResult
    {
        /// <summary>是否接收该命令；接收后仍可能发生结算故障。</summary>
        public bool Accepted { get; }
        /// <summary>稳定的拒绝原因；成功为空。</summary>
        public string Error { get; }
        /// <summary>这次调用新增的公开事实。</summary>
        public IReadOnlyList<EventRecord> Events { get; }
        /// <summary>当前公共状态。</summary>
        public BattleSnapshot Snapshot { get; }
        /// <summary>构造隔离的返回数据。</summary>
        public SubmitResult(bool accepted, string error, IEnumerable<EventRecord> events, BattleSnapshot snapshot)
        { Accepted = accepted; Error = error; Events = System.Array.AsReadOnly(events.ToArray()); Snapshot = snapshot; }
    }
}
