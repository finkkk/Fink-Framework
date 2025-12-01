using FinkFramework.Runtime.Environments;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Windows
{
    [InitializeOnLoad]
    public class FrameworkWelcome
    {
        private const string Key = "Framework_WelcomePanel_Shown";

        static FrameworkWelcome()
        {
            // 只在导入包或首次打开项目后弹出
            if (!SessionState.GetBool(Key, false))
            {
                SessionState.SetBool(Key, true);
                EditorApplication.delayCall += FrameworkWelcomeWindow.ShowWindow;
            }
        }
    }

    public class FrameworkWelcomeWindow : EditorWindow
    {
        private static Texture2D logo;

        [MenuItem("Fink Framework/欢迎使用面板", priority = 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<FrameworkWelcomeWindow>(true, "欢迎使用 Fink Framework");
            window.minSize = new Vector2(480, 380);
        }

        private void OnEnable()
        {
            logo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Framework/Resources/f_logo.png"
            );
        }

        private void OnGUI()
        {
            // 背景整体留白
            GUILayout.Space(20);

            // ===== Logo =====
            if (logo)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(logo, GUILayout.Width(128), GUILayout.Height(128));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }

            // ===== 大标题 =====
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            GUILayout.Label("欢迎使用 Fink Framework！", titleStyle);
            
            // ===== 版本号 =====
            GUIStyle versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label($"当前版本：v{EnvironmentState.FrameworkVersion}", versionStyle);

            GUILayout.Space(15);
            
            // ===== 分隔线 =====
            DrawLine(1, new Color(0.25f, 0.25f, 0.25f));
            GUILayout.Space(15);

            // ===== 顶部欢迎文字描述 =====
            GUIStyle topStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
            
            // ===== 正文描述 =====
            GUIStyle descStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                wordWrap = true,
                richText = true,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "感谢使用 Fink Framework！\n\n" +
                "Fink Framework 是一套模块化游戏开发框架，适用于中小型项目的快速研发。" +
                "框架提供：数据驱动管线、UI 系统、资源加载、对象池、事件系统、输入系统、计时器、运行时工具链等核心基础设施。\n\n" +

                "<b>首次使用请前往 Project Settings → Fink Framework 完成基础配置。</b>\n" +
                "<color=#AAAAAA>Please go to Project Settings → Fink Framework to configure the basic settings.</color>\n\n" +

                "下面提供一些常用入口，帮助你快速开始使用。",
                descStyle,
                GUILayout.Width(440)
            );
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(25);

            // ===== 大按钮样式 =====
            GUIStyle bigButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fixedHeight = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white }
            };

            // ===== 按钮组 =====
            GUILayout.BeginHorizontal();
            GUILayout.Space(40);
            GUILayout.BeginVertical(GUILayout.Width(position.width - 80));

            // ---- 文档 ----
            if (GUILayout.Button("查看使用文档（Documentation）", bigButton))
            {
                Application.OpenURL("https://finkkk.cn/docs/fink-framework");
            }
            GUILayout.Space(10);

            // ---- 跳转到 Project Settings ----
            if (GUILayout.Button("打开框架设置（Project Settings）", bigButton))
            {
                SettingsService.OpenProjectSettings("Project/Fink Framework");
            }
            GUILayout.Space(10);

            // ---- 个人网站 ----
            if (GUILayout.Button("联系作者/个人博客（finkkk.cn）", bigButton))
            {
                Application.OpenURL("https://finkkk.cn");
            }

            GUILayout.EndVertical();
            GUILayout.Space(40);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // ===== 底部关闭按钮 =====
            GUILayout.Space(25);
            GUIStyle closeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fixedHeight = 26
            };

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭", closeStyle, GUILayout.Width(120)))
                Close();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        // 工具函数：画分隔线
        private void DrawLine(int thickness, Color color)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, thickness);
            EditorGUI.DrawRect(rect, color);
        }
    }
}
