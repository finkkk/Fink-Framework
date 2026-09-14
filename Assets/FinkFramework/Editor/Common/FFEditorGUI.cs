using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Common
{
    public static class FFEditorGUI
    {
        /// <summary>
        /// 在当前窗口内容区域内水平居中绘制一段 GUI。
        /// </summary>
        public static void Center(System.Action content)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            content?.Invoke();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 开始带统一左右边距的窗口内容区域。
        /// </summary>
        public static void BeginWindowContent()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(FFEditorStyles.WindowPadding);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        }

        /// <summary>
        /// 结束由 BeginWindowContent 开启的窗口内容区域。
        /// </summary>
        public static void EndWindowContent()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(FFEditorStyles.WindowPadding);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制统一格式的分组标题和可选说明。
        /// </summary>
        public static void DrawSectionHeader(string title, string description = null)
        {
            EditorGUILayout.LabelField(title, FFEditorStyles.SectionTitle);
            if (!string.IsNullOrWhiteSpace(description))
            {
                GUILayout.Space(2f);
                EditorGUILayout.LabelField(description, FFEditorStyles.Description);
            }
        }

        /// <summary>
        /// 绘制标签和值对齐的状态行。
        /// </summary>
        public static void DrawStatusRow(string label, string value, float labelWidth = 112f)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(labelWidth));
            EditorGUILayout.LabelField(
                value ?? string.Empty,
                FFEditorStyles.Description,
                GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制一条适配当前皮肤的分隔线。
        /// </summary>
        public static void Separator(float thickness = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, thickness);
            Color color = EditorGUIUtility.isProSkin
                ? new Color(0.3f, 0.3f, 0.3f)
                : new Color(0.6f, 0.6f, 0.6f);
            EditorGUI.DrawRect(rect, color);
        }

        /// <summary>
        /// 绘制框架编辑器窗口统一使用的品牌页脚。
        /// </summary>
        public static void DrawFrameworkFooter(float topSpacing = 12f)
        {
            GUILayout.Space(topSpacing);
            Center(() =>
            {
                GUILayout.Label(
                    "Copyright \u00A9 2025 Fink Framework",
                    FFEditorStyles.Footer);
            });
            GUILayout.Space(4f);
        }
        
        /// <summary>
        /// 显示统一的确认对话框。
        /// </summary>
        public static bool ConfirmAction(string title, string message)
        {
            return EditorUtility.DisplayDialog(
                title,
                message,
                "确认执行",
                "取消"
            );
        }
    }
}
