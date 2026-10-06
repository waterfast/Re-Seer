using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ReSeer.Skills
{
    // One serialized catalog asset. Entries are plain data, not separate ScriptableObjects.
    [CreateAssetMenu(menuName = "Re-Seer/Skills/Database", fileName = "SkillDatabase")]
    public sealed class SkillDatabaseSO : ScriptableObject
    {
        public string contentVersion = "1";
        public List<SkillData> skills = new List<SkillData>();

        public bool TryGet(string id, out SkillData skill)
        {
            skill = skills.Find(item => item != null && item.id == id);
            return skill != null;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(contentVersion)) errors.Add("内容版本不能为空。");
            foreach (var skill in skills)
            {
                if (skill == null) { errors.Add("存在空技能记录。"); continue; }
                string label = skill.id ?? "<空 ID>";
                if (string.IsNullOrWhiteSpace(skill.id) || !ids.Add(skill.id)) errors.Add(label + "：ID 为空或重复。");
                if (string.IsNullOrWhiteSpace(skill.fallbackName)) errors.Add(label + "：名称不能为空。");
                if (skill.power < 0 || skill.accuracy < 0 || skill.accuracy > 10000 || skill.maxPp < 1 || skill.ppCost < 1 || skill.ppCost > skill.maxPp)
                    errors.Add(label + "：威力、命中率或 PP 不合法。");
                if (skill.category != "Physical" && skill.category != "Special" && skill.category != "Status") errors.Add(label + "：类别须为 Physical / Special / Status。");
                if (string.IsNullOrWhiteSpace(skill.elementId) || (skill.target != "enemy" && skill.target != "self"))
                    errors.Add(label + "：属性或目标不合法。");
                if (skill.effects == null) { errors.Add(label + "：效果列表为空引用。"); continue; }
                foreach (var effect in skill.effects)
                {
                    if (effect == null || string.IsNullOrWhiteSpace(effect.effectId) || effect.parameters == null)
                    { errors.Add(label + "：效果 ID 或参数列表为空。"); continue; }
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var parameter in effect.parameters)
                        if (parameter == null || string.IsNullOrWhiteSpace(parameter.key) || !keys.Add(parameter.key) || parameter.value == null)
                            errors.Add(label + "：效果参数名称为空、重复或值为空引用。");
                }
            }
            return errors;
        }

        // Optional transport projection for a future server; the client never executes these rules.
        public string ToServerJson()
        {
            var errors = Validate();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            var catalog = new SkillExportCatalog { contentVersion = contentVersion };
            foreach (var skill in skills) catalog.skills.Add(new SkillExportRow(skill));
            catalog.skills.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return JsonUtility.ToJson(catalog, true);
        }

        [Serializable]
        private sealed class SkillExportCatalog
        {
            public int schemaVersion = 1;
            public string contentVersion;
            public List<SkillExportRow> skills = new List<SkillExportRow>();
        }

        [Serializable]
        private sealed class SkillExportRow
        {
            public string id;
            public string elementId;
            public string category;
            public int power;
            public int accuracy;
            public int maxPp;
            public int ppCost;
            public int priority;
            public string target;
            public List<SkillEffectData> effects;

            public SkillExportRow(SkillData source)
            {
                id = source.id; elementId = source.elementId; category = source.category;
                power = source.power; accuracy = source.accuracy; maxPp = source.maxPp;
                ppCost = source.ppCost; priority = source.priority; target = source.target;
                effects = source.effects;
            }
        }
    }

    [Serializable]
    public sealed class SkillData
    {
        public string id = "new_skill";
        public string nameKey;
        public string descriptionKey;
        public string fallbackName = "新技能";
        [TextArea(3, 8)] public string fallbackDescription = "威力 {power}。";
        public string elementId = "normal";
        public string category = "Physical";
        public int power;
        public int accuracy = 10000;
        public int maxPp = 10;
        public int ppCost = 1;
        public int priority;
        public string target = "enemy";
        public List<SkillEffectData> effects = new List<SkillEffectData>();
        public Sprite icon;
        public string animationKey;
        public string vfxKey;
        public string sfxKey;

        public string DisplayName(Func<string, string> translate = null) => Resolve(nameKey, fallbackName, translate);

        public string Describe(Func<string, string> translate = null)
        {
            return Resolve(descriptionKey, fallbackDescription, translate)
                .Replace("{power}", power.ToString(CultureInfo.InvariantCulture))
                .Replace("{maxPp}", maxPp.ToString(CultureInfo.InvariantCulture))
                .Replace("{accuracy}", (accuracy / 100f).ToString("0.##", CultureInfo.InvariantCulture));
        }

        private static string Resolve(string key, string fallback, Func<string, string> translate)
        {
            string translated = string.IsNullOrWhiteSpace(key) ? null : translate?.Invoke(key);
            return string.IsNullOrEmpty(translated) || translated == key ? fallback ?? "" : translated;
        }
    }

    [Serializable]
    public sealed class SkillEffectData
    {
        public string effectId = "damage";
        public List<SkillEffectParameter> parameters = new List<SkillEffectParameter>();
    }

    [Serializable]
    public sealed class SkillEffectParameter
    {
        public string key;
        public string value;
    }
}
