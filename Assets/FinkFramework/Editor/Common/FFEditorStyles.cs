using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Common
{
    public static class FFEditorStyles
    {
        public const float WindowPadding = 12f;
        public const float SectionSpacing = 10f;
        public const float ControlSpacing = 6f;
        public const float ToolbarHeight = 24f;
        public const float ActionHeight = 26f;
        public const float CardHeight = 88f;

        private static GUIStyle sectionBox;
        private static GUIStyle sectionTitle;
        private static GUIStyle description;
        private static GUIStyle rowBox;
        private static GUIStyle centeredLabel;
        private static GUIStyle toolbarLabel;
        private static GUIStyle primaryButton;
        private static GUIStyle actionButton;

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
                if (description != null)
                    return description;

                description = new GUIStyle(EditorStyles.label)
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
                description.hover.textColor = description.normal.textColor;
                description.active.textColor = description.normal.textColor;
                description.focused.textColor = description.normal.textColor;

                description.hover.background = null;
                description.active.background = null;
                description.focused.background = null;

                return description;
            }
        }

        /// <summary>
        /// 窗口 选项标题
        /// </summary>
        public static GUIStyle SectionTitle
        {
            get
            {
                if (sectionTitle == null)
                {
                    sectionTitle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 13,
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                }

                return sectionTitle;
            }
        }

        /// <summary>
        /// 内嵌窗口
        /// </summary>
        public static GUIStyle SectionBox
        {
            get
            {
                if (sectionBox == null)
                {
                    sectionBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(12, 12, 9, 10),
                        stretchWidth = true
                    };
                }

                return sectionBox;
            }
        }

        /// <summary>
        /// 无内边距的表格行或操作行容器。
        /// </summary>
        public static GUIStyle RowBox
        {
            get
            {
                if (rowBox == null)
                {
                    rowBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(0, 0, 0, 0),
                        stretchWidth = true
                    };
                }

                return rowBox;
            }
        }

        public static GUIStyle CenteredLabel
        {
            get
            {
                if (centeredLabel == null)
                {
                    centeredLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        clipping = TextClipping.Clip
                    };
                }

                return centeredLabel;
            }
        }

        /// <summary>
        /// 工具栏中的普通文本标签。不要直接使用 Unity 内部的 toolbarLabel。
        /// </summary>
        public static GUIStyle ToolbarLabel
        {
            get
            {
                if (toolbarLabel == null)
                {
                    toolbarLabel = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(2, 2, 0, 0)
                    };
                }

                return toolbarLabel;
            }
        }

        public static GUIStyle PrimaryButton
        {
            get
            {
                if (primaryButton == null)
                {
                    primaryButton = new GUIStyle(GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fixedHeight = 0,
                        padding = new RectOffset(8, 8, 3, 3)
                    };
                }

                return primaryButton;
            }
        }

        public static GUIStyle ActionButton
        {
            get
            {
                if (actionButton == null)
                {
                    actionButton = new GUIStyle(GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fixedHeight = 0,
                        padding = new RectOffset(8, 8, 3, 3)
                    };
                }

                return actionButton;
            }
        }
        
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
        /// 窗口 小型按钮
        /// </summary>
        public static GUIStyle SmallButton
        {
            get
            {
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fixedHeight = 22,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(6, 6, 2, 2),
                    fixedWidth = 0 // 不强制铺满
                };
                return style;
            }
        }

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
