using System.Collections.Generic;
using FinkFramework.Singleton;
using FinkFramework.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FinkFramework.UI
{
    /// <summary>
    /// 当且仅当Canvas为worldSpace模式时启用： 管理场景里的所有 UI Canvas 根物体
    /// </summary>
    public class UIRootManager : Singleton<UIRootManager>
    {
        private UIRootManager(){ }
        private readonly Dictionary<(E_UIRoot, string), CanvasInfo> rootDic = new();
        
        // 获取唯一 Canvas
        public Canvas GetCanvas(E_UIRoot type, string id)
        {
            if (id == "")
            {
                LogUtil.Error($"[UIRootManager] 获取信息不能传入空id!");
            }
            string fullId = BuildCanvasKey(id);
            if (fullId == null) return null;
            if (rootDic.TryGetValue((type, fullId), out var root))
                return root.canvas;
            LogUtil.Warn($"[UIRootManager] 未找到 Canvas: Type={type}, Id={id}");
            return null;
        }

        // 获取唯一父节点
        public Transform GetCanvasRoot(E_UIRoot type, string id)
        {
            if (id == "")
            {
                LogUtil.Error($"[UIRootManager] 获取信息不能传入空id!");
            }
            id = SceneManager.GetActiveScene().name + "_" + id;
            if (rootDic.TryGetValue((type, id), out var root))
                return root.panelParent;

            LogUtil.Error($"[UIRootManager] 未找到 CanvasInfo: Type={type}, Id={id}");
            return null;
        }
        
        public CanvasInfo GetCanvasInfo(E_UIRoot type, string id)
        {
            if (id == "")
            {
                LogUtil.Error($"[UIRootManager] 获取信息不能传入空id!");
            }
            id = SceneManager.GetActiveScene().name + "_" + id;
            if (rootDic.TryGetValue((type, id), out var root))
                return root;
            LogUtil.Error($"[UIRootManager] 未找到 CanvasRoot: Type={type}, Id={id}");
            return null;
        }

        // 获取某类下所有 Canvas
        public IEnumerable<CanvasInfo> GetTypeAllCanvas(E_UIRoot type)
        {
            bool found = false;
            foreach (var kv in rootDic)
            {
                if (kv.Key.Item1 == type)
                {
                    found = true;
                    yield return kv.Value;
                }
            }

            if (!found)
            {
                LogUtil.Error($"[UIRootManager] 未找到任何 {type} 类型的 Canvas");
            }
        }
        
        public void RegisterAllCanvas()
        {
            rootDic.Clear();
            var markers = Object.FindObjectsOfType<CanvasMarker>(true);
            foreach (var marker in markers)
            {
                RegisterCanvas(marker);
            }
        }
        
        private void RegisterCanvas(CanvasMarker marker)
        {
            var canvas = marker.GetComponent<Canvas>();
            if (!canvas)
            {
                LogUtil.Warn($"{marker.name} 没有 Canvas 组件，已忽略");
                return;
            }
            // 兜底：如果没有 panelParent，就用 canvas 自身
            var parent = marker.panelParent ? marker.panelParent : canvas.transform;
            // 兜底：如果没填 Id，就用对象名
            var localId = string.IsNullOrEmpty(marker.canvasId) ? marker.name : marker.canvasId;
            // 拼接场景名，避免不同场景冲突
            string sceneName = canvas.gameObject.scene.name;
            string finalId = $"{sceneName}_{localId}";
            var key = (marker.rootType, finalId);
            // 自动设置 worldCamera
            if (canvas.renderMode == RenderMode.WorldSpace && !canvas.worldCamera)
            {
                var uiCam = UIManager.Instance.uiCamera;
                if (uiCam)
                {
                    canvas.worldCamera = uiCam;
                }
            }
            #if UNITY_XR_MANAGEMENT 
            if (GlobalConfig.isVR)
            {
                canvas.TryGetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(out var raycaster);
                if (!raycaster)
                {
                    canvas.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                }
            }
            #endif
            var root = new CanvasInfo
            {
                type = marker.rootType,
                canvasId = finalId,
                canvas = canvas,
                panelParent = parent
            };

            if (!rootDic.TryAdd(key, root))
            {
                LogUtil.Warn($"已存在 {key}，忽略 {canvas.name}");
            }
        }
        
        /// <summary>
        /// 构建画布唯一Key
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string BuildCanvasKey(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                LogUtil.Error("[UIRootManager] 传入的 Canvas Id 不能为空！");
                return null;
            }
            return $"{SceneManager.GetActiveScene().name}_{id}";
        }
    }
    
    /// <summary>
    /// 仅在VR项目中使用 面板的根位置
    /// </summary>
    public enum E_UIRoot
    {
        HUD,        // 头显固定的 HUD
        HandMenu,   // 手边跟随的菜单
        WorldPanel, // 场景中的面板（坐标固定）
    }
    
    /// <summary>
    /// 每个 CanvasInfo 的封装
    /// </summary>
    public class CanvasInfo
    {
        public E_UIRoot type;
        public Canvas canvas;
        public string canvasId;
        public Transform panelParent; // 用于放置面板的根节点
    }

}