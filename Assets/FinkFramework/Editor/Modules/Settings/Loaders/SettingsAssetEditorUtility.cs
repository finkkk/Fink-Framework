using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Settings.Loaders
{
    /// <summary>模块配置共用的资产创建与引用绑定，仅供编辑器主线程使用。</summary>
    internal static class SettingsAssetEditorUtility
    {
        internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            if (IsOccupied(path))
                throw new InvalidOperationException($"配置路径已被占用或类型不是 {typeof(T).Name}，未覆盖：{path}");
            string folder = path.Substring(0, path.LastIndexOf('/'));
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (IsOccupied(next) || string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[i])))
                        throw new InvalidOperationException($"无法创建配置目录：{next}");
                }
                current = next;
            }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            if (!AssetDatabase.Contains(asset))
            {
                UnityEngine.Object.DestroyImmediate(asset);
                throw new InvalidOperationException($"无法创建配置：{path}");
            }
            return asset;
        }

        internal static T Bind<T>(ScriptableObject owner, T current, string path, Action<T> assign)
            where T : ScriptableObject
        {
            if (!owner) throw new ArgumentNullException(nameof(owner));
            if (current) return current;
            var asset = LoadOrCreate<T>(path);
            Undo.RecordObject(owner, "绑定模块配置");
            assign(asset);
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // 仅遗留 .meta 不代表资产仍存在，允许 Unity 复用该 GUID 补建缺失配置。
        private static bool IsOccupied(string path) => File.Exists(path) || Directory.Exists(path)
            || AssetDatabase.LoadMainAssetAtPath(path);
    }
}
