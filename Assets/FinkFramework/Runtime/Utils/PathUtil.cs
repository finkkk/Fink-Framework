using System.IO;

namespace FinkFramework.Runtime.Utils
{
    public static class PathUtil
    {
        /// <summary>
        /// 标准化路径字符串：
        /// 1. 统一分隔符为 '/';
        /// 2. 去除多余空格;
        /// 3. 保留绝对路径前缀（不误删）
        /// </summary>
        public static string NormalizePath(string path)
        {
            path = TextsUtil.NormalizePunctuation(path);
            if (string.IsNullOrEmpty(path)) return string.Empty;

            // 统一分隔符
            path = path.Trim().Replace('\\', '/');

            // 只有当不是完整带前缀的路径且以“/”开头的时候，才去掉前导斜杠
            if (path.StartsWith("/") && !path.Contains("://"))
                path = path[1..];
            return path;
        }
        
        /// <summary>
        /// 确保路径文件夹存在
        /// </summary>
        /// <param name="path">路径</param>
        public static void EnsureDirectory(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

    }
}