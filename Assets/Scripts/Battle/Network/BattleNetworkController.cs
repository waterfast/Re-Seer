using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReSeer.Battle.UI;
using ReSeer.Network;
using ReSeer.Skills;
using UnityEngine;

namespace ReSeer.Battle.Network
{
    [DisallowMultipleComponent]
    public sealed class BattleNetworkController : MonoBehaviour
    {
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 9527;
        [SerializeField] private SkillDatabaseSO skillDatabase;
        [SerializeField] private BattleUIController ui;
        [SerializeField] private MainActionPanel actionPanel;
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private bool showConnectionControls = true;

        private FramedJsonConnection connection;
        private BattleSnapshotState state = new BattleSnapshotState();
        private readonly Dictionary<int, string> requests = new Dictionary<int, string>();
        private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
        private int nextRequestId;
        private int pendingAction;
        private bool connecting;
        private float lastRequestTime;

        public BattleSnapshotState State => state;
        public string Status { get; private set; } = "未连接";
        public event Action<BattleSnapshot> SnapshotReceived;

        private void Start()
        {
            if (ui == null) ui = GetComponent<BattleUIController>();
            if (actionPanel != null) actionPanel.SkillSelected += SelectSkill;
            if (connectOnStart) Connect();
        }

        public void Connect()
        {
            if (connecting) return;
            Disconnect();
            if (skillDatabase == null || ui == null || actionPanel == null)
            {
                Status = "请配置战斗界面与技能库";
                Debug.LogError(Status, this);
                return;
            }
            state = new BattleSnapshotState();
            requests.Clear();
            nextRequestId = 0;
            connecting = true;
            Status = "正在连接服务器…";
            connection = new FramedJsonConnection();
            _ = ConnectAsync(connection);
        }

        private async Task ConnectAsync(FramedJsonConnection current)
        {
            try
            {
                await current.ConnectAsync(host, port).ConfigureAwait(false);
                mainThread.Enqueue(() =>
                {
                    if (connection != current) return;
                    connecting = false;
                    Status = "已连接，正在建立战斗…";
                    SendRequest("session.hello", new BattleRequestParams());
                });
            }
            catch (Exception error)
            {
                mainThread.Enqueue(() => { if (connection == current) Fail(error.Message); });
            }
        }

        private int SendRequest(string method, BattleRequestParams parameters)
        {
            int id = ++nextRequestId;
            var packet = new BattleRpcRequest { id = id, method = method, @params = parameters };
            requests.Add(id, method);
            lastRequestTime = Time.realtimeSinceStartup;
            _ = SendAsync(connection, JsonUtility.ToJson(packet));
            return id;
        }

        private async Task SendAsync(FramedJsonConnection current, string message)
        {
            try { await current.SendAsync(message).ConfigureAwait(false); }
            catch (Exception error)
            {
                mainThread.Enqueue(() => { if (connection == current) Fail(error.Message); });
            }
        }

        public void SelectSkill(SkillData skill)
        {
            var snapshot = state.Snapshot;
            if (skill == null || connection == null || snapshot == null ||
                !snapshot.canAct || snapshot.finished || pendingAction != 0) return;
            bool offered = false;
            foreach (var slot in state.UiState.Skills)
                if (slot.SkillId == skill.id && slot.Available && slot.CurrentPp > 0) offered = true;
            var fifth = state.UiState.FifthSkill;
            if (fifth.HasValue && fifth.Value.SkillId == skill.id && fifth.Value.Available && fifth.Value.CurrentPp > 0)
                offered = true;
            if (!offered) return;
            state.LockInput();
            Status = "等待服务器结算…";
            pendingAction = SendRequest("battle.action", new BattleRequestParams
            {
                battleId = snapshot.battleId, promptId = snapshot.promptId,
                action = new BattleActionIntent { skillId = skill.id,
                    targetPetId = skill.target == "self" ? snapshot.selfPet.petId : snapshot.enemyPet.petId }
            });
        }

        private void Update() => Pump();

        /// <summary>Called on the Unity thread; editor acceptance checks use the same pump.</summary>
        public void Pump()
        {
            while (mainThread.TryDequeue(out var action)) action();
            if (connection == null) return;
            while (connection != null && connection.TryReceive(out string message))
            {
                try { HandleMessage(message); }
                catch (Exception error) { Fail("服务器数据不合法：" + error.Message); break; }
            }
            if (connection != null && connection.TryGetFailure(out string reason)) Fail(reason);
            if (connection != null && requests.Count > 0 && Time.realtimeSinceStartup - lastRequestTime > 10f)
                Fail("服务器响应超时，请重新连接。");
        }

        private void HandleMessage(string message)
        {
            var packet = JsonUtility.FromJson<BattleRpcPacket>(message);
            if (packet == null || packet.jsonrpc != "2.0") throw new ArgumentException("协议版本不匹配。");
            if (packet.method == "battle.update")
            {
                bool initial = state.Context == null;
                if (!state.Apply(packet.@params, skillDatabase)) return;
                if (initial)
                {
                    ui.Initialize(state.Context);
                    ui.BindDataState(state.UiState);
                }
                pendingAction = 0;
                foreach (var visual in packet.@params.events ?? Array.Empty<BattleVisualEvent>())
                {
                    bool self = visual.targetPetId == packet.@params.selfPet.petId;
                    if (visual.type == "Damage") ui.ShowNumber(self, visual.amount,
                        visual.critical ? DamageNumberStyle.Critical : DamageNumberStyle.Normal);
                    else if (visual.type == "Recover") ui.ShowNumber(self, visual.amount, DamageNumberStyle.Healing);
                }
                Status = packet.@params.finished
                    ? packet.@params.winnerPlayerId == "1" ? "战斗结束：胜利" : "战斗结束：失败"
                    : $"第 {packet.@params.round} 回合：请选择技能";
                SnapshotReceived?.Invoke(packet.@params);
                return;
            }
            if (!requests.TryGetValue(packet.id, out string method)) return;
            requests.Remove(packet.id);
            if (packet.error != null && packet.error.code != 0)
            {
                pendingAction = 0;
                state.RestoreInput();
                Status = "操作被拒绝：" + (packet.error.data ?? packet.error.message);
                Debug.LogWarning(Status, this);
                return;
            }
            if (method == "session.hello")
            {
                if (packet.result == null || packet.result.protocolVersion != 1)
                    throw new ArgumentException("服务器不支持当前战斗协议。");
                SendRequest("battle.start", new BattleRequestParams());
            }
        }

        private void Fail(string reason)
        {
            Disconnect();
            Status = "连接中断：" + reason;
            Debug.LogWarning(Status, this);
        }

        public void Disconnect()
        {
            connection?.Dispose();
            connection = null;
            connecting = false;
            pendingAction = 0;
            requests.Clear();
            state.LockInput();
        }

        private void OnDestroy()
        {
            if (actionPanel != null) actionPanel.SkillSelected -= SelectSkill;
            Disconnect();
        }

        private void OnGUI()
        {
            if (!showConnectionControls) return;
            GUILayout.BeginArea(new Rect(12, 12, 360, 92), GUI.skin.box);
            GUILayout.Label(Status);
            GUI.enabled = !connecting;
            if (GUILayout.Button(state.Snapshot != null && state.Snapshot.finished ? "再战一局" : "重新连接")) Connect();
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
