using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Mono;
using FinkFramework.Runtime.Pool;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.Audio
{
    /// <summary>
    /// 只管理框架创建的单个音乐播放器和池化音效播放器。所有 API 必须在运行期间的 Unity 主线程调用；关闭模块时，操作入口抛出说明未启用的异常。
    /// 音量直接作用于这些 AudioSource，不修改 Mixer 参数、效果、Snapshot 或用户播放器。
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        #region 状态与配置

        private const string SoundPlayerPath = "res://FinkFramework/Audio/Base/SoundPlayer";
        private const string DisabledMessage = "音频模块未启用，请在 Project Settings > Fink Framework > Audio System 中启用音频模块。";

        private bool audioEnabled;
        private bool ownsBuiltInMixer;
        private AudioMixerGroup musicGroup;
        private AudioMixerGroup sfxGroup;
        private AudioSource musicPlayer;
        private GameObject soundPlayers;
        private string musicPath;
        private int musicRequest;
        private int soundRequest;
        private bool soundIsPlay = true;
        private float globalVolume = 1f;
        private float musicVolume = 1f;
        private float soundVolume = 1f;
        private readonly List<AudioSource> soundList = new();
        private readonly HashSet<AudioSource> soundSet = new();
        private readonly Dictionary<AudioSource, string> soundPaths = new();
        // 同时记录加载中和播放中的音效，防止池复用或停止后的旧加载重新启动播放器。
        private readonly Dictionary<AudioSource, int> soundRequests = new();
        private readonly List<AudioSource> cleanupBuffer = new();

        #endregion

        #region 服务入口与音量状态

        /// <summary>只检查配置，不创建音频服务、播放器或帧回调。</summary>
        public static bool IsEnabled => AudioSettingsRuntimeLoader.Current.EnableAudioModule;

        /// <summary>仅在启用时创建服务；关闭时抛出明确的未启用异常，不进入单例注册和初始化。</summary>
        /// <exception cref="InvalidOperationException">模块未启用或音频输出配置无效。</exception>
        public new static AudioManager Instance
        {
            get
            {
                if (!IsEnabled) throw new InvalidOperationException(DisabledMessage);
                var manager = Singleton<AudioManager>.Instance;
                if (!manager.audioEnabled) manager.Initialize();
                return manager;
            }
        }

        /// <summary>关闭模块时返回 false，不触发单例创建或注册。</summary>
        public new static bool HasInstance => TryGetInstance() != null;

        /// <summary>仅返回已启用的现有服务，关闭时为 null，不触发初始化。</summary>
        public new static AudioManager TryGetInstance()
        {
            if (!IsEnabled) return null;
            var manager = Singleton<AudioManager>.TryGetInstance();
            return manager is { audioEnabled: true } ? manager : null;
        }

        /// <summary>设置框架全局音量，与音乐、音效分类音量相乘。立即作用于框架持有的播放器，不改变分类目标值或用户播放器。</summary>
        /// <param name="v">0～1；有限越界值限制到此范围，NaN 或 Infinity 抛出 ArgumentOutOfRangeException。0 为全局静音。</param>
        public void ChangeGlobalValue(float v)
        {
            EnsureReady();
            globalVolume = NormalizeVolume(v);
            ApplyMusicVolume();
            ApplySoundVolume();
        }

        /// <summary>当前框架全局目标音量，范围 0～1；与音乐、音效分类音量相乘。</summary>
        public float GlobalVolume => globalVolume;
        /// <summary>当前音乐分类目标音量，范围 0～1；不包含全局倍率，播放切换或停止不会重置。</summary>
        public float MusicVolume => musicVolume;
        /// <summary>当前音效分类目标音量，范围 0～1；不包含全局倍率，对象池复用不会重置。</summary>
        public float SoundVolume => soundVolume;

        #endregion

        #region 音乐播放

        /// <summary>同步加载并循环播放音乐；大文件建议使用异步入口。停止或新请求会取消旧请求的播放资格。</summary>
        /// <param name="fullPath">ResManager 支持的完整资源地址。</param>
        public void PlayMusic(string fullPath)
        {
            EnsureReady();
            int request = ++musicRequest;
            var clip = ResManager.Instance.Load<AudioClip>(fullPath);
            if (!clip) return;
            var source = CreateAudioSource(true, null);
            if (!IsCurrent(source, true, request))
            {
                ReleaseLoadedClip(fullPath);
                return;
            }
            CommitMusic(source, clip, true, fullPath);
        }

        /// <summary>异步加载并循环播放音乐，返回框架持有的播放器。失败或请求失效时返回 null。</summary>
        /// <param name="fullPath">完整资源地址。</param>
        /// <returns>框架音乐播放器，调用方不应销毁或归还对象池。</returns>
        public async UniTask<AudioSource> PlayMusicAsync(string fullPath)
        {
            var operation = PlayAudioHandle(fullPath, true, true);
            await operation.WaitUntilDone();
            return operation.IsFailed ? null : operation.Source;
        }

        /// <summary>通过回调异步播放音乐；加载失败时回调收到 null，模块关闭时抛出未启用异常，不发起请求。</summary>
        /// <param name="path">完整资源地址。</param>
        /// <param name="callback">加载结束回调，不是播放结束回调。</param>
        public void PlayMusicAsyncCallback(string path, UnityAction<AudioSource> callback = null)
        {
            var operation = PlayAudioHandle(path, true, true);
            operation.Completed += completed => callback?.Invoke(completed.IsFailed ? null : completed.Source);
        }

        /// <summary>句柄式异步音乐播放。后发请求优先，停止后旧请求不会恢复播放。</summary>
        /// <param name="fullPath">完整资源地址。</param>
        /// <returns>加载与播放操作，加载失败或请求失效时 IsFailed 为 true；模块关闭或配置无效时抛出异常。</returns>
        public AudioOperation PlayMusicAsyncHandle(string fullPath)
            => PlayAudioHandle(fullPath, true, true);

        /// <summary>停止音乐并归还当前音频资源，同时使尚未完成的音乐请求失效。</summary>
        public void StopMusic()
        {
            EnsureReady();
            musicRequest++;
            if (musicPlayer)
            {
                musicPlayer.Stop();
                musicPlayer.clip = null;
            }
            ReleaseLoadedClip(musicPath);
            musicPath = null;
        }

        /// <summary>暂停当前音乐播放器。</summary>
        /// <param name="name">当前只有一个音乐播放器，该参数不参与查找。</param>
        public void PauseMusic(string name)
        {
            EnsureReady();
            if (musicPlayer) musicPlayer.Pause();
        }

        /// <summary>设置框架音乐分类音量，与全局音量相乘，立即影响当前及后续音乐。无需暴露 Mixer 参数。</summary>
        /// <param name="v">0～1；有限越界值会限制到此范围，NaN 或 Infinity 抛出 ArgumentOutOfRangeException。0 为静音。</param>
        public void ChangeMusicValue(float v)
        {
            EnsureReady();
            musicVolume = NormalizeVolume(v);
            ApplyMusicVolume();
        }

        #endregion

        #region 音效播放

        /// <summary>同步加载并播放音效，资源和播放器由框架持有。</summary>
        /// <param name="fullPath">完整资源地址。</param>
        /// <param name="isLoop">是否循环播放。</param>
        /// <param name="fatherObj">跟随的父物体，为空时使用框架音效容器。</param>
        /// <param name="callback">加载结束回调，失败时收到 null。</param>
        /// <returns>成功时返回框架音效播放器，加载失败时为 null；模块关闭或配置无效时抛出异常。</returns>
        public AudioSource PlaySound(string fullPath, bool isLoop = false, GameObject fatherObj = null,
            UnityAction<AudioSource> callback = null)
        {
            EnsureReady();
            var source = CreateAudioSource(false, fatherObj);
            if (!source)
            {
                callback?.Invoke(null);
                return null;
            }
            int request = soundRequests[source];
            var clip = ResManager.Instance.Load<AudioClip>(fullPath);
            if (!clip || !IsCurrent(source, false, request))
            {
                if (clip) ReleaseLoadedClip(fullPath);
                if (IsCurrent(source, false, request)) StopSoundInternal(source);
                callback?.Invoke(null);
                return null;
            }
            PlaySoundClip(source, clip, isLoop, fullPath);
            callback?.Invoke(source);
            return source;
        }

        /// <summary>异步加载并播放音效。</summary>
        /// <param name="path">完整资源地址。</param>
        /// <param name="isLoop">是否循环。</param>
        /// <param name="fatherObj">跟随的父物体。</param>
        /// <returns>成功时返回框架持有的播放器，加载失败或请求失效时为 null；模块关闭或配置无效时抛出异常。</returns>
        public async UniTask<AudioSource> PlaySoundAsync(string path, bool isLoop = false, GameObject fatherObj = null)
        {
            var operation = PlayAudioHandle(path, isLoop, false, fatherObj);
            await operation.WaitUntilDone();
            return operation.IsFailed ? null : operation.Source;
        }

        /// <summary>回调式异步音效播放。</summary>
        /// <param name="path">完整资源地址。</param>
        /// <param name="callback">加载结束回调；失败或取消时收到 null。</param>
        /// <param name="isLoop">是否循环。</param>
        /// <param name="fatherObj">跟随的父物体。</param>
        public void PlaySoundAsyncCallback(string path, UnityAction<AudioSource> callback, bool isLoop = false,
            GameObject fatherObj = null)
        {
            var operation = PlayAudioHandle(path, isLoop, false, fatherObj);
            operation.Completed += completed => callback?.Invoke(completed.IsFailed ? null : completed.Source);
        }

        /// <summary>句柄式异步音效播放。请求的 Source 可用于 StopSound，包括加载尚未完成时。</summary>
        /// <param name="path">完整资源地址。</param>
        /// <param name="isLoop">是否循环。</param>
        /// <param name="fatherObj">跟随的父物体。</param>
        /// <returns>加载与播放操作；加载失败或请求失效时 IsFailed 为 true；模块关闭或配置无效时抛出异常。</returns>
        public AudioOperation PlaySoundHandle(string path, bool isLoop = false, GameObject fatherObj = null)
            => PlayAudioHandle(path, isLoop, false, fatherObj);

        /// <summary>停止框架持有的指定音效，取消其待完成请求，归还音频资源及池对象。外部播放器不受影响。</summary>
        /// <param name="source">框架当前持有的音效播放器；回收后不要长期保存并用于控制其他播放。</param>
        public void StopSound(AudioSource source)
        {
            EnsureReady();
            StopSoundInternal(source);
        }

        /// <summary>设置全部框架音效的音量，包括加载中及后续新音效；不调整用户的外部 AudioSource 或 Mixer 参数。</summary>
        /// <param name="v">0～1；有限越界值限制到此范围，非有限值抛出 ArgumentOutOfRangeException。0 为静音。</param>
        public void ChangeSoundValue(float v)
        {
            EnsureReady();
            soundVolume = NormalizeVolume(v);
            ApplySoundVolume();
        }

        /// <summary>暂停或继续全部框架音效，继续时保留播放位置；暂停期间的新音效也保持暂停。</summary>
        /// <param name="isPlay">true 继续，false 暂停。</param>
        public void ToggleAllSounds(bool isPlay)
        {
            EnsureReady();
            if (soundIsPlay == isPlay) return;
            soundIsPlay = isPlay;
            foreach (var source in soundList)
            {
                if (!source) continue;
                if (isPlay) source.UnPause();
                else source.Pause();
            }
        }

        /// <summary>停止全部框架音效（包括加载中的请求），归还资源与池对象。不停止音乐或外部播放器。</summary>
        public void ClearSound()
        {
            EnsureReady();
            var sources = new List<AudioSource>(soundRequests.Keys);
            foreach (var source in sources) StopSoundInternal(source);
        }

        #endregion

        #region 异步播放核心

        /// <summary>音乐/音效共用的句柄式入口，仅选择这两类播放，不管理额外音轨。</summary>
        /// <param name="fullPath">完整资源地址。</param>
        /// <param name="isLoop">是否循环。</param>
        /// <param name="isMusic">true 音乐，false 音效。</param>
        /// <param name="fatherObj">音效跟随的父物体。</param>
        /// <returns>操作句柄；加载失败或请求失效时 IsFailed 为 true；模块关闭或配置无效时抛出异常。</returns>
        public AudioOperation PlayAudioHandle(string fullPath, bool isLoop, bool isMusic, GameObject fatherObj = null)
        {
            EnsureReady();
            int request = isMusic ? ++musicRequest : 0;
            var source = CreateAudioSource(isMusic, fatherObj);
            if (!isMusic && source) request = soundRequests[source];
            var operation = new AudioOperation { Source = source };
            _ = PlayAudioAsyncWrapper(fullPath, isLoop, isMusic, source, request, operation);
            return operation;
        }

        private async UniTask PlayAudioAsyncWrapper(string path, bool isLoop, bool isMusic, AudioSource source,
            int request, AudioOperation operation)
        {
            AudioClip clip = null;
            bool committed = false;
            try
            {
                if (!IsCurrent(source, isMusic, request)) return;
                var resource = ResManager.Instance.LoadAsyncHandle<AudioClip>(path);
                while (!resource.IsDone)
                {
                    operation.SetProgress(resource.Progress);
                    await UniTask.Yield();
                }
                clip = resource.Result;
                if (!clip || !IsCurrent(source, isMusic, request)) return;
                if (isMusic) CommitMusic(source, clip, isLoop, path);
                else PlaySoundClip(source, clip, isLoop, path);
                committed = true;
                operation.SetResult(clip);
            }
            catch (Exception exception)
            {
                LogUtil.Error("AudioManager", $"异步播放失败：{path} => {exception}");
            }
            finally
            {
                if (!IsEnabled) ShutdownInternal();
                if (!committed)
                {
                    if (clip) ReleaseLoadedClip(path);
                    if (!isMusic && IsCurrent(source, false, request)) StopSoundInternal(source);
                    operation.SetFailed();
                }
            }
        }

        private bool IsCurrent(AudioSource source, bool isMusic, int request)
            => audioEnabled && IsEnabled && source && (isMusic ? source == musicPlayer && musicRequest == request
                : soundRequests.TryGetValue(source, out int current) && current == request);

        #endregion

        #region 播放器与资源回收

        private void StopSoundInternal(AudioSource source)
        {
            if (ReferenceEquals(source, null) || !soundRequests.Remove(source)) return;
            soundSet.Remove(source);
            soundList.Remove(source);
            if (source)
            {
                source.Stop();
                source.clip = null;
            }
            ReleaseSoundClip(source);
            if (source)
            {
                // 非调试模式的对象池不会重新挂父节点，先脱离业务/临时容器，避免回收后被父对象误删。
                source.transform.SetParent(null, true);
                PoolManager.Instance.Despawn(source.gameObject);
            }
        }

        private AudioSource CreateAudioSource(bool isMusic, GameObject fatherObj)
        {
            if (isMusic)
            {
                if (!musicGroup) return null;
                if (!musicPlayer)
                {
                    var obj = new GameObject("MusicPlayer");
                    Object.DontDestroyOnLoad(obj);
                    musicPlayer = obj.AddComponent<AudioSource>();
                }
                musicPlayer.outputAudioMixerGroup = musicGroup;
                musicPlayer.volume = globalVolume * musicVolume;
                return musicPlayer;
            }
            if (!sfxGroup) return null;
            var soundObject = PoolManager.Instance.Spawn(SoundPlayerPath);
            var source = soundObject ? soundObject.GetComponent<AudioSource>() : null;
            if (!source)
            {
                if (soundObject) PoolManager.Instance.Despawn(soundObject);
                LogUtil.Error("AudioManager", "无法创建 SoundPlayer AudioSource。");
                return null;
            }
            soundSet.Remove(source);
            soundList.Remove(source);
            source.Stop();
            source.clip = null;
            ReleaseSoundClip(source);
            soundRequests[source] = ++soundRequest;
            source.outputAudioMixerGroup = sfxGroup;
            source.volume = globalVolume * soundVolume;
            source.pitch = 1f;
            source.mute = false;
            Transform parent = fatherObj ? fatherObj.transform : null;
            if (!parent && !PoolManager.debugMode)
            {
                if (!soundPlayers) soundPlayers = new GameObject("SoundPlayers");
                parent = soundPlayers.transform;
            }
            // 池复用后重新贴合跟随目标，不能继承上次播放的世界坐标或父节点偏移。
            source.transform.SetParent(parent, false);
            source.transform.localPosition = Vector3.zero;
            source.transform.localRotation = Quaternion.identity;
            return source;
        }

        private void CommitMusic(AudioSource source, AudioClip clip, bool isLoop, string path)
        {
            source.Stop();
            source.clip = null;
            ReleaseLoadedClip(musicPath);
            musicPath = path;
            PlayAudioClip(source, clip, isLoop, true);
        }

        private void PlaySoundClip(AudioSource source, AudioClip clip, bool isLoop, string path)
        {
            soundPaths[source] = path;
            if (soundSet.Add(source)) soundList.Add(source);
            PlayAudioClip(source, clip, isLoop, false);
            if (!soundIsPlay) source.Pause();
        }

        private void PlayAudioClip(AudioSource source, AudioClip clip, bool isLoop, bool isMusic)
        {
            source.outputAudioMixerGroup = isMusic ? musicGroup : sfxGroup;
            source.clip = clip;
            source.loop = isLoop;
            source.volume = globalVolume * (isMusic ? musicVolume : soundVolume);
            source.Play();
        }

        private void CleanAudioSource()
        {
            if (!IsEnabled)
            {
                ShutdownInternal();
                return;
            }
            cleanupBuffer.Clear();
            foreach (var source in soundRequests.Keys)
                if (!source) cleanupBuffer.Add(source);
            foreach (var source in soundList)
                if (source && soundIsPlay && !source.isPlaying) cleanupBuffer.Add(source);
            foreach (var source in cleanupBuffer) StopSoundInternal(source);
        }

        private void ReleaseSoundClip(AudioSource source)
        {
            if (soundPaths.Remove(source, out string path)) ReleaseLoadedClip(path);
        }

        #endregion

        #region 模块生命周期与音量应用

        private AudioManager() => Initialize();

        private void Initialize()
        {
            if (!IsEnabled) throw new InvalidOperationException(DisabledMessage);
            if (!AudioSettingsRuntimeLoader.TryLoad(out var settings, out string error))
                throw new InvalidOperationException($"音频配置无效，无法初始化 AudioManager：{error}");
            if (settings == null)
                throw new InvalidOperationException(DisabledMessage);
            ownsBuiltInMixer = settings.UsesBuiltInMixer;
            musicGroup = settings.MusicGroup;
            sfxGroup = settings.SoundGroup;
            audioEnabled = true;
            MonoManager.Instance.AddFixedUpdateListener(CleanAudioSource);
        }

        private void EnsureReady()
        {
            if (!IsEnabled)
            {
                ShutdownInternal();
                throw new InvalidOperationException(DisabledMessage);
            }
            if (!audioEnabled) Initialize();
        }

        /// <summary>配置关闭时立即撤销现有服务的帧回调、播放器及资源引用；不创建服务。</summary>
        public static void ShutdownIfDisabled()
        {
            if (!IsEnabled) Singleton<AudioManager>.TryGetInstance()?.ShutdownInternal();
        }

        private void ShutdownInternal()
        {
            if (!audioEnabled) return;
            audioEnabled = false;
            // 暂停属于本轮播放状态，关闭后清除；用户设置的三个音量目标继续保留。
            soundIsPlay = true;
            musicRequest++;
            MonoManager.TryGetInstance()?.RemoveFixedUpdateListener(CleanAudioSource);
            if (musicPlayer)
            {
                musicPlayer.Stop();
                musicPlayer.clip = null;
                Object.Destroy(musicPlayer.gameObject);
                musicPlayer = null;
            }
            ReleaseLoadedClip(musicPath);
            musicPath = null;
            foreach (var source in new List<AudioSource>(soundRequests.Keys)) StopSoundInternal(source);
            cleanupBuffer.Clear();
            if (soundPlayers) Object.Destroy(soundPlayers);
            soundPlayers = null;
            musicGroup = sfxGroup = null;
            if (ownsBuiltInMixer)
            {
                ownsBuiltInMixer = false;
                ResManager.TryGetInstance()?.UnloadAsset<AudioMixer>(AudioSettingsRuntimeLoader.BuiltInMixerPath, true);
            }
        }

        private void ApplyMusicVolume()
        {
            if (musicPlayer) musicPlayer.volume = globalVolume * musicVolume;
        }

        private void ApplySoundVolume()
        {
            foreach (var source in soundRequests.Keys)
                if (source) source.volume = globalVolume * soundVolume;
        }

        private static float NormalizeVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "音量必须是有限数值。");
            return Mathf.Clamp01(value);
        }

        // 每次成功 Load 获得一份引用；转交播放器后由停止/回收释放，失效请求由 finally 释放。
        private static void ReleaseLoadedClip(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                ResManager.TryGetInstance()?.UnloadAsset<AudioClip>(path, true);
        }

        #endregion
    }
}
