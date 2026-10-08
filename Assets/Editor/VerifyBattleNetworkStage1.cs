using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ReSeer.Battle.Network;
using ReSeer.Battle.UI;
using ReSeer.Skills;
using ReSeer.Pets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ReSeer.Editor
{
    /// <summary>Checks the actual Unity scene, real skill click path and live server.</summary>
    public static class VerifyBattleNetworkStage1
    {
        private static readonly List<string> Report = new List<string>();
        private static Scene scene;
        private static BattleNetworkController network;
        private static SkillDatabaseSO catalog;
        private static double deadline;
        private static int phase;
        private static int lastSequence;
        private static int clickCount;
        private static int fifthBefore;
        private static int hpBefore;
        private static string output;

        [MenuItem("Tools/Re-Seer/Verify Battle Network Stage 1")]
        public static void Run()
        {
            if (network != null) throw new InvalidOperationException("网络验收已在运行。");
            Report.Clear();
            output = Environment.GetEnvironmentVariable("RESEER_NETWORK_REPORT");
            if (string.IsNullOrEmpty(output)) output = "Temp/battle-network-verification.txt";
            try
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleNetwork.unity", OpenSceneMode.Additive);
                network = Find<BattleNetworkController>();
                var ui = Find<BattleUIController>();
                var panel = Find<MainActionPanel>();
                var controller = Find<BattleActionController>();
                catalog = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Game Data/SkillDatabase.asset");
                Check(network != null && ui != null && panel != null && catalog != null, "场景网络组件与技能库已装配");
                Check((MainActionPanel)Read(network, "actionPanel") == panel && (BattleUIController)Read(network, "ui") == ui,
                    "网络组件序列化引用指向本场景的界面与操作区");
                var source = Find<BattleUiTestDataSource>();
                Check(!(bool)Read(source, "autoStartOnPlay") && !(bool)Read(source, "enableKeyboardShortcuts"),
                    "联机场景已关闭本地演示数据和扣血快捷键");
                // Bind this additive scene explicitly so the user's open scene is untouched.
                foreach (var bar in Components<HpBar>())
                {
                    Invoke(bar, "Awake");
                    if (bar.name == "SelfHpBar") Write(ui, "selfHpBar", bar);
                    if (bar.name == "EnemyHpBar") Write(ui, "enemyHpBar", bar);
                }
                foreach (var display in Components<DamageDisplay>())
                {
                    if (display.name == "DamageDisplay") Write(ui, "enemyNumberDisplay", display);
                    if (display.name == "SelfDamageDisplay") Write(ui, "selfNumberDisplay", display);
                }
                Write(controller, "actionPanel", panel);
                Write(ui, "actionController", controller);
                Invoke(panel, "OnDisable");
                Invoke(panel, "OnEnable");
                Write(network, "connectOnStart", false);
                string port = Environment.GetEnvironmentVariable("RESEER_NETWORK_PORT");
                if (!string.IsNullOrEmpty(port)) Write(network, "port", int.Parse(port));
                Invoke(network, "Start");
                phase = 0;
                lastSequence = 0;
                clickCount = 0;
                deadline = EditorApplication.timeSinceStartup + 40;
                EditorApplication.update += Tick;
                network.Connect();
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Tick()
        {
            try
            {
                network.Pump();
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("等待真实服务器超时：" + network.Status);
                var state = network.State;
                var snapshot = state.Snapshot;
                if (snapshot == null || snapshot.sequence == lastSequence) return;
                lastSequence = snapshot.sequence;
                Check(state.Context.PlayerSide.ActivePet.CurrentHp == state.UiState.SelfHp &&
                    state.Context.EnemySide.ActivePet.CurrentHp == state.UiState.OpponentHp,
                    "快照同步精灵状态与两侧 HP，未重复扣血");
                if (phase == 0)
                {
                    VerifyReducer(snapshot);
                    Check(snapshot.selfPet.currentHp == snapshot.selfPet.maxHp && snapshot.canAct,
                        "Unity 收到真实服务器初始快照并开放技能选择");
                    fifthBefore = snapshot.fifthSkill.currentPp;
                    hpBefore = snapshot.enemyPet.currentHp;
                    Click(Find<FifthSkillButton>());
                    Check(state.UiState.FifthSkill.Value.CurrentPp == fifthBefore &&
                        !state.UiState.FifthSkill.Value.Available, "第五技能点击只发送意图并立即锁定，不在本地扣 PP");
                    phase = 1;
                    return;
                }
                if (phase == 1)
                {
                    Check(snapshot.fifthSkill.currentPp == fifthBefore - 1 && snapshot.enemyPet.currentHp < hpBefore,
                        "第五技能点击链路接入 Lua，服务器返回 PP 消耗和伤害");
                    Check(snapshot.events.Length > 0, "伤害事件随快照到达 Unity");
                    phase = 2;
                }
                if (snapshot.finished)
                {
                    Check(!snapshot.canAct && snapshot.winnerPlayerId != "", "完整战斗结束并收到胜负");
                    foreach (var slot in state.UiState.Skills) Check(!slot.Available, "结束后技能输入保持锁定");
                    Finish(null);
                    return;
                }
                foreach (var button in Components<SkillButton>())
                    if (!(button is FifthSkillButton) && button.BoundSkill?.id == "demo_thunder")
                    {
                        Click(button);
                        return;
                    }
                throw new InvalidOperationException("技能面板没有生成雷光冲击按钮。");
            }
            catch (Exception error) { Finish(error); }
        }

        private static void VerifyReducer(BattleSnapshot snapshot)
        {
            var state = new BattleSnapshotState();
            Check(state.Apply(snapshot, catalog), "确认快照能够初始化页面数据");
            Check(!state.Apply(snapshot, catalog), "重复序号不会重复应用状态或表现事件");
            var corrupt = JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(snapshot));
            corrupt.sequence++;
            corrupt.skills[0].currentPp = -1;
            bool rejected = false;
            try { state.Apply(corrupt, catalog); } catch (ArgumentException) { rejected = true; }
            Check(rejected && state.Snapshot.sequence == snapshot.sequence, "错误 PP 快照被整体拒绝，保留已确认状态");
        }

        private static void Click(SkillButton button)
        {
            if (++clickCount > 30) throw new InvalidOperationException("战斗超过验收点击上限。");
            Check(button != null && button.CanUse, "服务器开放的技能按钮可点击");
            button.OnPointerClick(new PointerEventData(Find<EventSystem>()) { button = PointerEventData.InputButton.Left });
        }

        private static T Find<T>() where T : Component
        {
            foreach (var item in Components<T>()) return item;
            return null;
        }

        private static IEnumerable<T> Components<T>() where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<T>(true)) yield return item;
        }

        private static object Read(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Write(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            Report.Add("PASS " + label);
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (error != null) Report.Add("FAIL " + error);
            network?.Disconnect();
            network = null;
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Temp");
            File.WriteAllLines(output, Report);
            if (error == null) Debug.Log("战斗联机第一阶段验收通过：" + output);
            else Debug.LogError("战斗联机验收失败：" + error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
