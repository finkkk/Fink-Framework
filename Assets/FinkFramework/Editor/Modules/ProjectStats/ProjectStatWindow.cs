#if UNITY_EDITOR
using FinkFramework.Editor.Common;
using FinkFramework.Editor.Utils;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.ProjectStats
{
    /// <summary>
    /// 项目统计面板：统计项目代码规模与资源构成，并同时显示和输出统计报告。
    /// </summary>
    public class ProjectStatWindow : EditorWindow
    {
        private ProjectStatUtil.StatOptions options;
        private Vector2 _scrollPos;
        private Vector2 _reportScrollPos;
        private GUIStyle _reportStyle;
        private string _reportText = string.Empty;

        [MenuItem("Fink Framework/统计与归档/项目统计面板", false, 100)]
        public static void Open()
        {
            var window = GetWindow<ProjectStatWindow>("项目统计面板");
            window.minSize = new Vector2(480, 760);
        }

        private void OnEnable()
        {
            options = ProjectStatsPreferences.Load();
            _reportStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = false,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };
        }

        private void OnDisable()
        {
            ProjectStatsPreferences.Save(options);
        }

        private void OnGUI()
        {
            GUILayout.Space(8);

            FFEditorGUI.Center(() =>
                GUILayout.Label("项目统计面板", FFEditorStyles.Title));

            GUILayout.Space(4);
            GUILayout.Label(
                "统计项目中的代码规模与资源构成。统计结果会显示在下方，并同步输出到 Console。",
                FFEditorStyles.Description);

            GUILayout.Space(4);
            FFEditorGUI.Separator();
            GUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(
                _scrollPos,
                GUILayout.ExpandHeight(true));

            DrawCodeSection();
            GUILayout.Space(4);
            DrawAssetSection();
            GUILayout.Space(6);
            DrawReportOutput();
            GUILayout.Space(6);
            DrawStatisticsButton();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4);
            FFEditorGUI.Separator();
            GUILayout.Space(4);
            GUILayout.Label(
                "Copyright © 2025 Fink Framework",
                FFEditorStyles.Footer,
                GUILayout.ExpandWidth(true));
        }

        private void DrawCodeSection()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Label("代码统计", FFEditorStyles.SectionTitle);

            options.countCode = EditorGUILayout.Toggle("启用代码统计", options.countCode);

            if (options.countCode)
            {
                EditorGUI.indentLevel++;
                options.onlyTargetScriptFolder =
                    EditorGUILayout.Toggle("仅统计指定脚本目录", options.onlyTargetScriptFolder);

                if (options.onlyTargetScriptFolder)
                {
                    options.scriptFolderPath =
                        EditorGUILayout.TextField("脚本目录：Assets/", options.scriptFolderPath);
                }

                options.countShader =
                    EditorGUILayout.Toggle("包含 Shader 行数", options.countShader);
                EditorGUI.indentLevel--;
            }

            GUILayout.EndVertical();
        }

        private void DrawAssetSection()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Label("资产统计", FFEditorStyles.SectionTitle);

            options.countMaterial = EditorGUILayout.Toggle("材质 (.mat)", options.countMaterial);
            options.countModel = EditorGUILayout.Toggle("模型 (.fbx / .obj / .glb)", options.countModel);
            options.countAudio = EditorGUILayout.Toggle("音频 (.wav / .mp3 / .ogg)", options.countAudio);
            options.countPrefab = EditorGUILayout.Toggle("Prefab (.prefab)", options.countPrefab);
            options.countScene = EditorGUILayout.Toggle("场景 (.unity)", options.countScene);
            options.countTexture = EditorGUILayout.Toggle("图片 / 纹理", options.countTexture);
            options.countAddressables = EditorGUILayout.Toggle("Addressables 组", options.countAddressables);
            options.countAssetBundle = EditorGUILayout.Toggle("AssetBundle 资源", options.countAssetBundle);

            GUILayout.EndVertical();
        }

        private void DrawReportOutput()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Label("统计输出", FFEditorStyles.SectionTitle);

            _reportScrollPos = EditorGUILayout.BeginScrollView(
                _reportScrollPos,
                GUILayout.Height(320));

            string text = string.IsNullOrEmpty(_reportText)
                ? "尚未生成统计报告。"
                : _reportText;
            EditorGUILayout.SelectableLabel(
                text,
                _reportStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.MinHeight(300));

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawStatisticsButton()
        {
            FFEditorGUI.Center(() =>
            {
                if (!GUILayout.Button("重新统计", FFEditorStyles.BigButton, GUILayout.Width(200)))
                    return;

                // 每次重新统计都先清空旧结果，避免新旧报告混在一起。
                _reportText = string.Empty;
                _reportScrollPos = Vector2.zero;

                if (!HasAnySelection())
                {
                    EditorUtility.DisplayDialog("项目统计", "请至少选择一个统计项。", "确定");
                    Repaint();
                    return;
                }

                // 使用同一份带颜色报告，保证面板和 Console 内容一致。
                _reportText = ProjectStatUtil.GenerateConsoleReport(options);
                LogUtil.Info("ProjectStatUtil", _reportText);
                Repaint();
            });
        }

        private bool HasAnySelection()
        {
            return options.countCode
                   || options.countMaterial
                   || options.countModel
                   || options.countAudio
                   || options.countPrefab
                   || options.countScene
                   || options.countAddressables
                   || options.countAssetBundle
                   || options.countTexture;
        }
    }
}
#endif
