using UnityEngine;

namespace FinkFramework.Input
{
    /// <summary>
    /// 输入信息
    /// </summary>
    public class InputInfo
    {
        public enum E_KeyOrMouse
        {
            /// <summary>
            /// 键盘输入
            /// </summary>
            Key,
            /// <summary>
            /// 鼠标输入
            /// </summary>
            Mouse,
        }

        public enum E_InputType
        {
            /// <summary>
            /// 按下
            /// </summary>
            Down,
            /// <summary>
            /// 抬起
            /// </summary>
            Up,
            /// <summary>
            /// 长按
            /// </summary>
            Always,
        }

        //具体输入的类型——键盘还是鼠标
        public E_KeyOrMouse keyOrMouse;
        //输入的类型——抬起、按下、长按
        public E_InputType inputType;
        //KeyCode
        public KeyCode key;
        //mouseID
        public int mouseID;

        /// <summary>
        /// 主要给键盘输入初始化
        /// </summary>
        /// <param name="inputType"></param>
        /// <param name="key"></param>
        public InputInfo(E_InputType inputType, KeyCode key)
        {
            this.keyOrMouse = E_KeyOrMouse.Key;
            this.inputType = inputType;
            this.key = key;
        }

        /// <summary>
        /// 主要给鼠标输入初始化
        /// </summary>
        /// <param name="inputType"></param>
        /// <param name="mouseID"></param>
        public InputInfo(E_InputType inputType, int mouseID)
        {
            this.keyOrMouse = E_KeyOrMouse.Mouse;
            this.inputType = inputType;
            this.mouseID = mouseID;
        }
    }
}