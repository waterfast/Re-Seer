using System;
using System.Collections.Generic;
using System.Linq;

namespace ReSeer.Pets
{
    /// <summary>共享的精灵物种资料。Pet 保存个体养成，物种对象可以由多个个体共用。</summary>
    public sealed class PetSpecies
    {
        /// <summary>物种编号。</summary>
        public string Id { get; }
        /// <summary>物种显示名。</summary>
        public string Name { get; }
        /// <summary>元素属性编号，预留双属性。</summary>
        public IReadOnlyList<string> Elements { get; }
        /// <summary>种族值，不是计算后的战斗面板。</summary>
        public StatValues BaseStats { get; }
        /// <summary>允许学习的技能编号；具体学习条件以后由养成服务补充。</summary>
        public IReadOnlyList<string> LearnableSkills { get; }
        /// <summary>素材索引；领域层不引用 Unity Sprite。</summary>
        public string ArtKey { get; }
        /// <summary>装配并固定物种资料。</summary>
        public PetSpecies(string id, string name, IEnumerable<string> elements, StatValues baseStats,
            IEnumerable<string> learnableSkills, string artKey = "")
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("物种编号和名称不能为空");
            var types = (elements ?? throw new ArgumentNullException(nameof(elements))).ToArray();
            var skills = (learnableSkills ?? throw new ArgumentNullException(nameof(learnableSkills))).ToArray();
            if (types.Length == 0 || types.Any(string.IsNullOrWhiteSpace) || types.Distinct().Count() != types.Length ||
                skills.Any(string.IsNullOrWhiteSpace) || skills.Distinct().Count() != skills.Length)
                throw new ArgumentException("属性与学习表包含空值或重复值");
            BaseStats = baseStats ?? throw new ArgumentNullException(nameof(baseStats));
            Id = id; Name = name; ArtKey = artKey ?? "";
            Elements = Array.AsReadOnly(types); LearnableSkills = Array.AsReadOnly(skills);
        }
        /// <summary>查询物种是否允许学习某招式，不改变学习记录。</summary>
        public bool CanLearn(string skillId) => LearnableSkills.Contains(skillId);
    }
}
