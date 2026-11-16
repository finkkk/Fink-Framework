using UnityEngine;
using FinkFramework.Runtime.Input;
namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>输入系统的设备检测与绑定冲突配置。</summary>
    [CreateAssetMenu(fileName = "InputSettingsAsset", menuName = "Fink Framework/Settings/Input Settings")]
    public sealed class InputSettingsAsset : ScriptableObject
    {
        #region ===== 输入配置 =====

        [Header("启用设备输入检测")]
        [Tooltip("关闭后，框架不再判断当前主要输入设备，所有订阅者都会回到 Unknown 状态。")]
        public bool EnableDeviceDetection = true;

        [Header("鼠标移动视为键鼠输入")]
        [Tooltip("关闭后，鼠标按键仍会被识别为键鼠输入，但单纯移动鼠标不会改变当前主要输入设备。")]
        public bool TreatMouseMovementAsKeyboardMouseInput = true;

        [Header("鼠标移动检测阈值（像素）")]
        [Tooltip("单帧鼠标移动距离达到此值时，才视为键鼠输入。较大值可减少轻微抖动造成的设备切换。")]
        [Min(0f)]
        public float MouseMovementDetectionThreshold = DefaultMouseMovementDetectionThreshold;

        public const float DefaultMouseMovementDetectionThreshold = 2f;

        [Header("绑定冲突处理模式")]
        [Tooltip("Warning 允许冲突绑定并由设置界面标红提示；Strict 拒绝冲突绑定。")]
        public InputConflictMode InputConflictMode = InputConflictMode.Warning;

        #endregion
    }
}
