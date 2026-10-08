using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ReSeer.Battle.UI;
using ReSeer.Pets;
using ReSeer.Skills;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>在独立预览场景验证数据源到普通/第五技能 UI 的更新，不改动当前战斗场景。</summary>
public static class VerifySkillPpUpdates
{
    private const string ReportPath = "Logs/skill-pp-updates-verification.txt";

    [MenuItem("Tools/Re-Seer/验证普通和第五技能 PP 更新")]
    public static void Verify()
    {
        var report = new List<string>();
        var scene = EditorSceneManager.NewPreviewScene();
        var preview = new PreviewRenderUtility();
        try
        {
            var database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Game Data/SkillDatabase.asset");
            string originalDatabase = JsonUtility.ToJson(database);
            var panelRoot = InstantiatePrefab("Assets/Prefabs/BattleUI/MainActionPanel.prefab", scene);
            panelRoot.transform.position = Vector3.zero;
            var panel = panelRoot.GetComponent<MainActionPanel>();
            var container = panelRoot.transform.Find("SkillContainer");
            var fifth = panelRoot.GetComponentInChildren<FifthSkillButton>(true);
            var host = new GameObject("PP verification");
            SceneManager.MoveGameObjectToScene(host, scene);
            var controller = host.AddComponent<BattleUIController>();
            var actions = host.AddComponent<BattleActionController>();
            var source = host.AddComponent<BattleUiTestDataSource>();
            SetField(actions, "actionPanel", panel);
            SetField(controller, "actionController", actions);
            SetField(controller, "selfHpBar", InstantiatePrefab("Assets/Prefabs/BattleUI/HpBar.prefab", scene).GetComponent<HpBar>());
            SetField(controller, "enemyHpBar", InstantiatePrefab("Assets/Prefabs/BattleUI/EnemyHpBar.prefab", scene).GetComponent<HpBar>());
            SetReference(source, "controller", controller);
            SetReference(source, "skillDatabase", database);
            source.StartSession();
            preview.AddSingleGO(panelRoot);

            var buttons = new List<SkillButton>();
            for (int i = 0; i < container.childCount; i++)
            {
                var button = container.GetChild(i).GetComponent<SkillButton>();
                buttons.Add(button);
                Check(button.CurrentPP == database.skills[i].maxPp && button.MaxPP == database.skills[i].maxPp,
                    $"普通技能 {i} 按自己的最大 PP 初始化", report);
            }
            Check(buttons.Count == 4 && fifth.CurrentPP == 25 && Label(fifth) == "25/25", "四个普通技能与第五技能初始化", report);
            Check(ElementIcon(fifth).sprite == database.skills[3].icon, "第五技能使用技能资料中的属性图标", report);
            Capture(panelRoot, preview, "full");

            source.DrainSkillPp(0);
            Check(buttons[0].CurrentPP == 9 && Label(buttons[0]) == "PP：9/10", "第一次扣 PP 立即显示 10/10 → 9/10", report);
            Check(buttons[1].CurrentPP == 15 && fifth.CurrentPP == 25, "扣单个技能不影响其他技能", report);
            for (int i = 0; i < buttons.Count; i++)
                Check(container.GetChild(i).GetComponent<SkillButton>() == buttons[i], $"PP 更新保留按钮实例 {i}", report);
            source.DrainFifthSkillPp();
            Check(fifth.CurrentPP == 24 && fifth.MaxPP == 25 && Label(fifth) == "24/25", "第五技能独立扣 PP、上限保持不变", report);
            Check(buttons[3].CurrentPP == 25, "共用技能定义的普通槽与第五槽不共用剩余 PP", report);
            Capture(panelRoot, preview, "decreased");

            int clicks = 0;
            fifth.SkillSelected += _ => clicks++;
            fifth.OnPointerClick(new PointerEventData(null));
            Check(clicks == 1 && fifth.CurrentPP == 24, "第五技能可用时提交意图，点击本身不扣 PP", report);
            source.ExhaustFifthSkillPp();
            fifth.OnPointerClick(new PointerEventData(null));
            Check(!fifth.CanUse && Label(fifth) == "0/25" && clicks == 1, "第五技能耗尽时更新数字并阻止提交", report);
            Check(ElementIcon(fifth).color.r < 1f, "第五技能 PP 耗尽时属性图标同步变暗", report);
            Capture(panelRoot, preview, "exhausted");
            for (int i = 0; i < 9; i++) source.DrainSkillPp(0);
            Check(buttons[0].CurrentPP == 0 && Label(buttons[0]) == "PP：0/10" && !buttons[0].CanUse,
                "普通技能逐次扣至零，更新数字并禁用", report);
            source.RestoreSkillPp();
            Check(fifth.CanUse && Label(fifth) == "25/25" && buttons[0].CurrentPP == 10, "普通与第五技能恢复满 PP", report);

            var slots = new List<BattleUiSkillSlot>(source.DataState.Skills);
            slots[0] = new BattleUiSkillSlot(slots[0].SkillId, 10, 10, false);
            source.DataState.SetSkills(slots, new BattleUiSkillSlot("demo_spark", 25, 25, false));
            Check(!buttons[0].CanUse && !fifth.CanUse, "快照中的禁用状态传递到普通和第五技能", report);
            source.DataState.SetSkills(slots, new BattleUiSkillSlot("demo_break", 7, 15, true));
            Check(fifth.SkillId == "demo_break" && fifth.MaxPP == 15 && Label(fifth) == "7/15" && fifth.Power == 90,
                "更换第五技能同步名称、威力与 PP", report);
            Check(fifth.SkillName == database.skills[1].fallbackName, "第五技能名称与新定义一致", report);
            Check(ElementIcon(fifth).sprite == database.skills[1].icon && ElementIcon(fifth).color == Color.white,
                "更换第五技能更新属性图标并恢复正常颜色", report);
            source.DataState.SetSkills(slots);
            Check(!fifth.gameObject.activeSelf, "无第五技能时隐藏按钮", report);
            source.PushSkills();
            Check(fifth.gameObject.activeSelf && fifth.CanUse && Label(fifth) == "25/25", "再次同步恢复第五技能及可用状态", report);
            source.ToggleSelfActivePet();
            Check(container.childCount == 0 && !fifth.gameObject.activeSelf, "出战空态清空全部技能 UI", report);
            source.ToggleSelfActivePet();
            Check(container.childCount == 4 && fifth.gameObject.activeSelf, "切回出战恢复全部技能 UI", report);
            source.DrainFifthSkillPp();
            source.StartSession();
            Check(fifth.CurrentPP == 25, "重建会话重置技能 PP", report);
            Check(originalDatabase == JsonUtility.ToJson(database), "所有测试保持技能数据库不变", report);
            var standalone = InstantiatePrefab("Assets/Prefabs/BattleUI/Fifth Skill Button.prefab", scene).GetComponent<FifthSkillButton>();
            standalone.Bind(database.skills[0], 10);
            Check(ElementIcon(standalone).sprite == database.skills[0].icon, "独立第五技能预制体也绑定属性图标", report);
            standalone.Bind(database.skills[1], 15);
            Check(ElementIcon(standalone).sprite == database.skills[1].icon, "独立第五技能切换属性图标", report);
            report.Add("PASS");
            Debug.Log("普通与第五技能 PP 更新验证通过。");
        }
        catch (Exception exception)
        {
            report.Add("FAIL: " + exception);
            throw;
        }
        finally
        {
            preview.Cleanup();
            EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines(ReportPath, report);
        }
    }

