using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Mono;
using FinkFramework.Runtime.Pool;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Audio
{
    public class AudioManager : Singleton<AudioManager>
    {
        #region 初始化音效管理器

        private AudioManager()
        {
            // 初始化混音器
            InitMixer();
            // 未继承Mono的脚本只能通过Mono管理器执行生命周期函数
            MonoManager.Instance.AddFixedUpdateListener(CleanAudioSource);
        }
        
        /// <summary>
        /// 初始化混音器（从 Resources 加载）
        /// </summary>
        private void InitMixer()
        {
            // 1. 加载 Mixer 资源
            masterMixer = ResManager.Instance.Load<AudioMixer>("res://FinkFramework/Audio/MasterMixer");
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

        #endregion
        
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
        // 音效是否在播放
        private bool soundIsPlay = true;
        // 全局音效组件合集
        private GameObject soundPlayers;
        
        /// <summary>
        /// 播放音乐（同步加载）
        /// --------------------------------------
        /// ⚠ 不建议使用：
        ///   - 音乐文件通常体积较大
        ///   - 同步加载将阻塞主线程，引发卡顿
        /// 仅在特殊情况下使用，例如：
        ///   - 启动时加载极小的占位音乐
        ///   - 编辑器工具需要同步预览
        /// </summary>
        /// <param name="fullPath">带前缀的完整资源路径（如 res://Audio/Music/bgm）</param>
        public void PlayMusic(string fullPath)
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
            
            AudioClip clip = ResManager.Instance.Load<AudioClip>(fullPath);
            musicPlayer.clip = clip;
            musicPlayer.loop = true;
            musicPlayer.volume = 1;
            musicPlayer.Play();
        }

        /// <summary>
        /// 播放音乐（推荐使用 异步加载，await 方式）
        /// </summary>
        /// <param name="fullPath">带前缀的完整资源路径</param>
        /// <returns>加载完成后返回音乐播放器 AudioSource</returns>
        public async UniTask<AudioSource> PlayMusicAsync(string fullPath)
        {
            if (!musicGroup)
            {
                LogUtil.Warn("AudioManager", "MusicGroup 未初始化");
            }
            var op = PlayAudioHandle(fullPath, true, true, null);
            await op.WaitUntilDone();
            return op.Source;
        }
        
        /// <summary>
        /// 播放音乐（推荐使用 异步加载，回调方式）
        /// </summary>
        /// <param name="path">带前缀的完整资源路径</param>
        /// <param name="callback">加载并播放成功后的回调，返回 AudioSource</param>
        public void PlayMusicAsync(string path, UnityAction<AudioSource> callback)
        {
            if (!musicGroup)
            {
                LogUtil.Warn("AudioManager", "MusicGroup 未初始化");
            }
            var op = PlayAudioHandle(path, true, true, null);
            op.Completed += o => callback?.Invoke(o.Source);
        }
        
        /// <summary>
        /// 播放音乐（推荐使用 异步加载，句柄方式）
        /// 返回 AudioOperation，可用于：
        ///   - 监听 Progress（进度条）
        ///   - 注册 Completed 事件
        ///   - 查询 IsDone/IsFailed
        /// </summary>
        /// <param name="fullPath">带前缀的完整资源路径</param>
        /// <returns>音频异步播放操作句柄</returns>
        public AudioOperation PlayMusicHandle(string fullPath)
        {
            if (!musicGroup)
            {
                LogUtil.Warn("AudioManager", "MusicGroup 未初始化");
            }
            return PlayAudioHandle(fullPath, true, true, null);
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
        /// 播放音效（同步加载）
        /// </summary>
        /// <param name="fullPath">音乐文件带前缀完整路径</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="fatherObj">用于跟随的游戏对象父物体 若传空则默认挂载到全局音效游戏对象</param>
        /// <param name="callback">加载完成的回调函数(并非播放完成)</param>
        public AudioSource PlaySound(string fullPath, bool isLoop = false, GameObject fatherObj = null, UnityAction<AudioSource> callback = null)
        {
            if (!sfxGroup)
            {
                LogUtil.Warn("AudioManager", "SFXGroup 未初始化");
            }
            AudioSource source = PoolManager.Instance.Spawn("res://FinkFramework/Audio/Base/SoundPlayer").GetComponent<AudioSource>();
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
            // 同步加载资源
            AudioClip clip = ResManager.Instance.Load<AudioClip>(fullPath);
            PlayAudioClip(source, clip, isLoop);
            // 执行完毕逻辑后 执行回调
            callback?.Invoke(source);

            return source;
        }

        /// <summary>
        /// 播放音效（异步加载，await 方式）
        /// </summary>
        /// <param name="fullPath">音乐文件带前缀完整路径</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="fatherObj">用于跟随的游戏对象父物体 若传空则默认挂载到全局音效游戏对象</param>
        public async UniTask<AudioSource> PlaySoundAsync(string path, bool isLoop = false, GameObject fatherObj = null)
        {
            if (!sfxGroup)
            {
                LogUtil.Warn("AudioManager", "SFXGroup 未初始化");
            }
            var op = PlayAudioHandle(path, isLoop, false, fatherObj);
            await op.WaitUntilDone();
            return op.Source;
        }
        
        /// <summary>
        /// 播放音效（异步加载，回调方式）
        /// </summary>
        /// <param name="path">带前缀的完整资源路径</param>
        /// <param name="callback">加载并播放成功后的回调（返回 AudioSource）</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="fatherObj">父对象（用于跟随）</param>
        public void PlaySoundAsyncCallback(string path, UnityAction<AudioSource> callback, bool isLoop = false, GameObject fatherObj = null) 
        {
            if (!sfxGroup)
            {
                LogUtil.Warn("AudioManager", "SFXGroup 未初始化");
            }
            var op = PlayAudioHandle(path, isLoop, false, fatherObj);
            op.Completed += o => callback?.Invoke(o.Source);
        }
        
        /// <summary>
        /// 播放音效（异步加载，句柄方式）
        /// ✔ 返回 AudioOperation，可用于：
        ///   - 监听 Progress（进度条）
        ///   - 注册 Completed 事件
        ///   - 轮询 IsDone / IsFailed 状态
        /// ✔ 最适合 Loading UI / 框架层 / 多步骤流程
        /// </summary>
        /// <param name="path">带前缀的完整资源路径</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="fatherObj">父对象（用于跟随）</param>
        /// <returns>音频异步播放操作句柄 AudioOperation</returns>
        public AudioOperation PlaySoundHandle(string path, bool isLoop = false, GameObject fatherObj = null)
        {
            if (!sfxGroup)
            {
                LogUtil.Warn("AudioManager", "SFXGroup 未初始化");
            }

            // isMusic = false（音效）
            return PlayAudioHandle(path, isLoop, false, fatherObj);
        }
        
        /// <summary>
        /// 停止音效
        /// </summary>
        /// <param name="source">需要停止的播放源</param>
        public void StopSound(AudioSource source)
        {
            // 若不存在则直接退出
            if (!soundSet.Remove(source))
                return;
            // 从列表中移除（Remove 是 O(n)，但 SFX 数量通常不大）
            soundList.Remove(source);
            // 回收
            source.Stop();
            source.clip = null;
            PoolManager.Instance.Despawn(source.gameObject);
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
        
        /// <summary>
        /// 播放音效内部逻辑
        /// </summary>
        private void PlaySoundClip(AudioSource source, AudioClip clip, bool isLoop)
        {
            PlayAudioClip(source,clip,isLoop);
            // 若缓存池达到上限可能会取出之前正在播放的音效 所以需要避免重复添加
            if (soundSet.Add(source)) // 若不存在则添加并返回 true
            {
                soundList.Add(source);
            }
        }
        
        #endregion

        #region 通用工具方法
        
        /// <summary>
        /// 播放音频
        /// </summary>
        private void PlayAudioClip(AudioSource source, AudioClip clip, bool isLoop)
        {
            source.clip = clip;
            source.loop = isLoop;
            source.volume = 1f;
            source.Play();
        }
        
        /// <summary>
        /// 句柄式播放音效 (核心接口)
        /// </summary>
        public AudioOperation PlayAudioHandle(string fullPath, bool isLoop, bool isMusic, GameObject fatherObj = null)
        {
            var op = new AudioOperation();
            _ = PlayAudioAsyncWrapper(fullPath, isLoop, isMusic, fatherObj, op);
            return op;
        }
        
        /// <summary>
        /// 异步播放音频核心封装
        /// </summary>
        private async UniTask PlayAudioAsyncWrapper(string fullPath, bool isLoop, bool isMusic, GameObject fatherObj, AudioOperation op)
        {
            // 1. 创建 AudioSource
            AudioSource source = CreateAudioSource(isMusic, fatherObj);
            op.Source = source;
            op.SetProgress(0);
            // 2. 使用 ResManager 的句柄进行加载
            var resOp = ResManager.Instance.LoadAsyncHandle<AudioClip>(fullPath);
            // 3. 实时同步加载进度
            while (!resOp.IsDone)
            {
                op.SetProgress(resOp.Progress);
                await UniTask.Yield();
            }
            // 4. 加载失败
            if (!resOp.Result)
            {
                op.SetFailed();
                return;
            }
            // 5. 加载成功 → 播放
            AudioClip clip = resOp.Result;
            PlayAudioClip(source, clip, isLoop);
            op.SetResult(clip);
        }

        /// <summary>
        /// 创建音效资源
        /// </summary>
        /// <param name="isMusic">是否是音乐文件 若为否则是音效文件</param>
        /// <param name="fatherObj">需要跟随的父物体（仅音效文件时需要）</param>
        /// <returns></returns>
        private AudioSource CreateAudioSource(bool isMusic, GameObject fatherObj)
        {
            // 如果需要播放的是音乐文件
            if (isMusic)
            {
                // 动态创建 播放音乐 的播放器 且不会过场景移除 保证过场景时也能播放
                if (!musicPlayer)
                {
                    GameObject obj = new("MusicPlayer");
                    Object.DontDestroyOnLoad(obj);
                    musicPlayer = obj.AddComponent<AudioSource>();
                    musicPlayer.outputAudioMixerGroup = musicGroup;
                }
                return musicPlayer;
            }

            // --- 音效 ---
            AudioSource source = PoolManager.Instance.Spawn("res://FinkFramework/Audio/Base/SoundPlayer").GetComponent<AudioSource>();
            source.outputAudioMixerGroup = sfxGroup;
            // 若缓存池达到上限可能会取出之前正在播放的音效 所以可以先执行一次停止播放
            source.Stop();
            // 父节点绑定逻辑
            if (!fatherObj)
            {
                if (!soundPlayers)
                {
                    soundPlayers = new GameObject("SoundPlayers");
                }
                source.transform.parent = soundPlayers.transform;
            }
            else
            {
                source.transform.parent = fatherObj.transform;
            }

            return source;
        }

        #endregion
    }
}