#if UNITY_EDITOR
using System.Linq;
using FinkFramework.Editor.Common;
using FinkFramework.Editor.Utils;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.ProjectStats
{
    /// <summary>
    /// 项目归档面板：导出统计报告和项目源码等归档资料。
    /// </summary>
    public class ProjectArchiveWindow : EditorWindow
    {
        private ProjectStatUtil.StatOptions options;
        private Vector2 _scrollPos;

        [MenuItem("Fink Framework/统计与归档/项目归档面板", false, 110)]
        public static void Open()
        {
            var window = GetWindow<ProjectArchiveWindow>("项目归档面板");
            window.minSize = new Vector2(420, 560);
        }

        private void OnEnable()
        {
            options = ProjectStatsPreferences.Load();
        }

        private void OnDisable()
        {
            ProjectStatsPreferences.Save(options);
        }

        private void OnGUI()
        {
            GUILayout.Space(8);

            FFEditorGUI.Center(() =>
                GUILayout.Label("项目归档面板", FFEditorStyles.Title));

            GUILayout.Space(4);
            GUILayout.Label(
                "导出项目统计报告、源码文本和其他项目资料，用于项目留档。",
                FFEditorStyles.Description);

            GUILayout.Space(4);
            FFEditorGUI.Separator();
            GUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(
                _scrollPos,
                GUILayout.ExpandHeight(true));

            DrawArchiveSection();
            GUILayout.Space(6);
            DrawArchiveButton();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4);
            FFEditorGUI.Separator();
            GUILayout.Space(4);
            GUILayout.Label(
                "Copyright © 2025 Fink Framework",
                FFEditorStyles.Footer,
                GUILayout.ExpandWidth(true));
        }

        private void DrawArchiveSection()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Label("项目归档设置", FFEditorStyles.SectionTitle);

            options.enableArchive =
                EditorGUILayout.Toggle("启用项目归档", options.enableArchive);

            EditorGUI.BeginDisabledGroup(!options.enableArchive);
            EditorGUI.indentLevel++;

            options.exportStatReport =
                EditorGUILayout.Toggle("导出统计报告", options.exportStatReport);

            if (options.exportStatReport)
            {
                EditorGUI.indentLevel++;
                DrawExportPathField("统计导出目录", ref options.statExportDir);
                EditorGUILayout.LabelField(
                    "文件命名规则",
                    "ProjectStat_yyyyMMdd_HHmmss.txt",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(8);

            options.exportSourceCode =
                EditorGUILayout.Toggle("导出项目源码", options.exportSourceCode);

            if (options.exportSourceCode)
            {
                EditorGUI.indentLevel++;
                options.includeEditor =
                    EditorGUILayout.Toggle("包含 Editor 代码", options.includeEditor);
                options.addFilePathHeader =
                    EditorGUILayout.Toggle("在源码前写入文件路径", options.addFilePathHeader);

                GUILayout.Space(6);
                DrawSourceFolderList();

                GUILayout.Space(6);
                DrawExportPathField("源码导出目录", ref options.sourceExportDir);
                EditorGUILayout.LabelField(
                    "文件命名规则",
                    "SourceCode_yyyyMMdd_HHmmss.txt",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();
            GUILayout.EndVertical();
        }

        private void DrawArchiveButton()
        {
            if (!options.enableArchive)
                return;

            FFEditorGUI.Center(() =>
            {
                if (!GUILayout.Button("执行项目归档", FFEditorStyles.BigButton, GUILayout.Width(200)))
                    return;

                if (!EditorUtility.DisplayDialog(
                        "项目归档",
                        "将生成项目统计与源码归档文件，是否继续？",
                        "继续",
                        "取消"))
                    return;

                if (!ValidateArchiveOptions())
                    return;

                options.sourceCodeFolders = options.sourceCodeFolders
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(PathUtil.NormalizePath)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Distinct(System.StringComparer.Ordinal)
                    .ToList();

                ProjectStatUtil.Archive(options);
                EditorUtility.DisplayDialog("项目归档", "项目归档已完成。", "确定");
            });
        }

        private void DrawExportPathField(string label, ref string path)
        {
            GUILayout.BeginHorizontal();
            path = EditorGUILayout.TextField(label, path);

            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                string selected = EditorUtility.OpenFolderPanel(label, path, "");
                if (!string.IsNullOrEmpty(selected))
                    path = selected;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSourceFolderList()
        {
            GUILayout.Label(
                "源码读取路径（在 Assets 目录下，无需填写 Assets 前缀）",
                EditorStyles.label);

            for (int i = 0; i < options.sourceCodeFolders.Count; i++)
            {
                GUILayout.BeginHorizontal();
                options.sourceCodeFolders[i] =
                    EditorGUILayout.TextField(options.sourceCodeFolders[i]);

                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    options.sourceCodeFolders.RemoveAt(i);
                    i--;
                }

                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ 添加路径", FFEditorStyles.SmallButton) &&
                !options.sourceCodeFolders.Contains("Scripts"))
            {
                options.sourceCodeFolders.Add("Scripts");
            }
        }

        private bool ValidateArchiveOptions()
        {
            if (options.exportStatReport && string.IsNullOrEmpty(options.statExportDir))
            {
                EditorUtility.DisplayDialog(
                    "项目归档",
                    "已启用「导出统计报告」，但未指定导出目录。",
                    "确定");
                return false;
            }

            if (options.exportSourceCode)
            {
                if (string.IsNullOrEmpty(options.sourceExportDir))
                {
                    EditorUtility.DisplayDialog(
                        "项目归档",
                        "已启用「导出项目源码」，但未指定导出目录。",
                        "确定");
                    return false;
                }

                if (options.sourceCodeFolders == null || options.sourceCodeFolders.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        "项目归档",
                        "已启用「导出项目源码」，但未指定源码读取路径。",
                        "确定");
                    return false;
                }
            }

            if (!options.exportStatReport && !options.exportSourceCode)
            {
                EditorUtility.DisplayDialog(
                    "项目归档",
                    "请至少启用一种归档方式（统计报告或项目源码）。",
                    "确定");
                return false;
            }

            return true;
        }
    }
}
#endif
