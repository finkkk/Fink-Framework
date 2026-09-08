using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化 Key 的可搜索下拉菜单。
    /// 使用固定尺寸的 Popup，搜索框固定在顶部，条目区域超出后通过滚动条浏览。
    /// </summary>
    internal sealed class LocalizedKeyAdvancedDropdown : PopupWindowContent
    {
        internal sealed class Option
        {
            public Option(string fullKey, string displayName)
            {
                FullKey = fullKey;
                DisplayName = displayName;
            }

            public string FullKey { get; }
            public string DisplayName { get; }
        }

        private readonly IReadOnlyList<Option> options;
        private readonly Action<Option> onSelected;
        private string searchText = string.Empty;
        private Vector2 scrollPosition;
        private GUIStyle optionLabelStyle;

        public LocalizedKeyAdvancedDropdown(
            IReadOnlyList<Option> options,
            Action<Option> onSelected)
        {
            this.options = options ?? Array.Empty<Option>();
            this.onSelected = onSelected;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(380f, 360f);
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.BeginVertical();
            GUI.SetNextControlName("LocalizedKeySearchField");
            searchText = EditorGUILayout.TextField(
                searchText,
                GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarSearchField);

            List<Option> visibleOptions = options
                .Where(option => string.IsNullOrWhiteSpace(searchText)
                    || option.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || option.FullKey.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (visibleOptions.Count == 0)
            {
                EditorGUILayout.HelpBox("没有匹配的 Key。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            foreach (Option option in visibleOptions)
            {
                Rect optionRect = GUILayoutUtility.GetRect(
                    new GUIContent(BuildLabel(option)),
                    GetOptionLabelStyle(),
                    GUILayout.Width(Mathf.Max(1f, rect.width - 24f)));
                if (GUI.Button(optionRect, GUIContent.none, GUIStyle.none))
                {
                    onSelected?.Invoke(option);
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }

                // 保留纯文本条目的外观，但补回鼠标悬停时的蓝色背景提示。
                // 透明按钮只负责点击，不能依赖它绘制 hover，否则会恢复成按钮样式。
                if (Event.current.type == EventType.Repaint
                    && optionRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(optionRect, GetHoverBackgroundColor());
                }
                EditorGUI.LabelField(optionRect, BuildLabel(option), GetOptionLabelStyle());
                EditorGUIUtility.AddCursorRect(optionRect, MouseCursor.Link);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        public override void OnOpen()
        {
            EditorGUI.FocusTextInControl("LocalizedKeySearchField");
        }

        private static string BuildLabel(Option option)
        {
            return string.Equals(option.DisplayName, option.FullKey, StringComparison.Ordinal)
                ? option.DisplayName
                : $"{option.DisplayName}（{option.FullKey}）";
        }

        private GUIStyle GetOptionLabelStyle()
        {
            if (optionLabelStyle != null)
                return optionLabelStyle;

            optionLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 4, 2, 2),
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            return optionLabelStyle;
        }

        private static Color GetHoverBackgroundColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.48f, 0.78f, 1f)
                : new Color(0.35f, 0.60f, 0.92f, 1f);
        }

        public void Show(Rect rect)
        {
            PopupWindow.Show(rect, this);
        }
    }
}
