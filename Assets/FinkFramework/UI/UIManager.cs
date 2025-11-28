using System.Collections.Generic;
using FinkFramework.Config;
using FinkFramework.Singleton;
using FinkFramework.Utils;
using Framework.ResLoad;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if UNITY_RENDER_PIPELINE_URP
using UnityEngine.Rendering.Universal;
#endif

namespace FinkFramework.UI
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
        #region 内部类型定义
        /// <summary>
        /// 主要用于里式替换原则 在字典中 用父类容器装载子类对象
        /// </summary>
        private abstract class BasePanelInfo
        {
            public Canvas rootCanvas;
            public BasePanel panel;
            public bool isHide;
        }

        /// <summary>
        /// 用于存储面板信息 和加载完成的回调函数的
        /// </summary>
        /// <typeparam name="T">面板的类型</typeparam>
        private class PanelInfo<T> : BasePanelInfo where T:BasePanel
        {
            public new T panel  // 泛型 panel，隐藏基类
            {
                get => base.panel as T;
                set => base.panel = value;
            }
            public UnityAction<T> callBack;
            public PanelInfo(UnityAction<T> callBack)
            {
                this.callBack += callBack;
            }
        }
        #endregion
        
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
            // ========== 创建 UI Camera ==========
            uiCamera = Object.Instantiate(ResManager.Instance.Load<GameObject>("UI/Base/UICamera")).GetComponent<Camera>();
            Object.DontDestroyOnLoad(uiCamera.gameObject);
            
            // ========== 创建 主 Canvas ==========   PS:在worldSpace模式下该画布一般用于HUD使用
            mainCanvas = Object.Instantiate(ResManager.Instance.Load<GameObject>("UI/Base/MainCanvas")).GetComponent<Canvas>();
            Object.DontDestroyOnLoad(mainCanvas.gameObject);

            mainCanvas.renderMode = GlobalConfig.CurrentUIMode switch
            {
                GlobalConfig.UIMode.ScreenSpace => RenderMode.ScreenSpaceCamera,
                GlobalConfig.UIMode.WorldSpace => RenderMode.WorldSpace,
                GlobalConfig.UIMode.Auto => GlobalConfig.IsVRProject
                    ? RenderMode.WorldSpace
                    : RenderMode.ScreenSpaceCamera,
                _ => mainCanvas.renderMode
            };

            // 统一设置 UI Camera
            mainCanvas.worldCamera = uiCamera;

            #if UNITY_RENDER_PIPELINE_URP
            SetupCameraStack();   // 自动处理URP
            #endif
            
            // ========== 确保 EventSystem 存在 ==========
            if (!EventSystem.current)
            {
                if (GlobalConfig.IsVRProject)
                {
                    var eventSystem = Object.Instantiate(ResManager.Instance.Load<GameObject>($"{BASE_PATH}EventSystem_XR"));
                    Object.DontDestroyOnLoad(eventSystem);
                }
                else
                {
                    var eventSystem = Object.Instantiate(ResManager.Instance.Load<GameObject>($"{BASE_PATH}EventSystem"));
                    Object.DontDestroyOnLoad(eventSystem);
                }
                
            }
            
            // ========== 获取主层级 ==========
            bottomLayer = mainCanvas.transform.Find("Bottom");
            middleLayer = mainCanvas.transform.Find("Middle");
            topLayer = mainCanvas.transform.Find("Top");
            systemLayer = mainCanvas.transform.Find("System");
            
            LogUtil.Success("初始化完成");
        }
        
        /// <summary>
        /// 自动处理URP管线
        /// </summary>
        private void SetupCameraStack()
        {
        #if UNITY_RENDER_PIPELINE_URP
        var mainCam = Camera.main;
        if (!mainCam) return;

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
        
        #region 显示面板（异步/同步）
        /// <summary>
        /// 显示面板
        /// </summary>
        /// <typeparam name="T">面板的类型</typeparam>
        /// <param name="layer">面板显示的层级</param>
        /// <param name="callBack">由于可能是异步加载 因此通过委托回调的形式 将加载完成的面板传递出去进行使用</param>
        /// <param name="isAsync">是否同步加载</param>
        /// <param name="uiRootType">传入的画布模式 默认为HUD 即为主画布</param>
        /// <param name="canvasId">若传入的画布模式不为主画布 则基于此Id查找对应画布</param>
        public void ShowPanel<T>(E_MainLayer layer = E_MainLayer.Middle, UnityAction<T> callBack = null, bool isAsync = true, E_UIRoot uiRootType = E_UIRoot.HUD, string canvasId = "") where T:BasePanel
        {
            // 层级与key的处理 决定挂到哪个父物体
            string panelKey = BuildPanelKey<T>(uiRootType, canvasId);
            Transform canvasRoot = uiRootType != E_UIRoot.HUD
                ? UIRootManager.Instance.GetCanvasInfo(uiRootType, canvasId)?.panelParent
                : GetMainLayerFather(layer);
            if (!canvasRoot) canvasRoot = middleLayer;
            // 已存在面板
            if(panelDic.TryGetValue(panelKey, out var baseInfo))
            {
                // 取出字典中已经占好位置的数据
                PanelInfo<T> panelInfo = baseInfo as PanelInfo<T>;
                // 1. 正在异步加载中
                if (!panelInfo!.panel)
                {
                    // 若之前显示过又隐藏后 想再次显示 直接设置为false 避免重复异步加载一次
                    panelInfo.isHide = false;
                    // 若正在异步加载应该等待加载完毕 只需记录回调函数 加载完毕后调用即可
                    if (callBack != null)
                    {
                        panelInfo.callBack += callBack;
                    }
                }
                // 2. 异步加载结束
                else
                {
                    // 若面板是失活状态 直接激活面板
                    if (!panelInfo.panel.gameObject.activeSelf)
                    {
                        panelInfo.panel.gameObject.SetActive(true);
                    }
                    //如果要显示面板 会执行一次面板的默认显示逻辑
                    panelInfo.panel.ShowMe();
                    //调用生命周期钩子
                    panelInfo.panel.OnShow();
                    //如果存在回调 直接返回出去即可
                    callBack?.Invoke(panelInfo.panel);
                }
                return;
            }
            //若不存在面板 先存入字典当中 占个位置 之后如果又显示 我才能得到字典中的信息进行判断
            panelDic.Add(panelKey, new PanelInfo<T>(callBack));
            // 若是异步加载
            if (isAsync)
            {
                ResManager.Instance.LoadAsync<GameObject>($"{PANEL_PATH}{typeof(T).Name}", (res) =>
                {
                    if (!panelDic.TryGetValue(panelKey, out var basePanelInfo))
                        return; // 被清理了，直接中止

                    var panelInfo = basePanelInfo as PanelInfo<T>;
                    if (panelInfo!.isHide || !res)
                    {
                        panelDic.Remove(panelKey);
                        return;
                    }
                    //将面板预设体创建到对应父对象下 并且保持原本缩放大小
                    GameObject panelObj = Object.Instantiate(res, canvasRoot, false);
                    //获取对应UI组件返回出去
                    T panelCom = panelObj.GetComponent<T>();
                    // 赋值 rootCanvas
                    panelInfo.rootCanvas = panelObj.GetComponentInParent<Canvas>();
                    //显示面板时执行的默认方法
                    panelCom.ShowMe();
                    //调用生命周期钩子
                    panelCom.OnShow();
                    try
                    {
                        //传出去使用
                        panelInfo.callBack?.Invoke(panelCom);
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"异步回调执行出错: {ex.Message}");
                    }
                    finally
                    {
                        // 回调执行完毕直接置空 避免内存泄漏
                        panelInfo.callBack = null;
                    }
                    //存储panel
                    panelInfo.panel = panelCom;
                });
            }
            // 若是同步加载
            else
            {
                GameObject res = ResManager.Instance.Load<GameObject>($"{PANEL_PATH}{typeof(T).Name}");
                // 取出字典中已经占好位置的数据
                PanelInfo<T> panelInfo = panelDic[panelKey] as PanelInfo<T>;
                // 表示异步加载结束前 就想要隐藏该面板
                if (panelInfo!.isHide)
                {
                    panelDic.Remove(panelKey);
                    return;   
                }
                //层级的处理
                Transform father = canvasRoot ? canvasRoot : GetMainLayerFather(layer);
                //避免没有按指定规则传递层级参数 避免为空
                if (!father) father = middleLayer;
                //将面板预设体创建到对应父对象下 并且保持原本缩放大小
                GameObject panelObj = Object.Instantiate(res, father, false);
                //获取对应UI组件返回出去
                T panelCom = panelObj.GetComponent<T>();
                // 赋值 rootCanvas
                panelInfo.rootCanvas = panelObj.GetComponentInParent<Canvas>();
                //显示面板时执行的默认方法
                panelCom.ShowMe();
                //调用生命周期钩子
                panelCom.OnShow();
                try
                {
                    //传出去使用
                    panelInfo.callBack?.Invoke(panelCom);
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"回调执行出错: {ex.Message}");
                }
                finally
                {
                    // 回调执行完毕直接置空 避免内存泄漏
                    panelInfo.callBack = null;
                }
                //存储panel
                panelInfo.panel = panelCom;
            }
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
                    // 置空回调 
                    panelInfo.callBack = null;
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
            Canvas canvas = UIRootManager.Instance.GetCanvas(uiRootType,canvasId);
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
                var panelInfo = panel as PanelInfo<T>;
                //正在加载中
                if(!panelInfo!.panel)
                {
                    //加载中 应该等待加载结束 再通过回调传递给外部去使用
                    panelInfo.callBack += callBack;
                }
                else if(!panelInfo.isHide)//加载结束 并且没有隐藏
                {
                    callBack?.Invoke(panelInfo.panel);
                }
            }
            else
            {
                LogUtil.Warn($"未找到 GetPanel<{typeof(T).Name}> ，可能还没调用ShowMe初始化过");
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