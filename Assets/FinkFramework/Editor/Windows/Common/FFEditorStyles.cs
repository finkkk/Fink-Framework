using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Windows.Common
{
    public static class FFEditorStyles
    {
        /// <summary>
        /// 窗口 主标题
        /// </summary>
        public static GUIStyle Title
        {
            get
            {
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter,
                    normal =
                    {
                        textColor = new Color(0.85f,0.85f,0.85f )
                    }
                };
                // ======== 取消 hover / active / focused 效果 =========
                style.hover.textColor   = style.normal.textColor;
                style.active.textColor  = style.normal.textColor;
                style.focused.textColor = style.normal.textColor;

                style.hover.background   = null;
                style.active.background  = null;
                style.focused.background = null;
                return style;
            }
        }
        
        /// <summary>
        /// 窗口 小标题
        /// </summary>
        public static GUIStyle SubTitle =>
            new(EditorStyles.boldLabel)
            {
                fontSize = 15,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.9f, 0.9f, 0.9f)
                        : Color.black
                }
            };
        
        /// <summary>
        /// 窗口 正文
        /// </summary>
        public static GUIStyle Description
        {
            get
            {
                var style = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    richText = true,
                    wordWrap = true,
                    normal =
                    {
                        textColor = new Color(0.85f, 0.85f, 0.85f)
                    }
                };

                // ======== 取消 hover / active / focused 效果 =========
                style.hover.textColor   = style.normal.textColor;
                style.active.textColor  = style.normal.textColor;
                style.focused.textColor = style.normal.textColor;

                style.hover.background   = null;
                style.active.background  = null;
                style.focused.background = null;

                return style;
            }
        }

        /// <summary>
        /// 窗口 选项标题
        /// </summary>
        public static GUIStyle SectionTitle =>
            new(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

        /// <summary>
        /// 内嵌窗口
        /// </summary>
        public static GUIStyle SectionBox =>
            new("HelpBox")
            {
                padding = new RectOffset(14, 14, 10, 10)
            };
        
        /// <summary>
        /// 窗口 大型按钮
        /// </summary>
        public static GUIStyle BigButton =>
            new(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                fixedHeight = 38,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 4, 4)
            };

        /// <summary>
        /// 窗口 页脚
        /// </summary>
        public static GUIStyle Footer =>
            new(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic,
                normal = {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.55f, 0.55f, 0.55f)
                        : new Color(0.35f, 0.35f, 0.35f)
                }
            };
    }
}