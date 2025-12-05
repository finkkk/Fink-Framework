using System;
using System.IO;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.ResLoad.Base;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.ResLoad.Providers
{
    /// <summary>
    /// 本地文件提供器（支持 file:// 前缀）
    /// 支持类型：
    /// - TextAsset（.txt、.json、.xml、.ini、任何文本文件）
    /// - Texture2D
    /// - AudioClip（wav/ogg/mp3）
    /// - AssetBundle
    /// - Raw byte[]
    /// </summary>
    public class FileProvider : IResProvider
    {
        /// <summary>
        /// 同步加载（仅用于小文件，不建议加载大资源 可加载 wav，但不支持 mp3/ogg）
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            Type t = typeof(T);

            if (!File.Exists(path))
            {
                LogUtil.Error($"[FileProvider] 文件不存在: {path}");
                return null;
            }
            
            if (t == typeof(AudioClip))
            {
                string ext = Path.GetExtension(path).ToLower();
                if (ext != ".wav")
                {
                    LogUtil.Error("[FileProvider] AudioClip 同步加载仅支持 .wav，请使用 LoadAsync 加载 mp3/ogg！");
                    return null;
                }

                // 同步解析 wav
                byte[] bytes = File.ReadAllBytes(path);
                return CreateWavClip(bytes, Path.GetFileName(path)) as T;
            }

            byte[] data = File.ReadAllBytes(path);
            return ConvertBytesSync<T>(data, path);
        }

        /// <summary>
        /// 异步加载（推荐）
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            Type t = typeof(T);

            if (!File.Exists(path))
            {
                LogUtil.Error($"[FileProvider] 文件不存在: {path}");
                return null;
            }

            // AudioClip 独立处理（支持 wav/mp3/ogg）
            if (t == typeof(AudioClip))
            {
                string ext = Path.GetExtension(path).ToLower();
                AudioType audioType = AudioType.WAV;

                if (ext == ".mp3") audioType = AudioType.MPEG;
                else if (ext == ".ogg") audioType = AudioType.OGGVORBIS;

                string url = "file://" + path;

                using var req = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
                await req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogUtil.Error($"[FileProvider] 音频加载失败: {path} => {req.error}");
                    return null;
                }

                // 避免 streaming 造成 clip 引用失效
                var handler = (DownloadHandlerAudioClip)req.downloadHandler;
                handler.streamAudio = false;

                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = Path.GetFileName(path);
                return clip as T;
            }

            // 其它资源异步读取 bytes
            byte[] bytes = await File.ReadAllBytesAsync(path);
            return ConvertBytesAsync<T>(bytes, path);
        }

        /// <summary>
        /// 判断资源是否存在
        /// </summary>
        public bool Exists(string path) => File.Exists(path);

        /// <summary>
        /// 卸载资源（AssetBundle）
        /// </summary>
        public void Unload(string realPath)
        {
            // 本地文件不需要卸载
        }

        /// <summary>
        /// 清理缓存（FileProvider 无缓存）
        /// </summary>
        public void Clear()
        {
            // 本地文件不需要清理缓存
        }

        /// <summary>
        /// FileProvider 不需要进度（直接返回 false）
        /// </summary>
        public bool TryGetProgress(string url, out float progress)
        {
            progress = 0;
            return false;
        }

        #region 工具方法
        
        /// <summary>
        /// 同步资源解析（不允许出现 await）
        /// </summary>
        private T ConvertBytesSync<T>(byte[] bytes, string realPath) where T : Object
        {
            Type t = typeof(T);

            // TextAsset
            if (t == typeof(TextAsset))
                return new TextAsset(System.Text.Encoding.UTF8.GetString(bytes)) as T;

            // Texture2D
            if (t == typeof(Texture2D))
            {
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                tex.name = Path.GetFileName(realPath);
                return tex as T;
            }

            // AssetBundle
            if (t == typeof(AssetBundle))
                return AssetBundle.LoadFromMemory(bytes) as T;

            if (t == typeof(Object))
            {
                LogUtil.Error("[FileProvider] 不允许使用 Object 类型加载资源，请指定具体类型！");
                return null;
            }

            LogUtil.Error($"[FileProvider] 不支持同步加载类型: {t.Name}");
            return null;
        }

        /// <summary>
        /// 异步资源解析
        /// </summary>
        private T ConvertBytesAsync<T>(byte[] bytes, string realPath) where T : Object
        {
            return ConvertBytesSync<T>(bytes, realPath); // 其它类型同步解析足够快
        }
        // WAV 同步解码（可安全在主线程执行）
        private AudioClip CreateWavClip(byte[] wavData, string name)
        {
            int channels = BitConverter.ToInt16(wavData, 22);
            int sampleRate = BitConverter.ToInt32(wavData, 24);
            const int headerSize = 44;

            int sampleCount = (wavData.Length - headerSize) / 2;
            float[] floatData = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(wavData, headerSize + i * 2);
                floatData[i] = sample / 32768f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount / channels, channels, sampleRate, false);
            clip.SetData(floatData, 0);
            return clip;
        }

        #endregion
    }
}
