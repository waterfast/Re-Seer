using System;
using System.IO;
using ReSeer.Battle.UI;
using ReSeer.Pets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>给现有场景和预制体绑定 UI 点击事件，不改变按钮资料或布局。</summary>
public static class BattleButtonEventSetup
{
    public const string SkillEventPath = "Assets/Game Data/Events/UI/ClickSkill.asset";
    public const string MenuEventPath = "Assets/Game Data/Events/UI/ClickBattleMenu.asset";

    [MenuItem("Tools/Re-Seer/绑定按钮点击事件")]
    public static void BindButtonEvents()
    {
        var skillEvent = AssetDatabase.LoadAssetAtPath<SkillClickEventSO>(SkillEventPath);
        var menuEvent = AssetDatabase.LoadAssetAtPath<BattleMenuActionEventSO>(MenuEventPath);
        if (skillEvent == null || menuEvent == null)
            throw new InvalidOperationException("找不到 Game Data/Events/UI 中的技能或菜单点击事件。");

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            bool isOpenStage = stage != null && stage.assetPath == path;
            GameObject root = isOpenStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!BindHierarchy(root, skillEvent, menuEvent)) continue;
                if (isOpenStage) EditorSceneManager.MarkSceneDirty(stage.scene);
                else PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { if (!isOpenStage) PrefabUtility.UnloadPrefabContents(root); }
        }

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects()) changed |= BindHierarchy(root, skillEvent, menuEvent);
            if (!changed) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path) && File.Exists(scene.path)) EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("普通技能、第五技能和菜单按钮已绑定点击事件。");
    }

    private static bool BindHierarchy(GameObject root, SkillClickEventSO skillEvent, BattleMenuActionEventSO menuEvent)
    {
        bool changed = false;
        foreach (var button in root.GetComponentsInChildren<SkillButton>(true))
        {
            var serialized = new SerializedObject(button);
            var field = serialized.FindProperty("skillClickEvent");
            if (field.objectReferenceValue == skillEvent) continue;
            field.objectReferenceValue = skillEvent;
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            changed = true;
        }
        foreach (var button in root.GetComponentsInChildren<ActionMenuButton>(true))
        {
            if (!Enum.TryParse(button.ActionId, true, out BattleMenuAction action))
                throw new InvalidOperationException("未知菜单操作：" + button.ActionId);
            var serialized = new SerializedObject(button);
            var field = serialized.FindProperty("menuClickEvent");
            var actionField = serialized.FindProperty("menuAction");
            if (field.objectReferenceValue == menuEvent && actionField.intValue == (int)action) continue;
            field.objectReferenceValue = menuEvent;
            actionField.intValue = (int)action;
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            changed = true;
        }
        return changed;
    }
}
