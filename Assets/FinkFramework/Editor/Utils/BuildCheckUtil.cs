using System.IO;
using FinkFramework.Runtime.Settings;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace FinkFramework.Editor.Utils
{
    public class BuildCheckUtil : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // 读取配置文件
            if (!GlobalSettings.Current.EnableEditorUrlCheck)
            {
                LogUtil.Info("EditorUrlCheck 已关闭，跳过 editor:// 扫描。");
                return;
            }

            ScanCsFiles();
        }

        private void ScanCsFiles()
        {
            string[] scriptGuids = AssetDatabase.FindAssets("t:Script");

            foreach (var guid in scriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (IsIgnored(path)) 
                    continue;

                string text = File.ReadAllText(path);

                if (text.Contains("editor://"))
                {
                    FailBuild(path);
                }
            }

            LogUtil.Info("✔ 构建扫描通过，未发现 editor:// 路径。");
        }

        private bool IsIgnored(string path)
        {
            // 排除框架自身
            if (path.StartsWith("Assets/FinkFramework"))
                return true;

            // 可扩展 ignore 列表
            return false;
        }
        
        private void FailBuild(string filePath)
        {
            string msg =
                "构建失败：检测到非法 editor:// 资源路径！\n\n" +
                $"违规文件：{filePath}\n" +
                "editor:// 仅用于编辑器环境，不能出现在构建包内。\n" +
                "请将该路径替换为：res:// 或 ab:// 或自定义 Provider。\n" +
                "如需临时跳过此检测，可在 Project Settings 中关闭：\n" +
                "【Project Settings → Fink Framework → 环境配置 → 启用 编辑器加载 打包检测】\n";

            throw new BuildFailedException(msg);
        }
    }
}