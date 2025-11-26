using UnityEngine;

namespace Framework.ObjectPool
{
    /// <summary>
    /// 该脚本需要挂载到 需要使用对象池管理的预制体对象上
    /// </summary>
    public class PoolGameObjectConfig: MonoBehaviour
    {
        public int maxNum = 100;
    }
}
