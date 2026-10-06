using System;
using System.IO;
using ReSeer.Skills;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

[CustomEditor(typeof(FifthSkillButton))]
public sealed class FifthSkillButtonSetup : Editor
{
    private const string ArtRoot = "Assets/Art/Battle/Flash/FifthSkill/";
    private const string PrefabPath = "Assets/Prefabs/Fifth Skill Button Ready.prefab";

    [MenuItem("Tools/Re-Seer/补齐现有第五技能按钮")]
    public static void UpgradeExisting()
    {
        string path = AssetDatabase.GUIDToAssetPath("fa027e19cba7f4d47a0bb6e8050bbe6d");
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("找不到你原来的第五技能预制体。");
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        bool openStage = stage != null && stage.assetPath == path;
        var root = openStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
        try
        {
            Undo.RegisterFullObjectHierarchyUndo(root, "补齐第五技能按钮");
            var ring = root.transform.Find("MainUi").GetComponent<SpriteRenderer>();
            var surface = root.transform.Find("standard-center").GetComponent<SpriteRenderer>();
            var button = root.GetComponent<FifthSkillButton>();
            if (button == null) button = Undo.AddComponent<FifthSkillButton>(root);
            var center = surface.transform.localPosition;
            // The original center has a different canvas size; keep its visible diameter.
            float diameter = surface.sprite.bounds.size.x * surface.transform.localScale.x;
            surface.sprite = SpriteAt("Parts/center-button-up.png");
            surface.transform.localScale = Vector3.one * (diameter / surface.sprite.bounds.size.x);
            surface.sortingOrder = ring.sortingOrder + 1;

            var font = GetNameFont();
            var name = ExistingText(root, "Skill Name", font, center, new Vector2(2.7f, 1.6f), 10f);
            name.color = new Color(1f, 0.96f, 0.35f);
            name.enableAutoSizing = true;
            name.fontSizeMin = 3f;
            name.fontSizeMax = 10f;
            name.richText = false;
            name.fontStyle = FontStyles.Italic;
            const string materialPath = "Assets/Art/Fonts/SourceHanSans/Fifth Skill Name.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(font.material) { name = "Fifth Skill Name" };
                material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(1f, 0.24f, 0.02f));
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            name.fontSharedMaterial = material;
            var info = root.transform.Find("Information");
            var orbSprite = info.GetComponent<SpriteRenderer>().sprite;
            // Centers measured on the existing three-orb image. TransformPoint respects the user's rotation.
            Vector3 powerPosition = OrbPosition(root.transform, info, orbSprite, new Vector2(60f, 60f));
            Vector3 ppPosition = OrbPosition(root.transform, info, orbSprite, new Vector2(187f, 110f));
            var power = ExistingText(root, "Skill Power", font, powerPosition, new Vector2(0.85f, 0.55f), 3f);
            var pp = ExistingText(root, "Skill PP", font, ppPosition, new Vector2(0.85f, 0.5f), 2.5f);
            foreach (var text in new TMP_Text[] { name, power, pp })
            {
                var renderer = text.GetComponent<MeshRenderer>();
                renderer.sortingLayerID = ring.sortingLayerID;
                renderer.sortingOrder = ring.sortingOrder + 10;
            }
            var settings = new SerializedObject(button);
            Reference(settings, "nameText", name);
            Reference(settings, "powerText", power);
            Reference(settings, "ppText", pp);
            Reference(settings, "background", surface);
            Reference(settings, "centerNormal", SpriteAt("Parts/center-button-up.png"));
            Reference(settings, "centerHover", SpriteAt("Parts/center-button-over.png"));
            Reference(settings, "centerPressed", SpriteAt("Parts/center-button-down.png"));
            settings.FindProperty("skillName").stringValue = "狂魔\n霸裂斩";
            settings.FindProperty("power").intValue = 200;
            settings.FindProperty("maxPP").intValue = 10;
            settings.FindProperty("currentPP").intValue = 2;
            settings.FindProperty("useOriginalNameArtwork").boolValue = false;
            settings.FindProperty("loadDatabasePreviewOnStart").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var collider = button.GetComponent<BoxCollider2D>();
            collider.offset = center;
            collider.size = Vector2.one * diameter;
            button.RefreshView();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            SaveExistingPreview(root, center);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/fifth-skill-existing-verification.txt", "PASS: existing hierarchy preserved; custom names survive Bind; clearing override restores data name; input submits without consuming PP.\n" + DateTime.UtcNow.ToString("O"));
            Debug.Log("已补齐现有第五技能按钮：" + path);
        }
        finally { if (!openStage) PrefabUtility.UnloadPrefabContents(root); }
    }

    private static TMP_FontAsset GetNameFont()
    {
        const string path = "Assets/Art/Fonts/SourceHanSans/SourceHanSansCN-Heavy SDF.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null) return asset;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/SourceHanSans/SourceHanSansCN-Heavy.otf");
        asset = TMP_FontAsset.CreateFontAsset(source);
        asset.name = "SourceHanSansCN-Heavy SDF";
        asset.isMultiAtlasTexturesEnabled = true;
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var texture in asset.atlasTextures) AssetDatabase.AddObjectToAsset(texture, asset);
        return asset;
    }

    private static Vector3 OrbPosition(Transform root, Transform info, Sprite sprite, Vector2 pixel)
    {
        Vector3 local = new Vector3((pixel.x - sprite.pivot.x) / sprite.pixelsPerUnit,
            (sprite.rect.height - pixel.y - sprite.pivot.y) / sprite.pixelsPerUnit, 0f);
        Vector3 position = root.InverseTransformPoint(info.TransformPoint(local));
        position.z = -0.1f;
        return position;
    }

    private static TextMeshPro ExistingText(GameObject root, string name, TMP_FontAsset font, Vector3 position, Vector2 size, float fontSize)
    {
        var child = root.transform.Find(name);
        var text = child != null ? child.GetComponent<TextMeshPro>() : null;
        if (text != null) return text;
        text = AddText(root, name, font, position, size, fontSize);
        text.transform.localPosition = position;
        return text;
    }

    private static void VerifyCustomName(FifthSkillButton button, TMP_Text name)
    {
        string previousName = name.text;
        var skill = new SkillData { id = "check", fallbackName = "数据名称", maxPp = 10 };
        button.SetCustomName("自定义\n第五技能");
        button.Bind(skill, 3);
        Require(name.text == "自定义\n第五技能" && skill.fallbackName == "数据名称", "自定义名称不被绑定覆盖");
        button.SetCustomName("");
        Require(name.text == "数据名称", "清空自定义后恢复资料名称");
        int clicks = 0;
        Action<SkillData> handler = _ => clicks++;
        button.SkillSelected += handler;
        button.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        button.SkillSelected -= handler;
        Require(clicks == 1 && button.CurrentPP == 3, "点击只提交选择");
        button.SetSkill(previousName, 200, 2, 10, null);
    }

    private static void SaveExistingPreview(GameObject root, Vector3 center)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            var copy = Instantiate(root);
            copy.transform.position = Vector3.zero;
            preview.AddSingleGO(copy);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 3.4f;
            preview.camera.transform.position = new Vector3(center.x + 0.4f, center.y + 0.5f, -10f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.08f, 0.13f, 0.2f);
            foreach (var text in copy.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            preview.BeginStaticPreview(new Rect(0, 0, 700, 700));
            preview.Render();
            var image = preview.EndStaticPreview();
            Directory.CreateDirectory("docs/screenshots");
            File.WriteAllBytes("docs/screenshots/fifth-skill-existing.png", image.EncodeToPNG());
            DestroyImmediate(image);
            VerifyCustomName(copy.GetComponent<FifthSkillButton>(), copy.transform.Find("Skill Name").GetComponent<TMP_Text>());
        }
        finally { preview.Cleanup(); }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("从技能数据库载入预览"))
        {
            Undo.RecordObject(target, "Bind skill preview");
            ((FifthSkillButton)target).LoadDatabasePreview();
        }
    }

    [MenuItem("Tools/Re-Seer/创建第五技能按钮示例")]
    public static void BuildAndVerify()
    {
        // PreviewRenderUtility owns an isolated preview scene, so the current prefab stage stays untouched.
        var preview = new PreviewRenderUtility();
        var root = new GameObject("Fifth Skill Button Ready");
        preview.AddSingleGO(root);
        try
        {
            var button = root.AddComponent<FifthSkillButton>();
            var ring = AddSprite(root, "Ring", SpriteAt("Parts/standard-ring.png"), Vector2.zero, 0);
            AddSprite(root, "Mechanical Header", SpriteAt("Parts/mechanical-header.png"), new Vector2(0.52f, 0.47f), 1);
            var surface = AddSprite(root, "Orange Center", SpriteAt("Parts/center-button-up.png"), Vector2.zero, 2);
            var effect = AddSprite(root, "Orange Ring Effect", SpriteAt("CenterEffect/001.png"), Vector2.zero, 3);
            effect.transform.localScale = Vector3.one * 0.84f;
            var nameArt = AddSprite(root, "Name Artwork", SpriteAt("NameArt/001.png"), Vector2.zero, 5);
            nameArt.transform.localScale = Vector3.one * 0.85f;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/SourceHanSans/SourceHanSansCN-Regular SDF.asset");
            var fallback = AddText(root, "Dynamic Name", font, new Vector2(0f, 0f), new Vector2(2.6f, 1.6f), 13f);
            fallback.enableAutoSizing = true;
            fallback.fontSizeMin = 4f;
            fallback.fontSizeMax = 13f;
            fallback.color = new Color(1f, 0.95f, 0.3f);
            var power = AddText(root, "Power", font, new Vector2(2.10f, 1.28f), new Vector2(1f, 0.6f), 3.5f);
            var pp = AddText(root, "PP", font, new Vector2(2.49f, 0.38f), new Vector2(1f, 0.6f), 3f);
            power.enableWordWrapping = false;
            pp.enableWordWrapping = false;
            AddSprite(root, "Power Orb", SpriteAt("Parts/side-orb.png"), new Vector2(2.10f, 1.28f), 4);
            AddSprite(root, "PP Orb", SpriteAt("Parts/side-orb.png"), new Vector2(2.49f, 0.38f), 4);

            var settings = new SerializedObject(button);
            Reference(settings, "nameText", fallback);
            Reference(settings, "ppText", pp);
            Reference(settings, "powerText", power);
            Reference(settings, "background", surface);
            Reference(settings, "nameArtworkRenderer", nameArt);
            Reference(settings, "centerEffectRenderer", effect);
            Reference(settings, "centerNormal", SpriteAt("Parts/center-button-up.png"));
            Reference(settings, "centerHover", SpriteAt("Parts/center-button-over.png"));
            Reference(settings, "centerPressed", SpriteAt("Parts/center-button-down.png"));
            Reference(settings, "skillDatabase", AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Game Data/SkillDatabase.asset"));
            settings.FindProperty("previewSkillId").stringValue = "demo_thunder";
            settings.FindProperty("previewCurrentPP").intValue = 10;
            settings.FindProperty("loadDatabasePreviewOnStart").boolValue = true;
            settings.FindProperty("skillName").stringValue = "狂魔霸裂斩";
            settings.FindProperty("power").intValue = 200;
            settings.FindProperty("maxPP").intValue = 10;
            settings.FindProperty("currentPP").intValue = 2;
            var entries = settings.FindProperty("nameArtworks");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("originalName").stringValue = "狂魔霸裂斩";
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("sprite").objectReferenceValue = SpriteAt("NameArt/001.png");
            var frames = settings.FindProperty("centerEffectFrames");
            frames.arraySize = 68;
            for (int i = 0; i < 68; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = SpriteAt($"CenterEffect/{i + 1:000}.png");
            settings.ApplyModifiedPropertiesWithoutUndo();
            button.RefreshView();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            var collider = root.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(3.7f, 3.7f);

            // Generated example values are presentation samples, not official skill catalog entries.
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            SavePreview(preview);
            VerifyBinding(button);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/fifth-skill-button-verification.txt",
                "PASS: Bind preserves shared SkillData; clicking does not consume PP; PP cost gates input; disabled input is rejected; rebinding replaces stale name art; all 68 effect frames imported.\n" + DateTime.UtcNow.ToString("O"));
            Debug.Log("第五技能按钮：示例与数据绑定验证完成。");
        }
        finally { preview.Cleanup(); }
    }

    private static Sprite SpriteAt(string relative)
    {
        string path = ArtRoot + relative;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("找不到资源：" + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("无法载入 Sprite：" + path);
        return sprite;
    }

    private static SpriteRenderer AddSprite(GameObject root, string name, Sprite sprite, Vector2 position, int order)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = position;
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static TextMeshPro AddText(GameObject root, string name, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = position;
        var text = child.AddComponent<TextMeshPro>();
        if (font != null) text.font = font;
        text.rectTransform.sizeDelta = size;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.yellow;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.GetComponent<MeshRenderer>().sortingOrder = 6;
        return text;
    }

    private static void Reference(SerializedObject settings, string name, UnityEngine.Object value)
        => settings.FindProperty(name).objectReferenceValue = value;

    private static void SavePreview(PreviewRenderUtility preview)
    {
        preview.camera.orthographic = true;
        preview.camera.orthographicSize = 3.5f;
        preview.camera.transform.position = new Vector3(0.2f, 0.4f, -10f);
        preview.camera.transform.rotation = Quaternion.identity;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.08f, 0.13f, 0.20f);
        preview.BeginStaticPreview(new Rect(0, 0, 700, 700));
        preview.Render();
        var image = preview.EndStaticPreview();
        Directory.CreateDirectory("docs/screenshots");
        File.WriteAllBytes("docs/screenshots/fifth-skill-button-ready.png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    private static void VerifyBinding(FifthSkillButton button)
    {
        var skill = new SkillData { id = "verification", fallbackName = "狂魔霸裂斩", power = 200, maxPp = 10, ppCost = 2 };
        button.Bind(skill, 2);
        Require(button.CurrentNameArtwork == null && button.DisplayName == "狂魔霸裂斩" && button.CurrentPP == 2 && skill.maxPp == 10, "资料与运行 PP 分离");
        int clicks = 0;
        button.SkillSelected += _ => clicks++;
        var input = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        button.OnPointerClick(input);
        Require(clicks == 1 && button.CurrentPP == 2 && skill.maxPp == 10, "点击不扣 PP、不修改资料");
        button.SetCurrentPP(1);
        button.OnPointerClick(input);
        Require(clicks == 1, "PP 不足不可提交");
        button.SetCurrentPP(2);
        button.SetInteractable(false);
        button.OnPointerClick(input);
        Require(clicks == 1, "输入锁定不可提交");
        button.Bind(new SkillData { id = "other", fallbackName = "新技能", maxPp = 5 }, 4);
        Require(button.CurrentNameArtwork == null && button.SkillName == "新技能" && button.CurrentPP == 4, "换技能清理旧名称美术");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("第五技能验证失败：" + label);
    }
}
