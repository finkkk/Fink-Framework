using System.Collections.Generic;
using UnityEngine;
// ReSharper disable ForCanBeConvertedToForeach

namespace FinkFramework.Runtime.Utils
{
    /// <summary>
    /// 屏幕调试日志（IMGUI）
    /// 用于快速显示运行时信息
    /// </summary>
    public class LogUI
    {
        public static bool Enabled = true;
        private const int MaxLogCount = 20;
        
        /// <summary>
        /// 一条打印信息所需要的数据
        /// </summary>
        private class LogItem
        {
            public GUIContent content;
            public float startTime;
            public float duration;
            public Color color;
        }
        
        private static readonly List<LogItem> logs = new();
        private static readonly object logsLock = new object();
        
        private static GUIStyle style;
        private const float maxWidth = 900f;
        
        private static LogUIRunner runner;
        private static bool isQuitting;
        
        private static void EnsureInit()
        {
            if (isQuitting) return;
            if (runner) return;

            var go = new GameObject("[LogUIRunner]");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<LogUIRunner>();
        }
        
        #region 静态内部 API
        
        /// <summary>
        /// 在屏幕上显示一条日志
        /// </summary>
        private static void Push(string text, Color color, float duration = 1.5f)
        {
            if (!Enabled) return;
            if (isQuitting) return;

            EnsureInit();
            if (!runner) return;
            
          
            lock (logsLock)
            {
                if (logs.Count >= MaxLogCount)
                    logs.RemoveAt(0);

                logs.Add(new LogItem
                {
                    content = new GUIContent(text),
                    color = color,
                    startTime = Time.time,
                    duration = Mathf.Max(0.1f, duration)
                });
            }
        }

        #endregion

        #region 公开API

        /// <summary>
        /// 普通信息打印 默认白色字体
        /// </summary>
        /// <param name="msg">打印内容</param>
        /// <param name="time">持续时间</param>
        public static void Info(string msg, float time = 1.5f)
        {
            Push(msg, Color.white, time);
            LogUtil.Info(msg);
        }
        
        /// <summary>
        /// 普通信息打印 默认白色字体
        /// </summary>
        /// <param name="msg">打印内容</param>
        /// <param name="time">持续时间</param>
        public static void Log(string msg, float time = 1.5f)
        {
            Push(msg, Color.white, time);
            LogUtil.Log(msg);
        }

        /// <summary>
        /// 成功信息打印 默认绿色字体
        /// </summary>
        /// <param name="msg">打印内容</param>
        /// <param name="time">持续时间</param>
        public static void Success(string msg, float time = 1.5f)
        {
            Push(msg, new Color(0.4f, 1f, 0.4f), time);
            LogUtil.Success(msg);
        }

        /// <summary>
        /// 警告信息打印 默认黄色字体
        /// </summary>
        /// <param name="msg">打印内容</param>
        /// <param name="time">持续时间</param>
        public static void Warn(string msg, float time = 3f)
        {
            Push(msg, new Color(1f, 0.8f, 0.2f), time);
            LogUtil.Warn(msg);
        }

        /// <summary>
        /// 错误信息打印 默认红色字体
        /// </summary>
        /// <param name="msg">打印内容</param>
        /// <param name="time">持续时间</param>
        public static void Error(string msg, float time = 5f)
        {
            Push(msg, Color.red, time);
            LogUtil.Error(msg);
        }

        #endregion

        internal static void DrawGUI()
        {
            if (!Enabled) return;

            float now = Time.time;
            
            // 只在锁内：清理过期 + 拷贝快照
            List<LogItem> snapshot = null;
            
            lock (logsLock)
            {
                for (int i = logs.Count - 1; i >= 0; i--)
                {
                    if (now - logs[i].startTime > logs[i].duration)
                        logs.RemoveAt(i);
                }

                if (logs.Count == 0)
                    return;

                snapshot = new List<LogItem>(logs);
            }
            
            // 锁外：GUIStyle 初始化 + 计算 + 绘制
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter
                };
            }
            
            float screenWidth = Screen.width;
            float baseX = (screenWidth - maxWidth) * 0.5f;
            float baseY = 20f;

            for (int i = 0; i < snapshot.Count; i++)
            {
                var log = snapshot[i];
                    
                // ===== 生命周期进度 =====
                float elapsed = now - log.startTime;
                float t = Mathf.Clamp01(elapsed / log.duration);

                // ===== 渐隐（后 30%）=====
                float alpha = 1f;
                if (t > 0.7f)
                {
                    alpha = Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
                }

                // ===== 上浮（只在后 30%）=====
                float floatOffset = 0f;
                if (t > 0.7f)
                {
                    // 最多向上飘 18 像素（你可以调）
                    floatOffset = Mathf.Lerp(0f, -18f, (t - 0.7f) / 0.3f);
                }

                // ===== 颜色（保留原色，只改 alpha）=====
                Color guiColor = log.color;
                guiColor.a *= alpha;
                GUI.color = guiColor;

                float height = style.CalcHeight(
                    log.content,
                    maxWidth
                );

                GUI.Label(
                    new Rect(baseX, baseY + floatOffset, maxWidth, height),
                    log.content,
                    style
                );

                baseY += height + 8f;
            }
            
            GUI.color = Color.white; 
        }
        
        internal sealed class LogUIRunner : MonoBehaviour
        {
            private void Awake()
            {
                // 如果已经有 runner（极端情况：重复创建），保留第一个
                if (runner && runner != this)
                {
                    Destroy(gameObject);
                    return;
                }

                runner = this;
                DontDestroyOnLoad(gameObject);
            }
            
            private void OnGUI()
            {
                DrawGUI();
            }

            private void OnApplicationQuit()
            {
                isQuitting = true;
            }

            private void OnDestroy()
            {
                if (runner == this)
                    runner = null;
            }
        }

    }
}