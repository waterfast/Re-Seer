using System;
using System.IO;
using ReSeer.Pets;
using ReSeer.Skills;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>配置现有面板资源；预览和验证均在独立预览场景中进行。</summary>
public static class MainActionPanelSetup
{
    private const string PrefabPath = "Assets/Prefabs/BattleUI/MainActionPanel.prefab";

    [MenuItem("Tools/Re-Seer/配置动态技能槽并验证")]
    public static void ConfigureAndVerify()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath && stage.scene.isDirty)
            throw new InvalidOperationException("MainActionPanel 预制体有未保存编辑，请先保存再运行配置菜单。");

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var container = root.transform.Find("SkillContainer");
            if (container == null)
            {
                container = new GameObject("SkillContainer").transform;
                container.SetParent(root.transform, false);
            }
            container.localPosition = new Vector3(-6.4f, -0.7f, 0f);
            if (container.GetComponent<SkillSlotLayout>() == null) container.gameObject.AddComponent<SkillSlotLayout>();
            var group = container.GetComponent<SortingGroup>();
            if (group == null) group = container.gameObject.AddComponent<SortingGroup>();
            group.sortingLayerName = "L2";
            group.sortingOrder = 0;

            var skillPanel = root.GetComponent<SkillPanel>();
            if (skillPanel == null) skillPanel = root.AddComponent<SkillPanel>();
            var panelSettings = new SerializedObject(skillPanel);
            panelSettings.FindProperty("skillContainer").objectReferenceValue = container;
            panelSettings.FindProperty("skillButtonPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleUI/SkillButton.prefab").GetComponent<SkillButton>();
            panelSettings.ApplyModifiedPropertiesWithoutUndo();

            var main = root.GetComponent<MainActionPanel>();
            if (main == null) main = root.AddComponent<MainActionPanel>();
            var mainSettings = new SerializedObject(main);
            mainSettings.FindProperty("skillPanel").objectReferenceValue = skillPanel;
            mainSettings.FindProperty("fifthSkillButton").objectReferenceValue = root.GetComponentInChildren<FifthSkillButton>(true);
            mainSettings.FindProperty("skillDatabase").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Game Data/SkillDatabase.asset");
            mainSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        VerifyAndRender();
        Debug.Log("MainActionPanel：动态技能槽配置与渲染验证完成。");
    }

    private static void VerifyAndRender()
    {
        var preview = new PreviewRenderUtility();
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        preview.AddSingleGO(root);
        try
        {
            root.transform.position = Vector3.zero;
            var main = root.GetComponent<MainActionPanel>();
            var container = root.transform.Find("SkillContainer");
            Require(container.childCount == 0, "预制体不预放固定技能槽");
            // 独立编辑器预览不运行 MonoBehaviour 生命周期，显式模拟启用和 Start。
            main.SendMessage("OnEnable");
            main.SendMessage("Start");
            Require(container.childCount == 0, "默认不自动加载测试数据");
            main.Test();
            Require(container.childCount == 3, "Test 动态生成三个技能槽");
            main.x = -5f;
            main.y = -1f;
            main.spacing = 5.2f;
            main.ApplySkillLayout();
            Require(container.localPosition == new Vector3(-5f, -1f, 0f) &&
                Mathf.Approximately(container.GetChild(1).localPosition.x, 5.2f), "公开坐标和间距立即生效");
            main.x = -6.4f;
            main.y = -0.7f;
            main.spacing = 4.8f;
            main.ApplySkillLayout();
            var database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Game Data/SkillDatabase.asset");
            string before = JsonUtility.ToJson(database);
            for (int count = 0; count <= 4; count++)
            {
                var skills = database.skills.GetRange(0, count);
                var pp = new int[count];
                for (int i = 0; i < count; i++) pp[i] = i + 2;
                main.ShowSkills(skills, pp);
                Require(container.childCount == count, $"刷新后只有 {count} 个技能槽");
                for (int i = 0; i < count; i++)
                {
                    var button = container.GetChild(i).GetComponent<SkillButton>();
                    Require(button.BoundSkill == skills[i] && button.CurrentPP == pp[i], "顺序和剩余 PP 与输入一致");
                    Require(Mathf.Approximately(button.transform.localPosition.x, i * 4.8f), "槽位依次排列");
                }
            }
            int selected = 0;
            main.SkillSelected += _ => selected++;
            var first = container.GetChild(0).GetComponent<SkillButton>();
            int remaining = first.CurrentPP;
            first.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Require(selected == 1 && first.CurrentPP == remaining, "选择向上转发且点击不扣 PP");
            first.SetCurrentPP(0);
            first.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Require(selected == 1, "耗尽 PP 不上报选择");
            main.ShowSkills(new[] { database.skills[1] }, new[] { 3 });
            main.SendMessage("Start");
            Require(container.childCount == 1 && container.GetChild(0).GetComponent<SkillButton>().CurrentPP == 3,
                "已绑定战斗数据时 Start 不覆盖技能和 PP");
            Require(before == JsonUtility.ToJson(database), "技能资料未被修改");
            main.LoadDatabasePreview();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            SortingGroup.UpdateAllSortingGroups();
            SavePreview(preview);
            main.SendMessage("OnDisable");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/main-action-panel-verification.txt",
                "PASS: Test renders 3 catalog skills; no automatic preview; editable X/Y/Spacing; refresh counts 0/1/2/3/4; ordering and supplied PP; selection forwards without consuming PP; exhausted PP rejects selection; Start preserves previously bound battle data; catalog unchanged.\n" + DateTime.UtcNow.ToString("O"));
        }
        finally { preview.Cleanup(); }
    }

    private static void SavePreview(PreviewRenderUtility preview)
    {
        preview.camera.orthographic = true;
        preview.camera.orthographicSize = 4.5f;
        preview.camera.transform.position = new Vector3(1.1f, -0.6f, -10f);
        preview.camera.transform.rotation = Quaternion.identity;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.08f, 0.13f, 0.2f);
        preview.BeginStaticPreview(new Rect(0, 0, 1600, 450));
        preview.Render();
        var image = preview.EndStaticPreview();
        Directory.CreateDirectory("docs/screenshots");
        File.WriteAllBytes("docs/screenshots/main-action-panel-skills.png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("动态技能槽验证失败：" + label);
    }
}
