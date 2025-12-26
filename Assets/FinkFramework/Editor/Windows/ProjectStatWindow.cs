#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FinkFramework.Editor.Utils;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json;

namespace FinkFramework.Editor.Windows
{
    public class ProjectStatWindow : EditorWindow
    {
        private readonly ProjectStatUtil.StatOptions options = new();
        private const string PrefKey = "FinkFramework_ProjectStat_";
        private Vector2 _scrollPos;

        [MenuItem("Fink Framework/统计归档面板")]
        public static void Open()
        {
            var window = GetWindow<ProjectStatWindow>("项目数据统计与归档");
            window.minSize = new Vector2(420, 560);
        }

        private void OnEnable()
        {
            options.countCode = EditorPrefs.GetBool(PrefKey + "countCode", true);
            options.onlyTargetScriptFolder = EditorPrefs.GetBool(PrefKey + "onlyTargetScriptFolder", true);
            options.scriptFolderPath = EditorPrefs.GetString(PrefKey + "scriptFolderPath", "Scripts");

            options.countShader = EditorPrefs.GetBool(PrefKey + "countShader", true);
            options.countMaterial = EditorPrefs.GetBool(PrefKey + "countMaterial", true);
            options.countModel = EditorPrefs.GetBool(PrefKey + "countModel", true);
            options.countAudio = EditorPrefs.GetBool(PrefKey + "countAudio", true);
            options.countPrefab = EditorPrefs.GetBool(PrefKey + "countPrefab", true);
            options.countScene = EditorPrefs.GetBool(PrefKey + "countScene", true);
            options.countTexture = EditorPrefs.GetBool(PrefKey + "countTexture", true);
            options.countAddressables = EditorPrefs.GetBool(PrefKey + "countAddressables", true);
            options.countAssetBundle = EditorPrefs.GetBool(PrefKey + "countAssetBundle", true);

            options.enableArchive = EditorPrefs.GetBool(PrefKey + "enableArchive", true);
            options.exportStatReport = EditorPrefs.GetBool(PrefKey + "exportStatReport", false);
            options.exportSourceCode = EditorPrefs.GetBool(PrefKey + "exportSourceCode", false);

            options.statExportDir = EditorPrefs.GetString(PrefKey + "statExportDir", "");
            options.sourceExportDir = EditorPrefs.GetString(PrefKey + "sourceExportDir", "");

            options.includeEditor = EditorPrefs.GetBool(PrefKey + "includeEditor", true);
            options.addFilePathHeader = EditorPrefs.GetBool(PrefKey + "addFilePathHeader", true);

            // List<string> 用 Json 存
            var json = EditorPrefs.GetString(PrefKey + "sourceCodeFolders", "[]");
            options.sourceCodeFolders = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
        }
        
        private void OnDisable()
        {
            EditorPrefs.SetBool(PrefKey + "countCode", options.countCode);
            EditorPrefs.SetBool(PrefKey + "onlyTargetScriptFolder", options.onlyTargetScriptFolder);
            EditorPrefs.SetString(PrefKey + "scriptFolderPath", options.scriptFolderPath);

            EditorPrefs.SetBool(PrefKey + "countShader", options.countShader);
            EditorPrefs.SetBool(PrefKey + "countMaterial", options.countMaterial);
            EditorPrefs.SetBool(PrefKey + "countModel", options.countModel);
            EditorPrefs.SetBool(PrefKey + "countAudio", options.countAudio);
            EditorPrefs.SetBool(PrefKey + "countPrefab", options.countPrefab);
            EditorPrefs.SetBool(PrefKey + "countScene", options.countScene);
            EditorPrefs.SetBool(PrefKey + "countTexture", options.countTexture);
            EditorPrefs.SetBool(PrefKey + "countAddressables", options.countAddressables);
            EditorPrefs.SetBool(PrefKey + "countAssetBundle", options.countAssetBundle);

            EditorPrefs.SetBool(PrefKey + "enableArchive", options.enableArchive);
            EditorPrefs.SetBool(PrefKey + "exportStatReport", options.exportStatReport);
            EditorPrefs.SetBool(PrefKey + "exportSourceCode", options.exportSourceCode);

            EditorPrefs.SetString(PrefKey + "statExportDir", options.statExportDir ?? "");
            EditorPrefs.SetString(PrefKey + "sourceExportDir", options.sourceExportDir ?? "");

            EditorPrefs.SetBool(PrefKey + "includeEditor", options.includeEditor);
            EditorPrefs.SetBool(PrefKey + "addFilePathHeader", options.addFilePathHeader);
            
            EditorPrefs.SetString(PrefKey + "sourceCodeFolders", JsonConvert.SerializeObject(options.sourceCodeFolders));
        }

        private void OnGUI()
        {
            GUILayout.Space(8);

            // ===== 标题 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("项目数据统计与归档", FFEditorStyles.Title);
            });

            GUILayout.Space(4);
            GUILayout.Label(
                "用于统计项目中的代码规模与资源构成，并支持生成项目归档材料。",
                FFEditorStyles.Description
            );

            GUILayout.Space(4);
            FFEditorGUI.Separator();
            GUILayout.Space(4);
            
            // =====================================================
            // 可滚动内容区域
            // =====================================================
            _scrollPos = EditorGUILayout.BeginScrollView(
                _scrollPos,
                GUILayout.ExpandHeight(true)
            );

            // ---------- 数据统计 ----------
            DrawCodeSection();
            GUILayout.Space(4);
            DrawAssetSection();

            GUILayout.Space(6);
            DrawPrintReportButton();

            GUILayout.Space(8);
            
            // ---------- 数据归档 ----------
            DrawArchiveSection();
            GUILayout.Space(6);
            DrawArchiveButton();

