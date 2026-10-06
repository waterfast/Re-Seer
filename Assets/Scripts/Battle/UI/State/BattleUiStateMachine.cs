using System;

namespace ReSeer.Battle.UI
{
    public enum BattleUiMode
    {
        Main,
        SelectingSkill,
        SelectingPet,
        SelectingItem,
        SelectingTarget,
        WaitingServer,
        Animating
    }

    // Navigation only. Battle legality and result validation remain outside the UI.
    public sealed class BattleUiStateMachine
    {
        public BattleUiMode Mode { get; private set; } = BattleUiMode.Main;
        public event Action<BattleUiMode> ModeChanged;

        public bool TryEnter(BattleUiMode next)
        {
            if (next == Mode || !Allows(Mode, next)) return false;
            Mode = next;
            ModeChanged?.Invoke(next);
            return true;
        }

        public bool Back()
        {
            return Mode == BattleUiMode.SelectingSkill || Mode == BattleUiMode.SelectingPet ||
                Mode == BattleUiMode.SelectingItem || Mode == BattleUiMode.SelectingTarget
                ? TryEnter(BattleUiMode.Main) : false;
        }

        private static bool Allows(BattleUiMode current, BattleUiMode next)
        {
            switch (current)
            {
                case BattleUiMode.Main:
                    return next == BattleUiMode.SelectingSkill || next == BattleUiMode.SelectingPet ||
                        next == BattleUiMode.SelectingItem || next == BattleUiMode.WaitingServer;
                case BattleUiMode.SelectingSkill:
                case BattleUiMode.SelectingPet:
                case BattleUiMode.SelectingItem:
                    return next == BattleUiMode.Main || next == BattleUiMode.SelectingTarget || next == BattleUiMode.WaitingServer;
                case BattleUiMode.SelectingTarget:
                    return next == BattleUiMode.Main || next == BattleUiMode.WaitingServer;
                case BattleUiMode.WaitingServer:
                    return next == BattleUiMode.Animating || next == BattleUiMode.Main;
                case BattleUiMode.Animating:
                    return next == BattleUiMode.Main;
                default:
                    return false;
            }
        }
    }
}
