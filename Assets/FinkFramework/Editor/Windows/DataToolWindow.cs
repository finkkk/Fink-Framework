using FinkFramework.Editor.Modules.Data;
using FinkFramework.Editor.Windows.Common;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Windows
{
    /// <summary>
    /// 数据工具面板
    /// </summary>
    public class DataToolWindow : EditorWindow
    {
        private Vector2 scroll;
        private string logOutput = "";
        private GUIStyle logStyle; 

        [MenuItem("Fink Framework/数据工具面板")]
        public static void Open()
        {
            var window = GetWindow<DataToolWindow>("数据工具面板");
            window.minSize = new Vector2(420, 380);
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            // ===== 标题 =====
            GUILayout.Label("数据工具面板", FFEditorStyles.Title);
            GUILayout.Space(10);

            // ===== 主功能区 =====
            DrawSection("主功能", () =>
            {
                if (GUILayout.Button("一键处理全部数据", FFEditorStyles.BigButton))
                    LogAndRun("一键处理全部数据", DataHandleTool.HandleAllData);

                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("清空加密数据",GUILayout.Height(26)))
                    LogAndRun("清空加密数据", DataCleanTool.ClearExportedData);

                if (GUILayout.Button("仅生成数据文件",GUILayout.Height(26)))
                    LogAndRun("仅生成数据文件", () => DataGenTool.GenerateAllData());

                if (GUILayout.Button("仅解析导出数据",GUILayout.Height(26)))
                    LogAndRun("仅解析导出数据", () => DataExportTool.ExportAllData());
                GUILayout.EndHorizontal();
            });

            GUILayout.Space(10);

            // ===== QA 区 =====
            DrawSection("QA 验证", () =>
            {
                if (GUILayout.Button("验证所有表格", FFEditorStyles.BigButton))
                    DataQATool.ValidateAllData();
            });

            GUILayout.Space(15);

            // ===== 日志区 =====
            DrawSection("执行日志", () =>
            {
                // log style 初始化
                if (logStyle == null)
                {
                    logStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true,
                        richText = true,
                        fontSize = 12,
                        normal = {
                            textColor = new Color(0.85f, 0.85f, 0.85f),
                            background = MakeTex(1,1,new Color(0.13f,0.13f,0.13f))
                        },
                        padding = new RectOffset(6,6,6,6)
                    };
                }

                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(160));
                GUILayout.TextArea(logOutput, logStyle);
                EditorGUILayout.EndScrollView();

                GUILayout.Space(6);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("导出日志", GUILayout.Height(24)))
                    ExportLog();
                if (GUILayout.Button("清空日志", GUILayout.Height(24)))
                    logOutput = "";
                GUILayout.EndHorizontal();
            });

            GUILayout.Space(8);

            GUILayout.Label("Copyright \u00A9 2025 Fink Framework",
                FFEditorStyles.Footer, GUILayout.ExpandWidth(true));

            GUILayout.Space(8);
        }

        // ===== Section 区块封装（统一盒子） =====
        private void DrawSection(string title, System.Action content)
        {
            EditorGUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Label(title, FFEditorStyles.SubTitle);
            GUILayout.Space(4);
            content?.Invoke();
            EditorGUILayout.EndVertical();
        }

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

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
