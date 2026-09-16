using System;
using System.Collections.Generic;
using System.IO;
using FinkFramework.Editor.Common;
using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.UI.Base;
using FinkFramework.Runtime.UI.Layout;
using FinkFramework.Runtime.UI.Transitions;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FinkFramework.Editor.Modules.UI
{
    /// <summary>
    /// 创建 UI 面板
    /// </summary>
    public class UIBuilderWindow : EditorWindow
    {
        private const float DefaultWindowWidth = 540f;
        private const float MinimumWindowHeight = 460f;
        private const float InitialWindowHeightPadding = 30f;

        // 这些值只用于跨脚本重编译传递一次性的生成任务，不属于项目配置。
        private const string PendingPanelNameKey = "FinkFramework.UIBuilder.Pending.PanelName";
        private const string PendingPrefabPathKey = "FinkFramework.UIBuilder.Pending.PrefabPath";
        private const string PendingScriptPathKey = "FinkFramework.UIBuilder.Pending.ScriptPath";
        private const string PendingUseTMPKey = "FinkFramework.UIBuilder.Pending.UseTMP";
        private const string PendingAddButtonKey = "FinkFramework.UIBuilder.Pending.AddButton";
        private const string PendingAddInputKey = "FinkFramework.UIBuilder.Pending.AddInput";
        private const string PendingAddToggleKey = "FinkFramework.UIBuilder.Pending.AddToggle";
        private const string PendingAddSliderKey = "FinkFramework.UIBuilder.Pending.AddSlider";
        private const string PendingAddTransitionKey = "FinkFramework.UIBuilder.Pending.AddTransition";
        private const string PendingAddSafeAreaKey = "FinkFramework.UIBuilder.Pending.AddSafeArea";

        private string panelName = "NewPanel";
        private string scriptPath;
        private string prefabPath;
        private bool useTMP = true;
        private bool addExampleButton = false;
        private bool addExampleInput = false;
        private bool addExampleToggle = false;
        private bool addExampleSlider = false;
        private bool addDefaultTransition = true;
        private bool addSafeArea = true;
        private Vector2 contentScrollPosition;
        [SerializeField] private bool initialSizeConfigured;
        private bool initialResizeScheduled;
        private float measuredContentHeight;

        /// <summary>
        /// 在 Unity 完成 EditorWindow 实例化后初始化依赖项目资源的默认值。
        /// 如果全局配置已被删除，Editor loader 会先恢复默认配置资产。
        /// </summary>
        private void OnEnable()
        {
            if (!TrySyncConfiguredPaths(out _))
            {
                scriptPath = GlobalSettingsAsset.GetUIPanelScriptOutputPath(
                    GlobalSettingsAsset.DefaultScriptRootDirectory,
                    GlobalSettingsAsset.DefaultUIPanelScriptOutputSuffix);
                prefabPath = GlobalSettingsAsset.GetUIPanelPrefabOutputPath(
                    GlobalSettingsAsset.DefaultUIPanelPrefabOutputRelativePath);
            }
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= ApplyInitialWindowSize;
        }

        private void OnFocus()
        {
            TrySyncConfiguredPaths(out _);
        }

        [MenuItem("Fink Framework/UI 系统/创建 UI 面板", false, 100)]
        public static void Open()
        {
            var window = GetWindow<UIBuilderWindow>("创建 UI 面板");
            window.minSize = new Vector2(DefaultWindowWidth, MinimumWindowHeight);
            if (!window.initialSizeConfigured)
                window.Repaint();
        }

        #region 编辑器面板绘制
        private void OnGUI()
        {
            contentScrollPosition = EditorGUILayout.BeginScrollView(
                contentScrollPosition,
                GUILayout.ExpandHeight(true));
            FFEditorGUI.BeginWindowContent();
            GUILayout.Space(10f);

            FFEditorGUI.Center(() => GUILayout.Label("创建 UI 面板", FFEditorStyles.Title));
            FFEditorGUI.Center(() => GUILayout.Label(
                "生成可直接由 UIManager 打开的面板脚本与预制体",
                EditorStyles.centeredGreyMiniLabel));
            GUILayout.Space(8f);
            FFEditorGUI.Separator();
            GUILayout.Space(FFEditorStyles.SectionSpacing);

            DrawInputs();

            GUILayout.Space(FFEditorStyles.SectionSpacing);
            DrawOutputPreview();
            GUILayout.Space(FFEditorStyles.SectionSpacing);
            DrawUsageTips();
            GUILayout.Space(14f);
            FFEditorGUI.Separator();
            GUILayout.Space(14f);

            DrawCreateButton();
            GUILayout.Space(12f);
            FFEditorGUI.DrawFrameworkFooter(4f);

            ScheduleInitialResize();
            FFEditorGUI.EndWindowContent();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 首次绘制完成后，根据实际 IMGUI 布局结果调整窗口高度。
        /// 这样描述文本换行变化时仍能得到不带滚动条的初始尺寸。
        /// </summary>
        private void ScheduleInitialResize()
        {
            if (initialSizeConfigured
                || initialResizeScheduled
                || Event.current == null
                || Event.current.type != EventType.Repaint)
                return;

            Rect lastContentRect = GUILayoutUtility.GetLastRect();
            if (lastContentRect.height <= 0f)
                return;

            measuredContentHeight = lastContentRect.yMax;
            initialResizeScheduled = true;
            EditorApplication.delayCall += ApplyInitialWindowSize;
        }

        /// <summary>
        /// 在 IMGUI 当前帧结束后应用测量结果，避免在 OnGUI 内直接修改窗口几何尺寸。
        /// </summary>
        private void ApplyInitialWindowSize()
        {
            initialResizeScheduled = false;
            if (this == null || initialSizeConfigured || measuredContentHeight <= 0f)
                return;

            Rect currentPosition = position;
            float desiredHeight = Mathf.Max(
                MinimumWindowHeight,
                Mathf.Ceil(measuredContentHeight + InitialWindowHeightPadding));
            position = new Rect(
                currentPosition.x,
                currentPosition.y,
                Mathf.Max(DefaultWindowWidth, currentPosition.width),
                desiredHeight);
            initialSizeConfigured = true;
            Repaint();
        }
        
        private void DrawInputs()
        {
            FFEditorGUI.DrawSectionHeader(
                "基础信息",
                "面板名称同时作为脚本类名和预制体名称。建议使用以 Panel 结尾的英文标识符。");
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            panelName = EditorGUILayout.TextField("面板名称", panelName);
            if (!string.IsNullOrWhiteSpace(panelName)
                && !ValidatePanelName(panelName, out string nameError))
                EditorGUILayout.HelpBox(GetFirstLine(nameError), MessageType.Warning);
            GUILayout.EndVertical();

            GUILayout.Space(FFEditorStyles.SectionSpacing);
            FFEditorGUI.DrawSectionHeader(
                "基础能力",
                "这些选项会直接添加到生成的面板根节点或内容节点上。");
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            useTMP = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "使用 TextMeshPro 文本组件",
                    "取消勾选后改用 Unity 旧版文本控件。"),
                useTMP);
            addDefaultTransition = EditorGUILayout.ToggleLeft("添加默认进入和退出过渡", addDefaultTransition);
            addSafeArea = EditorGUILayout.ToggleLeft("内容适配屏幕安全区域", addSafeArea);
            GUILayout.EndVertical();

            GUILayout.Space(FFEditorStyles.SectionSpacing);
            FFEditorGUI.DrawSectionHeader(
                "示例控件（可选）",
                "仅用于快速搭建原型；正式面板可以全部不选。");
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.BeginHorizontal();
            addExampleButton = EditorGUILayout.ToggleLeft("按钮", addExampleButton);
            addExampleInput = EditorGUILayout.ToggleLeft("输入框", addExampleInput);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            addExampleToggle = EditorGUILayout.ToggleLeft("开关", addExampleToggle);
            addExampleSlider = EditorGUILayout.ToggleLeft("滑动条", addExampleSlider);
            EditorGUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawOutputPreview()
        {
            bool pathsValid = TrySyncConfiguredPaths(out string pathError);
            FFEditorGUI.DrawSectionHeader(
                "输出预览",
                "输出目录由 UI 配置统一管理，本窗口只显示最终结果。");
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            if (!pathsValid)
            {
                EditorGUILayout.HelpBox(pathError, MessageType.Error);
            }
            else
            {
                string safeName = string.IsNullOrWhiteSpace(panelName) ? "面板名称" : panelName.Trim();
                DrawReadOnlyPath("脚本文件", $"{scriptPath}/{safeName}.cs");
                DrawReadOnlyPath("面板预制体", $"{prefabPath}/{safeName}.prefab");
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("打开 UI 配置", FFEditorStyles.ActionButton, GUILayout.Width(120f)))
                SettingsService.OpenProjectSettings("Project/Fink Framework/UI Settings");
            EditorGUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawUsageTips()
        {
            FFEditorGUI.DrawSectionHeader("创建流程");
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.LabelField(
                "1. 先生成脚本并等待 Unity 完成编译。\n" +
                "2. 编译成功后自动创建预制体、挂载脚本并定位资源。\n" +
                "3. 业务代码只需通过 UIManager 打开面板，无需传入资源路径。",
                FFEditorStyles.Description);
            GUILayout.EndVertical();
        }

        private static void DrawReadOnlyPath(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(72f));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(value);
            EditorGUILayout.EndHorizontal();
        }

        private static string GetFirstLine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int lineBreak = value.IndexOf('\n');
            return lineBreak < 0 ? value : value.Substring(0, lineBreak);
        }

        /// <summary>
        /// 从统一 UI 配置读取生成目录。窗口不保存路径副本，确保生成器和运行时始终使用同一配置。
        /// </summary>
        private bool TrySyncConfiguredPaths(out string error)
        {
            GlobalSettingsAsset settings = GlobalSettingsEditorLoader.LoadOrCreate();
            if (!settings)
            {
                error = "未找到 UI 配置文件。";
                return false;
            }

            if (!GlobalSettingsAsset.TryNormalizeUIPanelScriptOutputSuffix(
                    settings.UIPanelScriptOutputSuffix,
                    settings.ScriptRootDirectory,
                    out string scriptOutputSuffix,
                    out error))
                return false;

            if (!GlobalSettingsAsset.TryNormalizeUIPanelPrefabOutputRelativePath(
                    settings.UIPanelPrefabOutputRelativePath,
                    out string prefabOutputRelativePath,
                    out error))
                return false;

            scriptPath = GlobalSettingsAsset.GetUIPanelScriptOutputPath(
                settings.ScriptRootDirectory,
                scriptOutputSuffix);
            prefabPath = GlobalSettingsAsset.GetUIPanelPrefabOutputPath(
                prefabOutputRelativePath);
            error = string.Empty;
            return true;
        }
        
        private void DrawCreateButton()
        {
            bool canCreate = CanCreatePanel(out string reason);
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button(
                        "创建 UI 面板",
                        FFEditorStyles.BigButton,
                        GUILayout.ExpandWidth(true)))
                    CreatePanel();
            }

            if (!canCreate)
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
            else
                FFEditorGUI.Center(() => GUILayout.Label(
                    "创建后将自动等待脚本编译并继续生成预制体",
                    EditorStyles.centeredGreyMiniLabel));
        }

        private bool CanCreatePanel(out string reason)
        {
            if (!TrySyncConfiguredPaths(out reason))
                return false;

            if (!ValidatePanelName(panelName, out reason))
            {
                reason = GetFirstLine(reason);
                return false;
            }

            if (CheckScriptConflict(panelName, scriptPath))
            {
                reason = "目标目录中已经存在同名脚本。";
                return false;
            }

            if (CheckPrefabConflict(panelName, prefabPath))
            {
                reason = "目标目录中已经存在同名面板预制体。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
        
        #endregion

        #region 生成 UI 面板主流程

        /// <summary>校验配置并启动面板生成流程。</summary>
        private void CreatePanel()
        {
            if (!TrySyncConfiguredPaths(out string pathError))
            {
                EditorUtility.DisplayDialog(
                    "UI 路径配置无效",
                    $"{pathError}\n\n请前往 Project Settings > Fink Framework > UI Settings 修改。",
                    "确认");
                return;
            }

            if (!ValidatePanelName(panelName, out var error))
            {
                EditorUtility.DisplayDialog(
                    "非法的面板名称",
                    error,
                    "确认"
                );
                return;
            }

            if (CheckScriptConflict(panelName, scriptPath))
            {
                string fullScriptPath = $"{scriptPath}/{panelName}.cs";
                EditorUtility.DisplayDialog(
                    "脚本已存在",
                    $"检测到同名 UI 脚本：\n\n{fullScriptPath}\n\n请更换面板名称，或先处理已有脚本。",
                    "确认");
                LogUtil.Warn("UIBuilderWindow", $"UI 脚本已存在，已中断创建：{fullScriptPath}");
                return;
            }
    
            if (CheckPrefabConflict(panelName, prefabPath))
            {
                string fullPrefabPath = $"{prefabPath}/{panelName}.prefab";

                EditorUtility.DisplayDialog(
                    "预制体已存在",
                    $"检测到已存在同名 UI 预制体：\n\n{fullPrefabPath}\n\n" +
                    "请更换面板名称，或手动删除已有预制体后再创建。",
                    "确认"
                );

                LogUtil.Warn("UIBuilderWindow",
                    $"检测到同名预制体，已中断 UI 面板创建流程：{fullPrefabPath}");
                return;
            }

            if (!CreateScript())
                return;

            // 保存一次性任务状态，供脚本重编译完成后的回调继续创建预制体。
            // SessionState 会跨程序集重载保留，但 Unity 退出后自动清空，不会残留旧任务。
            SessionState.SetString(PendingPanelNameKey, panelName);
            SessionState.SetString(PendingPrefabPathKey, prefabPath);
            SessionState.SetString(PendingScriptPathKey, scriptPath);
            
            // 保存示例控件选项
            SessionState.SetBool(PendingUseTMPKey, useTMP);
            SessionState.SetBool(PendingAddButtonKey, addExampleButton);
            SessionState.SetBool(PendingAddInputKey, addExampleInput);
            SessionState.SetBool(PendingAddToggleKey, addExampleToggle);
            SessionState.SetBool(PendingAddSliderKey, addExampleSlider);
            SessionState.SetBool(PendingAddTransitionKey, addDefaultTransition);
            SessionState.SetBool(PendingAddSafeAreaKey, addSafeArea);

            // 触发编译
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("提示",
                "脚本已生成，点击确认后 Unity 将开始重新编译。\n重编译期间请勿操作，等待编译完成。\n编译结束后将自动创建 prefab 并挂载脚本。",
                "确认");
        }

        #endregion
        
        #region 生成面板对应脚本
        
        private bool CreateScript()
        {
            if (!AssetDatabase.IsValidFolder(scriptPath))
                Directory.CreateDirectory(scriptPath);

            // 加载模板
            TextAsset templateAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/FinkFramework/Editor/EditorResources/UI/template_ui_panel.txt"
            );

            if (!templateAsset)
            {
                LogUtil.Error("UIBuilderWindow", "模板文件未找到：Assets/FinkFramework/Editor/EditorResources/UI/template_ui_panel.txt");
                EditorUtility.DisplayDialog(
                    "无法创建 UI 面板",
                    "未找到 UI 面板脚本模板，请确认框架文件完整。",
                    "确认");
                return false;
            }

            string template = templateAsset.text;

            // 动态 namespace
            string ns = BuildNamespace(scriptPath);
            string fields = BuildFields(useTMP, addExampleButton, addExampleInput, addExampleToggle, addExampleSlider);
            string assign = BuildAssign(useTMP, addExampleButton, addExampleInput, addExampleToggle, addExampleSlider);
            string usings = BuildUsings(useTMP, addExampleButton, addExampleInput, addExampleToggle, addExampleSlider);
            string logicBtn = BuildLogicButton(addExampleButton);
            string logicInput = BuildLogicInput(addExampleInput);
            string logicToggle = BuildLogicToggle(addExampleToggle);
            string logicSlider = BuildLogicSlider(addExampleSlider);
            // 替换模板变量
            string code = template
                .Replace("#DATE#", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("#CLASSNAME#", panelName)
                .Replace("#USINGS#", usings)
                .Replace("#FIELDS#", fields)
                .Replace("#ASSIGN#", assign)
                .Replace("#LOGIC_BUTTON#", logicBtn)
                .Replace("#LOGIC_INPUT#", logicInput)
                .Replace("#LOGIC_TOGGLE#", logicToggle)
                .Replace("#LOGIC_SLIDER#", logicSlider)
                .Replace("#NAMESPACE#", ns);

            // 写文件
            string filePath = $"{scriptPath}/{panelName}.cs";
            File.WriteAllText(filePath, code);

            LogUtil.Info($"UIBuilderWindow: 生成 UI 脚本 → {filePath}");
            return true;
        }
        
        /// <summary>
        /// 根据选中的控件，生成字段区域
        /// </summary>
        private string BuildFields(bool useTMPro, bool addBtn, bool addInput, bool addToggle, bool addSlider)
        {
            string result = "";

            if (addBtn)
                result += "        private Button demoButton;\n";

            if (addInput)
                result += useTMPro
                    ? "        private TMP_InputField demoInput;\n"
                    : "        private InputField demoInput;\n";

            if (addToggle)
                result += "        private Toggle demoToggle;\n";

            if (addSlider)
                result += "        private Slider demoSlider;\n";

            return result;
        }
        
        /// <summary>
        /// 根据选中的控件，生成绑定代码
        /// </summary>
        private string BuildAssign(bool useTMPro, bool addBtn, bool addInput, bool addToggle, bool addSlider)
        {
            string result = "";

            if (addBtn)
                result += "            demoButton = GetControl<Button>(\"DemoButton\");\n";

            if (addInput)
                result += useTMPro
                    ? "            demoInput = GetControl<TMP_InputField>(\"DemoInput\");\n"
                    : "            demoInput = GetControl<InputField>(\"DemoInput\");\n";

            if (addToggle)
                result += "            demoToggle = GetControl<Toggle>(\"DemoToggle\");\n";

            if (addSlider)
                result += "            demoSlider = GetControl<Slider>(\"DemoSlider\");\n";

            return result;
        }

        private string BuildLogicButton(bool addBtn)
        {
            return !addBtn ? "            // 无按钮控件\n" : @"if (btnName == ""DemoButton"")
            {
                LogUtil.Info(""UI"", ""点击了 DemoButton"");
            }";
        }
        
        private string BuildLogicInput(bool addInput)
        {
            return !addInput ? "            // 无输入框控件\n" : @"if (inputName == ""DemoInput"")
            {
                LogUtil.Info(""UI"", $""输入内容：{value}"");
            }";
        }
        
        private string BuildLogicToggle(bool addToggle)
        {
            return !addToggle ? "            // 无 Toggle 控件\n" : @"if (toggleName == ""DemoToggle"")
            {
                LogUtil.Info(""UI"", $""Toggle 状态：{value}"");
            }";
        }
        
        private string BuildLogicSlider(bool addSlider)
        {
            return !addSlider ? "            // 无 Slider 控件\n" : @"if (sliderName == ""DemoSlider"")
            {
                LogUtil.Info(""UI"", $""Slider 数值：{value}"");
            }";
        }
        #endregion

        #region 生成面板对应预制体
   
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            // 1. 读取关键状态；没有面板名说明本次编译不是由本工具触发的。
            string panelName = SessionState.GetString(PendingPanelNameKey, string.Empty);
            
            if (string.IsNullOrEmpty(panelName)) return;

            // 2. 读取其余配置
            string prefabPath = SessionState.GetString(PendingPrefabPathKey, string.Empty);
            string scriptPath = SessionState.GetString(PendingScriptPathKey, string.Empty);
            bool useTMP = SessionState.GetBool(PendingUseTMPKey, true);
            bool addButton = SessionState.GetBool(PendingAddButtonKey, false);
            bool addInput = SessionState.GetBool(PendingAddInputKey, false);
            bool addToggle = SessionState.GetBool(PendingAddToggleKey, false);
            bool addSlider = SessionState.GetBool(PendingAddSliderKey, false);
            bool addTransition = SessionState.GetBool(PendingAddTransitionKey, true);
            bool addSafeArea = SessionState.GetBool(PendingAddSafeAreaKey, true);

            // 读取完成后立即消费状态，避免延迟回调或异常导致重复执行。
            ClearPendingOperation();

            // 3. 使用 DelayCall 确保 AssetDatabase 状态完全就绪
            EditorApplication.delayCall += () =>
            {
                CreatePrefabAfterCompile(
                    panelName,
                    prefabPath,
                    scriptPath,
                    useTMP,
                    addButton,
                    addInput,
                    addToggle,
                    addSlider,
                    addTransition,
                    addSafeArea);
            };
        }
        
        private static void ClearPendingOperation()
        {
            SessionState.EraseString(PendingPanelNameKey);
            SessionState.EraseString(PendingPrefabPathKey);
            SessionState.EraseString(PendingScriptPathKey);
            SessionState.EraseBool(PendingUseTMPKey);
            SessionState.EraseBool(PendingAddButtonKey);
            SessionState.EraseBool(PendingAddInputKey);
            SessionState.EraseBool(PendingAddToggleKey);
            SessionState.EraseBool(PendingAddSliderKey);
            SessionState.EraseBool(PendingAddTransitionKey);
            SessionState.EraseBool(PendingAddSafeAreaKey);
        }

        private static void CreatePrefabAfterCompile(
            string panelName,
            string prefabPath,
            string scriptPath,
            bool useTMP,
            bool addBtn,
            bool addInput,
            bool addToggle,
            bool addSlider,
            bool addTransition,
            bool addSafeArea)
        {
            // 确保 Prefab 文件夹存在
            if (!Directory.Exists(prefabPath))
            {
                Directory.CreateDirectory(prefabPath);
                AssetDatabase.Refresh();
            }
            // 创建根节点
            GameObject root = new GameObject(panelName);
            RectTransform rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (addTransition)
                root.AddComponent<UICanvasGroupTransition>();
            
            // ==========================================================
            // 1. 创建背景 Background（全屏透明 Image）
            // ==========================================================
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);

            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 默认透明但仍拦截射线，避免一个新建页面无意间呈现白色蒙层，
            // 也避免点击直接穿透到场景或下层 UI。
            var img = bg.GetComponent<Image>();
            img.color = Color.clear;

            // 背景参与射线检测，可用于实现点击遮罩关闭等交互。
            img.raycastTarget = true;

            // ==========================================================
            // 2. 创建内容 Content（放真实 UI 的地方）
            // ==========================================================
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(0, 0);
            contentRect.offsetMax = new Vector2(0, 0);
            if (addSafeArea)
                content.AddComponent<UISafeArea>();
            
            // ==========================================================
            // 3. 创建示例 UI 控件（根据用户选择）
            // ==========================================================
            Transform parent = content.transform;

            // Content 是全屏容器，由锚点决定自身尺寸；只让 Layout Group 排列子控件。
            // 不在同一对象添加 ContentSizeFitter，避免它与拉伸锚点、Layout Group 相互争夺尺寸。
            if (addSlider || addInput || addToggle || addBtn)
            {
                var layout = content.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 15;
                layout.padding = new RectOffset(0, 0, 20, 0);
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
            // 按需实例化控件（全部从预制体生成）
            if (addBtn)  CreateDemoControl("DemoButton",     useTMP, parent);
            if (addInput)CreateDemoControl("DemoInput", useTMP, parent);
            if (addToggle)CreateDemoControl("DemoToggle",   useTMP, parent);
            if (addSlider)CreateDemoControl("DemoSlider",   useTMP, parent);
            // ==========================================================
            // 4. 自动挂载脚本
            // ==========================================================
            string fullScriptPath = $"{scriptPath}/{panelName}.cs";
            
            MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(fullScriptPath);
            Type scriptType = ms != null ? ms.GetClass() : null;

            if (scriptType == null
                || scriptType.IsAbstract
                || !typeof(BasePanel).IsAssignableFrom(scriptType))
            {
                string error = $"无法挂载 UI 面板脚本：{fullScriptPath}\n\n"
                               + "请确认脚本已成功编译、类名与文件名一致，并继承自非抽象的 BasePanel。";
                LogUtil.Error("UIBuilderWindow", error);
                EditorUtility.DisplayDialog("创建 UI 面板失败", error, "确认");
                GameObject.DestroyImmediate(root);
                return;
            }

            root.AddComponent(scriptType);

            // ==========================================================
            // 5. 保存 Prefab
            // ==========================================================
            string fullPrefabPath = $"{prefabPath}/{panelName}.prefab";
            // === 同名 Prefab 检测 ===
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath) != null)
            {
                EditorUtility.DisplayDialog(
                    "预制体已存在",
                    $"检测到已存在同名 UI 预制体：\n\n{fullPrefabPath}\n\n" +
                    "请更换面板名称，或手动删除已有预制体后再创建。",
                    "确认"
                );

                LogUtil.Warn("UIBuilderWindow",
                    $"UI 预制体已存在，已中断创建：{fullPrefabPath}");

                GameObject.DestroyImmediate(root);
                return;
            }
            
            PrefabUtility.SaveAsPrefabAsset(root, fullPrefabPath);

            // 销毁场景中的临时对象
            GameObject.DestroyImmediate(root);

            EditorUtility.DisplayDialog("创建完成", $"UI 面板 {panelName} 已创建。", "确认");
            
            // 高亮选中新文件
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath));
        }
        
        #endregion

        #region 工具方法
        
        private bool ValidatePanelName(string panel_name, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(panel_name))
            {
                error = "面板名称不能为空。";
                return false;
            }

            if (panel_name.Contains(" "))
            {
                error = "面板名称不能包含空格。";
                return false;
            }

            // C# 标识符规则：字母或下划线开头，后续字母/数字/下划线
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    panel_name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                error =
                    "面板名称不合法。\n\n" +
                    "命名规则：\n" +
                    "· 必须以字母或下划线开头\n" +
                    "· 只能包含字母、数字、下划线\n" +
                    "· 不能包含中文或特殊字符";
                return false;
            }

            return true;
        }
        
        private bool CheckPrefabConflict(string panel, string path)
        {
            string fullPrefabPath = $"{path}/{panel}.prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath) != null;
        }

        private bool CheckScriptConflict(string panel, string path)
        {
            string fullScriptPath = $"{path}/{panel}.cs";
            return File.Exists(fullScriptPath)
                   || AssetDatabase.LoadAssetAtPath<MonoScript>(fullScriptPath) != null;
        }
        
        private string BuildNamespace(string scriptFolder)
        {
            string full = scriptFolder.Replace("\\", "/");

            // 寻找 Scripts/ 作为锚点
            int index = full.IndexOf("Scripts/", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                // 如果没有 Scripts/，则从 Assets/ 后计算
                int idx2 = full.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                if (idx2 >= 0)
                {
                    string rel = full.Substring(idx2 + "Assets/".Length);
                    return PathToNamespace(rel);
                }
                return ""; // fallback
            }

            // 取 Scripts/ 后面的路径
            string relative = full.Substring(index + "Scripts/".Length);

            return PathToNamespace(relative);
        }

        private string PathToNamespace(string path)
        {
            path = path.Trim('/');

            return string.IsNullOrEmpty(path) ? "" : path.Replace("/", ".");
        }
        
        /// <summary>
        /// 根据用户选的控件类型，动态生成 using 区域
        /// </summary>
        private string BuildUsings(bool usedTMP, bool addBtn, bool addInput, bool addToggle, bool addSlider)
        {
            var us = new HashSet<string> {
                // BasePanel 必须要
                "using FinkFramework.Runtime.UI;",
                "using FinkFramework.Runtime.UI.Base;" };

            // LogUtil 必须要（只要有任何逻辑）
            if (addBtn || addInput || addToggle || addSlider)
                us.Add("using FinkFramework.Runtime.Utils;");

            // 是否需要 UGUI
            if (addBtn || addToggle || addSlider || (addInput && !usedTMP))
                us.Add("using UnityEngine.UI;");

            // 只有 TMP_InputField 会直接出现在生成脚本的类型签名中。
            // TMP 按钮和 Toggle 的 TMP 文本保留在 Prefab 内，不应让脚本产生包依赖。
            if (usedTMP && addInput)
                us.Add("using TMPro;");

            // 整理输出
            var list = new List<string>(us);
            list.Sort();

            string result = "";
            foreach (var u in list)
                result += u + "\n";

            return result;
        }
        
        private static string GetUIPrefabPath(bool useTMP, string prefabName)
        {
            string typeFolder = useTMP ? "TMP" : "Legacy";

            return $"Assets/FinkFramework/Editor/EditorResources/UI/UIBuilder/{typeFolder}/{prefabName}.prefab";
        }

        private static void CreateDemoControl(string prefabName, bool useTMP, Transform parent)
        {
            string path = GetUIPrefabPath(useTMP, prefabName);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                LogUtil.Error($"找不到预制体：{path}");
                return;
            }

            GameObject obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (obj != null) obj.transform.SetParent(parent, false);
        }
        
        #endregion
    }
}
