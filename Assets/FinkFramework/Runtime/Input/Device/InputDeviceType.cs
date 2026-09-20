namespace FinkFramework.Runtime.Input
{
    /// <summary>
    /// 当前主要输入设备类别。
    /// 该状态表示最近一次有效操作来自哪里，而不是设备是否已连接。
    /// </summary>
    public enum InputDeviceType
    {
        Unknown,
        KeyboardMouse,
        Gamepad,
        Touch
    }
}
