using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Settings;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.UI.Base;
using FinkFramework.Runtime.UI.Canva;
using FinkFramework.Runtime.UI.Panel;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
// ReSharper disable HeuristicUnreachableCode
#if UNITY_RENDER_PIPELINE_URP
using UnityEngine.Rendering.Universal;
#endif

namespace FinkFramework.Runtime.UI
{
    /// <summary>
    /// Main 层级枚举 (即共享Canvas 一般用于VR项目中HUD或者非VR项目中固定位置UI使用)
    /// </summary>
    public enum E_MainLayer
    {
        /// <summary>
        /// 最底层
        /// </summary>
        Bottom,
        /// <summary>
        /// 中层
        /// </summary>
        Middle,
        /// <summary>
        /// 高层
        /// </summary>
        Top,
        /// <summary>
        /// 系统层 最高层
        /// </summary>
        System,
    }   
    
    /// <summary>
    /// 管理所有UI面板的管理器
    /// 注意：面板预设体名要和面板类名一致！！！！！
    /// </summary>
    public class UIManager : Singleton<UIManager>
    {
        #region 常量定义
        private const string PANEL_PATH = "UI/Panels/";
        private const string BASE_PATH = "UI/Base/";
        private const string DEFAULT_PREFIX = "Default";
        #endregion
        
        #region 字段定义
        // Main层级父对象
        private readonly Transform bottomLayer;
        private readonly Transform middleLayer;
        private readonly Transform topLayer;
        private readonly Transform systemLayer;
        /// <summary>
        /// 用于存储所有画布的所有的面板对象
        /// </summary>
        private readonly Dictionary<string, BasePanelInfo> panelDic = new();
        // 主画布(在world space模式下一般用于HUD)
        public readonly Canvas mainCanvas;
        // UI Camera
        public readonly Camera uiCamera;
        #endregion

        #region 初始化UI管理器模块
        private UIManager()
        {
            // ======================
            // 1. 创建 UI Camera（VR 项目不需要 UI Camera）
            // ======================
            if (!EnvironmentState.FinalIsVR)
            {
                uiCamera = Object.Instantiate(
                    ResManager.Instance.Load<GameObject>($"res://{BASE_PATH}UICamera")
                ).GetComponent<Camera>();

                Object.DontDestroyOnLoad(uiCamera.gameObject);
            }
            
            // ======================
            // 2. 创建主 Canvas（VR = WorldSpace / 非 VR = ScreenSpaceCamera）
            // ======================
            mainCanvas = Object.Instantiate(ResManager.Instance.Load<GameObject>($"res://{BASE_PATH}MainCanvas")).GetComponent<Canvas>();
            Object.DontDestroyOnLoad(mainCanvas.gameObject);

            mainCanvas.renderMode = GlobalSettings.Current.CurrentUIMode switch
            {
                EnvironmentState.UIMode.ScreenSpace => RenderMode.ScreenSpaceCamera,
                EnvironmentState.UIMode.WorldSpace => RenderMode.WorldSpace,
                EnvironmentState.UIMode.Auto => EnvironmentState.FinalIsVR
                    ? RenderMode.WorldSpace
                    : RenderMode.ScreenSpaceCamera,
                _ => mainCanvas.renderMode
            };

            // 非 VR 模式绑定 UI Camera
            if (!EnvironmentState.FinalIsVR)
                mainCanvas.worldCamera = uiCamera;
            else
                mainCanvas.worldCamera = null; // VR 不使用 UI Camera

            // ======================
            // 3. URP CameraStack（非 VR 才用 UI Camera）
            // ======================
            if (!EnvironmentState.FinalIsVR && EnvironmentState.FinalUseURP)
            {
                SetupCameraStack();
            }
            
            // ======================
            // 4. EventSystem 选择逻辑（核心部分）
            // ======================
            if (!EventSystem.current)
            {
                string prefabName;

                if (EnvironmentState.FinalIsVR)
                {
                    prefabName = "EventSystem_XR";
                }
                else if (EnvironmentState.FinalUseNewInputSystem)
                {
                    prefabName = "EventSystem_New";
                }
                else
                {
                    prefabName = "EventSystem_Old";
                }

                var eventSystem = Object.Instantiate(
                    ResManager.Instance.Load<GameObject>($"res://{BASE_PATH}{prefabName}")
                );
                Object.DontDestroyOnLoad(eventSystem);
            }
            // ======================
            // 5. 获取主层级
            // ======================
            bottomLayer = mainCanvas.transform.Find("Bottom");
            middleLayer = mainCanvas.transform.Find("Middle");
            topLayer = mainCanvas.transform.Find("Top");
            systemLayer = mainCanvas.transform.Find("System");
            
            LogUtil.Success("初始化完成");
        }
        
