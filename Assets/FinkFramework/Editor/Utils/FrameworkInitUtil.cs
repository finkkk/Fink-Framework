using System.IO;
using FinkFramework.Editor.Windows;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Utils
{
    /// <summary>
    /// Fink Framework 编辑器初始化工具（Editor-only）
    /// 设计说明：使用 InitializeOnLoad 保证在 Editor 环境下自动执行；
    /// </summary>
    [InitializeOnLoad]
    internal static class FrameworkInitUtil
    {
        static FrameworkInitUtil()
        {
            CheckFrameworkRootPath();
            CreateDataFolders();
            TryShowWelcomeWindow();
        }
                
        #region 框架路径校验

        private static void CheckFrameworkRootPath()
        {
            const string correctPath = "Assets/FinkFramework";

            if (!Directory.Exists(correctPath))
            {
                EditorApplication.delayCall += () =>
                {
                    EditorUtility.DisplayDialog(
                        "Fink Framework 错误位置",
                        "检测到 FinkFramework 文件夹不在项目根目录下！\n\n" +
                        "正确路径必须为：\n" +
                        "Assets/FinkFramework/\n\n" +
                        "请将整个文件夹恢复到该路径，否则框架功能将无法正常工作。",
                        "我知道了"
                    );
                };
            }
        }

        #endregion

        #region 数据管线初始化
        
        private static void CreateDataFolders()
        {
            string root = Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                "FinkFramework_Data"
            );

            string dataTables = Path.Combine(root, "DataTables");
            string autoExport = Path.Combine(root, "AutoExport");

            bool created = false;

            if (!Directory.Exists(dataTables))
            {
                Directory.CreateDirectory(dataTables);
                created = true;
            }

            if (!Directory.Exists(autoExport))
            {
                Directory.CreateDirectory(autoExport);
                created = true;
            }

            if (created)
            {
                // 写 README
                string readme = Path.Combine(root, "README.txt");
                if (!File.Exists(readme))
                {
                    File.WriteAllText(readme,
                        @"FinkFramework_Data 目录说明：

DataTables/
- 放置 Excel 源数据（用户手写）
- 工具只读，不会删除

AutoExport/
- 工具自动生成的数据（JSON / Binary）
- 内容可能被清空或覆盖

请勿手动修改 AutoExport 内文件。
");
                }
                LogUtil.Info("FinkFramework","已初始化 FinkFramework_Data 目录结构。");
            }
        }

        #endregion

        #region 欢迎面板初始化

        private static void TryShowWelcomeWindow()
        {
            FrameworkWelcomeScheduler.Schedule();
        }

        #endregion

  
    }
}