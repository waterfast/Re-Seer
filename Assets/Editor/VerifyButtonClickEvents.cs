using System;
using System.Collections.Generic;
using System.IO;
using ReSeer.Battle.UI;
using ReSeer.Pets;
using ReSeer.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class VerifyButtonClickEvents
{
    [MenuItem("Tools/Re-Seer/验证按钮点击事件")]
    public static void Verify()
    {
        var report = new List<string>();
        var scene = EditorSceneManager.NewPreviewScene();
        var skillEvent = ScriptableObject.CreateInstance<SkillClickEventSO>();
        var menuEvent = ScriptableObject.CreateInstance<BattleMenuActionEventSO>();
        try
        {
            var database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Game Data/SkillDatabase.asset");
            SkillData original = database.skills[0];
            SkillData next = database.skills[1];
            int originalMaxPP = original.maxPp;
            foreach (string path in new[] { "Assets/Prefabs/BattleUI/SkillButton.prefab", "Assets/Prefabs/BattleUI/Fifth Skill Button.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var button = root.GetComponent<SkillButton>();
                var serialized = new SerializedObject(button);
                Check(serialized.FindProperty("skillClickEvent").objectReferenceValue == AssetDatabase.LoadAssetAtPath<SkillClickEventSO>(BattleButtonEventSetup.SkillEventPath), "预制体事件已绑定", report);
                serialized.FindProperty("skillClickEvent").objectReferenceValue = skillEvent;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                button.Bind(original, original.maxPp);
                int received = 0;
                SkillData payload = null;
                UnityEngine.Events.UnityAction<SkillData> handler = value =>
                {
                    received++;
                    payload = value;
                    Check(ReferenceEquals(skillEvent.LastSkill, value), "先记录技能、再通知监听", report);
                    Check(skillEvent.lastSender == button.ToString(), "发送者为点击按钮", report);
                };
                skillEvent.OnEventRaised += handler;
                SkillData legacyPayload = null;
                button.SkillSelected += value => legacyPayload = value;
                // 回调重绑技能不能改变本次点击携带的数据。
                button.OnClick.AddListener(() => button.Bind(next, next.maxPp));
                var pointer = new PointerEventData(null);
                button.OnPointerClick(pointer);
                Check(received == 1 && ReferenceEquals(payload, original), button.GetType().Name + " 点击只广播一次完整技能资料", report);
                Check(ReferenceEquals(legacyPayload, original), "其他点击回调重绑后仍传递原技能", report);
                pointer.button = PointerEventData.InputButton.Right;
                button.OnPointerClick(pointer);
                Check(received == 1, "右键不广播", report);
                pointer.button = PointerEventData.InputButton.Left;
                button.SetInteractable(false);
                button.OnPointerClick(pointer);
                Check(received == 1, "禁用不广播", report);
                button.SetInteractable(true);
                button.SetCurrentPP(0);
                button.OnPointerClick(pointer);
                Check(received == 1, "PP 耗尽不广播", report);
                button.SetCurrentPP(next.maxPp);
                button.OnClick.RemoveAllListeners();
                button.SetSkill("仅显示预览", 1, 1, 1, null);
                Check(button.BoundSkill == null, "显示预览不会保留上一次绑定的技能", report);
                skillEvent.OnEventRaised -= handler;
                UnityEngine.Object.DestroyImmediate(root);
            }
            Check(original.maxPp == originalMaxPP, "点击不修改数据库技能资料", report);
            skillEvent.RaiseEvent(next, null);
            Check(ReferenceEquals(skillEvent.LastSkill, next) && skillEvent.lastSender == string.Empty, "空发送者及最近技能记录", report);

            var menu = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleUI/SwitchMenu.prefab"), scene);
            int menuCount = 0;
            BattleMenuAction lastAction = BattleMenuAction.Fight;
            menuEvent.OnEventRaised += action => { menuCount++; lastAction = action; };
            foreach (var button in menu.GetComponentsInChildren<ActionMenuButton>())
            {
                var serialized = new SerializedObject(button);
                Check(serialized.FindProperty("menuClickEvent").objectReferenceValue == AssetDatabase.LoadAssetAtPath<BattleMenuActionEventSO>(BattleButtonEventSetup.MenuEventPath), "菜单预制体事件已绑定", report);
                serialized.FindProperty("menuClickEvent").objectReferenceValue = menuEvent;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                int before = menuCount;
                var pointer = new PointerEventData(null);
                button.OnPointerClick(pointer);
                Check(menuCount == before + 1 && lastAction == Enum.Parse<BattleMenuAction>(button.ActionId, true), button.ActionId + " 菜单事件与操作匹配", report);
                pointer.button = PointerEventData.InputButton.Right;
                button.OnPointerClick(pointer);
                Check(menuCount == before + 1, "菜单右键不广播", report);
                button.enabled = false;
                pointer.button = PointerEventData.InputButton.Left;
                button.OnPointerClick(pointer);
                Check(menuCount == before + 1, "菜单停用不广播", report);
            }
            Check(menuCount == 5, "五个菜单按钮全部验证", report);
            report.Add("PASS");
        }
        catch (Exception exception)
        {
            report.Add("FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(skillEvent);
            UnityEngine.Object.DestroyImmediate(menuEvent);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/button-click-events-verification.txt", report);
        }
    }

    private static void Check(bool condition, string message, List<string> report)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.Add("OK: " + message);
    }
}
