using System;
using System.Collections.Generic;
using System.IO;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FinkFramework.Editor.Windows
{
    /// <summary>
    /// 创建 UI 面板
    /// </summary>
    public class UIBuilderWindow : EditorWindow
    {
        private string panelName = "TestPanel";
        private string scriptPath = "Assets/Scripts/UI/Panels";
        private string prefabPath = "Assets/Resources/UI/Panels";
        private bool useTMP = true;  // 默认使用 TMP（Unity 推荐）
        // 是否生成示例 UI
        private bool addExampleButton = false;
        private bool addExampleInput = false;
        private bool addExampleToggle = false;
        private bool addExampleSlider = false;

        [MenuItem("Fink Framework/创建 UI 面板")]
        public static void Open()
        {
            var window = GetWindow<UIBuilderWindow>("创建UI面板");
            window.minSize = new Vector2(480, 360);
        }

        #region 编辑器面板绘制
        private void OnGUI()
        {
            // ========== 主标题 ==========
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("创建 UI 面板", FFEditorStyles.Title);
            });

            FFEditorGUI.Separator(1);
            GUILayout.Space(12);

            // ========== 输入区域 ==========
            DrawInputs();

            GUILayout.Space(12);
            
            // ----------- 说明标题 -------------
            GUILayout.Label("特别说明 Description", FFEditorStyles.SectionTitle);

            // ========== 说明文字 ==========
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Space(5);

            GUILayout.Label(
                "· 面板名称建议以 <b>XXXPanel</b> 形式命名，如：<color=#66CCFF>TestPanel</color>。\n" +
                "· 注意面板名称请勿与对应路径下的已有预制体或脚本名重名！\n" +
                "· 面板预制体默认路径：<b>Assets/Resources/UI/Panels</b>。\n" +
                "· 若使用默认路径，可直接调用：<color=#A8FF60>UIManager.Instance.ShowPanel<面板名称>()</color>\n" +
                "· 若更改了预制体路径，则需要手动传入带前缀的完整路径 fullPath 才能加载。",
                FFEditorStyles.Description);

            GUILayout.Space(6);
            GUILayout.EndVertical();

            GUILayout.Space(12);
            FFEditorGUI.Separator(1);
            GUILayout.Space(15);

            // ========== 创建按钮 ==========
            DrawCreateButton();

            GUILayout.FlexibleSpace();

            // ========== 底部版权 ==========
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright \u00A9 2025  Fink Framework",
                    FFEditorStyles.Footer);
            });
        }
        
        private void DrawInputs()
        {
            // ----------- Panel Settings -------------
            GUILayout.Label("面板设置 Panel Settings", FFEditorStyles.SectionTitle);
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            GUILayout.Space(5);

            panelName = EditorGUILayout.TextField("面板名称 Panel Name", panelName);
            GUILayout.Space(4);

            scriptPath = EditorGUILayout.TextField("代码文件输出路径 Script Output Folder", scriptPath);
            prefabPath = EditorGUILayout.TextField("UI预制体输出路径 Prefab Output Folder", prefabPath);

            GUILayout.Space(5);
            GUILayout.EndVertical();     // ← 提前结束 Panel Settings

            GUILayout.Space(12);

            // ----------- Example UI (独立卡片) -------------
            GUILayout.Label("示例控件 Example UI", FFEditorStyles.SectionTitle);
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            GUILayout.Space(5);
            useTMP = EditorGUILayout.Toggle("是否使用 TextMeshPro", useTMP);
            addExampleButton = EditorGUILayout.Toggle("添加示例按钮 Button", addExampleButton);
            addExampleInput  = EditorGUILayout.Toggle("添加示例输入框 InputField", addExampleInput);
            addExampleToggle = EditorGUILayout.Toggle("添加示例 Toggle", addExampleToggle);
            addExampleSlider = EditorGUILayout.Toggle("添加示例 Slider", addExampleSlider);

            GUILayout.Space(5);
            GUILayout.EndVertical();     // ← Example UI 卡片结束
        }
        
        private void DrawCreateButton()
        {
            FFEditorGUI.Center(() =>
            {
                if (GUILayout.Button("创建 UI 面板", FFEditorStyles.BigButton, GUILayout.Width(220)))
                {
                    CreatePanel();
                }
            });
        }
        
        #endregion

        #region 生成UI面板主流程

        /// <summary>
        /// 创建UI面板主入口
        /// </summary>
        private void CreatePanel()
        {
            // ====== PanelName 合法性校验 ======
            if (!ValidatePanelName(panelName, out var error))
            {
                EditorUtility.DisplayDialog(
                    "非法的面板名称",
                    error,
                    "确认"
                );
                return;
            }
    
            if (CheckPrefabConflict(panelName, prefabPath))
            {
                string fullPrefabPath = $"{prefabPath}/{panelName}.prefab";

                EditorUtility.DisplayDialog(
                    "Prefab 已存在",
                    $"检测到已存在同名 UI Prefab：\n\n{fullPrefabPath}\n\n" +
                    "请更换面板名称，或手动删除已有 Prefab 后再创建。",
                    "确认"
                );

                LogUtil.Warn("UIBuilderWindow",
                    $"检测到同名 Prefab，已完全中断 UI 面板创建流程：{fullPrefabPath}");
                return;
            }

            CreateScript();

            // 保存数据等编译后使用
            EditorPrefs.SetString("FF_UIBuilder_PanelName", panelName);
            EditorPrefs.SetString("FF_UIBuilder_PrefabPath", prefabPath);
            EditorPrefs.SetString("FF_UIBuilder_ScriptPath", scriptPath);
            
            // 保存示例控件选项
            EditorPrefs.SetBool("FF_UIBuilder_UseTMP", useTMP);
            EditorPrefs.SetBool("FF_UIBuilder_AddExampleButton", addExampleButton);
            EditorPrefs.SetBool("FF_UIBuilder_AddExampleInput", addExampleInput);
            EditorPrefs.SetBool("FF_UIBuilder_AddExampleToggle", addExampleToggle);
            EditorPrefs.SetBool("FF_UIBuilder_AddExampleSlider", addExampleSlider);

            // 触发编译
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("提示",
                "脚本已生成，点击确认后 Unity 将开始重新编译。\n重编译期间请勿操作，等待编译完成。\n编译结束后将自动创建 prefab 并挂载脚本。",
                "确认");
        }

        #endregion
        
        #region 生成面板对应脚本
        
        private void CreateScript()
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
                return;
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
        }
        
        /// <summary>
        /// 根据选中的控件，生成字段区域
        /// </summary>
        private string BuildFields(bool useTMP, bool addBtn, bool addInput, bool addToggle, bool addSlider)
        {
            string result = "";

            if (addBtn)
                result += "        private Button DemoButton;\n";

            if (addInput)
                result += useTMP
                    ? "        private TMP_InputField DemoInput;\n"
                    : "        private InputField DemoInput;\n";

            if (addToggle)
                result += "        private Toggle DemoToggle;\n";

            if (addSlider)
                result += "        private Slider DemoSlider;\n";

            return result;
        }
        
        /// <summary>
        /// 根据选中的控件，生成绑定代码
        /// </summary>
        private string BuildAssign(bool useTMP, bool addBtn, bool addInput, bool addToggle, bool addSlider)
        {
            string result = "";

            if (addBtn)
                result += "            DemoButton = GetControl<Button>(\"DemoButton\");\n";

            if (addInput)
                result += useTMP
                    ? "            DemoInput = GetControl<TMP_InputField>(\"DemoInput\");\n"
                    : "            DemoInput = GetControl<InputField>(\"DemoInput\");\n";

            if (addToggle)
                result += "            DemoToggle = GetControl<Toggle>(\"DemoToggle\");\n";

            if (addSlider)
                result += "            DemoSlider = GetControl<Slider>(\"DemoSlider\");\n";

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
            // 1. 读取关键 Key，如果没有 PanelName 说明不是本工具触发的编译
            string panelName = EditorPrefs.GetString("FF_UIBuilder_PanelName", "");
            
            if (string.IsNullOrEmpty(panelName)) return;

            // 2. 读取其余配置
            string prefabPath = EditorPrefs.GetString("FF_UIBuilder_PrefabPath", "");
            string scriptPath = EditorPrefs.GetString("FF_UIBuilder_ScriptPath", "");

            // 3. 使用 DelayCall 确保 AssetDatabase 状态完全就绪
            EditorApplication.delayCall += () =>
            {
                CreatePrefabAfterCompile(panelName, prefabPath, scriptPath);

                // 4. 清理所有用过的 Key，防止污染
                EditorPrefs.DeleteKey("FF_UIBuilder_PanelName");
                EditorPrefs.DeleteKey("FF_UIBuilder_PrefabPath");
                EditorPrefs.DeleteKey("FF_UIBuilder_ScriptPath");
                
                EditorPrefs.DeleteKey("FF_UIBuilder_UseTMP");
                EditorPrefs.DeleteKey("FF_UIBuilder_AddExampleButton");
                EditorPrefs.DeleteKey("FF_UIBuilder_AddExampleInput");
                EditorPrefs.DeleteKey("FF_UIBuilder_AddExampleToggle");
                EditorPrefs.DeleteKey("FF_UIBuilder_AddExampleSlider");
            };
        }
        
        private static void CreatePrefabAfterCompile(string panelName, string prefabPath, string scriptPath)
        {
            // 确保 Prefab 文件夹存在
            if (!Directory.Exists(prefabPath))
            {
                Directory.CreateDirectory(prefabPath);
                AssetDatabase.Refresh();
            }
            bool useTMP = EditorPrefs.GetBool("FF_UIBuilder_UseTMP", true);
            bool addBtn = EditorPrefs.GetBool("FF_UIBuilder_AddExampleButton", false);
            bool addInput = EditorPrefs.GetBool("FF_UIBuilder_AddExampleInput", false);
            bool addToggle = EditorPrefs.GetBool("FF_UIBuilder_AddExampleToggle", false);
            bool addSlider = EditorPrefs.GetBool("FF_UIBuilder_AddExampleSlider", false);
            
            // 创建根节点
            GameObject root = new GameObject(panelName);
            RectTransform rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            // ==========================================================
            // 1. 创建背景 Background（全屏半透明 Image）
            // ==========================================================
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);

            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 设置半透明颜色（随便你改）
            var img = bg.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.35f); // 白色 35% 透明

            // 让背景不挡住子节点事件（如果你想挡就不要开启）
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
            
            // ==========================================================
            // 3. 创建示例 UI 控件（根据用户选择）
            // ==========================================================
            Transform parent = content.transform;

            // 自动布局（避免控件堆叠）
            if (addSlider || addInput || addToggle || addBtn)
            {
                var layout = content.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 15;
                layout.padding = new RectOffset(0, 0, 20, 0);
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
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

            if (scriptType != null)
            {
                root.AddComponent(scriptType);
            }
            else
            {
                // 如果精准路径找不到，再尝试兜底（可选）或报错
                LogUtil.Warn($"无法挂载脚本，路径: {fullScriptPath} 请检查类名是否与文件名一致。");
            }

            // ==========================================================
            // 5. 保存 Prefab
            // ==========================================================
            string fullPrefabPath = $"{prefabPath}/{panelName}.prefab";
            // === 同名 Prefab 检测 ===
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath) != null)
            {
                EditorUtility.DisplayDialog(
                    "Prefab 已存在",
                    $"检测到已存在同名 UI Prefab：\n\n{fullPrefabPath}\n\n" +
                    "请更换面板名称，或手动删除已有 Prefab 后再创建。",
                    "确认"
                );

                LogUtil.Warn("UIBuilderWindow",
                    $"UI Prefab 已存在，已中断创建：{fullPrefabPath}");

                // 中断流程
                return;
            }
            
            PrefabUtility.SaveAsPrefabAsset(root, fullPrefabPath);

            // 销毁场景中的临时对象
            GameObject.DestroyImmediate(root);

            EditorUtility.DisplayDialog("Success", $"UI面板 {panelName} 创建完毕!", "确认");
            
            // 高亮选中新文件
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath));
        }
        
        #endregion

        #region 工具方法
        
        private bool ValidatePanelName(string name, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "面板名称不能为空。";
                return false;
            }

            if (name.Contains(" "))
            {
                error = "面板名称不能包含空格。";
                return false;
            }

            // C# 标识符规则：字母或下划线开头，后续字母/数字/下划线
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
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
        
        private bool CheckPrefabConflict(string panelName, string prefabPath)
        {
            string fullPrefabPath = $"{prefabPath}/{panelName}.prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath) != null;
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
        private string BuildUsings(bool useTMP, bool addBtn, bool addInput, bool addToggle, bool addSlider)
        {
            var us = new HashSet<string> {
                // BasePanel 必须要
                "using FinkFramework.Runtime.UI.Base;" };

            // LogUtil 必须要（只要有任何逻辑）
            if (addBtn || addInput || addToggle || addSlider)
                us.Add("using FinkFramework.Runtime.Utils;");

            // 是否需要 UGUI
            if (addBtn || addToggle || addSlider || (addInput && !useTMP))
                us.Add("using UnityEngine.UI;");

            // TMP 版本控件必加
            if (useTMP && (addBtn || addInput || addToggle))
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
            obj.transform.SetParent(parent, false);
            
        }
        
        #endregion
    }
}