using System;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化变更预览窗口，统一显示编辑器修改和 Excel 导入差异。
    /// </summary>
    public sealed class LocalizationDiffPreviewWindow : EditorWindow
    {
        public enum SectionType
        {
            Summary,
            Added,
            Removed,
            Changed,
            Warning
        }

        public sealed class Section
        {
            public Section(string title, SectionType type, IReadOnlyList<string> items)
            {
                Title = title;
                Type = type;
                Items = items ?? Array.Empty<string>();
                Changes = Array.Empty<Change>();
            }

            private Section(string title, SectionType type, IReadOnlyList<Change> changes)
            {
                Title = title;
                Type = type;
                Items = Array.Empty<string>();
                Changes = changes ?? Array.Empty<Change>();
            }

            public static Section CreateChanges(
                string title,
                SectionType type,
                IReadOnlyList<Change> changes)
            {
                return new Section(title, type, changes);
            }

            public sealed class Change
            {
                public Change(string context, string before, string after)
                {
                    Context = context ?? string.Empty;
                    Before = before ?? string.Empty;
                    After = after ?? string.Empty;
                }

                public string Context { get; }
                public string Before { get; }
                public string After { get; }
            }

            public string Title { get; }
            public SectionType Type { get; }
            public IReadOnlyList<string> Items { get; }
            public IReadOnlyList<Change> Changes { get; }
        }

        private string description;
        private string confirmLabel;
        private Action confirmAction;
        private List<Section> sections = new List<Section>();
        private Vector2 scrollPosition;
        private GUIStyle descriptionStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle itemStyle;
        private GUIStyle countStyle;
        private GUIStyle changeHeaderStyle;
        private GUIStyle beforeValueStyle;
        private GUIStyle afterValueStyle;

        private void OnDestroy()
        {
            DestroyPreviewTexture(beforeValueStyle);
            DestroyPreviewTexture(afterValueStyle);
        }

        public static void Open(
            string title,
            string description,
            IReadOnlyList<Section> sections,
            string confirmLabel = null,
            Action confirmAction = null)
        {
            var window = CreateInstance<LocalizationDiffPreviewWindow>();
            window.titleContent = new GUIContent(title);
            window.description = description ?? string.Empty;
            window.sections = sections?.ToList() ?? new List<Section>();
            window.confirmLabel = confirmLabel;
            window.confirmAction = confirmAction;
            float pixelsPerPoint = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            float windowWidth = Mathf.Max(680f, 960f / pixelsPerPoint);
            window.minSize = new Vector2(500f, 420f);
            Rect defaultPosition = new Rect(200f, 120f, windowWidth, 540f);
            window.ShowUtility();
            // Unity 可能在 ShowUtility 前恢复旧的浮动窗口尺寸，因此显示后再设置一次。
            window.position = defaultPosition;
            window.Focus();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                false,
                false,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(FFEditorStyles.WindowPadding);
            EditorGUILayout.BeginVertical(
                GUILayout.ExpandWidth(true));
            DrawHeader();
            if (sections.Count == 0)
            {
                EditorGUILayout.HelpBox("没有可显示的差异。", MessageType.Info);
            }
            else
            {
                foreach (Section section in sections)
                    DrawSection(section);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(FFEditorStyles.WindowPadding);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            GUILayout.Space(FFEditorStyles.ControlSpacing);
            EditorGUILayout.BeginHorizontal(
                EditorStyles.toolbar,
                GUILayout.Height(FFEditorStyles.ToolbarHeight));
            GUILayout.Space(FFEditorStyles.ControlSpacing);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    string.IsNullOrEmpty(confirmLabel) ? "关闭" : "取消",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(90f)))
            {
                Close();
                GUIUtility.ExitGUI();
            }

            if (!string.IsNullOrEmpty(confirmLabel) && confirmAction != null)
            {
                if (GUILayout.Button(
                        confirmLabel,
                        EditorStyles.toolbarButton,
                        GUILayout.Width(110f)))
                {
                    Action action = confirmAction;
                    Close();
                    action?.Invoke();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(titleContent.text, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(description))
                EditorGUILayout.LabelField(description, GetDescriptionStyle());
            EditorGUILayout.EndVertical();
        }

        private void DrawSection(Section section)
        {
            Color accentColor = GetAccentColor(section.Type);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Rect accentRect = EditorGUILayout.GetControlRect(
                false,
                3f,
                GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(
                accentRect,
                accentColor);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(section.Title, GetSectionTitleStyle());
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                GetSectionItemCount(section).ToString(),
                GetCountStyle(accentColor),
                GUILayout.Width(42f));
            EditorGUILayout.EndHorizontal();

            if (section.Changes.Count > 0)
            {
                DrawChangeTable(section.Changes);
            }
            else if (section.Items.Count == 0)
            {
                EditorGUILayout.LabelField("没有项目", GetItemStyle());
            }
            else
            {
                foreach (string item in section.Items)
                    EditorGUILayout.LabelField(item, GetItemStyle());
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private static int GetSectionItemCount(Section section)
        {
            return section.Changes.Count > 0
                ? section.Changes.Count
                : section.Items.Count;
        }

        private void DrawChangeTable(IReadOnlyList<Section.Change> changes)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(
                false,
                28f,
                GUILayout.ExpandWidth(true));
            DrawGridRow(
                headerRect,
                "位置",
                "修改前",
                "修改后",
                GetChangeHeaderStyle(),
                GetChangeHeaderStyle(),
                GetChangeHeaderStyle(),
                new Color(0.22f, 0.22f, 0.22f, 1f),
                new Color(0.22f, 0.22f, 0.22f, 1f),
                new Color(0.22f, 0.22f, 0.22f, 1f),
                28f);

            foreach (Section.Change change in changes)
            {
                float availableValueWidth = GetValueColumnWidth(headerRect.width);
                float beforeHeight = GetBeforeValueStyle().CalcHeight(
                    new GUIContent(change.Before),
                    availableValueWidth);
                float afterHeight = GetAfterValueStyle().CalcHeight(
                    new GUIContent(change.After),
                    availableValueWidth);
                float rowHeight = Mathf.Clamp(
                    Mathf.Max(38f, beforeHeight, afterHeight),
                    38f,
                    120f);
                Rect rowRect = EditorGUILayout.GetControlRect(
                    false,
                    rowHeight,
                    GUILayout.ExpandWidth(true));
                DrawGridRow(
                    rowRect,
                    change.Context,
                    change.Before,
                    change.After,
                    GetItemStyle(),
                    GetBeforeValueStyle(),
                    GetAfterValueStyle(),
                    new Color(0.18f, 0.18f, 0.18f, 1f),
                    new Color(0.34f, 0.13f, 0.13f, 0.8f),
                    new Color(0.12f, 0.32f, 0.16f, 0.8f),
                    rowHeight);
                GUILayout.Space(2f);
            }
        }

        private static float GetValueColumnWidth(float totalWidth)
        {
            float contextWidth = Mathf.Clamp(totalWidth * 0.28f, 190f, 280f);
            return Mathf.Max(150f, (totalWidth - contextWidth - 2f) * 0.5f);
        }

        private static void DrawGridRow(
            Rect rowRect,
            string context,
            string before,
            string after,
            GUIStyle contextStyle,
            GUIStyle beforeStyle,
            GUIStyle afterStyle,
            Color contextColor,
            Color beforeColor,
            Color afterColor,
            float rowHeight)
        {
            float contextWidth = Mathf.Clamp(rowRect.width * 0.28f, 190f, 280f);
            float valueWidth = (rowRect.width - contextWidth - 2f) * 0.5f;
            Rect contextRect = new Rect(rowRect.x, rowRect.y, contextWidth, rowHeight);
            Rect beforeRect = new Rect(
                contextRect.xMax + 1f,
                rowRect.y,
                valueWidth,
                rowHeight);
            Rect afterRect = new Rect(
                beforeRect.xMax + 1f,
                rowRect.y,
                Mathf.Max(0f, rowRect.xMax - beforeRect.xMax - 1f),
                rowHeight);

            DrawGridCell(contextRect, context, contextStyle, contextColor);
            DrawGridCell(beforeRect, before, beforeStyle, beforeColor);
            DrawGridCell(afterRect, after, afterStyle, afterColor);
        }

        private static void DrawGridCell(Rect rect, string text, GUIStyle style, Color backgroundColor)
        {
            Color borderColor = EditorGUIUtility.isProSkin
                ? new Color(0.08f, 0.08f, 0.08f, 1f)
                : new Color(0.45f, 0.45f, 0.45f, 1f);
            EditorGUI.DrawRect(rect, borderColor);
            Rect contentRect = new Rect(
                rect.x + 1f,
                rect.y + 1f,
                Mathf.Max(0f, rect.width - 2f),
                Mathf.Max(0f, rect.height - 2f));
            EditorGUI.DrawRect(contentRect, backgroundColor);
            GUI.Label(contentRect, text ?? string.Empty, style);
        }

        private GUIStyle GetDescriptionStyle()
        {
            if (descriptionStyle != null)
                return descriptionStyle;

            descriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
            return descriptionStyle;
        }

        private GUIStyle GetSectionTitleStyle()
        {
            if (sectionTitleStyle != null)
                return sectionTitleStyle;

            sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
            return sectionTitleStyle;
        }

        private GUIStyle GetItemStyle()
        {
            if (itemStyle != null)
                return itemStyle;

            itemStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                padding = new RectOffset(10, 4, 2, 2)
            };
            return itemStyle;
        }

        private GUIStyle GetChangeHeaderStyle()
        {
            if (changeHeaderStyle != null)
                return changeHeaderStyle;

            changeHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 4, 4)
            };
            return changeHeaderStyle;
        }

        private GUIStyle GetBeforeValueStyle()
        {
            if (beforeValueStyle != null)
                return beforeValueStyle;

            beforeValueStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6),
                normal =
                {
                    background = CreateColorTexture(new Color(0.34f, 0.13f, 0.13f, 0.8f))
                }
            };
            return beforeValueStyle;
        }

        private GUIStyle GetAfterValueStyle()
        {
            if (afterValueStyle != null)
                return afterValueStyle;

            afterValueStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6),
                normal =
                {
                    background = CreateColorTexture(new Color(0.12f, 0.32f, 0.16f, 0.8f))
                }
            };
            return afterValueStyle;
        }

        private static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DestroyPreviewTexture(GUIStyle style)
        {
            if (style?.normal.background != null)
                DestroyImmediate(style.normal.background);
        }

        private GUIStyle GetCountStyle(Color color)
        {
            countStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleRight
            };

            countStyle.normal.textColor = color;
            return countStyle;
        }

        private static Color GetAccentColor(SectionType type)
        {
            switch (type)
            {
                case SectionType.Added:
                    return new Color(0.35f, 0.78f, 0.45f);
                case SectionType.Removed:
                    return new Color(0.92f, 0.38f, 0.35f);
                case SectionType.Changed:
                    return new Color(0.95f, 0.72f, 0.28f);
                case SectionType.Warning:
                    return new Color(0.95f, 0.56f, 0.25f);
                default:
                    return new Color(0.35f, 0.62f, 0.9f);
            }
        }
    }
}
