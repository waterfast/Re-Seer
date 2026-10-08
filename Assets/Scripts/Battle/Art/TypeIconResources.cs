using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReSeer.Battle.Art
{
    /// <summary>UI 属性图按官方编号加载；英文属性和双属性通过同一份素材清单解析。</summary>
    public static class TypeIconResources
    {
        private static TypeIconCatalog catalog;
        private static Dictionary<string, string> aliases;
        private static Dictionary<string, string> pairs;
        private static Dictionary<string, string> petTypes;

        [Serializable] private sealed class TypeList { public TypeEntry[] assets; }
        [Serializable] private sealed class TypeEntry
        {
            public string typeId;
            public string englishName;
            public string[] components;
        }
        [Serializable] private sealed class PetList { public PetEntry[] entries; }
        [Serializable] private sealed class PetEntry { public string petId; public string typeId; }

        public static Sprite Get(IReadOnlyList<string> elements)
        {
            EnsureTypes();
            if (aliases == null || elements == null || elements.Count == 0) return null;
            var ids = new List<string>();
            foreach (string element in elements)
            {
                if (string.IsNullOrWhiteSpace(element) || !aliases.TryGetValue(element.Trim(), out string id)) return null;
                if (!ids.Contains(id)) ids.Add(id);
            }
            string typeId;
            if (ids.Count == 1) typeId = ids[0];
            else if (!pairs.TryGetValue(PairKey(ids.ToArray()), out typeId)) return null;
            return catalog.Get(typeId);
        }

        /// <summary>只传精灵编号的预览入口，使用本地官方配置的属性；正式状态优先使用 Elements。</summary>
        public static string GetPetTypeId(string petId)
        {
            if (string.IsNullOrEmpty(petId)) return null;
            if (petTypes == null)
            {
                EnsureTypes();
                var json = catalog != null ? catalog.PetDefinitions : null;
                if (json == null) return null;
                petTypes = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pet in JsonUtility.FromJson<PetList>(json.text).entries) petTypes[pet.petId] = pet.typeId;
            }
            return petTypes.TryGetValue(petId, out string id) ? id : null;
        }

        private static void EnsureTypes()
        {
            if (aliases != null) return;
            if (catalog == null) catalog = Resources.Load<TypeIconCatalog>(TypeIconCatalog.ResourceName);
            var json = catalog != null ? catalog.TypeDefinitions : null;
            if (json == null) return;
            aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            pairs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var type in JsonUtility.FromJson<TypeList>(json.text).assets)
            {
                aliases[type.typeId] = type.typeId;
                aliases[type.englishName] = type.typeId;
                if (type.components != null && type.components.Length > 1)
                    pairs[PairKey(type.components)] = type.typeId;
            }
            // 技能库沿用 fighting，官方素材清单使用 fight。
            if (aliases.TryGetValue("fight", out string fighting)) aliases["fighting"] = fighting;
            if (aliases.TryGetValue("steel", out string mechanical)) aliases["mechanical"] = mechanical;
        }

        private static string PairKey(string[] ids)
        {
            var sorted = (string[])ids.Clone();
            Array.Sort(sorted, StringComparer.Ordinal);
            return string.Join("+", sorted);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { catalog = null; aliases = null; pairs = null; petTypes = null; }
    }
}
