using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>仅在当前 Battle 中组合用户的 Prefab，不生成技能或接入战斗信号。</summary>
[InitializeOnLoad]
public static class BattleSceneComposer
{
    private const string BattlePath = "Assets/Scenes/Battle.unity";
    private const string PanelPath = "Assets/Prefabs/BattleUI/MainActionPanel.prefab";
    private const string RequestPath = "Temp/battle-current-layout.request";

    static BattleSceneComposer()
    {
        if (File.Exists(RequestPath)) EditorApplication.update += ApplyRequestedLayout;
    }

    private static void ApplyRequestedLayout()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= ApplyRequestedLayout;
        File.Delete(RequestPath);
        try
        {
            ApplyToBattle();
            File.WriteAllText("Temp/battle-current-layout.result", "OK: Battle uses authored MainActionPanel prefab only");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/battle-current-layout.result", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/ReSeer/使用现有 Prefab 整理 Battle")]
    public static void ApplyToBattle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请在编辑模式整理 Battle。");
        var scene = SceneManager.GetSceneByPath(BattlePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("请打开当前 Battle 场景。");
            scene = EditorSceneManager.OpenScene(BattlePath, OpenSceneMode.Single);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
        if (prefab == null) throw new InvalidOperationException("找不到 MainActionPanel Prefab。");
        SceneManager.SetActiveScene(scene);
        Transform hud = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "BattleHUD") hud = root.transform;
        if (hud == null) throw new InvalidOperationException("Battle 中缺少 BattleHUD。");

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("组合现有战斗 Prefab");
        // 只替换之前装配的面板实例。新的第五技能、按钮与空技能容器均来自用户 Prefab。
        var previous = hud.Find("MainActionPanel");
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hud);
        panel.name = "MainActionPanel";
        Undo.RegisterCreatedObjectUndo(panel, "装配用户 MainActionPanel");
        panel.transform.localPosition = Vector3.zero;
        panel.transform.localScale = Vector3.one;
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        // 外层排序组统一面板层级；内部图片和文字的绑定、排序及比例沿用 Prefab。
        var sorting = panel.GetComponent<SortingGroup>();
        if (sorting == null) sorting = Undo.AddComponent<SortingGroup>(panel);
        sorting.sortingLayerID = 0;
        sorting.sortingOrder = 100;
        FitPanel(panel.transform, 30.8f, new Vector2(0f, -6.3f));
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        // 这些都是上一版由装配工具额外创建的对象，不属于用户的基础 Prefab。
        foreach (string name in new[] { "RoundPlate", "Round", "ClockPlate", "Countdown", "Prompt", "ReserveHeads" })
        {
            var decoration = hud.Find(name);
            if (decoration != null) Undo.DestroyObjectImmediate(decoration.gameObject);
        }
        Undo.CollapseUndoOperations(undoGroup);
        SortingGroup.UpdateAllSortingGroups();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Capture(scene);
        Selection.activeGameObject = panel;
    }

    private static void FitPanel(Transform panel, float width, Vector2 center)
    {
        var images = panel.GetComponentsInChildren<SpriteRenderer>();
        if (images.Length == 0) throw new InvalidOperationException("面板没有图片组件。");
        Bounds bounds = images[0].bounds;
        foreach (var image in images) bounds.Encapsulate(image.bounds);
        // 完整容纳较高的第五技能框，不改变 Prefab 内部各组件的相对尺寸。
        float scale = Mathf.Min(width / bounds.size.x, 4.8f / bounds.size.y);
        Vector3 offset = bounds.center - panel.position;
        panel.localScale *= scale;
        panel.localPosition = (Vector3)center - offset * scale;
    }

    private static void Capture(Scene scene)
    {
        Camera camera = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var candidate = root.GetComponent<Camera>();
            if (candidate != null && candidate.CompareTag("MainCamera")) camera = candidate;
        }
        if (camera == null) return;
        var target = RenderTexture.GetTemporary(1920, 1080, 24);
        var previous = RenderTexture.active;
        var oldTarget = camera.targetTexture;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            Directory.CreateDirectory("docs/screenshots");
            File.WriteAllBytes("docs/screenshots/battle-layout.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
