using UnityEngine;

namespace FinkFramework.Runtime.ResLoad.Base
{
    /// <summary>
    /// 资源信息的抽象基类，用于记录资源加载时里氏替换原则 记录时是记录父类 实际调用时转换为子类
    /// </summary>
    public abstract class BaseResInfo
    {
        // 引用计数 
        public int refCount;
        
        // 标记是否需要卸载
        public bool isDel;      
        
        // 统一获取资产
        public abstract Object GetAsset();      
        
        // 统一设置资产
        public abstract void SetAsset(Object obj); 
                          
    }
}