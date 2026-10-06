using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Run manually when the project's Chinese TMP fallback needs to be recreated.
public static class ChineseFontSetup
{
    private const string Folder = "Assets/Art/Fonts/SourceHanSans/";
    private const string AssetPath = Folder + "SourceHanSansCN-Regular SDF.asset";

    [MenuItem("Tools/Re-Seer/Setup Chinese Font")]
    public static void Install()
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "SourceHanSansCN-Regular.otf");
        if (source == null || TMP_Settings.instance == null)
        {
            Debug.LogError("ChineseFontSetup: Import the source font and TMP Essential Resources first.");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (font == null)
                throw new InvalidOperationException("Unable to create the Chinese TMP font.");
            font.name = "SourceHanSansCN-Regular SDF";
            AssetDatabase.CreateAsset(font, AssetPath);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.name = font.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
        }

        if (!TMP_Settings.fallbackFontAssets.Contains(font))
            TMP_Settings.fallbackFontAssets.Add(font);
        EditorUtility.SetDirty(font);
        EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();

        // Rebuild loaded labels without saving or overwriting the user's unsaved scene.
        int refreshed = 0;
        foreach (var label in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (EditorUtility.IsPersistent(label) || !label.gameObject.scene.IsValid())
                continue;
            label.havePropertiesChanged = true;
            label.ForceMeshUpdate(true, true);
            refreshed++;
        }
        SceneView.RepaintAll();
        Debug.Log($"ChineseFontSetup: Installed dynamic Chinese fallback, refreshed {refreshed} text objects.");
    }
}
