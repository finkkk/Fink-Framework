using FinkFramework.Editor.Common;
using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    /// <summary>
    /// 存档设置入口。所有值仍然写入同一份 GlobalSettingsAsset，页面本身不维护第二份配置。
    /// </summary>
    public sealed class SaveSettingsProvider : SettingsProvider
    {
        public SaveSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SaveSettingsProvider("Project/Fink Framework/11 Save System", SettingsScope.Project)
            {
                label = "Save System",
                keywords = new[]
                {
                    "Fink", "Save", "System", "Slot", "History", "Json", "Binary",
                    "Compression", "Encryption", "AES"
                }
            };
        }

        private GlobalSettingsAsset asset;

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            asset = GlobalSettingsEditorLoader.LoadOrCreate();
        }

        public override void OnGUI(string searchContext)
        {
            if (!asset)
            {
                EditorGUILayout.HelpBox("GlobalSettingsAsset 缺失，请重新导入框架或点击按钮自动修复。", MessageType.Error);
                if (GUILayout.Button("重新创建配置文件"))
                    asset = GlobalSettingsEditorLoader.LoadOrCreate();
                return;
            }

            GUILayout.Space(10);
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("存档配置 (Save System Settings)", FFEditorStyles.Title);
            });
            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "存档格式、文件命名、历史备份和压缩配置。AES 开关与密钥继续复用数据管线配置。",
                FFEditorStyles.Description);
            GUILayout.Space(12);

            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            EditorGUILayout.LabelField("存档数据格式", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.SaveDataLoadMode = (EnvironmentState.DataLoadMode)EditorGUILayout.EnumPopup(
                "导出格式",
                asset.SaveDataLoadMode);
            EditorGUILayout.LabelField(
                asset.SaveDataLoadMode == EnvironmentState.DataLoadMode.Json
                    ? "当前存档会使用 JSON Payload，文件后缀固定为 .json。"
                    : "当前存档会使用 Binary Payload，文件后缀使用下方自定义配置。",
                FFEditorStyles.Description);

            if (asset.SaveDataLoadMode == EnvironmentState.DataLoadMode.Binary)
            {
                GUILayout.Space(8);
                string extension = EditorGUILayout.TextField(
                    new GUIContent("二进制存档后缀", "只控制存档系统，不影响数据管线的文件后缀。"),
                    asset.SaveBinaryExtension);
                asset.SaveBinaryExtension = GlobalSettingsAsset.NormalizeSaveBinaryExtension(extension);
            }

            GUILayout.Space(16);
            EditorGUILayout.LabelField("槽位与历史", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.MultiSlotMode = EditorGUILayout.Toggle(
                new GUIContent("启用多槽位", "关闭时固定使用 slot_01。"),
                asset.MultiSlotMode);
            asset.EnableSaveHistory = EditorGUILayout.Toggle(
                new GUIContent("保留历史备份", "开启后会生成 slot_01_bak1、slot_01_bak2 等文件。"),
                asset.EnableSaveHistory);
            using (new EditorGUI.DisabledScope(!asset.EnableSaveHistory))
            {
                asset.SaveHistoryLimit = EditorGUILayout.IntSlider(
                    "历史备份数量",
                    asset.SaveHistoryLimit,
                    0,
                    50);
            }

            GUILayout.Space(16);
            EditorGUILayout.LabelField("文件体积", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            using (new EditorGUI.DisabledScope(asset.SaveDataLoadMode == EnvironmentState.DataLoadMode.Json))
            {
                asset.EnableSaveCompression = EditorGUILayout.Toggle(
                    new GUIContent("压缩存档 Payload", "Binary 模式写入前使用 GZip 压缩 Payload。"),
                    asset.EnableSaveCompression);
            }
            EditorGUILayout.LabelField(
                asset.SaveDataLoadMode == EnvironmentState.DataLoadMode.Json
                    ? "JSON 模式为保证文件可直接打开，不压缩也不加密。"
                    : "Binary 模式可按需启用 GZip；AES 加密继续使用数据管线配置。",
                FFEditorStyles.Description);

            GUILayout.Space(16);
            EditorGUILayout.LabelField("AES 加密", FFEditorStyles.SectionTitle);
            EditorGUILayout.LabelField(
                "加密开关和密钥不在此重复配置，直接使用“数据管线”页面中的“启用 AES 加密”和“AES 密钥”。",
                FFEditorStyles.Description);

            GUILayout.EndVertical();

            GUILayout.Space(20);
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright © 2025 Fink Framework", FFEditorStyles.Footer);
            });

            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }

    }
}
