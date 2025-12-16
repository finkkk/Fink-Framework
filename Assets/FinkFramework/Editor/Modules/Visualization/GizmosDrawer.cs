using System;
using System.Collections.Generic;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Mono;
using FinkFramework.Runtime.Utils;
using UnityEngine;

// ReSharper disable HeuristicUnreachableCode
#pragma warning disable CS0162 // 检测到不可到达的代码

namespace FinkFramework.Editor.Modules.Visualization
{
    /// <summary>
    /// 通用调试绘制工具（常驻 Gizmos 绘制）
    /// </summary>
    public class GizmosDrawer
    {
        #region 调试绘制器的全局设置相关
        /// <summary>
        /// 总开关（控制整个绘制器）
        /// </summary>
        public static bool EnableDrawer = true; 
        
        // 绘制任务的委托列表
        private static readonly List<Action> gizmoDrawers = new();
        
        // 绘制时机
        public enum GizmoDrawMode
        {
            Always,     // OnDrawGizmos
            Selected    // OnDrawGizmosSelected
        }

        public const GizmoDrawMode DrawMode = GizmoDrawMode.Always;

        #endregion
        
        static GizmosDrawer()
        {
            // 开关关闭 → 不进入队列
            if (!EnvironmentState.DebugMode) return;
            // 初始化 默认开启所有形状绘制器
            ResetFeatures();
            // 永远注册到 MonoManager 的两个事件
            MonoManager.Instance.AddGizmosListener(OnDrawGizmosInternal);
            MonoManager.Instance.AddGizmosSelectedListener(OnDrawGizmosSelectedInternal);
        }
        
        private static void OnDrawGizmosInternal()
        {
            if (DrawMode == GizmoDrawMode.Always)
                DoDraw();
        }

        private static void OnDrawGizmosSelectedInternal()
        {
            if (DrawMode == GizmoDrawMode.Selected)
                DoDraw();
        }
        
        public static void RequestDraw(Action drawer)
        {
            // 开关关闭 → 不进入队列
            if (!EnvironmentState.DebugMode || !EnableDrawer) return;
            gizmoDrawers.Add(drawer);
        }
        
        private static void DoDraw()
        {
            // 顶层检查：若未开启调试 → 不遍历、不清空，完全无消耗
            if (!EnvironmentState.DebugMode || !EnableDrawer) return;
            // 顶层检查：若无申请绘制需求 → 不遍历、不清空，完全无消耗
            if (gizmoDrawers.Count == 0) return;
            // 每帧遍历需要执行的绘制指令 全部执行一次后 清空指令
            foreach (var drawer in gizmoDrawers)
                drawer?.Invoke();
            gizmoDrawers.Clear(); // 每帧清空，只保留“瞬时指令”
        }
        
        #region 管理常用形状绘制开关
        /// <summary>
        /// 常用形状
        /// </summary>
        public enum GizmoFeature
        {
            Sector, // 扇形
            Ray,    // 射线
            Box,    // 盒形
            Sphere  // 球形
        }
        // 局部开关 （分别管理各个形状绘制开关）
        private static readonly bool[] featureToggles = new bool[Enum.GetValues(typeof(GizmoFeature)).Length];
        
        public static void ResetFeatures(bool enabled = true)
        {
            for (int i = 0; i < featureToggles.Length; i++)
                featureToggles[i] = enabled;
        }
        
        public static void SetFeature(GizmoFeature feature, bool enabled)
        {
            featureToggles[(int)feature] = enabled;
        }

        public static bool IsFeatureEnabled(GizmoFeature feature)
        {
            return featureToggles[(int)feature];
        }
        #endregion