    public static void VerifyBatch()
    {
        Verify();
        VerifyButtonClickEvents.Verify();
        if (!File.ReadAllText("Logs/button-click-events-verification.txt").TrimEnd().EndsWith("PASS"))
            throw new InvalidOperationException("既有按钮点击事件回归验证失败。");
    }

    private static GameObject InstantiatePrefab(string path, Scene scene) =>
        (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var settings = new SerializedObject(target);
        settings.FindProperty(name).objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string Label(SkillButton button) =>
        ((TMP_Text)new SerializedObject(button).FindProperty("ppText").objectReferenceValue).text;

    private static SpriteRenderer ElementIcon(SkillButton button) =>
        (SpriteRenderer)new SerializedObject(button).FindProperty("elementIcon").objectReferenceValue;

    private static void Check(bool condition, string label, List<string> report)
    {
        if (!condition) throw new InvalidOperationException(label);
        report.Add("OK: " + label);
    }

    private static void Capture(GameObject panel, PreviewRenderUtility preview, string phase)
    {
        // 面板交给 PreviewRenderUtility 管理，血条仍留在独立测试场景。
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        SortingGroup.UpdateAllSortingGroups();
        preview.camera.orthographic = true;
        preview.camera.orthographicSize = 4.5f;
        preview.camera.transform.position = new Vector3(1.1f, -0.6f, -10f);
        preview.camera.transform.rotation = Quaternion.identity;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.08f, 0.13f, 0.2f);
        preview.BeginStaticPreview(new Rect(0, 0, 1600, 450));
        preview.Render();
        var image = preview.EndStaticPreview();
        Directory.CreateDirectory("docs/screenshots/pp-updates");
        File.WriteAllBytes($"docs/screenshots/pp-updates/{phase}.png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }
}
