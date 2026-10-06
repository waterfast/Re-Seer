using UnityEngine;

namespace ReSeer.Battle.UI
{
    /// <summary>单个头像槽的显示示例；接入真实战斗数据后移除或停用。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HpBar))]
    public sealed class HpBarTest : MonoBehaviour
    {
        [SerializeField] private string petName = "武心婵";
        [SerializeField, Min(1)] private int level = 60;
        [SerializeField] private string petId = "4500";
        [SerializeField, Min(0)] private int currentHp = 200;
        [SerializeField, Min(1)] private int maxHp = 300;

        /// <summary>只在主动点击菜单时切换示例，不自动覆盖正式绑定的数据。</summary>
        [ContextMenu("同步测试：切换60级武心婵（200／300 HP）")]
        public void SwitchToWuXinChan()
        {
            petName = "武心婵";
            level = 60;
            currentHp = 200;
            maxHp = 300;
            petId = "4500";
            ApplyPreview();
        }

        /// <summary>使用同一只精灵和 300 点上限，恢复到 100 级、300/300 满血。</summary>
        [ContextMenu("同步测试：还原100级武心婵（300／300 满血）")]
        public void RestoreFullHealth()
        {
            petName = "武心婵";
            level = 100;
            maxHp = 300;
            currentHp = maxHp;
            petId = "4500";
            ApplyPreview();
        }

        [ContextMenu("同步测试：当前体力减少50")]
        private void ReduceHealth()
        {
            var hpBar = GetComponent<HpBar>();
            hpBar.SetHealth(hpBar.CurrentHp - 50, hpBar.MaxHp);
            MarkPreviewDirty();
        }

        public void ApplyPreview()
        {
            GetComponent<HpBar>().BindPet(petId, petName, level, currentHp, maxHp);
            MarkPreviewDirty();
        }

        private void MarkPreviewDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(GetComponent<HpBar>());
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
    }
}
