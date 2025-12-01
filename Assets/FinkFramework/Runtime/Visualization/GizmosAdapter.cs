using System;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Visualization
{
    /// <summary>
    /// Gizmos 适配器（Adapter）
    /// 设计目的：
    /// Runtime 程序集中禁止引用 UnityEditor，因此不能直接使用 GizmosDrawer（位于 Editor 程序集）
    /// 为实现运行时的“调试可视化”，需要一种安全的桥接机制
    /// GizmosAdapter 仅暴露委托（Action），由 EditorBridge 在编辑器模式下绑定真正的 GizmosDrawer 方法
    /// 使用方式（Runtime）：
    /// Runtime 代码（例如 MathUtil、Raycast 工具类等）会这样调用：
    /// GizmosAdapter.DrawRayAction?.Invoke(...);
    /// 运行时（玩家端）：
    /// UNITY_EDITOR 不存在 → 这些委托不会被定义
    /// 所有可视化功能自动失效（不会报错、不会产生 GC、不会影响性能）
    /// 编辑器下：
    /// EditorBridge 自动在 InitializeOnLoad 时为这些 Action 绑定真实绘制方法
    /// 运行时工具类调用 → 实际执行 GizmosDrawer.* 可视化绘制
    /// 两者结合：
    /// Runtime 完全不会引用 UnityEditor
    /// Editor 下自动启用可视化调试
    /// Build 后无缝剔除所有调试绘制
    /// </summary>
    public static class GizmosAdapter
    {
#if UNITY_EDITOR
        /// <summary>
        /// 绘制一条射线（Ray）
        /// 参数：起点、方向、长度、是否命中
        /// </summary>
        public static Action<Vector3, Vector3, float, bool> DrawRayAction;
        /// <summary>
        /// 绘制一个扇形区域
        /// 参数：位置、方向、半径、角度、平面类型、是否命中
        /// 注意：GizmosDrawer 实现中有额外的 segments（细分程度）参数，
        ///       将由 EditorBridge 自动补 默认值
        /// </summary>
        public static Action<Vector3, Vector3, float, float, MathUtil.PlaneType, bool> DrawSectorAction;
        /// <summary>
        /// 绘制一个 Box（3D 盒）
        /// 参数：中心点、旋转角度、半宽值、是否命中
        /// </summary>
        public static Action<Vector3, Quaternion, Vector3, bool> DrawBoxAction;
        /// <summary>
        /// 绘制一个球体（Sphere）
        /// 参数：中心点、半径、是否命中
        /// 注意：实际 GizmosDrawer.DrawSphere 有 segments 参数，
        ///       将由 EditorBridge 自动补 默认值
        /// </summary>
        public static Action<Vector3, float, bool> DrawSphereAction;
#endif
    }
}