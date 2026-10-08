using System;

namespace ReSeer.Battle.Network
{
    // Wire records are separate from scene objects and the local display model.
    [Serializable]
    public sealed class BattlePetSnapshot
    {
        public string petId;
        public string speciesId;
        public string name;
        public string[] elementIds;
        public int level;
        public int currentHp;
        public int maxHp;
    }

    [Serializable]
    public sealed class BattleSkillSnapshot
    {
        public string skillId;
        public int currentPp;
        public int maxPp;
        public bool available;
    }

    [Serializable]
    public sealed class BattleVisualEvent
    {
        public string type;
        public string targetPetId;
        public int amount;
        public bool critical;
    }

    [Serializable]
    public sealed class BattleSnapshot
    {
        public string battleId;
        public int sequence;
        public int round;
        public bool finished;
        public string winnerPlayerId;
        public bool canAct;
        public int promptId;
        public BattlePetSnapshot selfPet;
        public BattlePetSnapshot enemyPet;
        public BattleSkillSnapshot[] skills;
        public BattleSkillSnapshot fifthSkill;
        public BattleVisualEvent[] events;
    }

    [Serializable]
    internal sealed class BattleRpcPacket
    {
        public string jsonrpc;
        public int id;
        public string method;
        public BattleSnapshot @params;
        public BattleRpcResult result;
        public BattleRpcError error;
    }

    [Serializable]
    internal sealed class BattleRpcResult
    {
        public int protocolVersion;
        public string playerId;
        public bool accepted;
        public int sequence;
    }

    [Serializable]
    internal sealed class BattleRpcError
    {
        public int code;
        public string message;
        public string data;
    }

    [Serializable]
    internal sealed class BattleRpcRequest
    {
        public string jsonrpc = "2.0";
        public int id;
        public string method;
        public BattleRequestParams @params = new BattleRequestParams();
    }

    [Serializable]
    internal sealed class BattleRequestParams
    {
        public string battleId;
        public int promptId;
        public BattleActionIntent action;
    }

    [Serializable]
    public sealed class BattleActionIntent
    {
        public string type = "UseSkill";
        public string skillId;
        public string targetPetId;
    }
}
