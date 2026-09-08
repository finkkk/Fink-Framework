using System;
using System.Collections.Generic;
using UnityEngine;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 本地化模块的独立配置资产。
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizationSettingsAsset",
        menuName = "Fink Framework/Localization/Localization Settings")]
    public sealed class LocalizationSettingsAsset : ScriptableObject
    {
        [Header("模块配置")]
        [Tooltip("关闭后，本地化运行时不会初始化或加载语言文件。")]
        [SerializeField] private bool enableLocalization = true;

        [Tooltip("仅对希伯来语、阿拉伯语等少数从右向左书写的语言启用文字方向适配。")]
        [SerializeField] private bool enableRtlSupport = false;

        [Header("数据配置")]
        [Tooltip("默认语言，使用标准语言标识，例如 en-US。")]
        [SerializeField] private string defaultLocale = LocaleIds.EnglishUS;

        [Tooltip("当前语言找不到文本时使用的默认回退语言。")]
        [SerializeField] private string defaultFallbackLocale = LocaleIds.EnglishUS;

        [Tooltip("语言文件名格式。{0} 会替换为小写下划线格式的语言名。")]
        [SerializeField] private string jsonFileNamePattern = "{0}.json";

        [Header("运行时加载")]
        [SerializeField] private RuntimeLoadingMode runtimeLoadingMode = RuntimeLoadingMode.LoadAll;

        [Header("固定语言")]
        [SerializeField] private List<LocaleInfo> supportedLocales = new List<LocaleInfo>
        {
            new LocaleInfo(LocaleIds.EnglishUS, "美国英语"),
            new LocaleInfo(LocaleIds.ChineseSimplified, "简体中文")
        };

        [Header("主分类")]
        [Tooltip("用于组织语言文件和本地化 Key 的顶层分类。")]
        [SerializeField] private List<string> categories = new List<string>();

        /// <summary>是否启用本地化模块。</summary>
        public bool EnableLocalization => enableLocalization;
        /// <summary>是否启用 RTL 文字方向适配。</summary>
        public bool EnableRtlSupport => enableRtlSupport;
        /// <summary>默认运行时语言。</summary>
        public string DefaultLocale => LocaleInfo.Normalize(defaultLocale);
        /// <summary>当前语言缺失时使用的回退语言。</summary>
        public string DefaultFallbackLocale => LocaleInfo.Normalize(defaultFallbackLocale);
        /// <summary>语言文件名格式。</summary>
        public string JsonFileNamePattern => jsonFileNamePattern;
        /// <summary>运行时语言表加载策略。</summary>
        public RuntimeLoadingMode LoadingMode => runtimeLoadingMode;
        /// <summary>项目支持的语言列表。</summary>
        public IReadOnlyList<LocaleInfo> SupportedLocales => supportedLocales;
        /// <summary>项目配置的语言表分类列表。</summary>
        public IReadOnlyList<string> Categories => categories;
        /// <summary>
        /// 查询是否支持指定语言。
        /// </summary>
        public bool SupportsLocale(string code)
        {
            if (!LocaleInfo.TryNormalize(code, out var normalized))
                return false;

            if (supportedLocales == null)
                return false;

            for (int i = 0; i < supportedLocales.Count; i++)
            {
                LocaleInfo locale = supportedLocales[i];
                if (locale != null && string.Equals(locale.Code, normalized, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 将配置中的内置语言和默认值标准化，并移除重复语言和分类。
        /// 不属于 LocaleCatalog 的自定义语言会被忽略。
        /// 编辑器保存配置前会调用此方法。
        /// Project Settings 保存流程会先校验分类；这里保留最后的防御性规范化，
        /// 避免运行时或其他非界面调用把无效分类继续带入加载和同步流程。
        /// </summary>
        public void NormalizeConfiguration()
        {
            if (!LocaleInfo.TryNormalize(defaultLocale, out var normalizedDefault)
                || !LocaleCatalog.IsBuiltIn(normalizedDefault))
                normalizedDefault = LocaleIds.EnglishUS;
            defaultLocale = normalizedDefault;

            if (!LocaleInfo.TryNormalize(defaultFallbackLocale, out var normalizedFallback)
                || !LocaleCatalog.IsBuiltIn(normalizedFallback))
                normalizedFallback = defaultLocale;
            defaultFallbackLocale = normalizedFallback;

            supportedLocales ??= new List<LocaleInfo>();
            categories ??= new List<string>();

            var localeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = supportedLocales.Count - 1; i >= 0; i--)
            {
                LocaleInfo locale = supportedLocales[i];
                if (locale == null
                    || !LocaleInfo.TryNormalize(locale.Code, out var normalized)
                    || !LocaleCatalog.IsBuiltIn(normalized))
                {
                    supportedLocales.RemoveAt(i);
                    continue;
                }

                string chineseName = LocaleCatalog.GetChineseName(normalized);
                locale.Set(normalized,
                    string.IsNullOrWhiteSpace(chineseName) ? locale.DisplayName : chineseName);
                if (!localeCodes.Add(normalized))
                    supportedLocales.RemoveAt(i);
            }

            if (!SupportsLocale(defaultLocale))
                supportedLocales.Insert(0,
                    new LocaleInfo(defaultLocale, LocaleCatalog.GetChineseName(defaultLocale)));

            if (!SupportsLocale(defaultFallbackLocale))
                supportedLocales.Add(
                    new LocaleInfo(defaultFallbackLocale, LocaleCatalog.GetChineseName(defaultFallbackLocale)));

            // 分类同时参与目录和完整 Key 组成，必须与运行时路径校验使用同一规则。
            // 否则 Inspector 中看似已保存的分类，会在加载时才因非法文件名字符被拒绝。
            // 统一按不区分大小写去重，避免 UI/ui 产生歧义。
            var categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = categories.Count - 1; i >= 0; i--)
            {
                string category = categories[i]?.Trim();
                if (!LocalizationPath.IsSafeCategory(category))
                {
                    categories.RemoveAt(i);
                    continue;
                }

                categories[i] = category;
                if (!categoryNames.Add(category))
                    categories.RemoveAt(i);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 修改默认语言时立即规范化标识，避免短暂保存大小写或分隔符不一致的数据。
        /// 完整的语言与分类去重仍由保存前的 NormalizeConfiguration 负责。
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(defaultLocale) || !LocaleInfo.TryNormalize(defaultLocale, out var normalizedDefault))
                defaultLocale = LocaleIds.EnglishUS;
            else
                defaultLocale = normalizedDefault;

            if (string.IsNullOrWhiteSpace(defaultFallbackLocale) || !LocaleInfo.TryNormalize(defaultFallbackLocale, out var normalizedFallback))
                defaultFallbackLocale = defaultLocale;
            else
                defaultFallbackLocale = normalizedFallback;
        }
#endif
    }

    /// <summary>
    /// 运行时语言数据的加载策略。
    /// </summary>
    public enum RuntimeLoadingMode
    {
        /// <summary>启动时加载所有语言和所有分类。</summary>
        LoadAll = 0,
        /// <summary>启动时加载当前语言与 Fallback，切换语言时再加载新的语言包。</summary>
        OnDemandLocalePackage = 1,
        /// <summary>首次使用某个模块或分类时才加载对应语言表。</summary>
        OnDemandModule = 2
    }
}
