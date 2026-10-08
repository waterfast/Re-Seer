using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ReSeer.Battle;
using ReSeer.Battle.Art;
using ReSeer.Battle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>验证迁移后的运行时图标索引，以及真实战斗场景中的 HP、快照和数字同步。</summary>
public static class VerifyBattleHealthAndArt
{
    public static void VerifyBatch()
    {
        TypeIconCatalogBuilder.Rebuild();
        VerifySkillPpUpdates.VerifyBatch();
        Verify();
    }

    [MenuItem("Tools/Re-Seer/验证 HP 变化与统一素材")]
    public static void Verify()
    {
        var report = new List<string>();
        // Unity 2022 用独立场景文件隔离验证，避免加载同一路径时复用当前打开的战斗。
        string temporaryScenePath = "Assets/__BattleHealthVerification_" + Guid.NewGuid().ToString("N") + ".unity";
        Scene previousScene = SceneManager.GetActiveScene();
        try
        {
            TypeIconCatalogBuilder.Rebuild();
            Check(TypeIconResources.Get(new[] { "electric" }) == AssetDatabase.LoadAssetAtPath<Sprite>(TypeIconCatalog.AssetRoot + "/5.png"),
                "英文属性通过运行时索引读取 Art/UI 图标", report);
            Check(TypeIconResources.Get(new[] { "fighting" }) == TypeIconResources.Get(new[] { "11" }), "技能英文别名与官方编号一致", report);
            Check(TypeIconResources.Get(new[] { "88" }) != null && TypeIconResources.GetPetTypeId("4500") != null,
                "双属性图片与精灵属性清单可加载", report);
            Check(!AssetDatabase.IsValidFolder("Assets/Resources/UI/Types"), "Resources 中不再保存属性图片", report);

            // 读取场景的所有序列化引用；预览验证时不运行 Start，不启动玩家的真实战斗。
            if (!AssetDatabase.CopyAsset("Assets/Scenes/Battle.unity", temporaryScenePath))
                throw new InvalidOperationException("无法创建独立测试场景。");
            var battle = EditorSceneManager.OpenScene(temporaryScenePath, OpenSceneMode.Additive);
            try
            {
                BattleUIController controller = null;
                BattleUiTestDataSource source = null;
                foreach (GameObject root in battle.GetRootGameObjects())
                {
                    if (controller == null) controller = root.GetComponentInChildren<BattleUIController>(true);
                    if (source == null) source = root.GetComponentInChildren<BattleUiTestDataSource>(true);
                }
                Check(controller != null && source != null, "战斗场景控制器与测试数据源存在", report);
                var settings = new SerializedObject(controller);
                var enemyNumbers = (DamageDisplay)settings.FindProperty("enemyNumberDisplay").objectReferenceValue;
                var selfNumbers = (DamageDisplay)settings.FindProperty("selfNumberDisplay").objectReferenceValue;
                Check(enemyNumbers != null && selfNumbers != null && enemyNumbers != selfNumbers, "两侧数字显示器已独立绑定", report);
                var bars = new Dictionary<string, HpBar>();
                foreach (GameObject root in battle.GetRootGameObjects())
                    foreach (HpBar bar in root.GetComponentsInChildren<HpBar>(true)) bars[bar.name] = bar;

                // 预览对象不依赖全场景查找，确保菜单验证只更新本次载入的对象。
                BattleActionController actions = FindComponent<BattleActionController>(battle);
                SetField(actions, "actionPanel", FindComponent<MainActionPanel>(battle));
                SetField(controller, "actionController", actions);
                SetField(controller, "selfHpBar", bars["SelfHpBar"]);
                SetField(controller, "enemyHpBar", bars["EnemyHpBar"]);
                source.StartSession();
                BattlePetState enemy = source.Context.EnemySide.ActivePet;
                BattlePetState self = source.Context.PlayerSide.ActivePet;

                foreach (DamageNumberStyle style in new[] { DamageNumberStyle.Normal, DamageNumberStyle.Critical,
                             DamageNumberStyle.FixedDamage, DamageNumberStyle.TrueDamage })
                {
                    int before = enemy.CurrentHp;
                    Check(controller.TakeDamage(false, 20, style) == 20, style + " 返回实际扣血量", report);
                    Check(enemy.CurrentHp == before - 20 && source.DataState.OpponentHp == enemy.CurrentHp
                        && bars["EnemyHpBar"].CurrentHp == enemy.CurrentHp, style + " 同步模型、快照和血条", report);
                    Check(Glyph(enemyNumbers, "Glyph01") == StyleDigit(enemyNumbers, style, 2), style + " 使用正确的数字素材", report);
                }
                Check(self.CurrentHp == self.MaxHp, "敌方伤害不修改我方 HP", report);
                int missingHp = enemy.MaxHp - enemy.CurrentHp;
                Check(controller.Heal(false, int.MaxValue) == missingHp,
                    "大额治疗不会溢出最大 HP", report);
                Check(enemy.CurrentHp == enemy.MaxHp && source.DataState.OpponentHp == enemy.MaxHp, "治疗同步 HP 上限", report);
                enemyNumbers.Hide();
                Check(controller.Heal(false, 20) == 0 && !Content(enemyNumbers).activeSelf, "满血治疗不显示虚假的恢复量", report);

                Check(controller.TakeDamage(true, int.MaxValue, DamageNumberStyle.Critical) == self.MaxHp,
                    "过量伤害只扣剩余 HP", report);
                Check(self.State == BattlePetStatus.Dead && bars["SelfHpBar"].CurrentHp == 0 && source.DataState.SelfHp == 0,
                    "零血更新死亡状态及我方 UI", report);
                selfNumbers.Hide();
                Check(controller.TakeDamage(true, 50) == 0 && !Content(selfNumbers).activeSelf, "零血目标不重复显示扣血", report);
                Check(controller.Heal(true, 50) == 50 && self.CurrentHp == 50 && self.State == BattlePetStatus.Alive,
                    "确认的恢复结果更新存活状态", report);
                Check(Glyph(selfNumbers, "Glyph00") == StyleSign(selfNumbers, "healing", "plus"), "治疗使用绿色加号", report);
                int confirmedHp = self.CurrentHp;
                ExpectArgumentError(() => controller.TakeDamage(true, -1), report, "负伤害被拒绝");
                ExpectArgumentError(() => controller.Heal(true, -1), report, "负治疗被拒绝");
                ExpectArgumentError(() => controller.TakeDamage(true, 1, DamageNumberStyle.Healing), report, "治疗样式不能当伤害类型");
                Check(self.CurrentHp == confirmedHp, "非法输入不修改 HP", report);
                selfNumbers.Hide();
                Check(controller.TakeDamage(true, 0) == 0 && !Content(selfNumbers).activeSelf, "零伤害不播放数字", report);

                CaptureStyles(enemyNumbers);
                source.HealAll();
                source.ToggleSelfActivePet();
                source.HealAll();
                source.ToggleSelfActivePet();
                Check(source.DataState.SelfHp == self.MaxHp && bars["SelfHpBar"].CurrentHp == self.MaxHp,
                    "空态恢复后切回出战保持 HP 一致", report);
            }
            finally { EditorSceneManager.CloseScene(battle, true); }
            report.Add("PASS");
        }
        catch (Exception exception)
        {
            report.Add("FAIL: " + exception);
            throw;
        }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            AssetDatabase.DeleteAsset(temporaryScenePath);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/battle-health-and-art-verification.txt", report);
        }
    }

    private static GameObject Content(DamageDisplay display) =>
        ((Transform)new SerializedObject(display).FindProperty("content").objectReferenceValue).gameObject;

    private static Sprite Glyph(DamageDisplay display, string name) =>
        Content(display).transform.Find(name).GetComponent<SpriteRenderer>().sprite;

    private static Sprite StyleDigit(DamageDisplay display, DamageNumberStyle style, int digit)
    {
        string field = style == DamageNumberStyle.Normal ? "normal" : style == DamageNumberStyle.Critical ? "critical"
            : style == DamageNumberStyle.FixedDamage ? "fixedDamage" : "trueDamage";
        return (Sprite)new SerializedObject(display).FindProperty(field).FindPropertyRelative("digits").GetArrayElementAtIndex(digit).objectReferenceValue;
    }

    private static Sprite StyleSign(DamageDisplay display, string field, string sign) =>
        (Sprite)new SerializedObject(display).FindProperty(field).FindPropertyRelative(sign).objectReferenceValue;

    private static void ExpectArgumentError(Action action, List<string> report, string label)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { report.Add("OK: " + label); return; }
        throw new InvalidOperationException(label);
    }

    private static void Check(bool value, string label, List<string> report)
    {
        if (!value) throw new InvalidOperationException(label);
        report.Add("OK: " + label);
    }

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        throw new InvalidOperationException("测试场景缺少 " + typeof(T).Name);
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void CaptureStyles(DamageDisplay template)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            var styles = new[] { DamageNumberStyle.Normal, DamageNumberStyle.Critical,
                DamageNumberStyle.FixedDamage, DamageNumberStyle.TrueDamage, DamageNumberStyle.Healing };
            for (int i = 0; i < styles.Length; i++)
            {
                var root = UnityEngine.Object.Instantiate(template.gameObject);
                root.transform.position = new Vector3((i - 2) * 3f, 0f, 0f);
                root.transform.localScale = Vector3.one;
                preview.AddSingleGO(root);
                root.GetComponent<DamageDisplay>().SetValue(123, styles[i]);
            }
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 2f;
            preview.camera.transform.position = new Vector3(0f, 0f, -10f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.08f, 0.13f, 0.2f);
            preview.BeginStaticPreview(new Rect(0, 0, 1500, 350));
            preview.Render();
            var image = preview.EndStaticPreview();
            Directory.CreateDirectory("docs/screenshots");
            File.WriteAllBytes("docs/screenshots/health-number-types.png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally { preview.Cleanup(); }
    }
}
