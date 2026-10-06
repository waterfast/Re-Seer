namespace ReSeer.Pets
{
    /// <summary>服务器基础面板计算接口：合并种族值、等级、个体值、性格和学习力；刻印由开战组装单独加算。</summary>
    public interface IPetStatCalculator
    {
        /// <summary>根据可信个体计算不含刻印的面板；生命及其余五项必须大于零，禁止重复加算刻印。</summary>
        StatValues Calculate(Pet pet);
    }

}