        /// <summary>
        /// 自动处理URP管线
        /// </summary>
        /// <summary>
        /// 自动处理 URP camera stack
        /// </summary>
        private void SetupCameraStack()
        {
#if UNITY_RENDER_PIPELINE_URP
            var mainCam = Camera.main;
            if (!mainCam || !uiCamera) return;

            var mainData = mainCam.GetUniversalAdditionalCameraData();
            var uiData = uiCamera.GetUniversalAdditionalCameraData();

            uiData.renderType = CameraRenderType.Overlay;

            if (!mainData.cameraStack.Contains(uiCamera))
                mainData.cameraStack.Add(uiCamera);
#endif
        }
        #endregion
        
        #region Key 生成逻辑统一封装
        /// <summary>
        /// 拼接面板Key
        /// </summary>
        /// <param name="uiRootType">仅在VR项目中使用 面板的根位置</param>
        /// <param name="canvasId">画布id</param>
        /// <typeparam name="T">面板类</typeparam>
        /// <returns></returns>
        private string BuildPanelKey<T>(E_UIRoot uiRootType, string canvasId)
        {
            return uiRootType != E_UIRoot.HUD ? $"{SceneManager.GetActiveScene().name}_{canvasId}_{typeof(T).Name}" : $"{DEFAULT_PREFIX}_{typeof(T).Name}";
        }
        #endregion

        #region 主层级父节点获取
        /// <summary>
        /// 获取Main对应层级的父对象
        /// </summary>
        /// <param name="layer">层级枚举值</param>
        /// <returns></returns>
        public Transform GetMainLayerFather(E_MainLayer layer)
        {
            return layer switch
            {
                E_MainLayer.Bottom => bottomLayer,
                E_MainLayer.Middle => middleLayer,
                E_MainLayer.Top => topLayer,
                E_MainLayer.System => systemLayer,
                _ => null
            };
        }
        #endregion
        
        #region 同步显示面板
        
        /// <summary>
        /// 同步显示面板 （单画布模式）
        /// </summary>
        public T ShowPanel<T>(string fullPath = null, E_MainLayer layer = E_MainLayer.Middle) where T : BasePanel
        {
            return ShowPanelInternal<T>(fullPath, layer, E_UIRoot.HUD, "");
        }
        
        /// <summary>
        /// 同步显示面板 （多画布模式）
        /// </summary>
        /// <param name="layer">UI层级 默认为中层</param>
        /// <param name="uiRootType">传入的画布模式 默认为HUD 即为主画布</param>
        /// <param name="canvasId">若传入的画布模式不为主画布 则基于此Id查找对应画布</param>
        /// <param name="fullPath">面板预制体文件所在的带前缀的完整路径</param>
        /// <typeparam name="T">面板类</typeparam>
        /// <returns></returns>
        public T ShowPanelMultiCanvas<T>(E_MainLayer layer = E_MainLayer.Middle, E_UIRoot uiRootType = E_UIRoot.HUD, string canvasId = "", string fullPath = null) where T : BasePanel
        {
            return ShowPanelInternal<T>(fullPath, layer, uiRootType, canvasId);
        }
        
