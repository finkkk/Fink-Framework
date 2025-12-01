#if UNITY_EDITOR
using FinkFramework.Runtime.Visualization;
using UnityEditor;

namespace FinkFramework.Editor.Visualization
{
    /// <summary>
    /// Editor GizmosDrawer 桥接器（Bridge）
    /// 作用：
    /// 将 Runtime 中的 GizmosAdapter 委托绑定到 Editor 下真实的 GizmosDrawer 方法
    /// 完成 Runtime → Editor 的“安全可视化功能转接”
    /// 为什么需要它？
    /// Runtime 程序集无法直接引用 UnityEditor（会导致构建报错）
    /// Runtime 只能调用 Adapter 提供的委托（Action）
    /// EditorBridge 在 Editor 环境中自动注册这些委托
    /// 工作流程：
    /// MathUtil / Raycast / 任何 Runtime 调试逻辑
    ///  → 调用 GizmosAdapter.(xxx)Action
    ///  → Editor 中的 GizmosDrawerEditorBridge 接管
    ///  → 最终调用真正的 GizmosDrawer 绘制可视化图形
    /// 注意：
    /// Editor 才会执行本类（InitializeOnLoad）
    /// 运行时（玩家端）不会包含这些注册逻辑（完全无性能影响）
    /// </summary>
    [InitializeOnLoad]
    public static class GizmosDrawerEditorBridge
    {
        /// <summary>
        /// 静态构造函数 —— Editor 加载时自动执行
        /// 在这里完成所有 GizmosAdapter → GizmosDrawer 的绑定。
        /// </summary>
        static GizmosDrawerEditorBridge()
        {
            // Runtime 调用 DrawRay → Editor 实际执行 GizmosDrawer.DrawRay
            GizmosAdapter.DrawRayAction = GizmosDrawer.DrawRay;
            // Runtime 调用 DrawBox → Editor 实际执行 GizmosDrawer.DrawBox
            GizmosAdapter.DrawBoxAction = GizmosDrawer.DrawBox;
            // DrawSector 的实际方法包含 segments 参数（可选） 为避免委托签名强制要求 segments，这里使用 wrapper 自动补 20
            GizmosAdapter.DrawSectorAction = (pos,forward, radius,angle, plane,result) =>
            {
                GizmosDrawer.DrawSector(pos,forward, radius,angle, plane,result,20); // 使用默认值
            };
            // DrawSphere 的实际方法包含 segments 参数（可选） 为避免委托签名强制要求 segments，这里使用 wrapper 自动补 20
            GizmosAdapter.DrawSphereAction = (center, radius, hit) =>
            {
                GizmosDrawer.DrawSphere(center, radius, hit, 20); // 使用默认值
            };
        }
    }
}
#endif