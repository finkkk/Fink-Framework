using System;
using System.IO;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Windows
{
    /// <summary>
    /// 创建UI面板
    /// </summary>
    public class UIBuilderWindow : EditorWindow
    {
        private string panelName = "TestPanel";
        private string scriptPath = "Assets/Scripts/UI/Panels";
        private string prefabPath = "Assets/Resources/UI/Panels";

        [MenuItem("Fink Framework/创建UI面板")]
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

            GUILayout.Space(10);

            // ========== 说明文字 ==========
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Space(6);

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
            GUILayout.Label("面板设置 Panel Settings", FFEditorStyles.SectionTitle);
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            GUILayout.Space(5);

            panelName = EditorGUILayout.TextField("面板名称 Panel Name", panelName);
            GUILayout.Space(4);

            scriptPath = EditorGUILayout.TextField("代码文件输出路径 Script Output Folder", scriptPath);
            prefabPath = EditorGUILayout.TextField("UI预制体输出路径 Prefab Output Folder", prefabPath);

            GUILayout.Space(5);
            GUILayout.EndVertical();
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
            if (string.IsNullOrEmpty(panelName))
            {
                EditorUtility.DisplayDialog("Error", "面板名称不能为空!", "OK");
                return;
            }

            CreateScript();

            // 保存数据等编译后使用
            EditorPrefs.SetString("FF_UIBuilder_PanelName", panelName);
            EditorPrefs.SetString("FF_UIBuilder_PrefabPath", prefabPath);
            EditorPrefs.SetString("FF_UIBuilder_ScriptPath", scriptPath);

            // 触发编译
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("提示",
                "脚本已生成，点击确认后 Unity 将开始重新编译...\n重编译期间请勿操作，等待编译完成。\n编译结束后将自动创建 prefab 并挂载脚本。",
                "OK");
        }

        #endregion
        
        #region 生成面板对应脚本
        
        private void CreateScript()
        {
            if (!AssetDatabase.IsValidFolder(scriptPath))
                Directory.CreateDirectory(scriptPath);

            // 加载模板
            TextAsset templateAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/FinkFramework/Editor/Resources/UI/template_ui_panel.txt"
            );

            if (!templateAsset)
            {
                LogUtil.Error("UIBuilderWindow", "模板文件未找到：Assets/FinkFramework/Editor/Resources/UI/template_ui_panel.txt");
                return;
            }

            string template = templateAsset.text;

            // 动态 namespace
            string ns = BuildNamespace(scriptPath);

            // 替换模板变量
            string code = template
                .Replace("#DATE#", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("#CLASSNAME#", panelName)
                .Replace("#NAMESPACE#", ns);

            // 写文件
            string filePath = $"{scriptPath}/{panelName}.cs";
            File.WriteAllText(filePath, code);

            LogUtil.Info($"UIBuilderWindow: 生成 UI 脚本 → {filePath}");
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

            if (string.IsNullOrEmpty(path))
                return "";

            return path.Replace("/", ".");
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

                // 4. 【重要】清理所有用过的 Key，防止污染
                EditorPrefs.DeleteKey("FF_UIBuilder_PanelName");
                EditorPrefs.DeleteKey("FF_UIBuilder_PrefabPath");
                EditorPrefs.DeleteKey("FF_UIBuilder_ScriptPath");
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
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bg.transform.SetParent(root.transform, false);

            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 设置半透明颜色（随便你改）
            var img = bg.GetComponent<UnityEngine.UI.Image>();
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
            // 3. 自动挂载脚本
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
                Debug.LogWarning($"[UIBuilder] 无法挂载脚本，路径: {fullScriptPath}\n请检查类名是否与文件名一致。");
            }

            // ==========================================================
            // 4. 保存 Prefab
            // ==========================================================
            string fullPrefabPath = $"{prefabPath}/{panelName}.prefab";
            // 这里使用 GenerateUniqueAssetPath 防止覆盖已有 Prefab（可选，看你需求，如果想覆盖就不加这个）
            fullPrefabPath = AssetDatabase.GenerateUniqueAssetPath(fullPrefabPath);
            
            PrefabUtility.SaveAsPrefabAsset(root, fullPrefabPath);

            // 销毁场景中的临时对象
            GameObject.DestroyImmediate(root);

            EditorUtility.DisplayDialog("Success", $"UI面板 {panelName} 创建完毕!", "OK");
            
            // 高亮选中新文件
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath));
        }
        
        #endregion
    }
}