        /// <summary>
        /// 内部同步加载显示面板通用逻辑
        /// </summary>
        private T ShowPanelInternal<T>(string fullPath, E_MainLayer layer, E_UIRoot uiRootType, string canvasId) where T : BasePanel
        {
            string panelKey = BuildPanelKey<T>(uiRootType, canvasId);

            // ===== 面板已存在（缓存） ===== 
            if (panelDic.TryGetValue(panelKey, out var baseInfo))
            {
                // 取出字典中已经占好位置的数据
                var info = baseInfo as PanelInfo<T>;
                // === 情况 1：已加载结束 === 
                if (info.panel) // 已加载完成
                {
                    if (!info.panel.gameObject.activeSelf)
                        info.panel.gameObject.SetActive(true);
                    if (!info.isInit)
                        info.panel.ShowMe();
                    info.panel.OnShow();  
                    return info.panel;
                }
                else
                {
                    // === 情况 2：正在异步加载 === 
                    LogUtil.Error($"[UIManager] 面板 {typeof(T).Name} 正在异步加载中，无法同步加载！");
                    return null;
                }
            }

            //  ===== 面板不存在 → 先在字典占位 ===== 
            var newInfo = new PanelInfo<T> { isInit = false };
            panelDic.Add(panelKey, newInfo);

            //  ===== 同步加载 面板预制体 =====
            // fullPath 如果不为空 = 用户完全自定义加载来源（例如 ab://、res://、remote://）
            // 如果完整路径为空
            if (string.IsNullOrEmpty(fullPath))
            {
                // 自动拼接路径（默认为res://UI/Panels/类名）
                fullPath = $"res://{PANEL_PATH}{typeof(T).Name}";
            }
            GameObject prefab = ResManager.Instance.Load<GameObject>(fullPath);
            // 异步期间 ClearAllPanels / Destroy 面板 → 要提前退出
            if (!panelDic.ContainsKey(panelKey))
                return null;
            if (!prefab)
            {
                panelDic.Remove(panelKey);
                LogUtil.Error($"ShowPanelInternal 加载失败：{typeof(T).Name}");
                return null;
            }

            // 异步期间被标记为隐藏
            if (newInfo.isHide)
            {
                panelDic.Remove(panelKey);
                return null;
            }
            
            // ==== 获取父节点 ====
            Transform canvasRoot = uiRootType != E_UIRoot.HUD
                ? CanvasManager.Instance.GetCanvasInfo(uiRootType, canvasId)?.panelParent
                : GetMainLayerFather(layer);

            if (!canvasRoot) canvasRoot = middleLayer;

            // ==== 实例化 ====
            GameObject obj = Object.Instantiate(prefab, canvasRoot, false);
            T panelCom = obj.GetComponent<T>();

            newInfo.panel = panelCom;
            newInfo.rootCanvas = obj.GetComponentInParent<Canvas>();

            // ==== 生命周期 ====
            panelCom.ShowMe();
            panelCom.OnShow();
            newInfo.isInit = true;

            return panelCom;
        }
        #endregion

        #region 异步显示面板核心逻辑
           
