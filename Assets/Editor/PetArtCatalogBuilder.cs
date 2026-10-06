using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReSeer.Battle.Art;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>新增、移动或删除编号图片后重建索引；构建前再次检查，避免编辑器能用、发布包缺图。</summary>
[InitializeOnLoad]
public sealed class PetArtCatalogBuilder : AssetPostprocessor, IPreprocessBuildWithReport
{
    private const string CatalogPath = "Assets/Resources/PetArtCatalog.asset";
    public int callbackOrder => 0;

    static PetArtCatalogBuilder() => ScheduleRebuild();

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(PetArtCatalog.AssetRoot + "/avatar/", StringComparison.Ordinal)
            && !assetPath.StartsWith(PetArtCatalog.AssetRoot + "/pets/", StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom)
            .Any(path => path.StartsWith(PetArtCatalog.AssetRoot + "/", StringComparison.Ordinal)))
            ScheduleRebuild();
    }

    private static void ScheduleRebuild()
    {
        EditorApplication.delayCall -= Rebuild;
        EditorApplication.delayCall += Rebuild;
    }

    public void OnPreprocessBuild(BuildReport report) => Rebuild();

    [MenuItem("Tools/ReSeer/重建精灵图片索引")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(PetArtCatalog.AssetRoot)) return;
        var entries = new SortedDictionary<string, PetArtCatalog.Entry>(StringComparer.Ordinal);
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PetArtCatalog.AssetRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string id = Path.GetFileNameWithoutExtension(path);
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            bool avatar = folder == PetArtCatalog.AssetRoot + "/avatar";
            if (!avatar && folder != PetArtCatalog.AssetRoot + "/pets") continue;
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || id.Length == 0 || id.Any(c => c < '0' || c > '9')) continue;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new BuildFailedException("精灵图片必须导入为单个 Sprite：" + path);
            if (!entries.TryGetValue(id, out var entry))
                entries.Add(id, entry = new PetArtCatalog.Entry { petId = id });
            if (avatar) entry.avatar = sprite;
            else entry.artwork = sprite;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<PetArtCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PetArtCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.ReplaceEntries(entries.Values.ToList());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }
}
