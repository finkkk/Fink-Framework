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

        /// <summary>
        /// 从 Fink Framework 菜单直接打开 UI 项目配置页面。
        /// </summary>
        [MenuItem("Fink Framework/UI 系统/UI 系统配置", false, 120)]
        public static void Open()
        {
            GlobalSettingsEditorLoader.LoadOrCreate();
            SettingsService.OpenProjectSettings("Project/Fink Framework/UI Settings");
        }

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
            DrawMainCanvasSettings();

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

        private void DrawCustomCanvasSettings()
        {
            GameObject selected = (GameObject)EditorGUILayout.ObjectField(
                "Main Canvas 预制体", asset.CustomMainCanvasPrefab, typeof(GameObject), false);
            if (selected != asset.CustomMainCanvasPrefab)
            {
                if (!selected || PrefabUtility.IsPartOfPrefabAsset(selected)
                    && selected.transform.parent == null && selected.GetComponent<Canvas>())
                    asset.CustomMainCanvasPrefab = selected;
                else
                    Debug.LogError("请选择根对象带有 Canvas 的预制体资产。");
            }

            GameObject prefab = asset.CustomMainCanvasPrefab;
            if (!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab)
                || prefab.transform.parent != null || !prefab.GetComponent<Canvas>())
                EditorGUILayout.HelpBox("必须指定根对象带有 Canvas 的预制体；无效配置会阻止 UI 初始化。", MessageType.Error);
            else
            {
                Canvas prefabCanvas = prefab.GetComponent<Canvas>();
                bool mayOverride = asset.CurrentUIMode == Runtime.Environments.EnvironmentState.UIMode.Auto
                    || prefabCanvas.renderMode != (asset.CurrentUIMode == Runtime.Environments.EnvironmentState.UIMode.WorldSpace
                        ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera);
                if (mayOverride)
                    EditorGUILayout.HelpBox(
                        $"预制体当前渲染模式为 {prefabCanvas.renderMode}；运行时以 Main Surface 渲染模式为准。"
                        + "Auto 会根据 VR 环境选择 World Space 或 Screen Space - Camera。",
                        MessageType.Info);
                foreach (Runtime.UI.UILayer layer in System.Enum.GetValues(typeof(Runtime.UI.UILayer)))
                {
                    int count = 0;
                    foreach (Transform child in prefab.transform)
                    {
                        if (child.name != layer.ToString())
                            continue;
                        count++;
                        if (!(child is RectTransform))
                            EditorGUILayout.HelpBox($"{layer} 层需要使用 RectTransform。", MessageType.Error);
                    }
                    if (count > 1)
                        EditorGUILayout.HelpBox($"存在重复的直接子节点 {layer}，请修正预制体。", MessageType.Error);
                }
            }
            EditorGUILayout.HelpBox(
                "渲染模式始终遵循 Main Surface 配置；缩放、排序等其余设置使用预制体自身参数。"
                + "保留所有子物体，复用或补齐 Bottom / Middle / Top / System，并按层顺序排列。"
                + "Camera 模式绑定框架 UI 相机；World Space 未指定事件相机时尝试使用主相机。",
                MessageType.Info);
        }

        private void DrawMainCanvasSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.LabelField("Main Canvas 基础设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            EditorGUILayout.LabelField(
                "在 UI 系统初始化时应用，仅影响框架默认 Main Canvas；修改后需重新运行。",
                FFEditorStyles.Description);

            GUILayout.Space(6);
            asset.CurrentUIMode = (Runtime.Environments.EnvironmentState.UIMode)
                EditorGUILayout.EnumPopup("Main Surface 渲染模式", asset.CurrentUIMode);
            EditorGUILayout.LabelField(
                "<b>ScreenSpace:</b> 普通 2D UI（适用于大多数项目）\n" +
                "<b>WorldSpace:</b> 仅让框架默认 Main Surface 使用世界空间\n" +
                "<b>Auto:</b> 自动判断当前项目是否为 VR 模式。\n" +
                "若为 VR → Main 使用 WorldSpace；否则使用 ScreenSpace。\n" +
                "普通世界面板请在场景 Canvas 上添加 UISurfaceRoot，不需要切换这里。",
                FFEditorStyles.Description);
            GUILayout.Space(6);

            asset.UseCustomMainCanvas = EditorGUILayout.Toggle("使用自定义 Main Canvas", asset.UseCustomMainCanvas);
            if (asset.UseCustomMainCanvas)
            {
                DrawCustomCanvasSettings();
                DrawResetMainCanvasButton();
                GUILayout.EndVertical();
                return;
            }

            bool worldSpace = asset.CurrentUIMode == Runtime.Environments.EnvironmentState.UIMode.WorldSpace;
            using (new EditorGUI.DisabledScope(worldSpace))
            {
                asset.UICanvasScaleMode = (UnityEngine.UI.CanvasScaler.ScaleMode)
                    EditorGUILayout.EnumPopup("屏幕缩放模式", asset.UICanvasScaleMode);
                switch (asset.UICanvasScaleMode)
                {
                    case UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize:
                        Vector2 resolution = EditorGUILayout.Vector2Field("参考分辨率", asset.UIReferenceResolution);
                        asset.UIReferenceResolution = new Vector2(
                            Mathf.Max(1f, resolution.x), Mathf.Max(1f, resolution.y));
                        asset.UIScreenMatchMode = (UnityEngine.UI.CanvasScaler.ScreenMatchMode)
                            EditorGUILayout.EnumPopup("屏幕适配模式", asset.UIScreenMatchMode);
                        if (asset.UIScreenMatchMode == UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
                            asset.UIMatchWidthOrHeight = EditorGUILayout.Slider(
                                "宽高匹配（0 宽 / 1 高）", asset.UIMatchWidthOrHeight, 0f, 1f);
                        break;
                    case UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize:
                        asset.UIScaleFactor = Mathf.Max(0.01f,
                            EditorGUILayout.FloatField("缩放倍率", asset.UIScaleFactor));
                        break;
                    case UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPhysicalSize:
                        asset.UIPhysicalUnit = (UnityEngine.UI.CanvasScaler.Unit)
                            EditorGUILayout.EnumPopup("物理尺寸单位", asset.UIPhysicalUnit);
                        asset.UIFallbackScreenDPI = Mathf.Max(1f,
                            EditorGUILayout.FloatField("备用屏幕 DPI", asset.UIFallbackScreenDPI));
                        asset.UIDefaultSpriteDPI = Mathf.Max(1f,
                            EditorGUILayout.FloatField("默认 Sprite DPI", asset.UIDefaultSpriteDPI));
                        break;
                }
                asset.UIReferencePixelsPerUnit = Mathf.Max(0.01f,
                    EditorGUILayout.FloatField("参考每单位像素数", asset.UIReferencePixelsPerUnit));
                asset.UICanvasPixelPerfect = EditorGUILayout.Toggle("像素对齐", asset.UICanvasPixelPerfect);
                asset.UICanvasPlaneDistance = Mathf.Max(0.01f,
                    EditorGUILayout.FloatField("距 UI 相机距离", asset.UICanvasPlaneDistance));
            }
            if (worldSpace)
                EditorGUILayout.HelpBox("世界空间模式不应用上述屏幕缩放参数。Auto 在 VR 下同样不应用。", MessageType.Info);

            GUILayout.Space(6);
            SortingLayer[] layers = SortingLayer.layers;
            string[] layerNames = new string[layers.Length];
            int selectedLayer = -1;
            for (int i = 0; i < layers.Length; i++)
            {
                layerNames[i] = layers[i].name;
                if (layers[i].id == asset.UICanvasSortingLayerId)
                    selectedLayer = i;
            }
            int newLayer = EditorGUILayout.Popup("排序层", selectedLayer, layerNames);
            if (newLayer >= 0 && newLayer < layers.Length)
                asset.UICanvasSortingLayerId = layers[newLayer].id;
            if (selectedLayer < 0)
                EditorGUILayout.HelpBox("配置的排序层已不存在，请重新选择。", MessageType.Warning);
            asset.UICanvasSortingOrder = EditorGUILayout.IntField("层内排序", asset.UICanvasSortingOrder);
            EditorGUILayout.LabelField(
                "距 UI 相机距离应位于相机的 Near / Far 裁剪范围内。世界空间 Canvas 的布局保留预制体设置。",
                FFEditorStyles.Description);
            DrawResetMainCanvasButton();
            GUILayout.EndVertical();
        }

        private void DrawResetMainCanvasButton()
        {
            GUILayout.Space(6);
            if (!GUILayout.Button("恢复 Main Canvas 默认设置"))
                return;

            // 从字段初始化值读取默认值，仅复制本板块字段，不重置整个配置资产。
            GlobalSettingsAsset defaults = ScriptableObject.CreateInstance<GlobalSettingsAsset>();
            try
            {
                Undo.RecordObject(asset, "恢复 Main Canvas 默认设置");
                asset.CurrentUIMode = defaults.CurrentUIMode;
                asset.UseCustomMainCanvas = defaults.UseCustomMainCanvas;
                asset.CustomMainCanvasPrefab = defaults.CustomMainCanvasPrefab;
                asset.UICanvasScaleMode = defaults.UICanvasScaleMode;
                asset.UIReferenceResolution = defaults.UIReferenceResolution;
                asset.UIScreenMatchMode = defaults.UIScreenMatchMode;
                asset.UIMatchWidthOrHeight = defaults.UIMatchWidthOrHeight;
                asset.UIScaleFactor = defaults.UIScaleFactor;
                asset.UIReferencePixelsPerUnit = defaults.UIReferencePixelsPerUnit;
                asset.UIPhysicalUnit = defaults.UIPhysicalUnit;
                asset.UIFallbackScreenDPI = defaults.UIFallbackScreenDPI;
                asset.UIDefaultSpriteDPI = defaults.UIDefaultSpriteDPI;
                asset.UICanvasSortingLayerId = defaults.UICanvasSortingLayerId;
                asset.UICanvasSortingOrder = defaults.UICanvasSortingOrder;
                asset.UICanvasPlaneDistance = defaults.UICanvasPlaneDistance;
                asset.UICanvasPixelPerfect = defaults.UICanvasPixelPerfect;
                GUI.changed = true;
            }
            finally
            {
                Object.DestroyImmediate(defaults);
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
