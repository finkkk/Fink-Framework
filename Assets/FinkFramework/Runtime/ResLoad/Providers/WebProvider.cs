using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.ResLoad.Base;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.ResLoad.Providers
{
    /// <summary>
    /// 网络资源 Provider（支持 http:// 与 https://）
    /// 支持类型：
    /// - TextAsset
    /// - Texture2D
    /// - AudioClip（wav/ogg/mp3）
    /// - AssetBundle
    /// - Raw bytes
    /// </summary>
    public class WebProvider : IResProvider
    {
        /// <summary> 保存正在运行的请求（用于获取下载进度） </summary>
        private readonly Dictionary<string, UnityWebRequestAsyncOperation> ops = new();

        /// <summary>
        /// 同步加载（Web 无法同步）
        /// </summary>
        public T Load<T>(string url) where T : Object
        {
            Debug.LogError($"[WebProvider] 不支持同步加载网络资源，请使用 LoadAsync！URL = {url}");
            return null;
        }


        /// <summary>
        /// 异步加载（业务主入口）
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string url) where T : Object
        {
            Type t = typeof(T);
            UnityWebRequest req = CreateRequestForType(t, url);

            if (req == null)
            {
                LogUtil.Error($"不支持的资源类型: {t.Name}");
                return null;
            }
            // 记录请求（用于进度条）
            var op = req.SendWebRequest();
            ops[url] = op;
            await op.ToUniTask();
            // 移除进度记录
            ops.Remove(url);

            if (req.result != UnityWebRequest.Result.Success)
            {
                LogUtil.Error($"网络加载失败: {url} => {req.error}");
                req.Dispose();
                return null;
            }
            // 解析结果
            T result = ParseResult<T>(req, url);
            req.Dispose();
            return result;
        }

        /// <summary>
        /// 创建不同类型的请求
        /// </summary>
        private UnityWebRequest CreateRequestForType(Type t, string url)
        {
            if (t == typeof(Texture2D))
                return UnityWebRequestTexture.GetTexture(url);

            if (t == typeof(AssetBundle))
                return UnityWebRequestAssetBundle.GetAssetBundle(url);

            if (t == typeof(AudioClip))
            {
                AudioType type = GetAudioTypeFromExtension(url);
                return UnityWebRequestMultimedia.GetAudioClip(url, type);
            }

            if (t == typeof(TextAsset) || t == typeof(byte[]))
                return UnityWebRequest.Get(url);

            return null;
        }

        /// <summary>
        /// 根据实际类型构造结果对象
        /// </summary>
        private T ParseResult<T>(UnityWebRequest req, string url) where T : Object
        {
            Type t = typeof(T);

            // Texture2D
            if (t == typeof(Texture2D))
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                tex.name = url;
                return tex as T;
            }

            // AssetBundle
            if (t == typeof(AssetBundle))
            {
                return DownloadHandlerAssetBundle.GetContent(req) as T;
            }

            // AudioClip
            if (t == typeof(AudioClip))
            {
                var handler = (DownloadHandlerAudioClip)req.downloadHandler;
                handler.streamAudio = false;

                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = url;
                return clip as T;
            }

            // TextAsset
            if (t == typeof(TextAsset))
            {
                string text = req.downloadHandler.text;
                return new TextAsset(text) as T;
            }

            // byte[]
            if (t == typeof(byte[]))
            {
                return req.downloadHandler.data as T;
            }

            Debug.LogError($"[WebProvider] 不支持的解析类型: {t.Name}");
            return null;
        }

        /// <summary>
        /// 进度查询
        /// </summary>
        public bool TryGetProgress(string url, out float progress)
        {
            if (ops.TryGetValue(url, out var op))
            {
                progress = op.progress;
                return true;
            }

            progress = 0;
            return false;
        }

        /// <summary>
        /// 网络资源永远假定存在（真正结果由请求决定）
        /// </summary>
        public bool Exists(string path) => true;

        public void Unload(string url)
        {
            // 网络资源不需要卸载
        }

        public void Clear()
        {
            ops.Clear();
        }

        /// <summary>
        /// 工具：根据 URL 推断音频类型
        /// </summary>
        private AudioType GetAudioTypeFromExtension(string url)
        {
            string ext = System.IO.Path.GetExtension(url).ToLower();

            return ext switch
            {
                ".mp3" => AudioType.MPEG,
                ".ogg" => AudioType.OGGVORBIS,
                ".wav" => AudioType.WAV,
                _ => AudioType.UNKNOWN
            };
        }
    }
}
