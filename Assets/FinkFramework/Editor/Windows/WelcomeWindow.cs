using FinkFramework.Editor.Utils;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Environments;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Windows
{
    /// <summary>
    /// 欢迎界面安全初始化（仅工程第一次加载时弹一次）
    /// </summary>
    [InitializeOnLoad]
    public static class FrameworkWelcome
    {
        private const string Key = "Framework_WelcomePanel_Shown";

        static FrameworkWelcome()
        {
            // 如果不是第一次，不做任何事
            if (EditorPrefs.GetBool(Key, false))
                return;

            // 延迟到 Editor 完全初始化之后执行
            EditorApplication.delayCall += SafeInit;
        }

        private static void SafeInit()
        {
            // 再次确保延迟执行不会重复
            if (EditorPrefs.GetBool(Key, false))
                return;

            // 设置已显示标记
            EditorPrefs.SetBool(Key, true);

            // 如果当前 Editor 正在打开 Project Settings，则不要弹出
            if (IsProjectSettingsOpening())
                return;

            WelcomeWindow.ShowWindow();
        }

        /// <summary>
        /// 更可靠的 Project Settings 打开检测
        /// </summary>
        private static bool IsProjectSettingsOpening()
        {
            var win = EditorWindow.focusedWindow;

            if (win == null)
            {
                // 重点：如果窗口为空，不要贸然弹窗（避免初始化早期和 Settings 打开中）
                return true;
            }

            string title = win.titleContent.text;
            return title.Contains("Project Settings");
        }
    }

    /// <summary>
    /// 欢迎使用面板
    /// </summary>
    public class WelcomeWindow : EditorWindow
    {
        private static Texture2D logo;
        private GUIStyle footerStyle;

        [MenuItem("Fink Framework/欢迎使用面板")]
        public static void ShowWindow()
        {
            var window = GetWindow<WelcomeWindow>(true, "欢迎使用 Fink Framework");
            window.minSize = new Vector2(540, 440);
        }

        private void OnEnable()
        {
            logo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/FinkFramework/Resources/FinkFramework_logo.png"
            );
        }

        private void OnGUI()
        {
            float ww = position.width;

            GUILayout.Space(20);

            // ===== Logo =====
            if (logo)
            {
                FFEditorGUI.Center(() =>
                {
                    GUILayout.Label(logo, GUILayout.Width(128), GUILayout.Height(128));
                });
                GUILayout.Space(10);
            }

            // ===== 标题 =====
            GUILayout.Label("欢迎使用 Fink Framework", FFEditorStyles.Title);

            // ===== 版本号 =====
            GUILayout.Label($"当前版本：v{EnvironmentState.FrameworkVersion}",
                EditorStyles.centeredGreyMiniLabel);

            GUILayout.Space(18);
            FFEditorGUI.Separator();
            GUILayout.Space(15);

            FFEditorGUI.Center(() =>
            {
                GUILayout.Label(
                    "感谢您使用 Fink Framework —— Unity 模块化开发框架！\n\n" +
                    "Fink Framework 提供：数据驱动管线、UI 系统、资源加载、对象池、事件系统、" +
                    "输入系统、计时器等基础设施，适用于中小型项目的快速研发。\n\n" +
                    "<b>首次使用请前往 Project Settings → Fink Framework 进行基础配置。</b>",
                    FFEditorStyles.Description,
                    GUILayout.Width(ww * 0.78f)
                );
            });

            GUILayout.Space(30);

            // ===== 按钮组 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.BeginVertical(GUILayout.Width(ww * 0.70f));

                if (GUILayout.Button("查看使用文档（Documentation）", FFEditorStyles.BigButton))
                    Application.OpenURL("https://finkkk.cn/docs/fink-framework");

                GUILayout.Space(10);

                if (GUILayout.Button("打开框架设置（Project Settings）", FFEditorStyles.BigButton))
                    SettingsService.OpenProjectSettings("Project/Fink Framework");

                GUILayout.Space(10);

                if (GUILayout.Button("联系作者 / 个人博客（finkkk.cn）", FFEditorStyles.BigButton))
                    Application.OpenURL("https://finkkk.cn");
                
                GUILayout.Space(10);

                if (GUILayout.Button("立即检查更新（Check Update）", FFEditorStyles.BigButton))
                {
                    UpdateCheckUtil.CheckUpdateManual();
                }

                GUILayout.EndVertical();
            });

            GUILayout.FlexibleSpace();
            GUILayout.Space(20);

            // ===== 关闭按钮 =====
            FFEditorGUI.Center(() =>
            {
                if (GUILayout.Button("关闭", GUILayout.Height(28), GUILayout.Width(140)))
                    Close();
            });

            GUILayout.Space(8);

            // ===== 页脚 =====
            GUILayout.Label("Copyright \u00A9 2025 Fink Framework",
                FFEditorStyles.Footer, GUILayout.ExpandWidth(true));

            GUILayout.Space(8);
        }
    }
}
