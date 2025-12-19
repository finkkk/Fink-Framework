#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Utils
{
    /// <summary>
    /// 项目统计工具（Editor-only）
    /// </summary>
    public static class ProjectStatUtil
    {
        public class StatOptions
        {
            public bool countCode;
            public bool onlyTargetScriptFolder;
            public string scriptFolderPath;

            public bool countShader;
            public bool countMaterial;
            public bool countModel;
            public bool countAudio;
            public bool countPrefab;
            public bool countScene;
            public bool countAddressables;
            public bool countAssetBundle;
            public bool countTexture;
        }

        public static string GenerateReport(StatOptions options)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("========== 项目统计数据 ==========\n");
            sb.AppendLine($"[统计时间]\n{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // 一次性枚举
            var allFiles = Directory.GetFiles(
                Application.dataPath,
                "*.*",
                SearchOption.AllDirectories
            );

            if (options.countCode)
                AppendCodeStats(sb, options, allFiles);

            AppendAssetStats(sb, options,allFiles);

            sb.AppendLine("==============================");
            return sb.ToString();
        }

        private static void AppendCodeStats(StringBuilder sb, StatOptions options, string[] allFiles)
        {
            if (!options.countCode)
                return;

            sb.AppendLine("[代码统计]");

            string codeRoot = options.onlyTargetScriptFolder
                ? Path.Combine(Application.dataPath, options.scriptFolderPath)
                : Application.dataPath;

            int csLines = 0;
            int shaderLines = 0;

            foreach (var file in allFiles)
            {
                var normalizedFile = PathUtil.NormalizePath(file);
                var normalizedRoot = PathUtil.NormalizePath(codeRoot);

                if (!normalizedFile.StartsWith(normalizedRoot))
                    continue;

                if (file.EndsWith(".cs"))
                    csLines += File.ReadLines(file).Count();
                else if (options.countShader && file.EndsWith(".shader"))
                    shaderLines += File.ReadLines(file).Count();
            }

            sb.AppendLine($"  C# 行数：{csLines}");

            if (options.countShader)
                sb.AppendLine($"  Shader 行数：{shaderLines}");

            sb.AppendLine();
        }

        private static void AppendAssetStats(StringBuilder sb, StatOptions options, string[] allFiles)
        {
            bool hasAssetContent =
                options.countMaterial ||
                options.countModel ||
                options.countAudio ||
                options.countPrefab ||
                options.countScene ||
                options.countAddressables ||
                options.countAssetBundle ||
                options.countTexture ;

            if (!hasAssetContent)
                return;

            sb.AppendLine("[资产统计]");

            if (options.countMaterial)
                sb.AppendLine($"  材质：{allFiles.Count(f => f.EndsWith(".mat"))}");

            if (options.countModel)
                sb.AppendLine($"  模型：{allFiles.Count(f => f.EndsWith(".fbx") || f.EndsWith(".obj") || f.EndsWith(".glb"))}");

            if (options.countAudio)
                sb.AppendLine($"  音频：{allFiles.Count(f => f.EndsWith(".wav") || f.EndsWith(".mp3") || f.EndsWith(".ogg"))}");

            if (options.countPrefab)
                sb.AppendLine($"  Prefab：{allFiles.Count(f => f.EndsWith(".prefab"))}");

            if (options.countScene)
                sb.AppendLine($"  场景：{allFiles.Count(f => f.EndsWith(".unity"))}");
            
            if (options.countTexture) 
                sb.AppendLine($"  图片/纹理：{allFiles.Count(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".tga") || f.EndsWith(".psd") || f.EndsWith(".exr") || f.EndsWith(".hdr"))}");
            
            if (options.countAddressables)
            {
                string[] guids = AssetDatabase.FindAssets("t:AddressableAssetGroup");
                sb.AppendLine($"  Addressables 组：{guids.Length}");
            }

            if (options.countAssetBundle)
            {
                int abCount = allFiles.Count(f =>
                {
                    string assetPath = "Assets" + f.Replace(Application.dataPath, "").Replace("\\", "/");
                    var importer = AssetImporter.GetAtPath(assetPath);
                    return importer != null && !string.IsNullOrEmpty(importer.assetBundleName);
                });

                sb.AppendLine($"  AssetBundle 资源：{abCount}");
            }

            sb.AppendLine();
        }
    }
}
#endif
