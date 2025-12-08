using System.Collections.Generic;
using FinkFramework.Runtime.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FinkFramework.Runtime.UI.Base
{
    public abstract class BasePanel : MonoBehaviour
    { 
        /// <summary>
        /// 用于存储所有要用到的UI控件，用里氏替换原则 父类装子类
        /// </summary>
        protected Dictionary<string, UIBehaviour> controlDic = new();

        /// <summary>
        /// 控件默认名字 如果得到的控件名字存在于这个容器 意味着我们不会通过代码去使用它 它只会是起到显示作用的控件
        /// </summary>
        private static readonly List<string> defaultNameList = new() { "Image",
                                                                       "Text (TMP)",
                                                                       "RawImage",
                                                                       "Background",
                                                                       "Checkmark",
                                                                       "Label",
                                                                       "Text (Legacy)",
                                                                       "Arrow",
                                                                       "Placeholder",
                                                                       "Fill",
                                                                       "Handle",
                                                                       "Viewport",
                                                                       "Scrollbar Horizontal",
                                                                       "Scrollbar Vertical"};
        protected virtual void Awake()
        {
            // ========== 交互控件（事件绑定优先） ==========
            FindChildrenControl<Button>();
            FindChildrenControl<Toggle>();
            FindChildrenControl<Slider>();
            FindChildrenControl<Scrollbar>();   

            // ========== 输入框（Legacy + TMP） ==========
            FindChildrenControl<InputField>();
            FindChildrenControl<TMP_InputField>();

            // ========== 下拉框（Legacy + TMP Dropdown） ==========
            FindChildrenControl<Dropdown>();
            FindChildrenControl<TMP_Dropdown>();

            // ========== ScrollView 系列 ==========
            FindChildrenControl<ScrollRect>();

            // ========== Toggle 组 ==========
            FindChildrenControl<ToggleGroup>();

            // ========== 文本（Legacy + TMP） ==========
            FindChildrenControl<Text>();
            FindChildrenControl<TextMeshProUGUI>();

            // ========== 图片 ==========
            FindChildrenControl<Image>();

            // ========== 布局组件 ==========
            FindChildrenControl<VerticalLayoutGroup>();
            FindChildrenControl<HorizontalLayoutGroup>();
            FindChildrenControl<GridLayoutGroup>();
        }


        /// <summary>
        /// 面板显示时会调用的逻辑(仅首次创建时调用一次）
        /// </summary>
        public abstract void ShowMe();

        /// <summary>
        /// 面板隐藏时会调用的逻辑
        /// </summary>
        public abstract void HideMe();
        
        /// <summary>
        /// 生命周期钩子：当面板每次被显示（包括重新显示）时调用
        /// —— 用于刷新面板内容（如更新数值、重置动画等）
        /// —— 与 ShowMe() 的区别：ShowMe 仅在首次创建时调用一次，而 OnShow 每次重新显示都会调用
        /// </summary>
        public virtual void OnShow() { }
        
        /// <summary>
        /// 生命周期钩子：当面板每次被隐藏时调用
        /// —— 用于保存状态、暂停动画、取消监听等
        /// —— 与 HideMe() 的区别：HideMe 负责主要隐藏逻辑（播放退出动画等），OnHide 更轻量，通常用于逻辑暂停
        /// </summary>
        public virtual void OnHide() { }
        
        /// <summary>
        /// 生命周期钩子：当面板被彻底销毁前调用（如 ClearAllPanels 或切场景）
        /// —— 用于释放引用、注销事件监听，防止内存泄漏
        /// </summary>
        public virtual void OnDestroyPanel() { }

        /// <summary>
        /// 获取指定名字以及指定类型的组件
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="name">组件名字</param>
        /// <returns></returns>
        public T GetControl<T>(string name) where T:UIBehaviour
        {
            if(controlDic.TryGetValue(name, out var col))
            {
                T control = col as T;
                if (!control)
                    LogUtil.Error($"不存在对应名字{name}类型为{typeof(T)}的组件");
                return control;
            }
            LogUtil.Error($"不存在对应名字{name}的组件");
            return null;
        }

        /// <summary>
        /// 按钮点击时调用
        /// </summary>
        /// <param name="btnName">按钮对象名称</param>
        protected virtual void ClickBtn(string btnName)
        {

        }

        /// <summary>
        /// 滑动条改变时调用
        /// </summary>
        /// <param name="sliderName">滑动条对象名称</param>
        /// <param name="value">当前滑动条的值</param>
        protected virtual void SliderValueChange(string sliderName, float value)
        {

        }

        /// <summary>
        /// 复选框改变时调用
        /// </summary>
        /// <param name="toggleName">输入框对象名称</param>
        /// <param name="value">当前复选框是否选中</param>
        protected virtual void ToggleValueChange(string toggleName, bool value)
        {

        }
        
        /// <summary>
        /// 输入框文本改变时调用（支持 InputField + TMP_InputField）
        /// </summary>
        /// <param name="inputName">输入框对象名称</param>
        /// <param name="value">当前文本内容</param>
        protected virtual void InputValueChange(string inputName, string value)
        {

        }

        private void FindChildrenControl<T>() where T:UIBehaviour
        {
            T[] controls = GetComponentsInChildren<T>(true);
            foreach (var t in controls)
            {
                //获取当前控件的名字
                string controlName = t.gameObject.name;
                //通过这种方式 将对应组件记录到字典中
                if (!controlDic.ContainsKey(controlName))
                {
                    // 如果控件不是默认名字
                    if(!defaultNameList.Contains(controlName))
                    {
                        controlDic.Add(controlName, t);
                        switch (t)
                        {
                            //判断控件的类型 决定是否加事件监听
                            case Button button:
                                button.onClick.AddListener(() =>
                                {
                                    ClickBtn(controlName);
                                });
                                break;
                            case Slider slider:
                                slider.onValueChanged.AddListener((value) =>
                                {
                                    SliderValueChange(controlName, value);
                                });
                                break;
                            case Toggle toggle:
                                toggle.onValueChanged.AddListener((value) =>
                                {
                                    ToggleValueChange(controlName, value);
                                });
                                break;
                            case InputField inputField:
                                inputField.onValueChanged.AddListener((value) =>
                                {
                                    InputValueChange(controlName, value);
                                });
                                break;
                            case TMP_InputField tmpInput:
                                tmpInput.onValueChanged.AddListener((value) =>
                                {
                                    InputValueChange(controlName, value);
                                });
                                break;
                        }
                    }
                }
            }
        }
    }
}
