using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Settings.Global
{
    public class GlobalSettingsProvider : SettingsProvider
    {
        private const string AssetPath = "Assets/FinkFramework/Settings/Global/GlobalSettingsAsset.asset";

        private static GlobalSettingsAsset asset;

        public GlobalSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            LoadAsset();

            if (asset == null)
            {
                CreateAsset();
                LoadAsset();
            }

            return new GlobalSettingsProvider("Project/Fink Framework", SettingsScope.Project)
            {
                keywords = new[] { "Fink", "Framework", "Global" }
            };
        }

        public override void OnGUI(string searchContext)
        {
            if (!asset)
            {
                EditorGUILayout.HelpBox("缺失配置文件，请重新创建。", MessageType.Error);
                if (GUILayout.Button("创建配置文件"))
                {
                    CreateAsset();
                    LoadAsset();
                }
                return;
            }
            
            GUILayout.Space(10);

            // ===== 标题 =====
            EditorGUILayout.LabelField("Fink Framework 全局设置", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Fink Framework Global Settings", EditorStyles.label);
            GUILayout.Space(10);

            DrawSectionTitle("环境相关 (Environment)");
            DrawToggle("强制关闭 XR", "Force Disable XR", ref asset.ForceDisableXR);
            DrawToggle("强制关闭 新输入系统", "Force Disable InputSystem", ref asset.ForceDisableNewInputSystem);
            DrawToggle("强制关闭 URP", "Force Disable URP", ref asset.ForceDisableURP);

            GUILayout.Space(12);

            DrawSectionTitle("UI 设置 (UI Settings)");
            DrawEnum("UI 模式", "UI Mode", ref asset.CurrentUIMode);

            GUILayout.Space(12);

            DrawSectionTitle("加密设置 (Encryption)");
            DrawPassword("AES 加密密码", "AES Password", ref asset.Password);
            DrawText("加密文件后缀名", "Encrypted Extension", ref asset.EncryptedExtension);

            GUILayout.Space(20);

            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                asset.ApplyToRuntime();
            }
        }

        // ===== 绘制辅助方法 =====

        private void DrawSectionTitle(string title)
        {
            GUILayout.Space(5);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.Space(3);
        }
        
        private void DrawLabel(string text)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = false
            };

            float width = style.CalcSize(new GUIContent(text)).x + 4;

            EditorGUILayout.LabelField(text, style, GUILayout.Width(width));
        }

        private void DrawToggle(string cn, string en, ref bool value)
        {
            EditorGUILayout.BeginHorizontal();

            // 自动适配宽度，永远不会挤压
            DrawLabel($"{cn} ({en})");

            GUILayout.FlexibleSpace();
            value = EditorGUILayout.Toggle(value, GUILayout.Width(20));

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawEnum<T>(string cn, string en, ref T value) where T : struct, System.Enum
        {
            EditorGUILayout.BeginHorizontal();

            DrawLabel($"{cn} ({en})");

            GUILayout.FlexibleSpace();
            value = (T)EditorGUILayout.EnumPopup((System.Enum)(object)value, GUILayout.Width(120));

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawPassword(string cn, string en, ref string value)
        {
            EditorGUILayout.BeginHorizontal();

            DrawLabel($"{cn} ({en})");

            GUILayout.FlexibleSpace();
            value = EditorGUILayout.PasswordField(value, GUILayout.Width(200));

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawText(string cn, string en, ref string value)
        {
            EditorGUILayout.BeginHorizontal();

            DrawLabel($"{cn} ({en})");

            GUILayout.FlexibleSpace();
            value = EditorGUILayout.TextField(value, GUILayout.Width(200));

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        // ===== Asset逻辑 =====
        private static void LoadAsset()
        {
            asset = AssetDatabase.LoadAssetAtPath<GlobalSettingsAsset>(AssetPath);
        }

        private static void CreateAsset()
        {
            var newAsset = ScriptableObject.CreateInstance<GlobalSettingsAsset>();
            AssetDatabase.CreateAsset(newAsset, AssetPath);
            AssetDatabase.SaveAssets();
        }
    }
}
