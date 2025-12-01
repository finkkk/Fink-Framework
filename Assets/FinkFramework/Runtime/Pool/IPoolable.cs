namespace FinkFramework.Runtime.Pool
{
    /// <summary>
    /// 想要池化的数据结构类、逻辑类都必须继承该接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 重置数据的方法
        /// </summary>
        void ResetInfo();
    }
}