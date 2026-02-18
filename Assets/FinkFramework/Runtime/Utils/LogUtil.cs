using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using FinkFramework.Runtime.Environments;
using Debug = UnityEngine.Debug;
// ReSharper disable HeuristicUnreachableCode

#pragma warning disable CS0162 // 检测到不可到达的代码

namespace FinkFramework.Runtime.Utils
{
    public static class LogUtil
    {
        private static readonly ConcurrentDictionary<int, string> _callerCache = new();

        #region 工具辅助方法

        private static string GetCallerClassName()
        {
            int hash = Environment.StackTrace.GetHashCode(); // 用栈签名简易做缓存Key
            if (_callerCache.TryGetValue(hash, out string cached))
                return cached;

            var trace = new StackTrace(false);
            for (int i = 1; i < trace.FrameCount; i++)
            {
                var frame = trace.GetFrame(i);
                var method = frame?.GetMethod();
                var type = method?.DeclaringType;
                if (type == null) continue;
                string typeName = type.Name;

                // ① 跳过 LogUtil 自身
                if (type == typeof(LogUtil))
                    continue;

                // ② 处理匿名类（lambda、闭包、计时器回调都会生成匿名显示类）
                if (typeName.Contains("<>") || typeName.Contains("DisplayClass"))
                {
                    string full = type.FullName;   // Ex: "Test+<>c__DisplayClass0_0"

                    if (!string.IsNullOrEmpty(full))
                    {
                        int plusIndex = full.IndexOf('+');
                        if (plusIndex > 0)
                        {
                            // 取外层类名，例如 "Test"
                            string outerClass = full.Substring(0, plusIndex)
                                .Split('.')
                                .Last();

                            _callerCache.TryAdd(hash, outerClass);
                            return outerClass;
                        }
                    }

                    continue;
                }
                
                // ③ async 状态机：<Method>d__XX
                if (typeName.StartsWith("<") && typeName.Contains(">d__"))
                {
                    string full = type.FullName;
                    int plusIndex = full.IndexOf('+');
                    if (plusIndex > 0)
                    {
                        string outerClass = full[..plusIndex]
                            .Split('.')
                            .Last();

                        _callerCache.TryAdd(hash, outerClass);
                        return outerClass;
                    }
                }

                // ④ 处理普通类
                string clean = typeName.Split('`')[0];  // 去掉泛型后缀
                _callerCache.TryAdd(hash, clean);
                return clean;
            }
            return "Unknown";
        }
        private static string FormatModuleTag(string module, string color)
        {
            if (string.IsNullOrEmpty(module))
                module = GetCallerClassName();
            return $"<color={color}>[{module}]</color>";
        }

        #endregion
        
        #region Log
        
        /// <summary>
        /// Log(日志信息)，自动模块
        /// </summary>
        public static void Log(string message, bool force = false)
        {
            if (!EnvironmentState.DebugMode && !force) return;
            string module = GetCallerClassName();
            Debug.Log($"{FormatModuleTag(module, "#87CEFA")} {message}");
        }

        /// <summary>
        /// Log(日志信息)，指定模块
        /// </summary>
        public static void Log(string module, string message, bool force = false)
        {
            if (!EnvironmentState.DebugMode && !force) return;
            Debug.Log($"{FormatModuleTag(module, "#87CEFA")} {message}");
        }
        
        #endregion
      
        #region Info
        
        /// <summary>
        /// Info(日志信息)，自动模块
        /// </summary>
        public static void Info(string message, bool force = false)
        {
            if (!EnvironmentState.DebugMode && !force) return;
            string module = GetCallerClassName();
            Debug.Log($"{FormatModuleTag(module, "#87CEFA")} {message}");
        }

        /// <summary>
        /// Info(日志信息)，指定模块
        /// </summary>
        public static void Info(string module, string message, bool force = false)
        {
            if (!EnvironmentState.DebugMode && !force) return;
            Debug.Log($"{FormatModuleTag(module, "#87CEFA")} {message}");
        }
        
        #endregion

        #region Success
        
        public static void Success(string message, bool force = false)
        {
            if (!EnvironmentState.DebugMode && !force) return;
            string module = GetCallerClassName();
            Debug.Log($"{FormatModuleTag(module, "#00FF7F")} {message} <color=#00FF7F>✓</color>");
        }

        public static void Success(string module, string message, bool force = false)
        {
            if (!EnvironmentState.DebugMode && !force) return;
            Debug.Log($"{FormatModuleTag(module, "#00FF7F")} {message} <color=#00FF7F>✓</color>");
        }
        
        #endregion

        #region Warn
        
        public static void Warn(string message, bool force = true)
        {
            if (!EnvironmentState.DebugMode && !force) return;
            string module = GetCallerClassName();
            Debug.LogWarning($"{FormatModuleTag(module, "#FFA500")} {message} <color=#FFA500>!</color>");
        }

        public static void Warn(string module, string message, bool force = true)
        {
            if (!EnvironmentState.DebugMode && !force) return;
            Debug.LogWarning($"{FormatModuleTag(module, "#FFA500")} {message} <color=#FFA500>!</color>");
        }
        
        #endregion

        #region Error
        
        public static void Error(string message)
        {
            string module = GetCallerClassName();
            Debug.LogError($"{FormatModuleTag(module, "#FF4500")} {message} <color=#FF4500>✗</color>");
        }

        public static void Error(string module, string message)
        {
            Debug.LogError($"{FormatModuleTag(module, "#FF4500")} {message} <color=#FF4500>✗</color>");
        }
        
        #endregion
    }
}