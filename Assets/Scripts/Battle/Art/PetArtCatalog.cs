using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReSeer.Battle.Art
{
    /// <summary>编辑器生成的默认素材索引；引用 Sprite，保证 Art 目录中的图片进入客户端构建。</summary>
    public sealed class PetArtCatalog : ScriptableObject
    {
        public const string AssetRoot = "Assets/Art/Pet";
        public const string ResourceName = "PetArtCatalog";

        [Serializable]
        public sealed class Entry
        {
            public string petId;
            public Sprite avatar;
            public Sprite artwork;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<string, Entry> index;

        public Sprite Get(string petId, bool avatar)
        {
            if (index == null)
            {
                index = new Dictionary<string, Entry>(StringComparer.Ordinal);
                foreach (var entry in entries) index.Add(entry.petId, entry);
            }
            return index.TryGetValue(petId, out var found)
                ? (avatar ? found.avatar : found.artwork) : null;
        }

#if UNITY_EDITOR
        public void ReplaceEntries(List<Entry> value)
        {
            entries = value;
            index = null;
        }
#endif
    }
}