            EditorGUILayout.EndScrollView();
            
            GUILayout.Space(4);
            FFEditorGUI.Separator();
            GUILayout.Space(4);

            // =====================================================
            // 固定底部页脚
            // =====================================================
            GUILayout.Label(
                "Copyright © 2025 Fink Framework",
                FFEditorStyles.Footer,
                GUILayout.ExpandWidth(true)
            );
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
        }

        private void DrawAssetSection()
        {
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

        private void DrawArchiveSection()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            GUILayout.Label("数据归档", FFEditorStyles.SectionTitle);

            // ===== 总开关 =====
            options.enableArchive =
                EditorGUILayout.Toggle("启用数据归档", options.enableArchive);

            EditorGUI.BeginDisabledGroup(!options.enableArchive);
            EditorGUI.indentLevel++;

            // =====================================================
            // 统计数据归档
            // =====================================================
            options.exportStatReport =
                EditorGUILayout.Toggle("导出统计数据文本", options.exportStatReport);

            if (options.exportStatReport)
            {
                EditorGUI.indentLevel++;

                DrawExportPathField(
                    "统计导出目录",
                    ref options.statExportDir
                );

                EditorGUILayout.LabelField(
                    "文件命名规则",
                    "ProjectStat_yyyyMMdd_HHmmss.txt",
                    EditorStyles.miniLabel
                );

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(8);

            // =====================================================
            // 源码归档
            // =====================================================
            options.exportSourceCode =
                EditorGUILayout.Toggle("导出项目源码文本", options.exportSourceCode);

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
                DrawExportPathField(
                    "源码导出目录",
                    ref options.sourceExportDir
                );

                EditorGUILayout.LabelField(
                    "文件命名规则",
                    "SourceCode_yyyyMMdd_HHmmss.txt",
                    EditorStyles.miniLabel
                );

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();

            GUILayout.EndVertical();
        }

        #endregion

        #region 组件绘制

        private void DrawPrintReportButton()
        {
            FFEditorGUI.Center(() =>
            {
                if (GUILayout.Button(
                        "打印统计报告",
                        FFEditorStyles.BigButton,
                        GUILayout.Width(200)))
                {
                    if (!HasAnySelection())
                    {
                        EditorUtility.DisplayDialog(
                            "项目统计",
                            "请至少选择一个统计项。",
                            "确定"
                        );
                        return;
                    }

                    var report = ProjectStatUtil.GenerateReport(options);
                    LogUtil.Info("ProjectStatUtil",report);
                }
            });
        }
        
        private void DrawArchiveButton()
        {
            if (!options.enableArchive)
                return;

            GUILayout.Space(6);

            FFEditorGUI.Center(() =>
            {
                if (GUILayout.Button(
                        "执行数据归档",
                        FFEditorStyles.BigButton,
                        GUILayout.Width(200)))
                {
                    // ===== 基础确认 =====
                    if (!EditorUtility.DisplayDialog(
                            "数据归档",
                            "将生成项目统计与源码归档文件，是否继续？",
                            "继续",
                            "取消"))
                    {
                        return;
                    }

                    // ===== 参数校验 =====
                    if (!ValidateArchiveOptions())
                        return;
                    // 规范化源码读取路径（去空 / 统一分隔符 / 去重）
                    options.sourceCodeFolders = options.sourceCodeFolders
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(PathUtil.NormalizePath)
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Distinct(System.StringComparer.Ordinal)
                        .ToList();
                    ProjectStatUtil.Archive(options);

                    EditorUtility.DisplayDialog(
                        "数据归档",
                        "项目数据归档已完成。",
                        "确定"
                    );
                }
            });
        }
        
        private void DrawExportPathField(string label, ref string path)
        {
            GUILayout.BeginHorizontal();
            path = EditorGUILayout.TextField(label, path);

            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                var selected = EditorUtility.OpenFolderPanel(label, path, "");
                if (!string.IsNullOrEmpty(selected))
                    path = selected;
            }

            GUILayout.EndHorizontal();
        }
        
        private void DrawSourceFolderList()
        {
            GUILayout.Label("源码读取路径 (在Assets目录下 无需填写Assets前缀)", EditorStyles.label);

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

            if (GUILayout.Button("+ 添加路径", FFEditorStyles.SmallButton))
            {
                if (!options.sourceCodeFolders.Contains("Scripts"))
                {
                    options.sourceCodeFolders.Add("Scripts");
                }
            }
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
        
        private bool ValidateArchiveOptions()
        {
            // 统计数据导出
            if (options.exportStatReport)
            {
                if (string.IsNullOrEmpty(options.statExportDir))
                {
                    EditorUtility.DisplayDialog(
                        "数据归档",
                        "已启用「统计数据导出」，但未指定导出目录。",
                        "确定"
                    );
                    return false;
                }
            }

            // 源码导出
            if (options.exportSourceCode)
            {
                if (string.IsNullOrEmpty(options.sourceExportDir))
                {
                    EditorUtility.DisplayDialog(
                        "数据归档",
                        "已启用「源码导出」，但未指定导出目录。",
                        "确定"
                    );
                    return false;
                }

                if (options.sourceCodeFolders == null ||
                    options.sourceCodeFolders.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        "数据归档",
                        "已启用「源码导出」，但未指定源码读取路径。",
                        "确定"
                    );
                    return false;
                }
            }

            // 至少启用一个子模块
            if (!options.exportStatReport && !options.exportSourceCode)
            {
                EditorUtility.DisplayDialog(
                    "数据归档",
                    "请至少启用一种归档方式（统计数据或源码）。",
                    "确定"
                );
                return false;
            }

            return true;
        }

        #endregion
    }
}
#endif