        #region 绘制常用可视化形状的方法
        /// <summary>
        /// 绘制扇形/圆锥可视化区域
        /// </summary>
        /// <param name="pos">扇形圆锥圆心(主体对象位置)</param>
        /// <param name="forward">主体对象正前方方向</param>
        /// <param name="radius">半径</param>
        /// <param name="angle">角度</param>
        /// <param name="plane">坐标系平面枚举</param>
        /// <param name="result">是否在扇形区域内(影响可视化颜色)</param>
        /// <param name="segments">画弧的分段数</param>
        public static void DrawSector(Vector3 pos, Vector3 forward, float radius, float angle, MathUtil.PlaneType plane, bool result, int segments = 20)
        {
            if (!IsFeatureEnabled(GizmoFeature.Sector)) return; // 功能点关闭 → 直接 return
            RequestDraw(() =>
            {
                Gizmos.color = result ? Color.yellow : Color.blue;
                float halfAngle = angle * 0.5f;
                switch (plane)
                {
                   case MathUtil.PlaneType.XY:
                   {
                        Vector3 startDir = Quaternion.Euler(0, 0, -halfAngle) * forward.normalized;
                        Vector3 endDir   = Quaternion.Euler(0, 0, halfAngle) * forward.normalized;
                        Gizmos.DrawLine(pos, pos + startDir * radius);
                        Gizmos.DrawLine(pos, pos + endDir * radius);

                        // 圆弧
                        Vector3 lastPoint = pos + startDir * radius;
                        for (int i = 1; i <= segments; i++)
                        {
                            float lerpAngle = -halfAngle + (angle / segments) * i;
                            Vector3 dir = Quaternion.Euler(0, 0, lerpAngle) * forward.normalized;
                            Vector3 nextPoint = pos + dir * radius;
                            Gizmos.DrawLine(lastPoint, nextPoint);
                            lastPoint = nextPoint;
                        }
                        break;
                   }

                   case MathUtil.PlaneType.XZ:
                   {
                        Vector3 startDir = Quaternion.Euler(0, -halfAngle, 0) * forward.normalized;
                        Vector3 endDir   = Quaternion.Euler(0, halfAngle, 0) * forward.normalized;

                        Gizmos.DrawLine(pos, pos + startDir * radius);
                        Gizmos.DrawLine(pos, pos + endDir * radius);

                        // 圆弧
                        Vector3 lastPoint = pos + startDir * radius;
                        for (int i = 1; i <= segments; i++)
                        {
                            float lerpAngle = -halfAngle + (angle / segments) * i;
                            Vector3 dir = Quaternion.Euler(0, lerpAngle, 0) * forward.normalized;
                            Vector3 nextPoint = pos + dir * radius;
                            Gizmos.DrawLine(lastPoint, nextPoint);
                            lastPoint = nextPoint;
                        }
                        break;
                   }

                   case MathUtil.PlaneType.YZ:
                   {
                        Vector3 startDir = Quaternion.Euler(-halfAngle, 0, 0) * forward.normalized;
                        Vector3 endDir   = Quaternion.Euler(halfAngle, 0, 0) * forward.normalized;

                        Gizmos.DrawLine(pos, pos + startDir * radius);
                        Gizmos.DrawLine(pos, pos + endDir * radius);

                        // 圆弧
                        Vector3 lastPoint = pos + startDir * radius;
                        for (int i = 1; i <= segments; i++)
                        {
                            float lerpAngle = -halfAngle + (angle / segments) * i;
                            Vector3 dir = Quaternion.Euler(lerpAngle, 0, 0) * forward.normalized;
                            Vector3 nextPoint = pos + dir * radius;
                            Gizmos.DrawLine(lastPoint, nextPoint);
                            lastPoint = nextPoint;
                        }
                        break;
                   }

                   case MathUtil.PlaneType.XYZ:
                   {
                        // 简单画锥体边界（上下左右四条线）
                        Gizmos.DrawLine(pos, pos + (Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward.normalized) * radius);
                        Gizmos.DrawLine(pos, pos + (Quaternion.AngleAxis(halfAngle, Vector3.up) * forward.normalized) * radius);
                        Gizmos.DrawLine(pos, pos + (Quaternion.AngleAxis(-halfAngle, Vector3.right) * forward.normalized) * radius);
                        Gizmos.DrawLine(pos, pos + (Quaternion.AngleAxis(halfAngle, Vector3.right) * forward.normalized) * radius);
                        break;
                   }
                }
            });
        }

        /// <summary>
        /// 绘制射线检测可视化线段
        /// </summary>
        /// <param name="start">起始点</param>
        /// <param name="dir">方向</param>
        /// <param name="length">长度</param>
        /// <param name="hit">是否打中(用于区分未打中状态分别设置不同的颜色)</param>
        public static void DrawRay(Vector3 start, Vector3 dir, float length = 5f,bool hit = true)
        {
            if (!IsFeatureEnabled(GizmoFeature.Ray)) return;
            RequestDraw(() =>
            {
                Gizmos.color = hit ? Color.green : Color.red;
                Gizmos.DrawLine(start, start + dir.normalized * length);
            });
        }
        /// <summary>
        /// 绘制范围检测盒体可视化区域
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="rotation">旋转</param>
        /// <param name="halfExtents">长宽高的一半</param>
        /// <param name="hit">是否检测到目标(用于区分不同颜色)</param>
        public static void DrawBox(Vector3 center, Quaternion rotation, Vector3 halfExtents, bool hit = true)
        {
            if (!IsFeatureEnabled(GizmoFeature.Box)) return;

            RequestDraw(() =>
            {
                // 保存旧矩阵
                Matrix4x4 oldMatrix = Gizmos.matrix;

                // 应用旋转（以及位置）
                Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);

                // 设置颜色
                Gizmos.color = hit ? Color.yellow : Color.gray;

                // 注意这里 center 要写 local (0,0,0)，因为已经套了 matrix
                Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2);

                // 恢复矩阵
                Gizmos.matrix = oldMatrix;
            });
        }

        /// <summary>
        /// 绘制范围检测球体可视化区域
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="radius">半径</param>
        /// <param name="hit">是否检测到目标(用于区分不同颜色)</param>
        /// <param name="segments">圆弧分段数（越大越圆滑）</param>
        public static void DrawSphere(Vector3 center, float radius, bool hit = true, int segments = 20)
        {
            if (!IsFeatureEnabled(GizmoFeature.Sphere)) return;

            RequestDraw(() =>
            {
                Gizmos.color = hit ? Color.magenta : Color.gray;

                // XY 平面圆
                DrawCircle(center, Vector3.forward, Vector3.right, radius, segments);
                // XZ 平面圆
                DrawCircle(center, Vector3.forward, Vector3.up, radius, segments);
                // YZ 平面圆
                DrawCircle(center, Vector3.up, Vector3.right, radius, segments);
            });
        }

        /// <summary>
        /// 绘制圆形辅助方法
        /// </summary>
        private static void DrawCircle(Vector3 center, Vector3 axis1, Vector3 axis2, float radius, int segments)
        {
            Vector3 lastPoint = center + (axis1 * radius);
            float angleStep = 360f / segments;

            for (int i = 1; i <= segments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * angleStep);
                Vector3 nextPoint = center + (axis1 * Mathf.Cos(rad) + axis2 * Mathf.Sin(rad)) * radius;
                Gizmos.DrawLine(lastPoint, nextPoint);
                lastPoint = nextPoint;
            }
        }
        #endregion
    }
}