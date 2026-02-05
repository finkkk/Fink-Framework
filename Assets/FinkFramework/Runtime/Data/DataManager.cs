using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 数据管理器
    /// 负责运行时数据初始化（路径注册等）
    /// </summary>
    public class DataManager : Singleton<DataManager>
    {
        private static bool _initialized;
        
        /// <summary>
        /// 初始化数据系统
        /// 仅当打包为移动端时才执行路径注册
        /// </summary>
        public static void Init()
        {
            if (_initialized)
                return;
#if UNITY_ANDROID || UNITY_IOS
            TryRegisterDataPaths();
#endif
            _initialized = true;
        }

#if UNITY_ANDROID || UNITY_IOS
        private static void TryRegisterDataPaths()
        {
            var registry = FilesUtil.LoadDefaultData<DataPathRegistry>();
            if (registry == null)
                return; // 没导出过数据，什么都不做

            foreach (var kv in registry)
            {
                RegisterOne(kv.Key, kv.Value);
            }
        }

        private static void RegisterOne(string containerTypeName, string relativePathNoExt)
        {
            // 通过名字找到 Container 类型
            Type containerType = DataUtil.FindType(containerTypeName);
            if (containerType == null)
            {
                LogUtil.Warn(
                    "DataManager",
                    $"未找到数据容器类型：{containerTypeName}"
                );
                return;
            }

            // 使用 Type 版注册
            FilesUtil.RegisterPath(containerType, relativePathNoExt);
        }
#endif
    }
}