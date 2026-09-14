using System;
using System.Collections.Generic;
using FinkFramework.Editor.Common;
using FinkFramework.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.UI
{
    /// <summary>在 Play Mode 中查看 UIManager 当前管理的面板、状态与显示策略。</summary>
    public sealed class UIRuntimeDebuggerWindow : EditorWindow
    {
        private const double RefreshInterval = 0.25d;

        private enum PanelFilter
        {
            All,
            Visible,
            Transitioning,
            Hidden
        }

        private static readonly string[] FilterNames =
        {
            "全部面板",
            "当前可见",
            "加载或过渡",
            "已经隐藏"
        };

        private static GUIStyle metricValueStyle;
        private static GUIStyle emptyTitleStyle;
        private static GUIStyle emptyDescriptionStyle;
        private static GUIStyle badgeStyle;

        private readonly List<UIPanelSnapshot> snapshots = new();
        private readonly List<UISurfaceSnapshot> surfaceSnapshots = new();
        private Vector2 scrollPosition;
        private string searchText = string.Empty;
        private bool autoRefresh = true;
        private bool showSurfaces = true;
        private bool showPanels = true;
        private PanelFilter panelFilter;
        private double nextRefreshTime;

        [MenuItem("Fink Framework/UI 系统/运行时 UI 调试器", false, 110)]
        public static void Open()
        {
            var window = GetWindow<UIRuntimeDebuggerWindow>("UI 运行时调试器");
            window.minSize = new Vector2(620f, 420f);
            window.RefreshSnapshots();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnGUI()
        {
            FFEditorGUI.BeginWindowContent();
            GUILayout.Space(10f);
            FFEditorGUI.Center(() => GUILayout.Label("UI 运行时调试器", FFEditorStyles.Title));
            FFEditorGUI.Center(() => GUILayout.Label(
                "实时查看挂载表面、面板状态、页面栈策略与资源生命周期",
                EditorStyles.centeredGreyMiniLabel));
            GUILayout.Space(10f);

            DrawToolbar();
            GUILayout.Space(FFEditorStyles.SectionSpacing);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (!EditorApplication.isPlaying)
            {
                DrawEmptyState(
                    "等待进入运行模式",
                    "运行游戏后，这里会自动显示 UIManager 管理的挂载表面和面板状态。");
            }
            else if (!UIManager.HasInstance)
            {
                DrawEmptyState(
                    "等待 UI 系统初始化",
                    "首次通过 UIManager 打开或预加载面板后，调试数据会自动出现。");
            }
            else
            {
                DrawSummary();
                GUILayout.Space(FFEditorStyles.SectionSpacing);
                DrawSurfaces();
                GUILayout.Space(FFEditorStyles.SectionSpacing);
                DrawPanels();
            }

            FFEditorGUI.DrawFrameworkFooter();
            EditorGUILayout.EndScrollView();
            FFEditorGUI.EndWindowContent();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("搜索", FFEditorStyles.ToolbarLabel, GUILayout.Width(34f));
            searchText = GUILayout.TextField(
                searchText,
                GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField,
                GUILayout.MinWidth(120f));

            if (!string.IsNullOrEmpty(searchText)
                && GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(24f)))
            {
                searchText = string.Empty;
                GUI.FocusControl(null);
            }

            panelFilter = (PanelFilter)EditorGUILayout.Popup(
                (int)panelFilter,
                FilterNames,
                EditorStyles.toolbarPopup,
                GUILayout.Width(88f));
            autoRefresh = GUILayout.Toggle(
                autoRefresh,
                new GUIContent("自动刷新", "每 0.25 秒刷新一次运行时快照"),
                EditorStyles.toolbarButton,
                GUILayout.Width(72f));
            if (GUILayout.Button("立即刷新", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                RefreshSnapshots();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            int visibleCount = 0;
            int transitionCount = 0;
            foreach (UIPanelSnapshot snapshot in snapshots)
            {
                if (snapshot.ActiveInHierarchy)
                    visibleCount++;
                if (snapshot.State is UIPanelState.Opening or UIPanelState.Closing)
                    transitionCount++;
            }

            FFEditorGUI.DrawSectionHeader("运行概览");
            EditorGUILayout.BeginHorizontal();
            DrawMetric("挂载表面", surfaceSnapshots.Count);
            DrawMetric("管理面板", snapshots.Count);
            DrawMetric("当前可见", visibleCount);
            DrawMetric("正在过渡", transitionCount);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawMetric(string label, int value)
        {
            EditorGUILayout.BeginVertical(
                FFEditorStyles.SectionBox,
                GUILayout.MinWidth(90f),
                GUILayout.ExpandWidth(true));
            GUILayout.Label(value.ToString(), MetricValueStyle);
            GUILayout.Label(label, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private static void DrawEmptyState(string title, string description)
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Space(12f);
            GUILayout.Label(title, EmptyTitleStyle);
            GUILayout.Label(description, EmptyDescriptionStyle);
            GUILayout.Space(12f);
            GUILayout.EndVertical();
        }

        private static void DrawBadge(string text, Color color)
        {
            var content = new GUIContent(text);
            float width = Mathf.Max(58f, BadgeStyle.CalcSize(content).x + 16f);
            Rect rect = GUILayoutUtility.GetRect(
                width,
                20f,
                GUILayout.Width(width),
                GUILayout.Height(20f));
            EditorGUI.DrawRect(rect, color);
            GUI.Label(rect, content, BadgeStyle);
        }

        private static GUIStyle MetricValueStyle
        {
            get
            {
                metricValueStyle ??= new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter
                };

                return metricValueStyle;
            }
        }

        private static GUIStyle EmptyTitleStyle => emptyTitleStyle ??=
            new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };

        private static GUIStyle EmptyDescriptionStyle => emptyDescriptionStyle ??=
            new GUIStyle(EditorStyles.wordWrappedMiniLabel) { alignment = TextAnchor.MiddleCenter };

        private static GUIStyle BadgeStyle
        {
            get
            {
                badgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal =
                    {
                        textColor = Color.white
                    }
                };

                return badgeStyle;
            }
        }

        private static Color GetSurfaceColor(UISurfaceSnapshot snapshot) =>
            snapshot.IsActive
                ? new Color(0.20f, 0.55f, 0.32f, 0.95f)
                : new Color(0.38f, 0.38f, 0.38f, 0.95f);

        private static Color GetStateColor(UIPanelState state) => state switch
        {
            UIPanelState.Active => new Color(0.20f, 0.55f, 0.32f, 0.95f),
            UIPanelState.Opening => new Color(0.16f, 0.48f, 0.70f, 0.95f),
            UIPanelState.Closing => new Color(0.73f, 0.43f, 0.14f, 0.95f),
            UIPanelState.Loading => new Color(0.42f, 0.38f, 0.72f, 0.95f),
            UIPanelState.Failed => new Color(0.72f, 0.22f, 0.22f, 0.95f),
            UIPanelState.Disposed => new Color(0.30f, 0.30f, 0.30f, 0.95f),
            _ => new Color(0.38f, 0.38f, 0.38f, 0.95f)
        };

        private void DrawSurfaces()
        {
            showSurfaces = EditorGUILayout.Foldout(
                showSurfaces,
                $"挂载表面（{surfaceSnapshots.Count}）",
                true,
                EditorStyles.foldoutHeader);
            if (!showSurfaces)
                return;

            if (surfaceSnapshots.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有已注册的挂载表面。", MessageType.None);
                return;
            }

            foreach (UISurfaceSnapshot snapshot in surfaceSnapshots)
            {
                EditorGUILayout.BeginVertical(FFEditorStyles.SectionBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(snapshot.Id.Value, EditorStyles.boldLabel);
                DrawBadge(snapshot.IsActive ? "已启用" : "已隐藏", GetSurfaceColor(snapshot));
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2f);
                FFEditorGUI.DrawStatusRow("画布类型", snapshot.IsWorldSpace ? "世界空间" : "屏幕空间");
                FFEditorGUI.DrawStatusRow("生命周期", GetSurfaceLifetimeName(snapshot.Lifetime));
                FFEditorGUI.DrawStatusRow("所属场景", snapshot.OwnerSceneName);
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawPanels()
        {
            int matchedCount = GetMatchedPanelCount();
            showPanels = EditorGUILayout.Foldout(
                showPanels,
                $"面板实例（{matchedCount}/{snapshots.Count}）",
                true,
                EditorStyles.foldoutHeader);
            if (!showPanels)
                return;

            bool hasResult = false;
            foreach (UIPanelSnapshot snapshot in snapshots)
            {
                if (!MatchesFilter(snapshot))
                    continue;

                hasResult = true;
                DrawPanelCard(snapshot);
                GUILayout.Space(4f);
            }

            if (!hasResult)
                DrawEmptyState("没有匹配结果", "请调整搜索内容或面板状态筛选条件。");
        }

        private void DrawPanelCard(UIPanelSnapshot snapshot)
        {
            EditorGUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetShortPanelName(snapshot.Key.PanelId.Value), EditorStyles.boldLabel);
            DrawBadge(GetStateName(snapshot.State), GetStateColor(snapshot.State));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(snapshot.Key.PanelId.Value, EditorStyles.miniLabel);
            GUILayout.Space(3f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            FFEditorGUI.DrawStatusRow("挂载表面", snapshot.Key.SurfaceId.Value, 68f);
            FFEditorGUI.DrawStatusRow("实例标识", snapshot.Key.InstanceId.Value, 68f);
            FFEditorGUI.DrawStatusRow("显示方式", GetPresentationName(snapshot.Options.Presentation), 68f);
            if (snapshot.Options.Presentation == UIPresentationMode.Page)
                FFEditorGUI.DrawStatusRow("导航操作", GetNavigationName(snapshot.Options.Navigation), 68f);
            EditorGUILayout.EndVertical();
            GUILayout.Space(10f);
            EditorGUILayout.BeginVertical();
            FFEditorGUI.DrawStatusRow("显示层级", GetLayerName(snapshot.Options.Layer), 68f);
            FFEditorGUI.DrawStatusRow("缓存策略", GetCacheName(snapshot.Options.CachePolicy), 68f);
            FFEditorGUI.DrawStatusRow("生命周期", GetPanelLifetimeName(snapshot.Lifetime), 68f);
            FFEditorGUI.DrawStatusRow("所属场景", snapshot.OwnerSceneName, 68f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2f);
            FFEditorGUI.DrawStatusRow("资源路径", snapshot.AssetPath);

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制标识", FFEditorStyles.ActionButton))
                EditorGUIUtility.systemCopyBuffer = snapshot.Key.ToString();

            using (new EditorGUI.DisabledScope(!CanFocus(snapshot.State)))
            {
                if (GUILayout.Button("定位焦点", FFEditorStyles.ActionButton))
                    UIManager.TryGetInstance()?.Focus(snapshot.Key);
            }

            using (new EditorGUI.DisabledScope(!CanClose(snapshot.State)))
            {
                if (GUILayout.Button("关闭", FFEditorStyles.ActionButton))
                    UIManager.TryGetInstance()?.Close(snapshot.Key);
                if (GUILayout.Button("关闭并释放", FFEditorStyles.ActionButton))
                    UIManager.TryGetInstance()?.Close(snapshot.Key, true);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void OnEditorUpdate()
        {
            if (!autoRefresh || EditorApplication.timeSinceStartup < nextRefreshTime)
                return;

            nextRefreshTime = EditorApplication.timeSinceStartup + RefreshInterval;
            RefreshSnapshots();
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange _) => RefreshSnapshots();

        private void RefreshSnapshots()
        {
            snapshots.Clear();
            surfaceSnapshots.Clear();
            if (!EditorApplication.isPlaying || !UIManager.HasInstance)
                return;

            UIManager manager = UIManager.TryGetInstance();
            if (manager == null)
                return;

            snapshots.AddRange(manager.GetPanelSnapshots());
            surfaceSnapshots.AddRange(manager.GetSurfaceSnapshots());
            snapshots.Sort((left, right) =>
            {
                int surface = string.Compare(
                    left.Key.SurfaceId.Value,
                    right.Key.SurfaceId.Value,
                    StringComparison.Ordinal);
                return surface != 0
                    ? surface
                    : string.Compare(
                        left.Key.PanelId.Value,
                        right.Key.PanelId.Value,
                        StringComparison.Ordinal);
            });
            surfaceSnapshots.Sort((left, right) => string.Compare(
                left.Id.Value,
                right.Id.Value,
                StringComparison.Ordinal));
        }

        private bool MatchesSearch(UIPanelSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            return Contains(snapshot.Key.PanelId.Value, searchText)
                   || Contains(snapshot.Key.InstanceId.Value, searchText)
                   || Contains(snapshot.Key.SurfaceId.Value, searchText)
                   || Contains(snapshot.AssetPath, searchText);
        }

        private bool MatchesFilter(UIPanelSnapshot snapshot)
        {
            if (!MatchesSearch(snapshot))
                return false;

            return panelFilter switch
            {
                PanelFilter.Visible => snapshot.ActiveInHierarchy,
                PanelFilter.Transitioning => snapshot.State is
                    UIPanelState.Loading or UIPanelState.Opening or UIPanelState.Closing,
                PanelFilter.Hidden => snapshot.State == UIPanelState.Hidden,
                _ => true
            };
        }

        private int GetMatchedPanelCount()
        {
            int count = 0;
            foreach (UIPanelSnapshot snapshot in snapshots)
            {
                if (MatchesFilter(snapshot))
                    count++;
            }

            return count;
        }

        private static bool Contains(string value, string search) =>
            value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string GetShortPanelName(string panelId)
        {
            if (string.IsNullOrEmpty(panelId))
                return "未命名面板";

            int separator = panelId.LastIndexOf('.');
            return separator >= 0 ? panelId[(separator + 1)..] : panelId;
        }

        private static bool CanFocus(UIPanelState state) =>
            state is UIPanelState.Opening or UIPanelState.Active;

        private static bool CanClose(UIPanelState state) =>
            state is not (UIPanelState.Disposed or UIPanelState.Failed);

        private static string GetStateName(UIPanelState state) => state switch
        {
            UIPanelState.Loading => "加载中",
            UIPanelState.Hidden => "已隐藏",
            UIPanelState.Opening => "打开中",
            UIPanelState.Active => "已激活",
            UIPanelState.Paused => "已暂停",
            UIPanelState.Closing => "关闭中",
            UIPanelState.Disposed => "已释放",
            UIPanelState.Failed => "失败",
            _ => state.ToString()
        };

        private static string GetPresentationName(UIPresentationMode mode) => mode switch
        {
            UIPresentationMode.Page => "页面栈",
            UIPresentationMode.Modal => "模态窗口",
            UIPresentationMode.Overlay => "叠加显示",
            _ => mode.ToString()
        };

        private static string GetNavigationName(UINavigationMode mode) => mode switch
        {
            UINavigationMode.Push => "压入栈顶",
            UINavigationMode.Replace => "替换当前页",
            UINavigationMode.PopTo => "返回到此页",
            UINavigationMode.Reset => "重置页面栈",
            _ => mode.ToString()
        };

        private static string GetLayerName(UILayer layer) => layer switch
        {
            UILayer.Bottom => "底层",
            UILayer.Middle => "中层",
            UILayer.Top => "顶层",
            UILayer.System => "系统层",
            _ => layer.ToString()
        };

        private static string GetCacheName(UICachePolicy policy) => policy switch
        {
            UICachePolicy.KeepAlive => "关闭后保留",
            UICachePolicy.DestroyOnClose => "关闭后释放",
            _ => policy.ToString()
        };

        private static string GetPanelLifetimeName(UIPanelLifetime lifetime) => lifetime switch
        {
            UIPanelLifetime.Scene => "跟随场景",
            UIPanelLifetime.Persistent => "跨场景保留",
            _ => lifetime.ToString()
        };

        private static string GetSurfaceLifetimeName(UISurfaceLifetime lifetime) => lifetime switch
        {
            UISurfaceLifetime.Scene => "跟随场景",
            UISurfaceLifetime.Persistent => "跨场景保留",
            _ => lifetime.ToString()
        };
    }
}
