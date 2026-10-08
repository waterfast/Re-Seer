using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReSeer.Battle.Art
{
    /// <summary>属性素材的引用索引；图片和清单保存在 Art/UI，Resources 只保留本索引。</summary>
    public sealed class TypeIconCatalog : ScriptableObject
    {
        public const string AssetRoot = "Assets/Art/UI/Types";
        public const string ResourceName = "TypeIconCatalog";

        [Serializable]
        public sealed class Entry
        {
            public string typeId;
            public Sprite sprite;
        }

        [SerializeField] private TextAsset typeDefinitions;
        [SerializeField] private TextAsset petDefinitions;
        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<string, Sprite> icons;

        public TextAsset TypeDefinitions => typeDefinitions;
        public TextAsset PetDefinitions => petDefinitions;

        public Sprite Get(string typeId)
        {
            if (icons == null)
            {
                icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (Entry entry in entries) icons.Add(entry.typeId, entry.sprite);
            }
            return icons.TryGetValue(typeId, out Sprite sprite) ? sprite : null;
        }

#if UNITY_EDITOR
        public void ReplaceEntries(TextAsset types, TextAsset pets, List<Entry> value)
        {
            typeDefinitions = types;
            petDefinitions = pets;
            entries = value;
            icons = null;
        }
#endif
    }
}