        /// <summary>
        /// 内部异步加载显示面板通用逻辑
        /// </summary>
        private async UniTask<T> ShowPanelInternalAsync<T>(string fullPath, E_MainLayer layer, E_UIRoot uiRootType, string canvasId) where T : BasePanel
        {
            string panelKey = BuildPanelKey<T>(uiRootType, canvasId);

            // 找到父节点（MainCanvas 或 WorldCanvas）
            Transform canvasRoot = uiRootType != E_UIRoot.HUD
                ? CanvasManager.Instance.GetCanvasInfo(uiRootType, canvasId)?.panelParent
                : GetMainLayerFather(layer);

            if (!canvasRoot) canvasRoot = middleLayer;

            // ===== 面板已存在（缓存） ===== 
            if (panelDic.TryGetValue(panelKey, out var baseInfo))
            {
                // 取出字典中已经占好位置的数据
                var info = baseInfo as PanelInfo<T>;
                // === 情况 1：正在异步加载 === 
                if (!info!.panel)
                {
                    // 若之前显示过又隐藏后想再次显示直接设置为false 防止重复异步加载
                    info.isHide = false; // 取消 hide
                    // 等待加载完成（等 Internal 再 return）
                    var panel = await WaitForPanelLoaded(info,panelKey);
                    panel.gameObject.SetActive(true);
                    if (!info.isInit)
                        panel.ShowMe();
                    panel.OnShow();  
                    return panel;
                }
                // === 情况 2：已加载结束 === 
                // 若面板是失活状态直接激活面板
                if (!info.panel.gameObject.activeSelf)
                    info.panel.gameObject.SetActive(true);
                if (!info.isInit)
                    info.panel.ShowMe();
                info.panel.OnShow();
                return info.panel;
            }

            //  ===== 面板不存在 → 先在字典占位 ===== 
            var newInfo = new PanelInfo<T> { isInit = false };
            panelDic.Add(panelKey, newInfo);

            //  ===== 异步加载 面板预制体 =====
            // fullPath 如果不为空 = 用户完全自定义加载来源（例如 ab://、res://、remote://）
            // 如果完整路径为空
            if (string.IsNullOrEmpty(fullPath))
            {
                // 自动拼接路径（默认为res://UI/Panels/类名）
                fullPath = $"res://{PANEL_PATH}{typeof(T).Name}";
            }
            GameObject prefab = await ResManager.Instance.LoadAsync<GameObject>(fullPath);
            // 异步期间 ClearAllPanels / Destroy 面板 → 要提前退出
            if (!panelDic.ContainsKey(panelKey))
                return null;
            if (!prefab)
            {
                panelDic.Remove(panelKey);
                LogUtil.Error($"ShowPanelInternal 加载失败：{typeof(T).Name}");
                return null;
            }

            // 异步期间被标记为隐藏
            if (newInfo.isHide)
            {
                panelDic.Remove(panelKey);
                return null;
            }

            // 实例化面板
            GameObject obj = Object.Instantiate(prefab, canvasRoot, false);
            T panelCom = obj.GetComponent<T>();

            newInfo.panel = panelCom;
            newInfo.rootCanvas = obj.GetComponentInParent<Canvas>();

            panelCom.ShowMe();
            panelCom.OnShow();
            newInfo.isInit = true;

            return panelCom;
        }

        /// <summary>
        /// 内部异步加载显示面板通用逻辑(句柄式)
        /// </summary>
        private async UniTask LoadPanelHandleInternalAsync<T>(UIOperation<T> op, E_MainLayer layer, E_UIRoot root, string canvasId, string fullPath) where T : BasePanel
        {
            // 与 ShowPanelInternalAsync 基本一样
            // 只是最后不调 OnShow，只返回面板实例

            if (string.IsNullOrEmpty(fullPath))
                fullPath = $"res://UI/Panels/{typeof(T).Name}";

            op.SetProgress(0.2f);

            var prefab = await ResManager.Instance.LoadAsync<GameObject>(fullPath);

            if (!prefab)
            {
                op.SetFailed();
                return;
            }

            op.SetProgress(0.6f);

            Transform parent = root != E_UIRoot.HUD
                ? CanvasManager.Instance.GetCanvasInfo(root, canvasId)?.panelParent
                : GetMainLayerFather(layer);

            if (!parent) parent = middleLayer;

            var obj = Object.Instantiate(prefab, parent);
            var panel = obj.GetComponent<T>();

            op.SetProgress(1f);

            op.SetResult(panel);
        }
        
        /// <summary>
        /// 异步等待 面板加载完毕
        /// </summary>
        private async UniTask<T> WaitForPanelLoaded<T>(PanelInfo<T> info, string panelKey) where T : BasePanel
        {
            while (!info.panel && !info.isHide)
            {
                // 若面板在等待期间被清理（如 ClearAllPanels），安全退出
                if (!panelDic.ContainsKey(panelKey))
                    return null;
                await UniTask.Yield();
            }
            return info.panel;
        }

        #endregion
        
        #region 异步显示面板单画布模式
        
