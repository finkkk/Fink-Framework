using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using FinkFramework.Editor.Settings.Core;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Settings;

namespace FinkFramework.Editor.Settings.Providers
{
    public class EncryptionSettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset asset;

        public EncryptionSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new EncryptionSettingsProvider("Project/Fink Framework/Encryption", SettingsScope.Project)
            {
                keywords = new[] { "Fink", "Encryption", "AES", "Security", "Password", "Data" }
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            asset = GlobalSettingsLoader.LoadOrCreate();
        }

        public override void OnGUI(string searchContext)
        {
            if (!asset)
            {
                EditorGUILayout.HelpBox("GlobalSettingsAsset 缺失，请重新导入框架或点击按钮自动修复。", MessageType.Error);
                if (GUILayout.Button("重新创建配置文件"))
                {
                    asset = GlobalSettingsLoader.LoadOrCreate();
                }
                return;
            }

            GUILayout.Space(10);
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("加密配置 (Encryption Settings)", FFEditorStyles.Title);
            });

            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "配置框架的数据加密策略，包括 AES 密钥、加密开关、文件后缀名等。",
                FFEditorStyles.Description);
            GUILayout.Space(12);

            // ===== 主区域 =====
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            // ----------------------------------------------------
            // Disable Encryption
            // ----------------------------------------------------
            EditorGUILayout.LabelField("加密开关", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            asset.EnableEncryption =
                EditorGUILayout.Toggle("全局开启加密", asset.EnableEncryption);

            EditorGUILayout.LabelField(
                asset.EnableEncryption
                    ? "当前状态：所有 DataUtil.Save() 调用将进行AES加密。"
                    : "当前状态：所有 DataUtil.Save() 调用将不再进行任何加密。",
                FFEditorStyles.Description);

            GUILayout.Space(12);

            // ----------------------------------------------------
            // AES Password
            // ----------------------------------------------------
            EditorGUILayout.LabelField("AES 密钥", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            asset.Password =
                EditorGUILayout.PasswordField("加密密钥", asset.Password);

            EditorGUILayout.LabelField(
                "用于 AES 加密与解密的核心密钥。请勿在正式环境使用简单字符串，可与混淆一起使用。",
                FFEditorStyles.Description);

            GUILayout.Space(12);

            // ----------------------------------------------------
            // File Extension
            // ----------------------------------------------------
            EditorGUILayout.LabelField("加密文件后缀名", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            asset.EncryptedExtension =
                EditorGUILayout.TextField("文件后缀名", asset.EncryptedExtension);

            EditorGUILayout.LabelField(
                "框架生成的所有加密数据文件都会使用此后缀，例如：.fink、.dat、.bytes。",
                FFEditorStyles.Description);

            GUILayout.Space(12);

            // ----------------------------------------------------
            // Suggestions / Notes
            // ----------------------------------------------------
            EditorGUILayout.LabelField("使用建议", FFEditorStyles.SectionTitle);
            EditorGUILayout.LabelField(
                "• 开发期可关闭加密加快调试速度。\n" +
                "• 正式项目建议启用加密并配合混淆策略使用。\n" +
                "• 若需要进一步加强安全，可实现数据签名与校验头。",
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
