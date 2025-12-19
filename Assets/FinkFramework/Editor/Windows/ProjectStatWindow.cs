#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FinkFramework.Editor.Utils;
using FinkFramework.Editor.Windows.Common;

namespace FinkFramework.Editor.Windows
{
    public class ProjectStatWindow : EditorWindow
    {
        private readonly ProjectStatUtil.StatOptions options = new();

        private Vector2 scroll;
        private string result;

        [MenuItem("Fink Framework/项目统计面板")]
        public static void Open()
        {
            var window = GetWindow<ProjectStatWindow>("项目数据统计");
            window.minSize = new Vector2(480, 360);
        }

        private void OnEnable()
        {
            options.countCode = true;
            options.onlyTargetScriptFolder = true;
            options.scriptFolderPath = "Scripts";

            options.countShader = false;
            options.countMaterial = true;
            options.countModel = true;
            options.countAudio = true;
            options.countPrefab = true;
            options.countScene = true;
            options.countTexture = true;
            options.countAddressables = false;
            options.countAssetBundle = false;
        }

        private void OnGUI()
        {
            GUILayout.Space(12);

            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("项目数据统计", FFEditorStyles.Title);
            });

            GUILayout.Space(6);
            GUILayout.Label("用于统计项目中的代码规模与资源构成。", FFEditorStyles.Description);

            GUILayout.Space(10);
            FFEditorGUI.Separator();

            DrawCodeSection();
            GUILayout.Space(6);
            DrawAssetSection();

            GUILayout.Space(10);
            FFEditorGUI.Separator();

            DrawGenerateButton();
            GUILayout.Space(8);
            DrawResultArea();

            GUILayout.FlexibleSpace();
            // ===== 页脚 =====
            GUILayout.Label("Copyright \u00A9 2025 Fink Framework",
                FFEditorStyles.Footer, GUILayout.ExpandWidth(true));
        }

        #region 选项区域

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
            options.countModel    = EditorGUILayout.Toggle("模型 (.fbx / .obj / .glb)", options.countModel);
            options.countAudio    = EditorGUILayout.Toggle("音频 (.wav / .mp3 / .ogg)", options.countAudio);
            options.countPrefab   = EditorGUILayout.Toggle("Prefab (.prefab)", options.countPrefab);
            options.countScene    = EditorGUILayout.Toggle("场景 (.unity)", options.countScene);
            options.countTexture = EditorGUILayout.Toggle("图片 / 纹理 ", options.countTexture);
            options.countAddressables = EditorGUILayout.Toggle("Addressables 组", options.countAddressables);
            options.countAssetBundle  = EditorGUILayout.Toggle("AssetBundle 资源", options.countAssetBundle);

            GUILayout.EndVertical();
        }

        #endregion

        #region 统计结果

        private void DrawGenerateButton()
        {
            FFEditorGUI.Center(() =>
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                bool clickGenerate = GUILayout.Button(
                    "生成统计报告",
                    FFEditorStyles.BigButton,
                    GUILayout.Width(200)
                );

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (!clickGenerate)
                    return;

                if (!HasAnySelection())
                {
                    EditorUtility.DisplayDialog(
                        "项目统计",
                        "请至少选择一个统计项。",
                        "确定"
                    );
                    return;
                }

                var raw = ProjectStatUtil.GenerateReport(options);
                result = ApplyColor(raw);
                scroll = Vector2.zero;
            });
        }

        private void DrawResultArea()
        {
            if (string.IsNullOrEmpty(result))
                return;

            EnsureResultStyle();

            GUILayout.Label("统计结果", FFEditorStyles.SectionTitle);

            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(290));

            GUILayout.Label(
                result,
                _resultStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)  
            );

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
        
        private GUIStyle _resultStyle;

        private void EnsureResultStyle()
        {
            if (_resultStyle != null)
                return;

            _resultStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
            };
        }

        #endregion

        #region 工具方法

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

        private string ApplyColor(string raw)
        {
            var lines = raw.Split('\n');
            var sb = new System.Text.StringBuilder();

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    sb.AppendLine($"<color=#4CAF50>{line}</color>");
                }
                else if (line.StartsWith("===="))
                {
                    sb.AppendLine($"<color=#9E9E9E>{line}</color>");
                }
                else
                {
                    sb.AppendLine($"<color=#FFFFFF>{line}</color>");
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}
#endif