        /// <summary>
        /// 异步显示主画布的面板 await形式（单画布模式） 
        /// </summary>
        /// <param name="layer">UI层级 默认为中层</param>
        /// <param name="uiRootType">传入的画布模式 默认为HUD 即为主画布</param>
        /// <param name="canvasId">若传入的画布模式不为主画布 则基于此Id查找对应画布</param>
        /// <param name="fullPath">面板预制体文件所在的带前缀的完整路径</param>
        /// <typeparam name="T">面板类</typeparam>
        /// <returns></returns>
        public async UniTask<T> ShowPanelAsync<T>(string fullPath = null, E_MainLayer layer = E_MainLayer.Middle) where T : BasePanel
        {
            return await ShowPanelInternalAsync<T>(fullPath, layer, E_UIRoot.HUD, "");
        }

        /// <summary>
        /// 异步显示主画布的面板 回调形式（单画布模式） 
        /// </summary>
        /// <param name="layer">UI层级 默认为中层</param>
        /// <param name="uiRootType">传入的画布模式 默认为HUD 即为主画布</param>
        /// <param name="canvasId">若传入的画布模式不为主画布 则基于此Id查找对应画布</param>
        /// <param name="fullPath">面板预制体文件所在的带前缀的完整路径</param>
        /// <typeparam name="T">面板类</typeparam>
        /// <returns></returns>
        public void ShowPanelCallback<T>(string fullPath = null, UnityAction<T> callback = null, E_MainLayer layer = E_MainLayer.Middle) where T : BasePanel
        {
            _ = Wrapper();
            return;

            async UniTask Wrapper()
            {
                var panel = await ShowPanelInternalAsync<T>(fullPath, layer, E_UIRoot.HUD, "");
                callback?.Invoke(panel);
            }
        }
        
        /// <summary>
        /// 异步显示主画布的面板 句柄形式（单画布模式） 
        /// </summary>
        /// <param name="layer">UI层级 默认为中层</param>
        /// <param name="uiRootType">传入的画布模式 默认为HUD 即为主画布</param>
        /// <param name="canvasId">若传入的画布模式不为主画布 则基于此Id查找对应画布</param>
        /// <param name="fullPath">面板预制体文件所在的带前缀的完整路径</param>
        /// <typeparam name="T">面板类</typeparam>
        /// <returns>句柄</returns>
        public UIOperation<T> LoadPanelHandle<T>( string fullPath = null, E_MainLayer layer = E_MainLayer.Middle) where T : BasePanel
        {
            var op = new UIOperation<T>();
            _ = LoadPanelHandleInternalAsync(op, layer, E_UIRoot.HUD, "", fullPath);
            return op;
        }
        
        #endregion

        #region 异步显示面板多画布模式

        /// <summary>
        /// 异步显示面板 await形式（多画布模式） 
        /// </summary>
        /// <param name="layer">UI层级 默认为中层</param>
        /// <param name="uiRootType">传入的画布模式 默认为HUD 即为主画布</param>
        /// <param name="canvasId">若传入的画布模式不为主画布 则基于此Id查找对应画布</param>
        /// <param name="fullPath">面板预制体文件所在的带前缀的完整路径</param>
        /// <typeparam name="T">面板类</typeparam>
        /// <returns></returns>
        public async UniTask<T> ShowPanelMultiCanvasAsync<T>(E_MainLayer layer = E_MainLayer.Middle, E_UIRoot uiRootType = E_UIRoot.HUD, string canvasId = "", string fullPath = null) where T : BasePanel
        {
            return await ShowPanelInternalAsync<T>(fullPath, layer, uiRootType, canvasId);
        }
        
        /// <summary>
        /// 异步显示面板 回调形式（多画布模式） 
        /// </summary>
        /// <param name="layer">UI层级 默认为中层</param>
        /// <param name="uiRootType">传入的画布模式 默认为HUD 即为主画布</param>
        /// <param name="canvasId">若传入的画布模式不为主画布 则基于此Id查找对应画布</param>
        /// <param name="fullPath">面板预制体文件所在的带前缀的完整路径</param>
        /// <typeparam name="T">面板类</typeparam>
        /// <returns></returns>
        public void ShowPanelMultiCanvasCallback<T>(UnityAction<T> callback = null, E_MainLayer layer = E_MainLayer.Middle, E_UIRoot uiRootType = E_UIRoot.HUD, string canvasId = "", string fullPath = null) where T : BasePanel
        {
            _ = Wrapper();
            return;

            async UniTask Wrapper()
            {
                var panel = await ShowPanelInternalAsync<T>(fullPath, layer, uiRootType, canvasId);
                callback?.Invoke(panel);
            }
        }

