using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReSeer.Skills.Editor
{
    public sealed class SkillEditorWindow : EditorWindow
    {
        public const string DatabasePath = "Assets/Game Data/SkillDatabase.asset";
        public const string ExportPath = "GameContent/Generated/Server/skills.json";
        private SkillDatabaseSO database;
        private ScrollView list;
        private ScrollView details;
        private Label status;
        private string search = "";

        [MenuItem("Tools/Re-Seer/Skill Editor")]
        public static void Open() => GetWindow<SkillEditorWindow>("技能数据库");

        public void CreateGUI()
        {
            database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>(DatabasePath);
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(AddSkill) { text = "+ 新增" });
            toolbar.Add(new ToolbarButton(Save) { text = "保存数据库" });
            toolbar.Add(new ToolbarButton(Export) { text = "导出服务器 JSON" });
            rootVisualElement.Add(toolbar);
            var filter = new ToolbarSearchField();
            filter.RegisterValueChangedCallback(e => { search = e.newValue; RefreshList(); });
            rootVisualElement.Add(filter);
            if (database != null)
            {
                var serialized = new SerializedObject(database);
                var version = new PropertyField(serialized.FindProperty("contentVersion"), "内容版本");
                version.Bind(serialized);
                rootVisualElement.Add(version);
            }
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            list = new ScrollView();
            list.style.width = 230;
            details = new ScrollView();
            details.style.flexGrow = 1;
            details.style.paddingLeft = 12;
            body.Add(list);
            body.Add(details);
            rootVisualElement.Add(body);
            status = new Label(database == null ? "未找到 Game Data/SkillDatabase.asset" : "所有技能都保存在同一个数据库资产中。");
            status.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(status);
            Undo.undoRedoPerformed -= RefreshList;
            Undo.undoRedoPerformed += RefreshList;
            RefreshList();
        }

        private void OnDisable() => Undo.undoRedoPerformed -= RefreshList;

        private void RefreshList()
        {
            if (list == null) return;
            list.Clear();
            if (database == null) return;
            for (int i = 0; i < database.skills.Count; i++)
            {
                int index = i;
                var skill = database.skills[index];
                string label = skill == null ? "<空记录>" : skill.id + "  " + skill.DisplayName();
                if (label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(new Button(() => Select(index)) { text = label });
            }
        }

        private void Select(int index)
        {
            details.Clear();
            if (database == null || index < 0 || index >= database.skills.Count) return;
            var serialized = new SerializedObject(database);
            var field = new PropertyField(serialized.FindProperty("skills").GetArrayElementAtIndex(index), "技能记录");
            details.Add(field);
            var preview = new Label(database.skills[index].Describe());
            preview.style.whiteSpace = WhiteSpace.Normal;
            preview.style.marginTop = 10;
            details.Add(preview);
            details.Add(new Button(() => RemoveSkill(index)) { text = "删除这条技能" });
            details.Bind(serialized);
            details.TrackSerializedObjectValue(serialized, _ =>
            {
                if (index < database.skills.Count && database.skills[index] != null)
                    preview.text = database.skills[index].Describe();
                RefreshList();
            });
        }

        private void AddSkill()
        {
            if (database == null) return;
            Undo.RecordObject(database, "新增技能记录");
            var skill = new SkillData { id = "skill_" + Guid.NewGuid().ToString("N").Substring(0, 8) };
            skill.nameKey = "skill." + skill.id + ".name";
            skill.descriptionKey = "skill." + skill.id + ".description";
            database.skills.Add(skill);
            EditorUtility.SetDirty(database);
            RefreshList();
            Select(database.skills.Count - 1);
        }

        private void RemoveSkill(int index)
        {
            if (database == null || index < 0 || index >= database.skills.Count) return;
            Undo.RecordObject(database, "删除技能记录");
            database.skills.RemoveAt(index);
            EditorUtility.SetDirty(database);
            details.Clear();
            RefreshList();
        }

        private bool ValidateData()
        {
            if (database == null) { status.text = "未找到数据库。"; return false; }
            var errors = database.Validate();
            status.text = errors.Count == 0 ? "校验通过。" : string.Join("\n", errors);
            return errors.Count == 0;
        }

        private void Save()
        {
            if (!ValidateData()) return;
            AssetDatabase.SaveAssets();
            status.text = "已保存：" + DatabasePath;
        }

        private void Export()
        {
            if (!ValidateData()) return;
            WriteExport(database);
            status.text = "已导出：" + ExportPath;
        }

        public static void WriteExport(SkillDatabaseSO source)
        {
            string json = source.ToServerJson();
            Directory.CreateDirectory(Path.GetDirectoryName(ExportPath));
            string temporary = ExportPath + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(ExportPath)) File.Replace(temporary, ExportPath, null);
            else File.Move(temporary, ExportPath);
            AssetDatabase.SaveAssets();
        }
    }
}
