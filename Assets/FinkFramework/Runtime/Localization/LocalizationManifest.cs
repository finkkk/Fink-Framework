using System;
using System.Collections.Generic;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 本地化运行时清单。编辑器生成，运行时用于在 Android 等无法扫描目录的平台定位分类。
    /// </summary>
    [Serializable]
    public sealed class LocalizationManifest
    {
        /// <summary>清单格式版本。</summary>
        public int version = 1;
        /// <summary>每个“分类 + 语言”文件的定位信息。</summary>
        public List<LocalizationManifestFile> files = new();
        /// <summary>完整 Key 到主分类的索引。</summary>
        public List<LocalizationManifestKey> keys = new();

        [NonSerialized] private Dictionary<string, string> keyCategories;

        /// <summary>
        /// 根据完整 Key 查找其主分类。
        /// </summary>
        public bool TryGetCategory(string key, out string category)
        {
            category = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            BuildKeyIndex();
            return keyCategories.TryGetValue(key.Trim(), out category);
        }

        /// <summary>
        /// 延迟构建完整 Key 到分类的索引，并兼容旧版仅保存文件 Key 列表的清单。
        /// </summary>
        private void BuildKeyIndex()
        {
            if (keyCategories != null)
                return;

            // Manifest 只负责定位分类，Key 比较与运行时入口保持大小写不敏感，并忽略外围空格。
            keyCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (keys != null)
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    LocalizationManifestKey entry = keys[i];
                    if (entry == null
                        || string.IsNullOrWhiteSpace(entry.key)
                        || string.IsNullOrWhiteSpace(entry.category))
                        continue;

                    keyCategories.TryAdd(entry.key.Trim(), entry.category.Trim());
                }
            }

            // 兼容未生成 key 索引的旧清单，读取文件项中的 Key 列表。
            if (files == null)
                return;

            for (int i = 0; i < files.Count; i++)
            {
                LocalizationManifestFile file = files[i];
                if (file?.keys == null || string.IsNullOrWhiteSpace(file.category))
                    continue;

                for (int j = 0; j < file.keys.Count; j++)
                {
                    string key = file.keys[j];
                    if (!string.IsNullOrWhiteSpace(key))
                        keyCategories.TryAdd(key.Trim(), file.category.Trim());
                }
            }
        }
    }

    [Serializable]
    public sealed class LocalizationManifestFile
    {
        /// <summary>主分类名称。</summary>
        public string category;
        /// <summary>标准语言标识。</summary>
        public string locale;
        /// <summary>相对于运行时数据根目录的 JSON 路径。</summary>
        public string relativePath;
        /// <summary>该文件包含的分类内 Key 列表。</summary>
        public List<string> keys = new();
    }

    [Serializable]
    public sealed class LocalizationManifestKey
    {
        /// <summary>完整本地化 Key。</summary>
        public string key;
        /// <summary>该 Key 所属的主分类。</summary>
        public string category;
    }
}