        /// <summary>
        /// 异步显示面板 句柄形式（多画布模式） 
        /// </summary>
        /// <param name="layer">UI层级 默认为中层</param>
        /// <param name="uiRootType">传入的画布模式 默认为HUD 即为主画布</param>
        /// <param name="canvasId">若传入的画布模式不为主画布 则基于此Id查找对应画布</param>
        /// <param name="fullPath">面板预制体文件所在的带前缀的完整路径</param>
        /// <typeparam name="T">面板类</typeparam>
        /// <returns>句柄</returns>
        public UIOperation<T> LoadPanelMultiCanvasHandle<T>(E_MainLayer layer = E_MainLayer.Middle, E_UIRoot root = E_UIRoot.HUD, string canvasId = "", string fullPath = null) where T : BasePanel
        {
            var op = new UIOperation<T>();
            _ = LoadPanelHandleInternalAsync(op, layer, root, canvasId, fullPath);
            return op;
        }
        
        #endregion
        
        #region 隐藏面板
        /// <summary>
        /// 隐藏面板
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        public void HidePanel<T>(bool isDestroy = false, E_UIRoot uiRootType = E_UIRoot.HUD,string canvasId = "") where T : BasePanel
        {
            // 拼接面板key
            string panelKey = BuildPanelKey<T>(uiRootType, canvasId);
            if(panelDic.TryGetValue(panelKey,out var panel))
            {
                // 取出字典中已经占好位置的数据
                var panelInfo = panel as PanelInfo<T>;
                // 1.正在加载中
                if (!panelInfo!.panel)
                {
                    // 修改隐藏标识 标识该面板需要被隐藏
                    panelInfo.isHide = true;
                }
                // 2. 已经加载结束
                else
                {
                    //执行默认的隐藏面板想要做的事情
                    panelInfo.panel.HideMe();
                    //调用生命周期钩子（轻逻辑：暂停UI、保存状态）
                    panelInfo.panel.OnHide();
                    // 若需要销毁
                    if (isDestroy)
                    {
                        //销毁前调用生命周期钩子（重逻辑：解绑事件、释放引用）
                        panelInfo.panel.OnDestroyPanel();
                        //销毁面板
                        Object.Destroy(panelInfo.panel.gameObject);
                        //从容器中移除
                        panelDic.Remove(panelKey);
                    }
                    else
                    {
                        // 若不销毁 则直接让UI面板失活
                        panelInfo.panel.gameObject.SetActive(false);
                    }
                }
            }
        }
        
        /// <summary>
        /// 隐藏Main主画布中某层级的全部面板
        /// </summary>
        public void HidePanelsInLayer(E_MainLayer layer, bool isDestroy = false)
        {
            Transform targetLayer = GetMainLayerFather(layer);
            if (!targetLayer) return;
            // 收集需要隐藏的 (key, panelInfo)
            List<KeyValuePair<string, BasePanelInfo>> toHide = new();
            foreach (var kv in panelDic)
            {
                if (kv.Value?.panel && kv.Value.panel.transform.IsChildOf(targetLayer))
                    toHide.Add(kv);
            }
            // 执行隐藏/销毁
            foreach (var kv in toHide)
            {
                HidePanelInternal(kv.Key, kv.Value, isDestroy);
            }
        }
        
