using System;
using Framework.Config;
using Framework.Visualization;
using UnityEngine;
using UnityEngine.Events;
// ReSharper disable HeuristicUnreachableCode
#pragma warning disable CS0162 // 检测到不可到达的代码
namespace Framework.Utils
{
    /// <summary>
    /// 数学运算工具类：角度弧度、距离、是否在屏幕外等
    /// </summary>
    public static class MathUtil
    {
        #region 数值处理相关

        /// <summary>
        /// 将数值限制在[min, max]之间（针对Mathf.Clamp重载，支持返回多种类型 int/double）
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        /// <summary>
        /// 判断 value 是否位于 [min, max] 区间（包含边界）
        /// </summary>
        public static bool InRange(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        public static bool InRange(int value, int min, int max)
        {
            return value >= min && value <= max;
        }

        public static bool InRange(double value, double min, double max)
        {
            return value >= min && value <= max;
        }

        /// <summary>
        /// 计算百分比（返回 0~1 之间的值）
        /// </summary>
        public static float Percent01(float value, float min, float max)
        {
            if (Math.Abs(max - min) < 0.00001f) return 0f;
            return (value - min) / (max - min);
        }

        /// <summary>
        /// 将百分比 (0~1) 映射回实际区间值
        /// </summary>
        public static float PercentToValue(float percent01, float min, float max)
        {
            return min + (max - min) * Clamp(percent01, 0f, 1f);
        }

        /// <summary>
        /// 区间映射：将 value 从[fromMin, fromMax] 映射到[toMin, toMax]
        /// </summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            if (Math.Abs(fromMax - fromMin) < 0.00001f)
                return toMin;
            float t = (value - fromMin) / (fromMax - fromMin);
            return toMin + (toMax - toMin) * t;
        }

