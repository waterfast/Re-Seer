namespace ReSeer.Pets
{
    /// <summary>刻印扩展接口。实现应核验装备资格并查服务器配置；非法装备抛出明确异常。</summary>
    public interface IInscriptionRules
    {
        /// <summary>检查槽位、等级、互斥及个体所有权，失败时不能创建战斗。</summary>
        void Validate(Pet pet);
        /// <summary>返回六项非负数值加成。特殊被动以后由独立的特性装配接口提供。</summary>
        StatValues GetStatBonus(Pet pet);
    }
}
