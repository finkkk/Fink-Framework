using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Editor.Common;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    public class UISettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset asset;
        private string scriptOutputPathDraft;
        private string prefabOutputPathDraft;

        public UISettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new UISettingsProvider("Project/Fink Framework/UI Settings", SettingsScope.Project)
            {
                keywords = new[] { "Fink", "Framework", "UI", "Settings", "Canvas" }
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            asset = GlobalSettingsEditorLoader.LoadOrCreate();
            SyncPathDrafts();
        }

        public override void OnGUI(string searchContext)
        {
            if (!asset)
            {
                EditorGUILayout.HelpBox("GlobalSettingsAsset 缺失，请重新导入框架或点击按钮自动修复。", MessageType.Error);
                if (GUILayout.Button("重新创建配置文件"))
                {
                    asset = GlobalSettingsEditorLoader.LoadOrCreate();
                    SyncPathDrafts();
                }
                return;
            }

            GUILayout.Space(10);

            // ===== 标题 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("UI 配置", FFEditorStyles.Title);
            });

            GUILayout.Space(6);
            EditorGUILayout.LabelField(
                "用于配置框架默认 Main Surface 和 UI 面板生成路径。",
                FFEditorStyles.Description);
            GUILayout.Space(10);

            // ===== 主区域 =====
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            EditorGUILayout.LabelField("Main Surface 渲染设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.CurrentUIMode = (Runtime.Environments.EnvironmentState.UIMode)EditorGUILayout.EnumPopup("Main Surface 渲染模式", asset.CurrentUIMode);
            EditorGUILayout.LabelField(
                "<b>ScreenSpace:</b> 普通 2D UI（适用于大多数项目）\n" +
                "<b>WorldSpace:</b> 仅让框架默认 Main Surface 使用世界空间\n" +
                "<b>Auto:</b> 自动判断当前项目是否为 VR 模式。\n" +
                "若为 VR → Main 使用 WorldSpace；否则使用 ScreenSpace。\n" +
                "普通世界面板请在场景 Canvas 上添加 UISurfaceRoot，不需要切换这里。",
                FFEditorStyles.Description);

            GUILayout.EndVertical();

            GUILayout.Space(12);
            DrawNavigationInteractionSettings();

            GUILayout.Space(12);
            DrawPanelPathSettings();

            GUILayout.Space(20);

            // ===== 页脚 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright \u00A9 2025 Fink Framework", FFEditorStyles.Footer);
            });

            // 保存
            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }

        private void SyncPathDrafts()
        {
            if (!asset)
                return;

            scriptOutputPathDraft = GlobalSettingsAsset.NormalizeUIPanelScriptOutputSuffix(
                asset.UIPanelScriptOutputSuffix,
                asset.ScriptRootDirectory);
            prefabOutputPathDraft = GlobalSettingsAsset.NormalizeUIPanelPrefabOutputRelativePath(
                asset.UIPanelPrefabOutputRelativePath);
        }

        private void DrawPanelPathSettings()
        {
            if (scriptOutputPathDraft == null || prefabOutputPathDraft == null)
                SyncPathDrafts();

            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.LabelField("面板生成路径", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            string scriptRoot = GlobalSettingsAsset.NormalizeScriptRootDirectory(
                asset.ScriptRootDirectory);
            DrawPathField(
                "代码文件输出路径",
                "全局脚本根目录/",
                ref scriptOutputPathDraft);
            if (GlobalSettingsAsset.TryNormalizeUIPanelScriptOutputSuffix(
                    scriptOutputPathDraft,
                    scriptRoot,
                    out string normalizedScriptPath,
                    out string scriptPathError))
            {
                if (!string.Equals(
                        asset.UIPanelScriptOutputSuffix,
                        normalizedScriptPath,
                        System.StringComparison.Ordinal))
                {
                    asset.UIPanelScriptOutputSuffix = normalizedScriptPath;
                    GUI.changed = true;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(scriptPathError, MessageType.Error);
            }

            GUILayout.Space(4);
            DrawPathField(
                "UI 预制体输出路径",
                "Assets/",
                ref prefabOutputPathDraft);
            if (GlobalSettingsAsset.TryNormalizeUIPanelPrefabOutputRelativePath(
                    prefabOutputPathDraft,
                    out string normalizedPrefabPath,
                    out string prefabPathError))
            {
                if (!string.Equals(
                        asset.UIPanelPrefabOutputRelativePath,
                        normalizedPrefabPath,
                        System.StringComparison.Ordinal))
                {
                    asset.UIPanelPrefabOutputRelativePath = normalizedPrefabPath;
                    GUI.changed = true;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(prefabPathError, MessageType.Error);
            }

            GUILayout.Space(6);
            EditorGUILayout.LabelField(
                "代码路径前缀跟随全局脚本根目录；预制体目录必须位于 Resources 下。"
                + "UIManager 的公共打开与预加载接口会自动从该目录加载面板。"
                + "预制体名称必须与对应面板脚本类名完全一致。",
                FFEditorStyles.Description);

            if (GUILayout.Button("恢复默认路径"))
            {
                scriptOutputPathDraft = GlobalSettingsAsset.DefaultUIPanelScriptOutputSuffix;
                prefabOutputPathDraft = GlobalSettingsAsset.DefaultUIPanelPrefabOutputRelativePath;
                asset.UIPanelScriptOutputSuffix = scriptOutputPathDraft;
                asset.UIPanelPrefabOutputRelativePath = prefabOutputPathDraft;
                GUI.changed = true;
            }

            GUILayout.EndVertical();
        }

        private void DrawNavigationInteractionSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.LabelField("输入与导航", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            asset.EnableAutoNavigationInteractionByInputDevice = EditorGUILayout.ToggleLeft(
                "按输入设备自动管理导航交互",
                asset.EnableAutoNavigationInteractionByInputDevice);

            if (asset.EnableAutoNavigationInteractionByInputDevice && !asset.EnableDeviceDetection)
            {
                EditorGUILayout.HelpBox(
                    "设备输入检测当前已在 Project Settings > Fink Framework > Input 中关闭。"
                    + "自动导航控制不会生效，键盘与手柄导航将保持开启。",
                    MessageType.Warning);
            }

            EditorGUILayout.LabelField(
                asset.EnableAutoNavigationInteractionByInputDevice
                    ? asset.EnableDeviceDetection
                        ? "前提：Project Settings > Fink Framework > Input 中的设备输入检测必须开启。"
                          + "预设策略：PC 键鼠输入时关闭键盘/手柄导航交互，手柄输入时重新开启。"
                          + "当前设备会以最近一次有效操作为准，而非设备是否已连接。"
                        : "设备检测已关闭，因此此项暂不生效；导航交互保持开启。"
                    : "已关闭自动判断。键盘与手柄导航交互始终保持开启，维持传统 UGUI 行为。",
                FFEditorStyles.Description);
            GUILayout.EndVertical();
        }

        private static void DrawPathField(string label, string fixedPrefix, ref string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(180f));
            GUILayout.Label(fixedPrefix, GUILayout.ExpandWidth(false));
            value = EditorGUILayout.TextField(value);
            EditorGUILayout.EndHorizontal();
        }
    }
}