        /// <summary>
        /// 外插：不限制 t 的 Lerp（可超过 0~1 范围）  应用场景 如：做“过冲”动画、外推预测等
        /// /// </summary>
        public static float LerpUnclamped(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        #endregion
        
        #region 参与计算的平面坐标系枚举
        /// <summary>
        /// 表示计算距离时考虑的平面
        /// </summary>
        public enum PlaneType { XY, XZ, YZ, XYZ }
        #endregion
        
        #region 角度和弧度
        /// <summary>
        /// 角度转弧度的方法
        /// </summary>
        /// <param name="deg">角度值</param>
        /// <returns>弧度值</returns>
        public static float Deg2Rad(float deg)
        {
            return deg * Mathf.Deg2Rad;
        }

        /// <summary>
        /// 弧度转角度的方法
        /// </summary>
        /// <param name="rad">弧度值</param>
        /// <returns>角度值</returns>
        public static float Rad2Deg(float rad)
        {
            return rad * Mathf.Rad2Deg;
        }
        #endregion

        #region 距离计算相关
        /// <summary>
        /// 获取两点之间的距离
        /// </summary>
        /// <param name="pos1">点1</param>
        /// <param name="pos2">点2</param>
        /// <param name="plane">平面类型，默认XYZ（3D完整距离）</param>
        /// <returns>两点间的距离</returns>
        public static float GetObjDistance(Vector3 pos1, Vector3 pos2, PlaneType plane = PlaneType.XYZ)
        {
            return plane switch
            {
                PlaneType.XY => Vector2.Distance(new Vector2(pos1.x, pos1.y), new Vector2(pos2.x, pos2.y)),
                PlaneType.XZ => Vector2.Distance(new Vector2(pos1.x, pos1.z), new Vector2(pos2.x, pos2.z)),
                PlaneType.YZ => Vector2.Distance(new Vector2(pos1.y, pos1.z), new Vector2(pos2.y, pos2.z)),
                _ => Vector3.Distance(pos1, pos2) // 默认XYZ
            };
        }

        /// <summary>
        /// 判断两点之间的距离是否小于等于目标值（平方比较避免开方，性能更优）
        /// </summary>
        /// <param name="pos1">点1</param>
        /// <param name="pos2">点2</param>
        /// <param name="dis">距离阈值</param>
        /// <param name="plane">平面类型，默认XYZ（3D完整距离）</param>
        /// <returns>是否在指定范围内</returns>
        public static bool CheckObjDistance(Vector3 pos1, Vector3 pos2, float dis, PlaneType plane = PlaneType.XYZ)
        {
            float dx, dy;
            switch (plane)
            {
                case PlaneType.XY:
                    dx = pos1.x - pos2.x;
                    dy = pos1.y - pos2.y;
                    return dx * dx + dy * dy <= dis * dis;

                case PlaneType.XZ:
                    dx = pos1.x - pos2.x;
                    dy = pos1.z - pos2.z;
                    return dx * dx + dy * dy <= dis * dis;

                case PlaneType.YZ:
                    dx = pos1.y - pos2.y;
                    dy = pos1.z - pos2.z;
                    return dx * dx + dy * dy <= dis * dis;

                case PlaneType.XYZ:
                default: // 默认走完整3D比较
                    return Vector3.SqrMagnitude(pos1 - pos2) <= dis * dis;
            }
        }
        #endregion

        #region 位置判断相关
        /// <summary>
        /// 判断世界坐标系下的某一个点 是否在屏幕可见范围外
        /// </summary>
        /// <param name="pos">世界坐标系下的一个点的位置</param>
        /// <returns>如果在可见范围外返回true，否则返回false</returns>
        public static bool IsWorldPosOutScreen(Vector3 pos)
        {
            //将世界坐标转为屏幕坐标
            Vector3 screenPos = Camera.main.WorldToScreenPoint(pos);
            if (screenPos.z <= 0) return true; // 在相机背面
            //判断是否在屏幕范围内
            return !(screenPos.x >= 0) || !(screenPos.x <= Screen.width) ||
                   !(screenPos.y >= 0) || !(screenPos.y <= Screen.height);
        }

        /// <summary>
        /// 判断目标是否在指定的扇形范围内 默认XYZ（3D圆锥）
        /// </summary>
        /// <param name="pos">扇形中心点</param>
        /// <param name="forward">面朝方向</param>
        /// <param name="targetPos">目标点</param>
        /// <param name="radius">扇形半径</param>
        /// <param name="angle">扇形角度</param>
        /// <param name="plane">检测的平面类型，默认XYZ（3D圆锥）</param>
        /// <param name="gizmosToggle">是否需要自动执行可视化 默认为true(可视化需要保证全局配置中的调试模式开启)</param>
        /// <returns></returns>
        public static bool IsInSectorRange(Vector3 pos, Vector3 forward, Vector3 targetPos, float radius, float angle, PlaneType plane = PlaneType.XYZ,bool gizmosToggle = true)
        {
            bool result;
            bool inRadius = true;
            switch (plane)
            {
                case PlaneType.XY:
                {
                    Vector2 dir = new Vector2(targetPos.x - pos.x, targetPos.y - pos.y);
                    if (dir.sqrMagnitude > radius * radius) inRadius = false;

                    Vector2 fwd = new Vector2(forward.x, forward.y).normalized;
                    dir.Normalize();
                    float dot = Vector2.Dot(fwd, dir);
                    result = inRadius && dot >= Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
                    break;
                }

                case PlaneType.XZ:
                {
                    Vector2 dir = new Vector2(targetPos.x - pos.x, targetPos.z - pos.z);
                    if (dir.sqrMagnitude > radius * radius) inRadius = false;

                    Vector2 fwd = new Vector2(forward.x, forward.z).normalized;
                    dir.Normalize();
                    float dot = Vector2.Dot(fwd, dir);
                    result = inRadius && dot >= Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
                    break;
                }

                case PlaneType.YZ:
                {
                    Vector2 dir = new Vector2(targetPos.y - pos.y, targetPos.z - pos.z);
                    if (dir.sqrMagnitude > radius * radius) inRadius = false;

                    Vector2 fwd = new Vector2(forward.y, forward.z).normalized;
                    dir.Normalize();
                    float dot = Vector2.Dot(fwd, dir);
                    result = inRadius && dot >= Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
                    break;
                }

                default: // XYZ 三维圆锥判定
                {
                    Vector3 dir = targetPos - pos;
                    if (dir.sqrMagnitude > radius * radius) inRadius = false;

                    dir.Normalize();
                    Vector3 fwd = forward.normalized;
                    float dot = Vector3.Dot(fwd, dir);
                    result = inRadius && dot >= Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
                    break;
                }
            }
            // --- 可视化调试功能 ---
            if (gizmosToggle && GlobalConfig.DebugMode)
            {
                GizmosDrawer.DrawSector(pos,forward,radius,angle,plane,result);
            }
            return result;
        }
        #endregion

        #region 射线检测相关
        /// <summary>
        /// 射线检测 获取一个对象 指定距离 指定层级的
        /// </summary>
        /// <param name="ray">射线</param>
        /// <param name="callBack">回调函数（会把碰到的RayCastHit信息传递出去）</param>
        /// <param name="maxDistance">最大距离</param>
        /// <param name="layerMask">层级筛选（默认为所有层级）</param>
        /// <param name="gizmosToggle">是否需要自动执行可视化 默认为true(可视化需要保证全局配置中的调试模式开启)</param>
        public static void RayCast<T>(Ray ray, UnityAction<T> callBack, float maxDistance, int layerMask = ~0, bool gizmosToggle = true) where T : class
        {
            bool hit = Physics.Raycast(ray, out var hitInfo, maxDistance, layerMask);
            if (gizmosToggle && GlobalConfig.DebugMode)
                GizmosDrawer.DrawRay(ray.origin, ray.direction, maxDistance, hit);
            if (hit)
            {
                if (typeof(T) == typeof(RaycastHit))
                    callBack(hitInfo as T);  // 直接把 hitInfo 传出去
                else if (typeof(T) == typeof(GameObject))
                    callBack(hitInfo.collider.gameObject as T);
                else if (typeof(T) == typeof(Collider))
                    callBack(hitInfo.collider as T);
                else
                    callBack(hitInfo.collider.gameObject.GetComponent<T>());
            }
            else if (gizmosToggle && GlobalConfig.DebugMode)
            {
                GizmosDrawer.DrawRay(ray.origin, ray.direction, maxDistance, false);
            }
        }

        /// <summary>
        /// 射线检测 获取到多个对象 指定距离 指定层级
        /// </summary>
        /// <param name="ray">射线</param>
        /// <param name="callBack">回调函数（会把碰到的RayCastHit信息传递出去） 每一个对象都会调用一次</param>
        /// <param name="maxDistance">最大距离</param>
        /// <param name="layerMask">层级筛选（默认为所有层级）</param>
        /// <param name="gizmosToggle">是否需要自动执行可视化 默认为true(可视化需要保证全局配置中的调试模式开启)</param>
        public static void RayCastAll<T>(Ray ray, UnityAction<T> callBack, float maxDistance, int layerMask = ~0, bool gizmosToggle = true) where T : class
        {
            RaycastHit[] hitInfos = Physics.RaycastAll(ray, maxDistance, layerMask);
        
            if (gizmosToggle && GlobalConfig.DebugMode)
                GizmosDrawer.DrawRay(ray.origin, ray.direction, maxDistance, hitInfos.Length > 0);

            foreach (var hitInfo in hitInfos)
            {
                if (typeof(T) == typeof(RaycastHit))
                    callBack(hitInfo as T);
                else if (typeof(T) == typeof(GameObject))
                    callBack(hitInfo.collider.gameObject as T);
                else if (typeof(T) == typeof(Collider))
                    callBack(hitInfo.collider as T);
                else
                    callBack(hitInfo.collider.gameObject.GetComponent<T>());
            }
        }
        #endregion

        #region 范围检测相关

        /// <summary>
        /// 进行盒装范围检测
        /// </summary>
        /// <typeparam name="T">想要获取的信息类型 可以填写 Collider GameObject 以及对象上依附的组件类型</typeparam>
        /// <param name="center">盒装中心点</param>
        /// <param name="rotation">盒子的角度</param>
        /// <param name="halfExtents">长宽高的一半</param>
        /// <param name="layerMask">层级筛选(默认全部层级)</param>
        /// <param name="callBack">回调函数 </param>
        /// <param name="gizmosToggle">是否需要自动执行可视化 默认为true(可视化需要保证全局配置中的调试模式开启)</param>
        public static void OverlapBox<T>(Vector3 center, Quaternion rotation, Vector3 halfExtents, UnityAction<T> callBack, int layerMask = ~0, bool gizmosToggle = true) where T : class
        {
            Type type = typeof(T);
            Collider[] colliders = Physics.OverlapBox(center, halfExtents, rotation, layerMask, QueryTriggerInteraction.Collide);
            if (gizmosToggle && GlobalConfig.DebugMode)
                GizmosDrawer.DrawBox(center, rotation, halfExtents, colliders.Length > 0);
            foreach (var t in colliders)
            {
                if (type == typeof(Collider))
                    callBack.Invoke(t as T);
                else if (type == typeof(GameObject))
                    callBack.Invoke(t.gameObject as T);
                else
                    callBack.Invoke(t.gameObject.GetComponent<T>());
            }
        }

        /// <summary>
        /// 进行球体范围检测
        /// </summary>
        /// <typeparam name="T">想要获取的信息类型 可以填写 Collider GameObject 以及对象上依附的组件类型</typeparam>
        /// <param name="center">球体的中心点</param>
        /// <param name="radius">球体的半径</param>
        /// <param name="layerMask">层级筛选(默认全部层级)</param>
        /// <param name="callBack">回调函数</param>
        /// <param name="gizmosToggle">是否需要自动执行可视化 默认为true(可视化需要保证全局配置中的调试模式开启)</param>
        public static void OverlapSphere<T>(Vector3 center, float radius, UnityAction<T> callBack, int layerMask = ~0, bool gizmosToggle = true) where T:class
        {
            Type type = typeof(T);
            Collider[] colliders = Physics.OverlapSphere(center, radius, layerMask, QueryTriggerInteraction.Collide);
            if (gizmosToggle && GlobalConfig.DebugMode)
                GizmosDrawer.DrawSphere(center, radius, colliders.Length > 0);
            foreach (var t in colliders)
            {
                if (type == typeof(Collider))
                    callBack.Invoke(t as T);
                else if (type == typeof(GameObject))
                    callBack.Invoke(t.gameObject as T);
                else
                    callBack.Invoke(t.gameObject.GetComponent<T>());
            }
        }
        #endregion
    }
}