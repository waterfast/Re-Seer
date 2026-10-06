using System;
using UnityEngine;
using UnityEngine.UI;

namespace ReSeer.Battle.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class BattleUiActionButton : MonoBehaviour
    {
        [SerializeField] private string actionId = "skill";
        public string ActionId => actionId;
        public event Action<string> Clicked;

        public void SetActionId(string id) => actionId = id;
        private void OnEnable() => GetComponent<Button>().onClick.AddListener(Raise);
        private void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Raise);
        private void Raise() { if (!string.IsNullOrWhiteSpace(actionId)) Clicked?.Invoke(actionId); }
        public void SetInteractable(bool value) => GetComponent<Button>().interactable = value;
    }
}
