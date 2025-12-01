using FinkFramework.Editor.DataTools;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Windows
{
    public class DataToolWindow : EditorWindow
    {
        private Vector2 scroll;
        private string logOutput = "";
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle logStyle;
        private GUIStyle footerStyle;

        [MenuItem("Fink Framework/数据工具面板", priority = 50)]
        public static void Open()
        {
            var window = GetWindow<DataToolWindow>("数据工具面板");
            window.minSize = new Vector2(420, 380);
        }

        private void OnEnable()
        {
            // 主标题样式
            titleStyle = new GUIStyle
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
                hover = { textColor = new Color(0.85f, 0.85f, 0.85f) },
                active = { textColor = new Color(0.85f, 0.85f, 0.85f) },
                focused = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
        }

        private void OnGUI()
        {
            // 确保 GUIStyle 只在 GUI Skin 准备好后创建
            if (logStyle == null && EditorStyles.textArea != null)
            {
                logStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    richText = true,
                    fontSize = 12,
                    normal = {
                        textColor = new Color(0.85f, 0.85f, 0.85f),
                        background = MakeTex(1, 1, new Color(0.13f, 0.13f, 0.13f))
                    },
                    padding = new RectOffset(6, 6, 6, 6)
                };
            }

            if (sectionStyle == null && GUI.skin)
            {
                sectionStyle = new GUIStyle("HelpBox")
                {
                    padding = new RectOffset(14, 14, 10, 10)
                };
            }

            if (footerStyle == null && EditorStyles.centeredGreyMiniLabel != null)
            {
                footerStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
                };
            }
            
            GUILayout.Space(10);
            GUILayout.Label("数据工具面板", titleStyle);
            GUILayout.Space(10);

            DrawSection("主功能", () =>
            {
                if (GUILayout.Button("一键处理全部数据（清空 → 生成 → 加密）", GUILayout.Height(38)))
                    LogAndRun("一键处理全部数据", DataHandleTool.HandleAllData);

                GUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("清空加密数据", GUILayout.Height(30)))
                    LogAndRun("清空加密数据", DataExportTool.ClearEncryptData);
                if (GUILayout.Button("仅生成数据文件", GUILayout.Height(30)))
                    LogAndRun("仅生成数据文件", () => DataGenTool.GenerateAllData());
                if (GUILayout.Button("仅解析导出数据", GUILayout.Height(30)))
                    LogAndRun("仅解析导出数据", () => DataExportTool.EncryptAllData());
         
                EditorGUILayout.EndHorizontal();
            });

            GUILayout.Space(10);
            
            DrawSection("QA 验证", () =>
            {
                if (GUILayout.Button("验证所有表格 (QA 模式)", GUILayout.Height(30)))
                {
                    DataQATool.ValidateAllData();
                }
            });

            GUILayout.Space(15);

            DrawSection("执行日志", () =>
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(160));
                GUILayout.TextArea(logOutput, logStyle);
                EditorGUILayout.EndScrollView();

                GUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("导出日志", GUILayout.Height(24)))
                    ExportLog();
                if (GUILayout.Button("清空日志", GUILayout.Height(24)))
                    logOutput = "";
                EditorGUILayout.EndHorizontal();
            });

            GUILayout.Space(8);
            GUILayout.Label("© Fink Game Framework", footerStyle);
        }

        // ====== 区块封装 ======
        private void DrawSection(string title, System.Action content)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Space(4);
            content?.Invoke();
            EditorGUILayout.EndVertical();
        }

        // ====== 执行逻辑 ======
        private void LogAndRun(string actionName, System.Action action)
        {
            AppendLog($"<b>执行操作：</b><color=#2196F3>{actionName}</color>");
            AppendLog("<color=#AAAAAA>执行中...</color>");
            try
            {
                action?.Invoke();
                AppendLog("<color=#00C853>操作完成</color>");
            }
            catch (System.Exception ex)
            {
                AppendLog($"<color=#FF5252>错误：{ex.Message}</color>");
            }
        }

        private void AppendLog(string msg)
        {
            logOutput += $"[{System.DateTime.Now:HH:mm:ss}] {msg}\n";
            scroll.y = float.MaxValue;
            Repaint();
        }

        private void ExportLog()
        {
            string path = EditorUtility.SaveFilePanel("导出日志", Application.dataPath, "DataToolLog.txt", "txt");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, logOutput);
                EditorUtility.RevealInFinder(path);
                AppendLog("<color=#4CAF50>日志已导出</color>");
            }
        }

        // 生成纯色纹理（用于背景）
        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
