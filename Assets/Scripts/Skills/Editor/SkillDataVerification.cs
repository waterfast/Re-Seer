using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReSeer.Skills.Editor
{
    public static class SkillDataVerification
    {
        [MenuItem("Tools/Re-Seer/Verify Skill Database")]
        public static void Run()
        {
            var database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>(SkillEditorWindow.DatabasePath);
            Require(database != null, "Database asset");
            Require(database.Validate().Count == 0, "Catalog validation");
            Require(database.skills.Count == 4, "Preserved four example records");
            foreach (string id in new[] { "demo_thunder", "demo_break", "demo_focus", "demo_spark" })
                Require(database.TryGet(id, out var row) && row.id == id, "Lookup " + id);
            string json = database.ToServerJson();
            Require(json.Contains("demo_spark") && json.Contains("apply_status"), "Rule export");
            Require(!json.Contains("fallbackName") && !json.Contains("icon") && !json.Contains("animationKey"), "Client-only fields excluded");
            var copy = UnityEngine.Object.Instantiate(database);
            try
            {
                copy.skills.Add(copy.skills[0]);
                Require(copy.Validate().Count > 0, "Duplicate ID rejection");
                bool rejected = false;
                try { copy.ToServerJson(); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Invalid export rejection");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            var sample = database.skills.First();
            Require(sample.DisplayName(_ => "translated") == "translated", "Translation hook");
            Require(sample.DisplayName(key => key) == sample.fallbackName, "Translation fallback");
            Require(!sample.Describe().Contains("{power}"), "Description interpolation");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/skill-database-verification.txt", "PASS: one Game Data asset, four entries, ID lookup, JSON projection, duplicate rejection and translation fallback.\n" + DateTime.UtcNow.ToString("O"));
            Debug.Log("Skill database verification: PASS");
        }

        private static void Require(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Skill database verification failed: " + label);
        }
    }
}
