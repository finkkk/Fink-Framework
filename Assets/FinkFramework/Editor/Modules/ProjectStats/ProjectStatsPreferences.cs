#if UNITY_EDITOR
using System.Collections.Generic;
using FinkFramework.Editor.Utils;
using Newtonsoft.Json;
using UnityEditor;

namespace FinkFramework.Editor.Modules.ProjectStats
{
    /// <summary>
    /// 项目统计与归档面板共用的编辑器偏好设置。
    /// </summary>
    internal static class ProjectStatsPreferences
    {
        private const string PrefKey = "FinkFramework_ProjectStat_";

        public static ProjectStatUtil.StatOptions Load()
        {
            var options = new ProjectStatUtil.StatOptions
            {
                countCode = EditorPrefs.GetBool(PrefKey + "countCode", true),
                onlyTargetScriptFolder = EditorPrefs.GetBool(PrefKey + "onlyTargetScriptFolder", true),
                scriptFolderPath = EditorPrefs.GetString(PrefKey + "scriptFolderPath", "Scripts"),
                countShader = EditorPrefs.GetBool(PrefKey + "countShader", true),
                countMaterial = EditorPrefs.GetBool(PrefKey + "countMaterial", true),
                countModel = EditorPrefs.GetBool(PrefKey + "countModel", true),
                countAudio = EditorPrefs.GetBool(PrefKey + "countAudio", true),
                countPrefab = EditorPrefs.GetBool(PrefKey + "countPrefab", true),
                countScene = EditorPrefs.GetBool(PrefKey + "countScene", true),
                countTexture = EditorPrefs.GetBool(PrefKey + "countTexture", true),
                countAddressables = EditorPrefs.GetBool(PrefKey + "countAddressables", true),
                countAssetBundle = EditorPrefs.GetBool(PrefKey + "countAssetBundle", true),
                enableArchive = EditorPrefs.GetBool(PrefKey + "enableArchive", true),
                exportStatReport = EditorPrefs.GetBool(PrefKey + "exportStatReport", false),
                exportSourceCode = EditorPrefs.GetBool(PrefKey + "exportSourceCode", false),
                statExportDir = EditorPrefs.GetString(PrefKey + "statExportDir", ""),
                sourceExportDir = EditorPrefs.GetString(PrefKey + "sourceExportDir", ""),
                includeEditor = EditorPrefs.GetBool(PrefKey + "includeEditor", true),
                addFilePathHeader = EditorPrefs.GetBool(PrefKey + "addFilePathHeader", true)
            };

            string json = EditorPrefs.GetString(PrefKey + "sourceCodeFolders", "[]");
            options.sourceCodeFolders = JsonConvert.DeserializeObject<List<string>>(json)
                                        ?? new List<string>();
            return options;
        }

        public static void Save(ProjectStatUtil.StatOptions options)
        {
            if (options == null)
                return;

            EditorPrefs.SetBool(PrefKey + "countCode", options.countCode);
            EditorPrefs.SetBool(PrefKey + "onlyTargetScriptFolder", options.onlyTargetScriptFolder);
            EditorPrefs.SetString(PrefKey + "scriptFolderPath", options.scriptFolderPath ?? "");

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

            EditorPrefs.SetString(
                PrefKey + "sourceCodeFolders",
                JsonConvert.SerializeObject(options.sourceCodeFolders ?? new List<string>()));
        }
    }
}
#endif
