#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace FinkFramework.Editor.Utils
{
    [InitializeOnLoad]
    public static class EditorPathUtil
    {
        static EditorPathUtil()
        {
            const string correctPath = "Assets/FinkFramework";

            // 判断 FinkFramework 根目录是否存在
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
    }
}
#endif