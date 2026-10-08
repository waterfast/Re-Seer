using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReSeer.Battle.Art;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>移动或新增属性图后自动更新引用索引，构建前检查图片导入及清单完整性。</summary>
[InitializeOnLoad]
public sealed class TypeIconCatalogBuilder : AssetPostprocessor, IPreprocessBuildWithReport
{
    private const string CatalogPath = "Assets/Resources/TypeIconCatalog.asset";
    public int callbackOrder => 0;

    [Serializable] private sealed class TypeList { public TypeEntry[] assets; }
    [Serializable] private sealed class TypeEntry { public string typeId; }

    static TypeIconCatalogBuilder() => ScheduleRebuild();

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom)
            .Any(path => path.StartsWith(TypeIconCatalog.AssetRoot + "/", StringComparison.Ordinal)))
            ScheduleRebuild();
    }

    private static void ScheduleRebuild()
    {
        EditorApplication.delayCall -= Rebuild;
        EditorApplication.delayCall += Rebuild;
    }

    public void OnPreprocessBuild(BuildReport report) => Rebuild();

    [MenuItem("Tools/Re-Seer/重建属性图标索引")]
    public static void Rebuild()
    {
        var types = AssetDatabase.LoadAssetAtPath<TextAsset>(TypeIconCatalog.AssetRoot + "/catalog.json");
        var pets = AssetDatabase.LoadAssetAtPath<TextAsset>(TypeIconCatalog.AssetRoot + "/PetTypes.json");
        // 初次导入时清单可能尚未就绪；构建前则必须完整。
        if (types == null || pets == null)
        {
            if (BuildPipeline.isBuildingPlayer) throw new BuildFailedException("Art/UI/Types 缺少属性清单。");
            return;
        }
        var entries = new List<TypeIconCatalog.Entry>();
        foreach (TypeEntry type in JsonUtility.FromJson<TypeList>(types.text).assets)
        {
            string path = TypeIconCatalog.AssetRoot + "/" + type.typeId + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new BuildFailedException("属性图标必须导入为 Sprite：" + path);
            entries.Add(new TypeIconCatalog.Entry { typeId = type.typeId, sprite = sprite });
        }
        var catalog = AssetDatabase.LoadAssetAtPath<TypeIconCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<TypeIconCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.ReplaceEntries(types, pets, entries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }
}