        /// <summary>
        /// 隐藏WorldSpace模式中特定 Canvas 下的所有面板
        /// </summary>
        public void HidePanelsInCanvas(E_UIRoot uiRootType = E_UIRoot.HUD,string canvasId = "", bool isDestroy = false)
        {
            Canvas canvas = CanvasManager.Instance.GetCanvas(uiRootType,canvasId);
            if (!canvas)
            {
                return;
            }
            // 收集需要隐藏的 (key, panelInfo)
            List<KeyValuePair<string, BasePanelInfo>> toHide = new();

            foreach (var kv in panelDic)
            {
                if (kv.Value.rootCanvas == canvas)
                {
                    toHide.Add(kv);
                }
            }

            // 执行隐藏/销毁
            foreach (var kv in toHide)
            {
                HidePanelInternal(kv.Key, kv.Value, isDestroy);
            }
        }
        
        /// <summary>
        /// 单个面板的隐藏/销毁逻辑
        /// </summary>
        private void HidePanelInternal(string key, BasePanelInfo panelInfo, bool isDestroy)
        {
            var panelObj = panelInfo.panel;
            if (!panelObj) return;

            panelObj.HideMe();
            panelObj.OnHide();

            if (isDestroy)
            {
                panelObj.OnDestroyPanel();
                Object.Destroy(panelObj.gameObject);
                panelDic.Remove(key);
            }
            else
            {
                panelObj.gameObject.SetActive(false);
            }
        }
        #endregion

        #region 获取面板
        /// <summary>
        /// 获取面板
        /// </summary>
        /// <typeparam name="T">面板的类型</typeparam>
        public void GetPanel<T>( UnityAction<T> callBack, E_UIRoot uiRootType = E_UIRoot.HUD,string canvasId = "") where T:BasePanel
        {
            // 拼接面板key
            string panelKey = BuildPanelKey<T>(uiRootType, canvasId);
            if (panelDic.TryGetValue(panelKey, out var panel))
            {
                //取出字典中已经占好位置的数据
                if (panel is not PanelInfo<T> panelInfo)
                {
                    LogUtil.Warn($"[UIManager] GetPanel<{typeof(T).Name}>：类型不匹配");
                    return;
                }
                //正在加载中
                if(!panelInfo!.panel)
                {
                    // 加载中 应该等待加载结束 启动等待流程
                    _ = WaitAndCallback();
                    return;

                    async UniTask WaitAndCallback()
                    {
                        var uiPanel = await WaitForPanelLoaded(panelInfo, panelKey);
                        if (uiPanel)
                            callBack?.Invoke(uiPanel);
                    }
                }
                else if(!panelInfo.isHide)//加载结束 并且没有隐藏
                {
                    callBack?.Invoke(panelInfo.panel);
                }
            }
            else
            {
                LogUtil.Warn($"[UIManager] GetPanel<{typeof(T).Name}>：尚未显示过该面板");
            }
        }
        #endregion

        #region 为控件添加自定义事件
        /// <summary>
        /// 为控件添加自定义事件
        /// </summary>
        /// <param name="control">对应的控件</param>
        /// <param name="type">事件的类型</param>
        /// <param name="callBack">响应的函数</param>
        public void AddCustomEventListener(UIBehaviour control, EventTriggerType type, UnityAction<BaseEventData> callBack)
        {
            //这种逻辑主要是用于保证 控件上只会挂载一个EventTrigger
            EventTrigger trigger = control.GetComponent<EventTrigger>();
            if (!trigger )
                trigger = control.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new()
            {
                eventID = type
            };
            entry.callback.AddListener(callBack);

            trigger.triggers.Add(entry);
        }
        #endregion
        
        #region 内存清理接口
        /// <summary>
        /// 清空所有已加载的面板（用于切场景或调试）
        /// </summary>
        public void ClearAllPanels()
        {
            int count = 0;
            foreach (var kv in panelDic.Values)
            {
                try
                {
                    if (kv.panel)
                    {
                        kv.panel.OnHide();
                        kv.panel.OnDestroyPanel();
                        Object.Destroy(kv.panel.gameObject);
                        count++;
                    }
                }
                catch (System.Exception ex)
                {
                    LogUtil.Warn($"清理面板时出错: {ex.Message}");
                }
            }
            panelDic.Clear();
            LogUtil.Success($"清理完毕，共清空 {count} 个面板。");
        }
        #endregion
        
    }
}