using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FifthSkillUpgradeOnce
{
    static FifthSkillUpgradeOnce() => EditorApplication.update += Run;

    private static void Run()
    {
        const string request = "Temp/fifth-skill-upgrade.request";
        if (!File.Exists(request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= Run;
        File.Delete(request);
        try { FifthSkillButtonSetup.UpgradeExisting(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/fifth-skill-upgrade.error", error.ToString());
            Debug.LogException(error);
        }
    }
}
