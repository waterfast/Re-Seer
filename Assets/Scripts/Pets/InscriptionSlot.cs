using System;

namespace ReSeer.Pets
{
    /// <summary>一个已装备刻印的持久化快照；空槽通过没有此索引的记录表达。</summary>
    public sealed class InscriptionSlot
    {
        /// <summary>从零开始的装备槽位。</summary>
        public int Index { get; }
        /// <summary>拥有的刻印个体编号，用于服务器核验所有权。</summary>
        public string InstanceId { get; }
        /// <summary>刻印种类编号，用于查表。</summary>
        public string InscriptionId { get; }
        /// <summary>强化等级；具体上限由刻印规则负责。</summary>
        public int Level { get; }
        /// <summary>构造基本装备资料；不相信客户端提供的刻印加成数值。</summary>
        public InscriptionSlot(int index, string instanceId, string inscriptionId, int level)
        {
            if (index < 0 || level < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(inscriptionId)) throw new ArgumentException("刻印编号不能为空");
            Index = index; InstanceId = instanceId; InscriptionId = inscriptionId; Level = level;
        }
    }
}
