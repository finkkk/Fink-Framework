using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Editor.Common;
using FinkFramework.Editor.Modules.UI;
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
        private bool prefabDraftUsesResources;

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
                keywords = new[] { "Fink", "Framework", "UI", "Settings", "Canvas", "Resources", "Addressables" }
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
            DrawPanelLoadingSettings();

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
            prefabDraftUsesResources = asset.UIPanelUsesResources;
            prefabOutputPathDraft = prefabDraftUsesResources
                ? asset.UIPanelPrefabOutputRelativePath
                : asset.UIPanelExternalPrefabOutputRelativePath;
        }

        private void DrawPanelLoadingSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.LabelField("面板资源加载", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            EditorGUI.BeginChangeCheck();
            int source = EditorGUILayout.Popup("加载来源", (int)asset.UIPanelLoadSource,
                new[] { "Resources", "跟随资源后端", "自定义" });
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "修改 UI 面板加载来源");
                asset.UIPanelLoadSource = (Runtime.UI.UIPanelLoadSource)source;
            }

            switch (asset.UIPanelLoadSource)
            {
                case Runtime.UI.UIPanelLoadSource.Resources:
                    EditorGUILayout.HelpBox("从下方面板生成目录对应的 Resources 路径加载，保持原有行为。", MessageType.Info);
                    break;
                case Runtime.UI.UIPanelLoadSource.FollowResourceBackend:
                    EditorGUILayout.LabelField("当前资源后端", asset.ResourceBackend.ToString());
                    switch (asset.ResourceBackend)
                    {
                        case Runtime.Environments.EnvironmentState.ResourceBackendType.None:
                            EditorGUILayout.HelpBox("未启用构建型资源后端，面板继续从 Resources 加载。", MessageType.Info);
                            break;
                        case Runtime.Environments.EnvironmentState.ResourceBackendType.Addressables:
                            asset.UIPanelAddressablesGroup = EditorGUILayout.TextField("UI Addressables Group", asset.UIPanelAddressablesGroup);
                            EditorGUILayout.HelpBox(
                                "生成器会自动加入指定 Group（不存在时创建），Address 使用唯一面板类名（区分大小写）。"
                                + "使用 OpenAsync<T>() 打开；同步 Open<T>() 需要先完成相同 Surface / InstanceId 的预加载。"
                                + "默认使用 PreloadAsync<T>()；资源后端开启 Allow Sync Load 后也可使用同步 Preload<T>()。"
                                + "已有面板不会自动迁移或修改 Address。", MessageType.Info);
                            break;
                        case Runtime.Environments.EnvironmentState.ResourceBackendType.AssetBundle:
                            asset.UIPanelAssetBundleName = EditorGUILayout.TextField("UI AssetBundle 包名", asset.UIPanelAssetBundleName);
                            string bundle = asset.UIPanelAssetBundleName?.Trim();
                            if (string.IsNullOrWhiteSpace(bundle) || bundle.Contains("/") || bundle.Contains("\\"))
                                EditorGUILayout.HelpBox("包名不能为空或包含斜杠。", MessageType.Error);
                            EditorGUILayout.HelpBox("生成器会自动设置小写包名，使用 ab://包名/面板类名。资源包需另行构建。", MessageType.Info);
                            break;
                        case Runtime.Environments.EnvironmentState.ResourceBackendType.Custom:
                            DrawCustomPanelLoadingHelp();
                            break;
                    }
                    break;
                case Runtime.UI.UIPanelLoadSource.Custom:
                    DrawCustomPanelLoadingHelp();
                    break;
            }

            try
            {
                UIPanelBuildRegistration.Capture(asset).Validate("", "");
            }
            catch (System.Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }

            GUILayout.EndVertical();
        }

        private static void DrawCustomPanelLoadingHelp()
        {
            EditorGUILayout.HelpBox(
                "启动时设置 UIManager.Instance.CustomPanelAssetPathResolver，按面板类型返回完整资源地址；"
                + "对应协议的 Provider 通过 ResManager.AddProvider 注册。生成器只创建脚本和预制体，资源注册由项目自行处理。"
                + "业务仍使用 OpenAsync<T>()。", MessageType.Info);
        }

        private void DrawPanelPathSettings()
        {
            if (scriptOutputPathDraft == null || prefabOutputPathDraft == null
                || prefabDraftUsesResources != asset.UIPanelUsesResources)
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
                asset.UIPanelUsesResources ? "Resources 预制体目录" : "非 Resources 预制体目录",
                "Assets/",
                ref prefabOutputPathDraft);
            string normalizedPrefabPath;
            string prefabPathError;
            bool validPrefabPath = asset.UIPanelUsesResources
                ? GlobalSettingsAsset.TryNormalizeUIPanelPrefabOutputRelativePath(
                    prefabOutputPathDraft, out normalizedPrefabPath, out prefabPathError)
                : GlobalSettingsAsset.TryNormalizeUIPanelExternalPrefabOutputRelativePath(
                    prefabOutputPathDraft, out normalizedPrefabPath, out prefabPathError);
            if (validPrefabPath)
            {
                string currentPath = asset.UIPanelUsesResources
                    ? asset.UIPanelPrefabOutputRelativePath : asset.UIPanelExternalPrefabOutputRelativePath;
                if (!string.Equals(
                        currentPath,
                        normalizedPrefabPath,
                        System.StringComparison.Ordinal))
                {
                    if (asset.UIPanelUsesResources)
                        asset.UIPanelPrefabOutputRelativePath = normalizedPrefabPath;
                    else
                        asset.UIPanelExternalPrefabOutputRelativePath = normalizedPrefabPath;
                    GUI.changed = true;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(prefabPathError, MessageType.Error);
            }

            GUILayout.Space(6);
            EditorGUILayout.LabelField(
                "代码路径前缀跟随全局脚本根目录。Resources 与非 Resources 目录分别保存，切换来源不会移动已有面板。"
                + (asset.UIPanelUsesResources ? "当前目录必须位于 Resources 下。" : "当前目录不能位于 Resources、Editor 或 StreamingAssets 下。")
                + "预制体名称必须与对应面板脚本类名完全一致。",
                FFEditorStyles.Description);

            if (GUILayout.Button("恢复默认路径"))
            {
                scriptOutputPathDraft = GlobalSettingsAsset.DefaultUIPanelScriptOutputSuffix;
                prefabOutputPathDraft = asset.UIPanelUsesResources
                    ? GlobalSettingsAsset.DefaultUIPanelPrefabOutputRelativePath
                    : GlobalSettingsAsset.DefaultUIPanelExternalPrefabOutputRelativePath;
                asset.UIPanelScriptOutputSuffix = scriptOutputPathDraft;
                if (asset.UIPanelUsesResources)
                    asset.UIPanelPrefabOutputRelativePath = prefabOutputPathDraft;
                else
                    asset.UIPanelExternalPrefabOutputRelativePath = prefabOutputPathDraft;
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
