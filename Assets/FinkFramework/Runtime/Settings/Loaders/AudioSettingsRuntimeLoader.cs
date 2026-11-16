using System;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEngine.Audio;

namespace FinkFramework.Runtime.Settings.Loaders
{
    /// <summary>只读解析音乐、音效的输出。必须在 Unity 主线程调用，不修改 Mixer。</summary>
    public static class AudioSettingsRuntimeLoader
    {
        /// <summary>内置 Mixer 的资源地址。</summary>
        public const string BuiltInMixerPath = "res://FinkFramework/Audio/MasterMixer";

        /// <summary>已有或自动补齐的音频配置；只获取配置，不创建音频服务或加载 Mixer。</summary>
        public static AudioSettingsAsset Current
            => GlobalSettingsRuntimeLoader.GetOrCreateModule(ref GlobalSettingsRuntimeLoader.Current.AudioSettings);

        /// <summary>初始化时的输出快照。其他分组和混音参数由用户管理。</summary>
        public sealed class ResolvedSettings
        {
            /// <summary>当前原生 Mixer。</summary>
            public AudioMixer Mixer { get; }
            /// <summary>是否使用内置 Mixer。</summary>
            public bool UsesBuiltInMixer { get; }
            /// <summary>音乐、音效共同经过的全局上级分组，不接管该组的 Mixer 参数。</summary>
            public AudioMixerGroup MasterGroup { get; }
            /// <summary>音乐输出分组。</summary>
            public AudioMixerGroup MusicGroup { get; }
            /// <summary>音效输出分组。</summary>
            public AudioMixerGroup SoundGroup { get; }

            internal ResolvedSettings(AudioMixer mixer, bool builtIn, AudioMixerGroup master, AudioMixerGroup music, AudioMixerGroup sound)
            {
                Mixer = mixer;
                UsesBuiltInMixer = builtIn;
                MasterGroup = master;
                MusicGroup = music;
                SoundGroup = sound;
            }
        }

        /// <summary>沿根引用加载输出。自定义资产不额外加载；内置 Mixer 引用由调用方持有。</summary>
        /// <param name="settings">有效快照；模块禁用时为 null，配置文件缺失时自动使用默认配置。</param>
        /// <param name="error">具体配置错误。</param>
        /// <returns>成功或禁用返回 true；无效自定义配置返回 false，不回退到其他输出。</returns>
        public static bool TryLoad(out ResolvedSettings settings, out string error)
        {
            var audio = Current;
            var global = GlobalSettingsRuntimeLoader.Current;
            if (!audio.EnableAudioModule)
                return TryResolve(global, null, out settings, out error);
            bool builtIn = !audio.CustomMixer;
            var mixer = builtIn ? ResManager.Instance.Load<AudioMixer>(BuiltInMixerPath) : null;
            bool success = TryResolve(global, mixer, out settings, out error);
            if (!success && mixer) ResManager.Instance.UnloadAsset<AudioMixer>(BuiltInMixerPath, true);
            return success;
        }

        /// <summary>供初始化、Inspector 与构建校验复用。不加载资源、不创建播放器、不写 Mixer。</summary>
        /// <param name="global">根配置；根或音频子配置缺失、模块禁用时跳过解析。</param>
        /// <param name="builtInMixer">未指定自定义 Mixer 时由调用方提供的内置资产。</param>
        /// <param name="settings">输出快照。</param>
        /// <param name="error">配置错误的具体字段。</param>
        /// <returns>成功或禁用返回 true，配置错误返回 false。</returns>
        public static bool TryResolve(GlobalSettingsAsset global, AudioMixer builtInMixer,
            out ResolvedSettings settings, out string error)
        {
            settings = null;
            error = string.Empty;
            if (!global || !global.AudioSettings || !global.AudioSettings.EnableAudioModule) return true;
            var audio = global.AudioSettings;
            bool builtIn = !audio.CustomMixer;
            var mixer = builtIn ? builtInMixer : audio.CustomMixer;
            if (!mixer)
            {
                error = $"无法加载内置 AudioMixer：{BuiltInMixerPath}。";
                return false;
            }
            AudioMixerGroup master, music, sound;
            if (builtIn)
            {
                if (!TryFindBuiltInGroup(mixer, "Master", out master, out error)
                    || !TryFindBuiltInGroup(mixer, "Music", out music, out error)
                    || !TryFindBuiltInGroup(mixer, "SFX", out sound, out error)) return false;
            }
            else
            {
                master = audio.MasterGroup;
                music = audio.MusicGroup;
                sound = audio.SoundGroup;
                if (!master || master.audioMixer != mixer)
                {
                    error = "AudioSettings.MasterGroup：请选择当前 Mixer 的全局输出分组。";
                    return false;
                }
                if (!music || music.audioMixer != mixer)
                {
                    error = "AudioSettings.MusicGroup：请选择当前 Mixer 的音乐输出分组。";
                    return false;
                }
                if (!sound || sound.audioMixer != mixer)
                {
                    error = "AudioSettings.SoundGroup：请选择当前 Mixer 的音效输出分组。";
                    return false;
                }
            }
            settings = new ResolvedSettings(mixer, builtIn, master, music, sound);
            return true;
        }

        private static bool TryFindBuiltInGroup(AudioMixer mixer, string name, out AudioMixerGroup group, out string error)
        {
            group = null;
            error = string.Empty;
            foreach (var candidate in mixer.FindMatchingGroups(name))
            {
                if (!candidate || !string.Equals(candidate.name, name, StringComparison.Ordinal)) continue;
                if (group)
                {
                    error = $"内置 Mixer 存在多个 '{name}' 分组。";
                    return false;
                }
                group = candidate;
            }
            if (group) return true;
            error = $"内置 Mixer 缺少 '{name}' 分组。";
            return false;
        }
    }
}
