using UnityEngine;

namespace ReSeer.Battle.UI
{
    public sealed class BattleUiPanel : MonoBehaviour
    {
        [SerializeField] private BattleUiMode mode;
        public BattleUiMode Mode => mode;

        public void SetMode(BattleUiMode value) => mode = value;
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
