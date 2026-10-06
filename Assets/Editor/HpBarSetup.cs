using System;
using System.IO;
using ReSeer.Battle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>给当前场景的 HpBar 绑定现有子对象和简单显示示例。</summary>
[InitializeOnLoad]
public static class HpBarSetup
{
    private const string RequestPath = "Temp/hpbar-setup.request";
    private const string ResultPath = "Temp/hpbar-setup.result";

    static HpBarSetup()
    {
        if (File.Exists(RequestPath)) EditorApplication.update += ApplyPendingRequest;
    }

    private static void ApplyPendingRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= ApplyPendingRequest;
        File.Delete(RequestPath);
        try
        {
            Configure();
            File.WriteAllText(ResultPath, "Configured current HpBar: 武心婵 / 60 / 200/300 HP");
        }
        catch (Exception error)
        {
            File.WriteAllText(ResultPath, error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/ReSeer/配置当前HpBar武心婵预览")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请在编辑模式配置 HpBar。");

        GameObject target = null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "HpBar") continue;
            if (target != null) throw new InvalidOperationException("当前场景存在多个名为 HpBar 的对象，请保留一个明确的配置目标。");
            target = child.gameObject;
        }
        if (target == null) throw new InvalidOperationException("当前场景未找到 HpBar，请确认它所在的场景处于打开状态。");

        var oldAvatar = target.transform.Find("avator");
        if (oldAvatar != null && target.transform.Find("avatar") == null)
        {
            Undo.RecordObject(oldAvatar.gameObject, "重命名头像对象");
            oldAvatar.gameObject.name = "avatar";
        }

        var portrait = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/Pet/avatar/4500.png");
        if (portrait == null) throw new InvalidOperationException("未找到武心婵头像 4500.png。");

        var hpBar = target.GetComponent<HpBar>();
        if (hpBar == null) hpBar = Undo.AddComponent<HpBar>(target);
        Undo.RecordObject(hpBar, "绑定 HpBar 组件");
        hpBar.ResolveComponents();
        var binding = new SerializedObject(hpBar);
        foreach (var name in new[] { "mainbar", "avatar", "petLevel", "petName" })
            if (binding.FindProperty(name).objectReferenceValue == null)
                throw new InvalidOperationException("HpBar 缺少组件引用：" + name);

        var preview = target.GetComponent<HpBarTest>();
        if (preview == null) preview = Undo.AddComponent<HpBarTest>(target);
        var settings = new SerializedObject(preview);
        settings.FindProperty("petName").stringValue = "武心婵";
        settings.FindProperty("level").intValue = 60;
        settings.FindProperty("petId").stringValue = "4500";
        settings.FindProperty("currentHp").intValue = 200;
        settings.FindProperty("maxHp").intValue = 300;
        settings.ApplyModifiedProperties();

        EnsureHealthText(hpBar, binding);

        Undo.RecordObject(binding.FindProperty("avatar").objectReferenceValue, "显示武心婵头像");
        Undo.RecordObject(binding.FindProperty("petLevel").objectReferenceValue, "显示精灵等级");
        Undo.RecordObject(binding.FindProperty("petName").objectReferenceValue, "显示精灵名字");
        Undo.RecordObject(binding.FindProperty("hpFill").objectReferenceValue, "显示体力填充");
        Undo.RecordObject(binding.FindProperty("hpText").objectReferenceValue, "显示体力数值");
        preview.ApplyPreview();
        EditorUtility.SetDirty(hpBar);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.scene);
        Selection.activeGameObject = target;
        // 用户的当前层级可能尚未保存，装配仅标记场景修改，不自动写盘。
    }

    private static void EnsureHealthText(HpBar hpBar, SerializedObject binding)
    {
        var fill = binding.FindProperty("hpFill").objectReferenceValue as SpriteRenderer;
        if (fill == null) throw new InvalidOperationException("请在 HpBar 下放置名为 hp 的满血填充图片，或手动指定 Hp Fill。");
        if (binding.FindProperty("hpText").objectReferenceValue != null) return;

        var level = binding.FindProperty("petLevel").objectReferenceValue as TMPro.TMP_Text;
        // 复用当前文字的字体与材质，避免引入另一套中文字体设置。
        var root = UnityEngine.Object.Instantiate(level.gameObject, hpBar.transform);
        root.name = "hp_text";
        Undo.RegisterCreatedObjectUndo(root, "添加体力数字");
        var text = root.GetComponent<TMPro.TMP_Text>();
        root.transform.position = fill.bounds.center;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.text = "200/300";
        text.GetComponent<Renderer>().sortingLayerID = fill.sortingLayerID;
        text.GetComponent<Renderer>().sortingOrder = fill.sortingOrder + 1;
        hpBar.ResolveComponents();
        binding.Update();
    }
}
