using System;
using System.Collections.Generic;
using System.IO;
using ReSeer.Battle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class ActionMenuButtonSetup
{
    private const string ArtPath = "Assets/Art/Battle/Flash/ActionControls/";
    private const string ReportPath = "Temp/ActionMenuButtonSetup.txt";
    private static readonly string[] Prefixes = { "capture", "item", "fight", "pet", "retreat", "surrender" };

    [Serializable] private sealed class SourceManifest { public SourceAsset[] assets; }
    [Serializable] private sealed class SourceAsset { public string file; public int[] trimBox; public int[] size; }

    [MenuItem("Tools/ReSeer/配置菜单按钮点击特效")]
    public static void SetupLoadedMenus()
    {
        AlignStateSprites();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ArtPath + "ActionMenuGlow.shader");
        if (shader == null) throw new InvalidOperationException("菜单发光 Shader 未导入。");
        var material = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "ActionMenuGlow.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ArtPath + "ActionMenuGlow.mat");
        }

        var report = new List<string>();
        var changedScenes = new HashSet<Scene>();
        // 只处理原菜单素材对应的场景对象，不改变其他按钮或底板。
        foreach (var renderer in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>(true))
        {
            if (renderer.sprite == null || EditorUtility.IsPersistent(renderer)) continue;
            string path = AssetDatabase.GetAssetPath(renderer.sprite);
            foreach (string prefix in Prefixes)
            {
                if (path != ArtPath + prefix + "-up.png") continue;
                ConfigureButton(renderer, prefix, material);
                changedScenes.Add(renderer.gameObject.scene);
                report.Add(prefix + ": " + renderer.name + " @ " + renderer.transform.position);
                break;
            }
        }

        foreach (Scene scene in changedScenes)
        {
            EnsureInput(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            // 保留当前未保存的编辑；已有场景保存完整的当前状态。
            if (!string.IsNullOrEmpty(scene.path) && File.Exists(scene.path))
                EditorSceneManager.SaveScene(scene);
        }
        const string menuPrefabPath = "Assets/Prefabs/BattleUI/SwitchMenu.prefab";
        if (File.Exists(menuPrefabPath))
        {
            GameObject menu = PrefabUtility.LoadPrefabContents(menuPrefabPath);
            try
            {
                foreach (var renderer in menu.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.sprite == null) continue;
                    string path = AssetDatabase.GetAssetPath(renderer.sprite);
                    foreach (string prefix in Prefixes)
                    {
                        if (path != ArtPath + prefix + "-up.png") continue;
                        ConfigureButton(renderer, prefix, material);
                        break;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(menu, menuPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(menu); }
        }
        AssetDatabase.SaveAssets();
        report.Add("Configured buttons: " + (report.Count));
        File.WriteAllLines(ReportPath, report);
        Debug.Log("菜单按钮特效已配置：" + string.Join("\n", report));
    }

    private static void AlignStateSprites()
    {
        var manifest = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(ArtPath + "sources.json"));
        foreach (string prefix in Prefixes)
        {
            SourceAsset normal = Array.Find(manifest.assets, asset => asset.file == ArtPath + prefix + "-up.png");
            if (normal == null) continue;
            // 裁切尺寸不同，用普通状态的注册点对齐，切换图片时不移动碰撞区域。
            float centerX = (normal.trimBox[0] + normal.trimBox[2]) * 0.5f;
            float centerY = (normal.trimBox[1] + normal.trimBox[3]) * 0.5f;
            foreach (string state in new[] { "over", "down" })
            {
                SourceAsset asset = Array.Find(manifest.assets, item => item.file == ArtPath + prefix + "-" + state + ".png");
                if (asset == null) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(asset.file);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2((centerX - asset.trimBox[0]) / asset.size[0],
                    (asset.trimBox[3] - centerY) / asset.size[1]);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }

    private static void ConfigureButton(SpriteRenderer renderer, string prefix, Material material)
    {
        var button = renderer.GetComponent<ActionMenuButton>();
        if (button == null) button = Undo.AddComponent<ActionMenuButton>(renderer.gameObject);
        var serialized = new SerializedObject(button);
        serialized.FindProperty("actionId").stringValue = prefix;
        serialized.FindProperty("menuAction").intValue = (int)Enum.Parse(typeof(BattleMenuAction), prefix, true);
        serialized.FindProperty("normalSprite").objectReferenceValue = renderer.sprite;
        serialized.FindProperty("hoverSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + prefix + "-over.png");
        serialized.FindProperty("pressedSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + prefix + "-down.png");
        serialized.FindProperty("glowMaterial").objectReferenceValue = material;
        serialized.ApplyModifiedProperties();

        var polygon = renderer.GetComponent<PolygonCollider2D>();
        Undo.RecordObject(polygon, "配置菜单按钮点击范围");
        // 采用普通按钮轮廓；悬停、按下和发光均不会改变命中形状。
        int count = renderer.sprite.GetPhysicsShapeCount();
        if (count > 0)
        {
            polygon.pathCount = count;
            var points = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                renderer.sprite.GetPhysicsShape(i, points);
                polygon.SetPath(i, points);
            }
        }
        var box = renderer.GetComponent<BoxCollider2D>();
        if (box != null) Undo.DestroyObjectImmediate(box);
        PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        PrefabUtility.RecordPrefabInstancePropertyModifications(polygon);
    }

    private static void EnsureInput(Scene scene)
    {
        bool hasEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>() != null;
        if (!hasEventSystem)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(events, "配置菜单输入");
            SceneManager.MoveGameObjectToScene(events, scene);
        }
        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>())
        {
            if (camera.gameObject.scene != scene || camera.GetComponent<Physics2DRaycaster>() != null) continue;
            Undo.AddComponent<Physics2DRaycaster>(camera.gameObject);
        }
    }
}
