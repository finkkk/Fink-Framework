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
        private const string DisableKey = "FinkFramework_Welcome_Disabled";
        private static int _waitCounter = 0;

        static FrameworkWelcome()
        {
            // 若用户选择了永久关闭 → 直接不再弹
            if (EditorPrefs.GetBool(DisableKey, false))
                return;

            // 每次打开项目都尝试弹出欢迎窗口
            EditorApplication.update += WaitForEditorReady;
        }

        private static void WaitForEditorReady()
        {
            _waitCounter++;

            // 等待 Editor UI 初始化完全稳定
            if (_waitCounter < 30)
                return;

            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // 停止循环
            EditorApplication.update -= WaitForEditorReady;

            // 打开欢迎界面
            WelcomeWindow.ShowWindow();
        }
    }

    /// <summary>
    /// 欢迎使用面板
    /// </summary>
    public class WelcomeWindow : EditorWindow
    {
        private static Texture2D logo;
        private GUIStyle footerStyle;
        private const string DisableKey = "FinkFramework_Welcome_Disabled";
        private bool dontShowAgain = false;

        [MenuItem("Fink Framework/欢迎使用面板")]
        public static void ShowWindow()
        {
            var window = GetWindow<WelcomeWindow>(true, "欢迎使用 Fink Framework");
            window.minSize = new Vector2(560, 460);
            window.Show();
        }

        private void OnEnable()
        {
            dontShowAgain = EditorPrefs.GetBool(DisableKey, false);
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
            
            // ====== 永久关闭选项 ======
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool newValue = GUILayout.Toggle(dontShowAgain, "下次不再显示此窗口");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (newValue != dontShowAgain)
            {
                dontShowAgain = newValue;
                EditorPrefs.SetBool(DisableKey, dontShowAgain);
            }
            
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
