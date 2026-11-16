using System.IO;
using System;
using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Editor.Modules.Settings.Providers;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace FinkFramework.Editor.Utils
{
    /// <summary>框架通用构建检查入口，各项检查由 Framework 构建设置中的独立开关控制。</summary>
    public class BuildCheckUtil : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // 即使未打开过配置页，也先补齐默认配置，再按各自开关执行检查。
            Validate(GlobalSettingsEditorLoader.LoadOrCreate());
        }

        internal static void Validate(GlobalSettingsAsset global)
        {
            if (!global) return;
            if (global.EnableEditorUrlCheck) ScanCsFiles();
            ValidateAudioSettings(global);
        }

        /// <summary>只检查已有 Mixer 输出，不修改混音器，不初始化音频运行时服务。</summary>
        private static void ValidateAudioSettings(GlobalSettingsAsset global)
        {
            if (!global.EnableAudioBuildCheck || !global.AudioSettings || !global.AudioSettings.EnableAudioModule) return;
            var audio = global.AudioSettings;
            var fallback = audio.CustomMixer ? null : Resources.Load<AudioMixer>("FinkFramework/Audio/MasterMixer");
            if (!AudioSettingsRuntimeLoader.TryResolve(global, fallback, out var settings, out string error))
                throw new BuildFailedException("[AudioManager] " + error);
            if (!AudioMixerGroupEditorUtility.ValidateHierarchy(audio, out error))
                throw new BuildFailedException("[AudioManager] " + error);
            foreach (var asset in new Object[] { settings.Mixer, settings.MasterGroup, settings.MusicGroup, settings.SoundGroup })
            {
                string path = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
                if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new BuildFailedException($"[AudioManager] 音频输出资产不能放在 Editor 目录：{path}");
            }
        }

        private static void ScanCsFiles()
        {
            string[] scriptGuids = AssetDatabase.FindAssets("t:Script");

            foreach (var guid in scriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (IsIgnored(path)) 
                    continue;

                string text = File.ReadAllText(path);

                if (text.Contains("editor://"))
                {
                    FailBuild(path);
                }
            }

            LogUtil.Info("✔ 构建扫描通过，未发现 editor:// 路径。");
        }

        private static bool IsIgnored(string path)
        {
            // 排除框架自身
            if (path.StartsWith("Assets/FinkFramework/", StringComparison.Ordinal))
                return true;

            // 可扩展 ignore 列表
            return false;
        }
        
        private static void FailBuild(string filePath)
        {
            string msg =
                "构建失败：检测到非法 editor:// 资源路径！\n\n" +
                $"违规文件：{filePath}\n" +
                "editor:// 仅用于编辑器环境，不能出现在构建包内。\n" +
                "请将该路径替换为：res:// 或 ab:// 或自定义 Provider。\n" +
                "如需临时跳过此检测，可在 Project Settings 中关闭：\n" +
                "【Project Settings → Fink Framework → Framework → 构建设置 → 启用编辑器加载打包检测】\n";

            throw new BuildFailedException(msg);
        }
    }
}
