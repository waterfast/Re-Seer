using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ReSeer.Battle.Art
{
    /// <summary>按精灵编号查找图片。自定义目录优先，缺失或损坏时回退客户端内置素材。</summary>
    public static class PetArtResources
    {
        private static readonly Dictionary<string, Sprite> customSprites = new Dictionary<string, Sprite>();
        private static PetArtCatalog catalog;
        public static string CustomDirectory { get; private set; }
        public static event Action ResourcesChanged;

        public static Sprite GetAvatar(string petId) => Get(petId, true);
        public static Sprite GetArtwork(string petId) => Get(petId, false);

        public static string GetRelativePath(string petId, bool avatar)
        {
            // 只接受精灵图片编号，避免把传入内容当成任意文件路径。
            if (string.IsNullOrEmpty(petId)) throw new ArgumentException("精灵编号不能为空。", nameof(petId));
            foreach (char digit in petId)
                if (digit < '0' || digit > '9') throw new ArgumentException("图片编号只支持数字。", nameof(petId));
            return (avatar ? "avatar/" : "pets/") + petId + ".png";
        }

        /// <summary>根目录下固定包含 avatar/ 和 pets/。传入 null 恢复默认，同目录调用可重新读取修改后的文件。</summary>
        public static void SetCustomDirectory(string directory)
        {
            string fullPath = string.IsNullOrWhiteSpace(directory) ? null : Path.GetFullPath(directory);
            if (fullPath != null && !Directory.Exists(fullPath))
                throw new DirectoryNotFoundException("自定义精灵资源目录不存在：" + fullPath);
            var oldSprites = new List<Sprite>(customSprites.Values);
            customSprites.Clear();
            CustomDirectory = fullPath;
            // 先让所有订阅者替换图片，再释放旧贴图，避免当前显示引用被提前销毁。
            try { ResourcesChanged?.Invoke(); }
            finally
            {
                foreach (var sprite in oldSprites)
                {
                    Release(sprite.texture);
                    Release(sprite);
                }
            }
        }

        private static Sprite Get(string petId, bool avatar)
        {
            string relativePath = GetRelativePath(petId, avatar);
            if (CustomDirectory != null)
            {
                if (customSprites.TryGetValue(relativePath, out var cached)) return cached;
                string path = Path.Combine(CustomDirectory, relativePath);
                if (File.Exists(path))
                {
                    Sprite loaded = LoadCustomSprite(path);
                    if (loaded != null)
                    {
                        customSprites.Add(relativePath, loaded);
                        return loaded;
                    }
                }
            }
#if UNITY_EDITOR
            // 编辑模式也能按 ID 预览，且不依赖索引生成的先后顺序。
            if (!Application.isPlaying)
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(PetArtCatalog.AssetRoot + "/" + relativePath);
#endif
            if (catalog == null) catalog = Resources.Load<PetArtCatalog>(PetArtCatalog.ResourceName);
            return catalog != null ? catalog.Get(petId, avatar) : null;
        }

        private static Sprite LoadCustomSprite(string path)
        {
            Texture2D texture = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, true)) throw new InvalidDataException("无法解码 PNG 图片。");
                texture.wrapMode = TextureWrapMode.Clamp;
                // 与默认导入素材一致，切换资源目录不改变物体缩放。
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException
                || error is UnauthorizedAccessException || error is UnityException)
            {
                if (texture != null) Release(texture);
                Debug.LogWarning($"读取精灵图片失败，使用默认素材：{path}\n{error.Message}");
                return null;
            }
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // 关闭 Domain Reload 时也不能沿用上一场运行的事件和 Unity 对象。
            foreach (var sprite in customSprites.Values)
                if (sprite != null) { Release(sprite.texture); Release(sprite); }
            customSprites.Clear();
            catalog = null;
            CustomDirectory = null;
            ResourcesChanged = null;
        }
    }
}
