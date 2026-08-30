using System;
using System.IO;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Data
{
    public class DataCleanTool
    {
        /// <summary>
        /// 清空生成的数据文件和本地缓存。保留原有公开签名，兼容已有调用方。
        /// </summary>
        public static void ClearExportedData()
        {
            TryClearExportedData();
        }

        public static bool TryClearExportedData()
        {
            string persistentPath = Path.Combine(Application.persistentDataPath, "FinkFramework_Data");
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "FinkFramework_Data");
            string externalDataPath = Path.Combine(DataPipelinePath.ProjectRoot, "FinkFramework_Data/AutoExport");
            try
            {
                void SafeDelete(string path)
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                        Directory.CreateDirectory(path);
                        LogUtil.Success("DataCleanTool", $"已清空目录: {path}");
                    }
                    else
                    {
                        Directory.CreateDirectory(path);
                        LogUtil.Warn("DataCleanTool", $"未发现目录，已自动创建: {path}");
                    }
                }
                SafeDelete(externalDataPath);
                if (Directory.Exists(persistentPath))
                {
                    Directory.Delete(persistentPath, true);
                    Directory.CreateDirectory(persistentPath);
                    LogUtil.Success("DataCleanTool", $"已清空目录: {persistentPath}");
                }
#if UNITY_EDITOR
                // 仅在编辑器环境清空 StreamingAssets
                if (Directory.Exists(streamingPath))
                {
                    Directory.Delete(streamingPath, true);
                    Directory.CreateDirectory(streamingPath);
                    LogUtil.Success("DataCleanTool", $"已清空目录: {streamingPath}");
                }
#endif
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataCleanTool", $"清空数据失败: {ex.Message}");
                return false;
            }
        }
    }
}
