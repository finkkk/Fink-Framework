using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Editor.Common;
using FinkFramework.Editor.Modules.Localization;
using FinkFramework.Runtime.Localization;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    public class FrameworkSettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset asset;
        private LocalizationSettingsAsset localizationAsset;
        private SerializedObject localizationSerializedAsset;
        private string scriptRootDirectoryDraft;

        public FrameworkSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new FrameworkSettingsProvider("Project/Fink Framework/00 Framework", SettingsScope.Project)
            {
                label = "Framework",
                keywords = new[] { "Fink", "Framework", "Framework", "Setting" }
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            asset = GlobalSettingsEditorLoader.LoadOrCreate();
            localizationAsset = LocalizationSettingsEditorLoader.LoadOrCreate();
            localizationSerializedAsset = localizationAsset != null
                ? new SerializedObject(localizationAsset)
                : null;
            scriptRootDirectoryDraft = GlobalSettingsAsset.NormalizeScriptRootDirectory(
                asset.ScriptRootDirectory);
        }

        public override void OnGUI(string searchContext)
        {
            if (!asset)
            {
                EditorGUILayout.HelpBox("GlobalSettingsAsset 缺失，请重新导入框架或点击按钮自动修复。", MessageType.Error);
                if (GUILayout.Button("重新创建配置文件"))
                {
                    asset = GlobalSettingsEditorLoader.LoadOrCreate();
                }
                return;
            }

            GUILayout.Space(10);

              // ===== 标题 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("框架配置 (Framework Settings)", FFEditorStyles.Title);
            });

            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "控制框架的全局设置，包括 XR、输入系统、渲染管线和版本更新等基础框架设置。",
                FFEditorStyles.Description);
            GUILayout.Space(12);

            DrawScriptRootDirectorySettings();
            GUILayout.Space(12);

            DrawModuleSwitchSettings();
            GUILayout.Space(12);

            DrawFrameworkUpdateSettings();
            GUILayout.Space(12);

            DrawOptionalDependencySettings();
            GUILayout.Space(12);

            DrawBuildSettings();

            GUILayout.Space(20);

            // ===== 页脚 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright \u00A9 2025 Fink Framework", FFEditorStyles.Footer);
            });

            // 保存并同步
            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// 绘制所有脚本生成器共用的 Assets 下脚本根目录。
        /// Assets/ 前缀固定显示，不允许被配置改掉。
        /// </summary>
        private void DrawScriptRootDirectorySettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.LabelField("代码生成设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("全局脚本根目录", GUILayout.Width(110f));
            EditorGUILayout.LabelField("Assets/", GUILayout.Width(48f));
            scriptRootDirectoryDraft = EditorGUILayout.TextField(scriptRootDirectoryDraft);
            EditorGUILayout.EndHorizontal();

            if (GlobalSettingsAsset.TryNormalizeScriptRootDirectory(
                    scriptRootDirectoryDraft,
                    out string normalized,
                    out string error))
            {
                if (!string.Equals(asset.ScriptRootDirectory, normalized, System.StringComparison.Ordinal))
                {
                    asset.ScriptRootDirectory = normalized;
                    GUI.changed = true;
                }

                EditorGUILayout.LabelField(
                    $"所有框架生成脚本的默认根目录：Assets/{normalized}",
                    FFEditorStyles.Description);
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 集中显示各模块的总开关。具体模块仍由自己的配置资产保存状态，
        /// 这里只提供统一入口，避免用户在多个 Project Settings 页面中查找。
        /// </summary>
        private void DrawModuleSwitchSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "模块开关设置",
                "控制各功能模块是否在运行时启用。关闭模块后，对应运行时服务不会初始化或加载关联资源。");
            GUILayout.Space(6);

            asset.EnableAudioModule = EditorGUILayout.Toggle(
                new GUIContent(
                    "启用音频模块",
                    "关闭后不会初始化 AudioManager、加载音频资源或播放音乐和音效。"),
                asset.EnableAudioModule);
            EditorGUILayout.LabelField(
                "关闭后，框架不会初始化 AudioManager，不加载音频资源，也不会播放任何音乐或音效。",
                FFEditorStyles.Description);

            GUILayout.Space(8);

            if (localizationAsset == null || localizationSerializedAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "LocalizationSettingsAsset 缺失，无法编辑本地化模块开关。",
                    MessageType.Error);
            }
            else
            {
                localizationSerializedAsset.Update();
                SerializedProperty enabledProperty =
                    localizationSerializedAsset.FindProperty("enableLocalization");
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUILayout.Toggle(
                    new GUIContent(
                        "启用本地化模块",
                        "关闭后，本地化运行时不会初始化或加载语言文件。"),
                    enabledProperty.boolValue);
                if (EditorGUI.EndChangeCheck())
                {
                    enabledProperty.boolValue = enabled;
                    localizationSerializedAsset.ApplyModifiedProperties();
                    EditorUtility.SetDirty(localizationAsset);
                    AssetDatabase.SaveAssets();
                }
            }

            EditorGUILayout.LabelField(
                "关闭后，本地化运行时不会初始化或加载语言文件。",
                FFEditorStyles.Description);
            GUILayout.EndVertical();
        }

        /// <summary>绘制框架更新检查的配置。</summary>
        private void DrawFrameworkUpdateSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "框架更新设置",
                "控制编辑器是否检查 Fink Framework 的新版本。");
            GUILayout.Space(6);

            asset.EnableUpdateCheck =
                EditorGUILayout.Toggle("启用版本更新检查", asset.EnableUpdateCheck);
            EditorGUILayout.LabelField(
                "启用后，编辑器会自动从 GitHub 检查框架新版本；关闭后不再提示。",
                FFEditorStyles.Description);

            GUILayout.Space(8);
            asset.UpdateCheckIntervalDays =
                EditorGUILayout.IntSlider("检查间隔（天）", asset.UpdateCheckIntervalDays, 1, 30);
            EditorGUILayout.LabelField(
                "设置编辑器多久执行一次更新检查。（默认 1 天）",
                FFEditorStyles.Description);
            GUILayout.EndVertical();
        }

        /// <summary>绘制项目已安装可选依赖的强制关闭开关。</summary>
        private void DrawOptionalDependencySettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "可选依赖设置",
                "即使项目已安装对应包，也可以让框架忽略 XR、Input System 或 URP。");
            GUILayout.Space(6);

            asset.ForceDisableXR =
                EditorGUILayout.Toggle("强制关闭 XR", asset.ForceDisableXR);
            EditorGUILayout.LabelField(
                "启用后，即使项目安装了 XR 插件（XRI），框架也会按非 VR 模式运行，所有 VR 相关功能失效。",
                FFEditorStyles.Description);

            GUILayout.Space(8);
            asset.ForceDisableNewInputSystem =
                EditorGUILayout.Toggle("强制关闭新输入系统", asset.ForceDisableNewInputSystem);
            EditorGUILayout.LabelField(
                "启用后，即使项目安装了 Input System，也会强制使用框架内关于旧输入系统（Input Manager）的逻辑。",
                FFEditorStyles.Description);

            GUILayout.Space(8);
            asset.ForceDisableURP =
                EditorGUILayout.Toggle("强制关闭 URP", asset.ForceDisableURP);
            EditorGUILayout.LabelField(
                "启用后，框架将按非 URP 环境运行，即使项目当前使用 URP 渲染管线。",
                FFEditorStyles.Description);
            GUILayout.EndVertical();
        }

        /// <summary>绘制 Player 构建前的安全检查配置。</summary>
        private void DrawBuildSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "构建设置",
                "控制构建 Player 前执行的框架安全检查。");
            GUILayout.Space(6);

            asset.EnableEditorUrlCheck =
                EditorGUILayout.Toggle("启用编辑器加载打包检测", asset.EnableEditorUrlCheck);
            EditorGUILayout.LabelField(
                "构建前自动扫描代码；发现 editor:// 路径引用时阻止打包，避免运行时无法加载资源。",
                FFEditorStyles.Description);
            GUILayout.EndVertical();
        }
    }
}
