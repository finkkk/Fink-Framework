using System.Collections.Generic;
using Framework.Mono;
using Framework.ObjectPool;
using Framework.ResLoad;
using Framework.Singleton;
using Framework.Utils;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace Framework.Audio
{
    public class AudioManager : Singleton<AudioManager>
    {
        #region 控制音乐相关
        // 全局 AudioMixer（从 Resources 加载）
        private AudioMixer masterMixer;
        // Mixer 分组（Music / SFX）
        private AudioMixerGroup musicGroup;
        private AudioMixerGroup sfxGroup;
        // 音乐播放器
        private AudioSource musicPlayer;
        // 记录所有正在播放的音效播放源容器
        private readonly List<AudioSource> soundList = new();
        private readonly HashSet<AudioSource> soundSet = new();
        // 全局音效的游戏对象
        private GameObject globalSoundObj;
        // 音效是否在播放
        private bool soundIsPlay = true;
        // 全局音效组件合集
        private GameObject soundPlayers;
        
        /// <summary>
        /// 初始化混音器（从 Resources 加载）
        /// </summary>
        private void InitMixer()
        {
            // 1. 加载 Mixer 资源
            masterMixer = ResManager.Instance.Load<AudioMixer>("Audio/MasterMixer");
            if (!masterMixer)
            {
                LogUtil.Error("AudioManager", "无法加载 AudioMixer：Audio/MasterMixer");
                return;
            }

            // 2. 获取 Music / SFX 组
            var musicGroups = masterMixer.FindMatchingGroups("Music");
            var sfxGroups = masterMixer.FindMatchingGroups("SFX");

            if (musicGroups.Length > 0)
                musicGroup = musicGroups[0];
            else
                LogUtil.Error("AudioManager", "AudioMixer 未找到 Music 组");

            if (sfxGroups.Length > 0)
                sfxGroup = sfxGroups[0];
            else
                LogUtil.Error("AudioManager", "AudioMixer 未找到 SFX 组");
        }

        /// <summary>
        /// 播放音乐
        /// </summary>
        /// <param name="name">音乐名</param>
        public void PlayMusic(string name)
        {
            if (!musicGroup)
            {
                LogUtil.Warn("AudioManager", "MusicGroup 未初始化");
            }
            // 动态创建 播放音乐 的播放器 且不会过场景移除 保证过场景时也能播放
            if (!musicPlayer)
            {
                GameObject obj = new("MusicPlayer");
                Object.DontDestroyOnLoad(obj);
                musicPlayer = obj.AddComponent<AudioSource>();
                musicPlayer.outputAudioMixerGroup = musicGroup;
            }
            
            ResManager.Instance.LoadAsync<AudioClip>("Audio/Music/" + name, (clip) =>
            {
                musicPlayer.clip = clip;
                musicPlayer.loop = true;
                musicPlayer.volume = 1;
                musicPlayer.Play();
            });
        }

        /// <summary>
        /// 停止音乐
        /// </summary>
        /// <param name="name">音乐名</param>
        public void StopMusic(string name)
        {
            if (!musicPlayer)
            {
                LogUtil.Warn("音乐播放器不存在，无法停止歌曲！");
                return;
            }
            musicPlayer.Stop();
        }
        
        /// <summary>
        /// 暂停音乐
        /// </summary>
        /// <param name="name">音乐名</param>
        public void PauseMusic(string name)
        {
            if (!musicPlayer)
            {
                LogUtil.Warn("音乐播放器不存在，无法停止歌曲！");
                return;
            }
            musicPlayer.Pause();
        }

        /// <summary>
        /// 调整音乐
        /// </summary>
        /// <param name="v">音量的值</param>
        public void ChangeMusicValue(float v)
        {
            if (!masterMixer)
            {
                LogUtil.Warn("AudioManager", "MasterMixer 未初始化");
                return;
            }
            // 将 0~1 映射到 -80~0 dB（真实可用的音量区间）
            float dB = MathUtil.Remap(v, 0f, 1f, -80f, 0f);
            masterMixer.SetFloat("MusicVolume", dB);
        }
        #endregion
        
        #region 控制音效相关
        
        private AudioManager()
        {
            // 初始化混音器
            InitMixer();
            // 未继承Mono的脚本只能通过Mono管理器执行生命周期函数
            MonoManager.Instance.AddFixedUpdateListener(CleanAudioSource);
        }

        /// <summary>
        /// 每一个FixedUpdate帧中 不停检测 当音效的播放源播放完毕时 销毁播放源组件（注意！如果只是未处于播放状态不销毁）
        /// 为避免边遍历边销毁出问题 采用逆向遍历
        /// </summary>
        private void CleanAudioSource()
        {
            if (!soundIsPlay)
            {
                return;
            }
            for (int i = soundList.Count - 1; i >= 0 ; --i)
            {
                AudioSource s = soundList[i];
                if (!s.isPlaying)
                {
                    s.clip = null;
                    PoolManager.Instance.Despawn(s.gameObject);

                    soundSet.Remove(s);
                    soundList.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="name">音乐名</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="fatherObj">用于跟随的游戏对象父物体 若传空则默认挂载到全局音效游戏对象</param>
        /// <param name="isAsync">是否异步加载</param>
        /// <param name="callback">加载完成的回调函数(并非播放完成)</param>
        public AudioSource PlaySound(string name, bool isLoop = false,GameObject fatherObj = null, bool isAsync = true,UnityAction<AudioSource> callback = null)
        {
            if (!sfxGroup)
            {
                LogUtil.Warn("AudioManager", "SFXGroup 未初始化");
            }
            AudioSource source = PoolManager.Instance.Spawn("Audio/Base/SoundPlayer").GetComponent<AudioSource>();
            source.outputAudioMixerGroup = sfxGroup;
            // 若缓存池达到上限可能会取出之前正在播放的音效 所以可以先执行一次停止播放
            source.Stop();
            // 若不传入依附的父对象 则默认用全局音效播放器播放
            if (!fatherObj)
            {
                if (!PoolManager.debugMode)
                {
                    if (!soundPlayers)
                    {
                        soundPlayers = new GameObject("SoundPlayers");
                    }
                    source.transform.parent = soundPlayers.transform;
                }
            }
            else
            {
                source.transform.parent = fatherObj.transform;
            }
            // 是否异步加载资源
            if (isAsync)
            {
                ResManager.Instance.LoadAsync<AudioClip>("Audio/SFX/" + name, (clip) =>
                {
                    PlayAudioClip(source, clip, isLoop);
                    // 执行完毕逻辑后 执行回调
                    callback?.Invoke(source);
                });
            }
            else
            {
                AudioClip clip = ResManager.Instance.Load<AudioClip>("Audio/SFX/" + name);
                PlayAudioClip(source, clip, isLoop);
                callback?.Invoke(source);
            }

            return source;
        }

        /// <summary>
        /// 停止音效
        /// </summary>
        /// <param name="source">需要停止的播放源</param>
        public void StopSound(AudioSource source)
        {
            if (soundSet.Contains(source))
            {
                soundSet.Remove(source);
                soundList.Remove(source);

                source.Stop();
                source.clip = null;

                PoolManager.Instance.Despawn(source.gameObject);
            }
        }

        /// <summary>
        /// 调整音效
        /// </summary>
        /// <param name="v">音量的值</param>
        public void ChangeSoundValue(float v)
        {
            if (!masterMixer)
            {
                LogUtil.Warn("AudioManager", "MasterMixer 未初始化");
                return;
            }
            // 将 0~1 映射到 -80~0 dB（真实可用的音量区间）
            float dB = MathUtil.Remap(v, 0f, 1f, -80f, 0f);
            masterMixer.SetFloat("SFXVolume", dB);
        }
        
        /// <summary>
        /// 控制所有音效开关（继续播放或暂停播放）
        /// </summary>
        /// <param name="isPlay">是否播放</param>
        public void ToggleAllSounds(bool isPlay)
        {
            if (isPlay)
            {
                soundIsPlay = true;
                foreach (var t in soundList)
                {
                    t.Play();
                }
            }
            else
            {
                soundIsPlay = false;
                foreach (var t in soundList)
                {
                    t.Stop();
                }
            }
        }
        
        /// <summary>
        /// 清空音效容器记录 在清空缓存池之前调用（一般用于过场景）
        /// </summary>
        public void ClearSound()
        {
            foreach (var t in soundList)
            {
                t.Stop();
                t.clip = null;
                PoolManager.Instance.Despawn(t.gameObject);
            }
            soundSet.Clear();
            soundList.Clear();
        }
        
        private void PlayAudioClip(AudioSource source, AudioClip clip, bool isLoop)
        {
            source.clip = clip;
            source.loop = isLoop;
            source.volume = 1f;
            source.Play();
            // 若缓存池达到上限可能会取出之前正在播放的音效 所以需要避免重复添加
            if (soundSet.Add(source)) // 若不存在则添加并返回 true
            {
                soundList.Add(source);
            }
        }
        #endregion
    }
}