using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    public class ResBackendSettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset asset;
        private UnityEditor.Editor backendSettingsEditor;
        private EnvironmentState.ResourceBackendType lastBackend;
        private ScriptableObject currentBackendSettings;

        public ResBackendSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new ResBackendSettingsProvider(
                "Project/Fink Framework/Resource Backend",
                SettingsScope.Project)
            {
                keywords = new[]
                {
                    "Fink",
                    "Resource",
                    "Backend",
                    "AssetBundle",
                    "Addressables",
                    "Hotfix"
                }
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            lastBackend = (EnvironmentState.ResourceBackendType)(-1);
            currentBackendSettings = null;
            asset = GlobalSettingsEditorLoader.LoadOrCreate();
        }

        public override void OnGUI(string searchContext)
        {
            if (!asset)
            {
                EditorGUILayout.HelpBox(
                    "GlobalSettingsAsset 缺失，请重新导入框架或点击按钮自动修复。",
                    MessageType.Error);

                if (GUILayout.Button("重新创建配置文件"))
                {
                    asset = GlobalSettingsEditorLoader.LoadOrCreate();
                }

                return;
            }

            GUILayout.Space(10);
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("资源后端配置 (Resource Backend Settings)", FFEditorStyles.Title);
            });

            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "用于配置项目所使用的构建型资源后端系统。\n" +
                "仅当项目需要使用 AssetBundle、Addressables 或自定义构建资源系统时才需要配置。",
                FFEditorStyles.Description);

            GUILayout.Space(12);

            // ===== 主区域 =====
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            EditorGUILayout.LabelField("资源构建型后端", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.ResourceBackend =
                (EnvironmentState.ResourceBackendType)
                EditorGUILayout.EnumPopup("资源后端类型", asset.ResourceBackend);

            DrawBackendDescription(asset.ResourceBackend);

            GUILayout.Space(14);
            
            var backendSettings = GetCurrentBackendSettings();

            if (asset.ResourceBackend != lastBackend || backendSettings != currentBackendSettings)
            {
                RefreshBackendEditor(backendSettings);
                lastBackend = asset.ResourceBackend;
                currentBackendSettings = backendSettings;
            }
            
            if (asset.ResourceBackend == EnvironmentState.ResourceBackendType.Custom
                && backendSettingsEditor == null)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "当前为自定义资源后端，请自行开发 ScriptableObject 作为自定义资源后端初始化的数据。(需要搭配开发自定义资源加载插件provider, 详情可见框架文档)",
                    MessageType.Info);
            }
            else if (backendSettingsEditor != null)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("后端详细配置", FFEditorStyles.SectionTitle);
                GUILayout.Space(6);

                backendSettingsEditor.OnInspectorGUI();
            }

            GUILayout.EndVertical();

            GUILayout.Space(20);

            // ===== 页脚 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright © 2025 Fink Framework", FFEditorStyles.Footer);
            });

            // 保存修改
            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
            }
        }

        private static void DrawBackendDescription(EnvironmentState.ResourceBackendType backend)
        {
            string desc = backend switch
            {
                EnvironmentState.ResourceBackendType.None => "当前状态：未启用任何构建型资源后端。\n" +
                                                             "项目将仅使用 Resources / File / Http 等即时型资源加载方式。\n" +
                                                             "适用于小型项目或无需热更新的场景。",
                EnvironmentState.ResourceBackendType.AssetBundle => "当前状态：使用 AssetBundle 作为构建型资源后端。\n" +
                                                                    "该模式需要提前构建 AB 包，并在运行时进行初始化。\n" +
                                                                    "此方案为传统方式，不再作为新项目的首选方案。",
                EnvironmentState.ResourceBackendType.Addressables => "当前状态：使用 Addressables 作为构建型资源后端（推荐）。\n" +
                                                                     "该模式由 Unity 官方维护，支持远程资源、版本管理与依赖处理。\n" +
                                                                     "适用于中大型项目与长期维护项目。",
                EnvironmentState.ResourceBackendType.Custom => "当前状态：使用自定义构建型资源后端。\n" +
                                                               "适用于接入 YooAsset 或完全自研的资源构建与加载系统。\n" +
                                                               "框架仅提供接入点，不干预具体实现。",
                _ => string.Empty
            };

            EditorGUILayout.LabelField(desc, FFEditorStyles.Description);
        }
        
        private ScriptableObject GetCurrentBackendSettings()
        {
            return asset.ResourceBackend switch
            {
                EnvironmentState.ResourceBackendType.AssetBundle =>
                    ResBackendSettingsEditorLoader.GetOrCreateAssetBundleSettings(asset),

                EnvironmentState.ResourceBackendType.Addressables =>
                    ResBackendSettingsEditorLoader.GetOrCreateAddressablesSettings(asset),

                EnvironmentState.ResourceBackendType.Custom =>
                    asset.CustomBackendSettings,

                _ => null
            };
        }
        
        private void RefreshBackendEditor(Object settingsAsset)
        {
            if (backendSettingsEditor)
            {
                Object.DestroyImmediate(backendSettingsEditor);
                backendSettingsEditor = null;
            }

            if (settingsAsset)
            {
                backendSettingsEditor = UnityEditor.Editor.CreateEditor(settingsAsset);
            }
        }
        
        public override void OnDeactivate()
        {
            if (backendSettingsEditor)
            {
                Object.DestroyImmediate(backendSettingsEditor);
                backendSettingsEditor = null;
            }
        }
    }
}
