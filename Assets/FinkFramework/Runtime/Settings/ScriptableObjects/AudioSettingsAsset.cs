using UnityEngine;
using UnityEngine.Audio;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>音乐、音效配置。运行时只读，不生成或修改用户 Mixer。</summary>
    [CreateAssetMenu(fileName = "AudioSettingsAsset", menuName = "Fink Framework/Settings/Audio Settings")]
    public class AudioSettingsAsset : ScriptableObject
    {
        [Header("模块配置")]
        [Tooltip("关闭后不加载 Mixer、不创建框架播放器。")]
        public bool EnableAudioModule = true;

        [Header("自定义混音器")]
        [Tooltip("留空时使用内置 MasterMixer 的 Master / Music / SFX 分组。框架不修改 Mixer 参数、效果或 Snapshot。")]
        public AudioMixer CustomMixer;

        [Header("播放输出")]
        [Tooltip("音乐和音效共同经过的上级分组，可与分类输出为同一分组。框架不重建 Mixer 层级；全局音量 API 与分类音量相乘控制框架播放器。")]
        public AudioMixerGroup MasterGroup;

        [Tooltip("音乐输出分组，必须属于上方 Mixer。音量 API 直接控制播放器，不要求暴露 Mixer 参数。")]
        public AudioMixerGroup MusicGroup;

        [Tooltip("音效输出分组，必须属于上方 Mixer。可与音乐使用同一分组，音量仍独立控制。")]
        public AudioMixerGroup SoundGroup;
    }
}
