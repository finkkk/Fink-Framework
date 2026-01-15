using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Event;
using FinkFramework.Runtime.Mono;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Input
{
    /// <summary>
    /// 输入管理器模块 仅适用于旧版输入系统（Input）
    /// </summary>
    public class InputManager : Singleton<InputManager>
    {
        private InputManager()
        {
            // 如果检测到项目正在使用新版输入系统，则不允许使用旧输入模块 除非手动设置
            if (EnvironmentState.FinalUseNewInputSystem)
            {
                LogUtil.Error("项目检测到新版输入系统(Input System)，已自动为您关闭框架输入模块。若想强制手动开启该模块，请前往全局设置手动设置ForceDisableNewInputSystem");
                return; //  提前退出
            }
            MonoManager.Instance.AddUpdateListener(InputUpdate);
        }
        private Dictionary<Enum, InputInfo> inputDic;
        // 懒加载字典 防止不启用输入管理器模块的时候空字典占用内存
        private Dictionary<Enum, InputInfo> InputDic => inputDic ??= new Dictionary<Enum, InputInfo>();

        //当前遍历时取出的输入信息
        private InputInfo nowInputInfo;
        //用于在改建时获取输入信息的委托 只有当update中获取到信息的时候 再通过委托传递给外部
        private UnityAction<InputInfo> getInputInfoCallBack;
        //是否开始检测输入信息
        private bool isBeginCheckInput;
        // 是否开启输入检测
        private bool toggle;
        
        /// <summary>
        /// 控制是否开启输入检测
        /// </summary>
        /// <param name="toggle">是否开启</param>
        public void ToggleInputCheck(bool toggle)
        {
            this.toggle = toggle;
        }

        private void InputUpdate()
        {
            //当委托不为空时 证明想要获取到输入的信息 传递给外部
            if(isBeginCheckInput)
            {
                //当一个键按下时 然后遍历所有按键信息 得到是谁被按下了
                if (UnityEngine.Input.anyKeyDown)
                {
                    InputInfo inputInfo = null;
                    //我们需要去遍历监听所有键位的按下 来得到对应输入的信息
                    //键盘
                    foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                    {
                        //判断到底是谁被按下了 那么就可以得到对应的输入的键盘信息
                        if (UnityEngine.Input.GetKeyDown(key))
                        {
                            inputInfo = new InputInfo(InputInfo.E_InputType.Down, key);
                            break;
                        }
                    }
                    //鼠标
                    for (int i = 0; i < 3; i++)
                    {
                        if (UnityEngine.Input.GetMouseButtonDown(i))
                        {
                            inputInfo = new InputInfo(InputInfo.E_InputType.Down, i);
                            break;
                        }
                    }
                    //把获取到的信息传递给外部
                    getInputInfoCallBack?.Invoke(inputInfo);
                    getInputInfoCallBack = null;
                    //检测一次后就停止检测了
                    isBeginCheckInput = false;
                }
            }

            
            //如果外部没有开启检测功能 就不要检测
            if (!toggle) return;
            
            foreach (var (eventType, info) in InputDic.ToArray())
            {
                nowInputInfo = info;
                //如果是键盘输入
                if(nowInputInfo.keyOrMouse == InputInfo.E_KeyOrMouse.Key)
                {
                    //是抬起还是按下还是长按
                    switch (nowInputInfo.inputType)
                    {
                        case InputInfo.E_InputType.Down:
                            if (UnityEngine.Input.GetKeyDown(nowInputInfo.key))
                                EventManager.Instance.EventTrigger(eventType);
                            break;
                        case InputInfo.E_InputType.Up:
                            if (UnityEngine.Input.GetKeyUp(nowInputInfo.key))
                                EventManager.Instance.EventTrigger(eventType);
                            break;
                        case InputInfo.E_InputType.Always:
                            if (UnityEngine.Input.GetKey(nowInputInfo.key))
                                EventManager.Instance.EventTrigger(eventType);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                //如果是鼠标输入
                else
                {
                    switch (nowInputInfo.inputType)
                    {
                        case InputInfo.E_InputType.Down:
                            if (UnityEngine.Input.GetMouseButtonDown(nowInputInfo.mouseID))
                                EventManager.Instance.EventTrigger(eventType);
                            break;
                        case InputInfo.E_InputType.Up:
                            if (UnityEngine.Input.GetMouseButtonUp(nowInputInfo.mouseID))
                                EventManager.Instance.EventTrigger(eventType);
                            break;
                        case InputInfo.E_InputType.Always:
                            if (UnityEngine.Input.GetMouseButton(nowInputInfo.mouseID))
                                EventManager.Instance.EventTrigger(eventType);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        /// <summary>
        /// 提供给外部改建或初始化的方法(键盘)
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="key">按键</param>
        /// <param name="inputType">输入类型</param>
        public void ChangeKeyboardInfo(Enum eventType, KeyCode key, InputInfo.E_InputType inputType)
        {
            //初始化
            if(!InputDic.ContainsKey(eventType))
            {
                InputDic.Add(eventType, new InputInfo(inputType, key));
            }
            else//改建
            {
                //如果之前是鼠标 我们必须要修改它的按键类型
                InputDic[eventType].keyOrMouse = InputInfo.E_KeyOrMouse.Key;
                InputDic[eventType].key = key;
                InputDic[eventType].inputType = inputType;
            }
        }

        /// <summary>
        /// 提供给外部改建或初始化的方法(鼠标)
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="mouseID">鼠标按键</param>
        /// <param name="inputType">输入类型</param>
        public void ChangeMouseInfo(Enum eventType, int mouseID, InputInfo.E_InputType inputType)
        {
            //初始化
            if (!InputDic.ContainsKey(eventType))
            {
                InputDic.Add(eventType, new InputInfo(inputType, mouseID));
            }
            else//改建
            {
                //如果之前是鼠标 我们必须要修改它的按键类型
                InputDic[eventType].keyOrMouse = InputInfo.E_KeyOrMouse.Mouse;
                InputDic[eventType].mouseID = mouseID;
                InputDic[eventType].inputType = inputType;
            }
        }

        /// <summary>
        /// 移除指定行为的输入监听
        /// </summary>
        /// <param name="eventType">事件类型</param>
        public void RemoveInputInfo(Enum eventType)
        {
            if (InputDic.ContainsKey(eventType))
                InputDic.Remove(eventType);
        }
    
        /// <summary>
        /// 获取下一次的输入信息
        /// </summary>
        /// <param name="callBack">回调</param>
        public void GetInputInfo(UnityAction<InputInfo> callBack)
        {
            getInputInfoCallBack = callBack;
            MonoManager.Instance.StartCoroutine(BeginCheckInput());
        }

        private IEnumerator BeginCheckInput()
        {
            //等一帧
            yield return 0;
            //一帧后才会被置成true
            isBeginCheckInput = true;
        }
    }
}