using System.Collections.Generic;
using Framework.Mono;
using Framework.ObjectPool;
using Framework.ResLoad;
using Framework.Singleton;
using Framework.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Framework.Audio
{
    public class AudioManager : Singleton<AudioManager>
    {
        #region 控制音乐相关
        // 音乐播放器
        private AudioSource musicPlayer;
        // 音乐音量大小
        private float musicValue = 0.4f;

        /// <summary>
        /// 播放音乐
        /// </summary>
        /// <param name="name">音乐名</param>
        public void PlayMusic(string name)
        {
            // 动态创建 播放音乐 的播放器 且不会过场景移除 保证过场景时也能播放
            if (!musicPlayer)
            {
                GameObject obj = new("MusicPlayer");
                Object.DontDestroyOnLoad(obj);
                musicPlayer = obj.AddComponent<AudioSource>();
            }
            
            ResManager.Instance.LoadAsync<AudioClip>("Audio/Music/" + name, (clip) =>
            {
                musicPlayer.clip = clip;
                musicPlayer.loop = true;
                musicPlayer.volume = musicValue;
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
            musicValue = v;
            if (!musicPlayer)
            {
                LogUtil.Warn("音乐播放器不存在，无法停止歌曲！");
                return;
            }
            musicPlayer.volume = musicValue;
        }
        #endregion
        
        #region 控制音效相关
        
        private AudioManager()
        {
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
                if (!soundList[i].isPlaying)
                {
                    soundList[i].clip = null;
                    PoolManager.Instance.Despawn(soundList[i].gameObject);
                    //Object.Destroy(soundList[i]);
                    soundList.RemoveAt(i);
                }
            }
        }
        // 记录所有正在播放的音效播放源容器
        private readonly List<AudioSource> soundList = new();
        // 全局音效的游戏对象
        private GameObject globalSoundObj;
        // 音效的音量大小
        private float soundValue = 0.4f;
        // 音效是否在播放
        private bool soundIsPlay = true;
        // 全局音效组件合集
        private GameObject soundPlayers;

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="name">音乐名</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="fatherObj">用于跟随的游戏对象父物体 若传空则默认挂载到全局音效游戏对象</param>
        /// <param name="isAsync">是否异步加载</param>
        /// <param name="callback">加载完成的回调函数(并非播放完成)</param>
        public void PlaySound(string name, bool isLoop = false,GameObject fatherObj = null, bool isAsync = true,UnityAction<AudioSource> callback = null)
        {
            
            // 是否异步加载资源
            if (isAsync)
            {
                ResManager.Instance.LoadAsync<AudioClip>("Audio/SFX/" + name, (clip) =>
                {
                    
                    AudioSource source = PoolManager.Instance.Spawn("Audio/Base/SoundPlayer").GetComponent<AudioSource>();
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
                        // 执行完毕逻辑后 执行回调 传出播放源组件
                    }
                    else
                    {
                        source.transform.parent = fatherObj.transform;
                        // 执行完毕逻辑后 执行回调 传出播放源组件
                    }
                    PlayAudioClip(source, clip, isLoop);
                    callback?.Invoke(source);
                });
            }
            else
            {
                AudioClip clip = ResManager.Instance.Load<AudioClip>("Audio/Audio/" + name);
                AudioSource source = PoolManager.Instance.Spawn("Audio/Base/SoundPlayer").GetComponent<AudioSource>();
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
                    // 执行完毕逻辑后 执行回调 传出播放源组件
                }
                else
                {
                    source.transform.parent = fatherObj.transform;
                    // 执行完毕逻辑后 执行回调 传出播放源组件
                }
                PlayAudioClip(source, clip, isLoop);
                callback?.Invoke(source);
            }
        }

        /// <summary>
        /// 停止音效
        /// </summary>
        /// <param name="source">需要停止的播放源</param>
        public void StopSound(AudioSource source)
        {
            if (soundList.Contains(source))
            {
                source.Stop();
                soundList.Remove(source);
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
            soundValue = v;
            foreach (var t in soundList)
            {
                t.volume = v;
            }
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
            soundList.Clear();
        }
        
        private void PlayAudioClip(AudioSource source, AudioClip clip, bool isLoop)
        {
            source.clip = clip;
            source.loop = isLoop;
            source.volume = soundValue;
            source.Play();
            // 若缓存池达到上限可能会取出之前正在播放的音效 所以需要避免重复添加
            if (!soundList.Contains(source))
            {
                soundList.Add(source);
            }
        }
        #endregion
    }
}