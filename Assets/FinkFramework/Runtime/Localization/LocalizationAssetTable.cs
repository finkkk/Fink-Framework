using System;
using System.Collections.Generic;
using UnityEngine;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 直接引用 Unity 资源的本地化资源表。
    /// 文本仍然保存在语言 JSON 中，Sprite、AudioClip、Font 等资源保存在此资产中。
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizationAssetTable",
        menuName = "Fink Framework/Localization/Localization Asset Table")]
    public sealed class LocalizationAssetTable : ScriptableObject
    {
        [SerializeField] private List<LocalizationAssetEntry> entries =
            new List<LocalizationAssetEntry>();

        /// <summary>
        /// 资源本地化条目。
        /// </summary>
        public IReadOnlyList<LocalizationAssetEntry> Entries => entries;

        /// <summary>
        /// 根据 Key 查找资源条目。
        /// </summary>
        public LocalizationAssetEntry FindEntry(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || entries == null)
                return null;

            string normalizedKey = key.Trim();
            foreach (var entry in entries)
            {
                if (entry != null
                    && string.Equals(entry.Key?.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// 获取指定 Key 和语言的资源引用，不会修改资产内容。
        /// </summary>
        public bool TryGetAsset(
            string key,
            string localeCode,
            out UnityEngine.Object asset)
        {
            asset = null;
            LocalizationAssetEntry entry = FindEntry(key);
            if (entry == null || entry.LocaleValues == null)
                return false;

            string normalizedLocale = LocalizationAssetTable.NormalizeLocaleCode(localeCode);
            foreach (var value in entry.LocaleValues)
            {
                if (value != null
                    && string.Equals(
                        NormalizeLocaleCode(value.LocaleCode),
                        normalizedLocale,
                        StringComparison.OrdinalIgnoreCase))
                {
                    asset = value.Asset;
                    return asset != null;
                }
            }

            return false;
        }

        /// <summary>
        /// 添加一个空的资源本地化条目。
        /// </summary>
        public LocalizationAssetEntry AddEntry()
        {
            entries ??= new List<LocalizationAssetEntry>();

            var entry = new LocalizationAssetEntry();
            entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// 移除指定资源条目；传入空值或不存在的条目时不做任何处理。
        /// </summary>
        public void RemoveEntry(LocalizationAssetEntry entry)
        {
            entries?.Remove(entry);
        }

        /// <summary>
        /// 标准化资源表中的语言标识；未知格式保留清理空白后的原值，便于编辑器诊断。
        /// </summary>
        internal static string NormalizeLocaleCode(string value)
        {
            return LocaleInfo.TryNormalize(value, out string normalized)
                ? normalized
                : (value ?? string.Empty).Trim();
        }
    }

    /// <summary>
    /// 一个资源 Key 的多语言资源映射。
    /// </summary>
    [Serializable]
    public sealed class LocalizationAssetEntry
    {
        [SerializeField] private string key;
        [SerializeField] private LocalizationAssetType assetType;
        [SerializeField] private List<LocalizationAssetLocaleValue> localeValues =
            new List<LocalizationAssetLocaleValue>();

        /// <summary>资源条目的完整本地化 Key。</summary>
        public string Key => key;
        /// <summary>编辑器展示用的资源类型。</summary>
        public LocalizationAssetType AssetType => assetType;
        /// <summary>该 Key 的多语言资源引用列表。</summary>
        public IReadOnlyList<LocalizationAssetLocaleValue> LocaleValues => localeValues;

        /// <summary>设置资源条目的完整 Key，并移除首尾空白。</summary>
        public void SetKey(string value)
        {
            key = value?.Trim();
        }

        /// <summary>设置编辑器展示用的资源类型。</summary>
        public void SetAssetType(LocalizationAssetType value)
        {
            assetType = value;
        }

        /// <summary>
        /// 获取指定语言的资源引用；不存在时创建一个空的语言槽位。
        /// </summary>
        public LocalizationAssetLocaleValue GetOrCreateLocaleValue(string localeCode)
        {
            localeValues ??= new List<LocalizationAssetLocaleValue>();

            string normalizedLocale = LocalizationAssetTable.NormalizeLocaleCode(localeCode);

            foreach (var value in localeValues)
            {
                if (value != null
                    && string.Equals(
                        LocalizationAssetTable.NormalizeLocaleCode(value.LocaleCode),
                        normalizedLocale,
                        StringComparison.OrdinalIgnoreCase))
                    return value;
            }

            var created = new LocalizationAssetLocaleValue(normalizedLocale);
            localeValues.Add(created);
            return created;
        }

        /// <summary>移除指定语言的全部资源槽位。</summary>
        public void RemoveLocaleValue(string localeCode)
        {
            if (localeValues == null)
                return;

            string normalizedLocale = LocalizationAssetTable.NormalizeLocaleCode(localeCode);

            for (int i = localeValues.Count - 1; i >= 0; i--)
            {
                LocalizationAssetLocaleValue value = localeValues[i];
                if (value != null
                    && string.Equals(
                        LocalizationAssetTable.NormalizeLocaleCode(value.LocaleCode),
                        normalizedLocale,
                        StringComparison.OrdinalIgnoreCase))
                    localeValues.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 一个语言对应的直接 Unity 资源引用。
    /// </summary>
    [Serializable]
    public sealed class LocalizationAssetLocaleValue
    {
        [SerializeField] private string localeCode;
        [SerializeField] private UnityEngine.Object asset;

        public LocalizationAssetLocaleValue() { }

        public LocalizationAssetLocaleValue(string localeCode)
        {
            SetLocaleCode(localeCode);
        }

        /// <summary>资源槽位对应的标准语言标识。</summary>
        public string LocaleCode => localeCode;
        /// <summary>该语言对应的 Unity 资源引用。</summary>
        public UnityEngine.Object Asset => asset;

        /// <summary>设置语言标识，并在可识别时标准化格式。</summary>
        public void SetLocaleCode(string value)
        {
            localeCode = LocaleInfo.TryNormalize(value, out string normalized)
                ? normalized
                : (value ?? string.Empty).Trim();
        }

        /// <summary>设置该语言对应的 Unity 资源引用。</summary>
        public void SetAsset(UnityEngine.Object value)
        {
            asset = value;
        }
    }

    /// <summary>
    /// 资源表编辑器使用的资源类型分类。
    /// </summary>
    public enum LocalizationAssetType
    {
        Sprite = 0,
        AudioClip = 1,
        Font = 2,
        TMPFontAsset = 3,
        Prefab = 4,
        ScriptableObject = 5,
        Other = 6
    }
}
