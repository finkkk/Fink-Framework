using FinkFramework.Editor.Common;
using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Runtime.Input;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    /// <summary>全局设备输入检测的项目配置页面。</summary>
    public sealed class InputSettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset asset;

        public InputSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new InputSettingsProvider("Project/Fink Framework/Input System", SettingsScope.Project)
            {
                keywords = new[] { "Fink", "Framework", "Input", "Input System", "Device", "Gamepad", "Mouse" }
            };
        }

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
            FFEditorGUI.Center(() => GUILayout.Label("Input System 配置", FFEditorStyles.Title));
            GUILayout.Space(6);
            EditorGUILayout.LabelField(
                "配置全局设备输入检测。当前主要设备表示最近一次有效操作的来源，而非设备是否已连接。",
                FFEditorStyles.Description);
            GUILayout.Space(10);

            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.LabelField("设备输入检测", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            asset.EnableDeviceDetection = EditorGUILayout.ToggleLeft(
                "启用设备输入检测",
                asset.EnableDeviceDetection);
            EditorGUILayout.LabelField(
                asset.EnableDeviceDetection
                    ? "开启后，框架会识别键鼠、手柄与触摸输入，并通过 DeviceDetectionManager 对外发布变化事件。"
                    : "关闭后，当前设备固定为 Unknown，依赖设备状态的功能会回退到各自的默认行为。",
                FFEditorStyles.Description);

            using (new EditorGUI.DisabledScope(!asset.EnableDeviceDetection))
            {
                GUILayout.Space(8);
                asset.TreatMouseMovementAsKeyboardMouseInput = EditorGUILayout.ToggleLeft(
                    "鼠标移动视为键鼠输入",
                    asset.TreatMouseMovementAsKeyboardMouseInput);

                using (new EditorGUI.DisabledScope(!asset.TreatMouseMovementAsKeyboardMouseInput))
                {
                    asset.MouseMovementDetectionThreshold = EditorGUILayout.Slider(
                        "鼠标移动检测阈值（像素）",
                        Mathf.Max(0.01f, asset.MouseMovementDetectionThreshold),
                        0.01f,
                        50f);
                }

                EditorGUILayout.LabelField(
                    asset.TreatMouseMovementAsKeyboardMouseInput
                        ? "单帧移动距离达到阈值时才会切换为键鼠。默认 2 像素；提高阈值可减少轻微抖动造成的切换。"
                        : "鼠标按键仍会识别为键鼠；仅移动鼠标不会改变当前主要设备。",
                    FFEditorStyles.Description);
            }

            GUILayout.Space(8);
            asset.InputConflictMode = (InputConflictMode)EditorGUILayout.EnumPopup(
                "绑定冲突处理模式",
                asset.InputConflictMode);
            EditorGUILayout.LabelField(
                asset.InputConflictMode == InputConflictMode.Warning
                    ? "允许重复绑定，但绑定信息会标记冲突，设置界面可以将其标红提示。"
                    : "检测到重复绑定时拒绝本次绑定。",
                FFEditorStyles.Description);

            GUILayout.EndVertical();
            GUILayout.Space(20);
            FFEditorGUI.Center(() => GUILayout.Label("Copyright © 2025 Fink Framework", FFEditorStyles.Footer));

            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
