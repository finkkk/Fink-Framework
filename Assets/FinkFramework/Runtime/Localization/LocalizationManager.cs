using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// Fink 本地化运行时查询入口。
    /// </summary>
    public static class LocalizationManager
    {
        private static readonly LocalizationDatabase database = new();
        private static readonly LocalizationFileDataSource dataSource = new();
        private static readonly IReadOnlyList<LocaleInfo> EmptyLocales = new List<LocaleInfo>().AsReadOnly();
        private static readonly Dictionary<string, string> SystemLanguageLocaleMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Chinese", LocaleIds.ChineseSimplified },
            { "ChineseSimplified", LocaleIds.ChineseSimplified },
            { "ChineseTraditional", LocaleIds.ChineseTraditional },
            { "English", LocaleIds.EnglishUS },
            { "French", LocaleIds.FrenchFrance },
            { "German", LocaleIds.German },
            { "Italian", LocaleIds.Italian },
            { "Japanese", LocaleIds.Japanese },
            { "Korean", LocaleIds.Korean },
            { "Portuguese", LocaleIds.PortugueseBrazil },
            { "Russian", LocaleIds.Russian },
            { "Spanish", LocaleIds.SpanishSpain },
            { "Thai", LocaleIds.Thai },
            { "Turkish", LocaleIds.Turkish },
            { "Ukrainian", LocaleIds.Ukrainian },
            { "Vietnamese", LocaleIds.Vietnamese }
        };
        private static bool initialized;
        // 每次重新初始化或 Clear 都递增，防止旧异步任务在新数据库上继续提交结果。
        private static int runtimeGeneration;
        // 每次有效的语言切换请求都递增，确保并发切换只有最后一次请求能够提交。
        private static int localeSwitchVersion;
        private static bool warnedSynchronousUriAccess;
        private static bool settingsResourceLoaded;
        private static bool asyncPreloadAttempted;
        private static bool manifestLoadAttempted;
        private static bool manifestAsyncLoadAttempted;
        private static LocalizationManifest manifest;
        private static bool assetTableLoadAttempted;
        private static bool assetTableResourceLoaded;
        private static LocalizationAssetTable assetTable;
        private static Task<bool> asyncReadyTask;
        private static Task<bool> manifestAsyncLoadTask;
        private static readonly Dictionary<string, Task<bool>> asyncTableLoadTasks =
            new(StringComparer.OrdinalIgnoreCase);
        // 当前初始化周期内记住失败的本地文件加载，避免未知 Key 重复触发 I/O。
        private static readonly HashSet<string> failedTableLoads =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> usedCategories =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> warnedMissingKeys = new();
        private static readonly HashSet<string> warnedFallbackKeys = new();
        private static readonly HashSet<string> warnedMissingAssets = new();

        /// <summary>
        /// 当前运行时使用的配置。
        /// </summary>
        public static LocalizationSettingsAsset Settings { get; private set; }

        /// <summary>
        /// 当前配置支持的语言列表。系统尚未初始化时返回空列表。
        /// </summary>
        public static IReadOnlyList<LocaleInfo> SupportedLocales
        {
            get
            {
                EnsureInitialized();
                return Settings?.SupportedLocales ?? EmptyLocales;
            }
        }

        /// <summary>
        /// 获取当前配置支持的语言列表。
        /// </summary>
        public static IReadOnlyList<LocaleInfo> GetSupportedLocales()
        {
            return SupportedLocales;
        }

        /// <summary>
        /// 确保本地化系统已经从项目本地化 Resources 配置初始化。
        /// 不会绕过配置中的本地化模块开关。
        /// </summary>
        public static bool EnsureInitialized()
        {
            return initialized || Initialize();
        }

        /// <summary>
        /// 当前运行时语言。
        /// </summary>
        public static string CurrentLocale { get; private set; }

        /// <summary>
        /// 当前语言是否需要从右向左的文字方向适配。
        /// 仅当配置开启 RTL 支持，且当前语言属于常见 RTL 语言时返回 true。
        /// </summary>
        public static bool IsCurrentLocaleRightToLeft
        {
            get
            {
                if (!EnsureInitialized() || Settings == null || !Settings.EnableRtlSupport)
                    return false;

                return LocaleInfo.IsRightToLeftCode(CurrentLocale);
            }
        }

        /// <summary>
        /// 当前语言发生变化时触发，参数依次为旧语言和新语言。
        /// </summary>
        public static event Action<string, string> OnLocaleChanged;

        /// <summary>
        /// 当前本地化数据库是否已初始化。
        /// </summary>
        public static bool IsInitialized => initialized;

        /// <summary>
        /// 当前已加载的文本数量，便于调试和测试。
        /// </summary>
        public static int LoadedEntryCount => database.Count;

        /// <summary>
        /// 当前已加载的独立语言表数量，便于调试按需加载和卸载行为。
        /// </summary>
        public static int LoadedTableCount => database.TableCount;

        /// <summary>
        /// 使用项目本地化 Resources 中的配置初始化本地化系统。
        /// </summary>
        public static bool Initialize()
        {
            return InitializeInternal(LoadSettingsResource(), null, false);
        }

        /// <summary>
        /// 使用项目 Resources 中的配置初始化，并优先选择当前系统语言。
        /// 系统语言不在支持列表时仍使用配置中的默认语言，不保存任何选择。
        /// </summary>
        public static bool InitializeFromSystemLanguage()
        {
            return InitializeInternal(
                LoadSettingsResource(),
                TryGetSystemLocale(out string systemLocale) ? systemLocale : null,
                false);
        }

        /// <summary>
        /// 使用指定配置初始化，并优先选择当前系统语言。
        /// </summary>
        public static bool InitializeFromSystemLanguage(LocalizationSettingsAsset localizationSettings)
        {
            return InitializeInternal(
                localizationSettings,
                TryGetSystemLocale(out string systemLocale) ? systemLocale : null,
                true);
        }

        /// <summary>
        /// 使用指定配置初始化本地化系统。编辑器测试和后续自定义加载器可使用此入口。
        /// </summary>
        public static bool Initialize(LocalizationSettingsAsset localizationSettings)
        {
            return InitializeInternal(localizationSettings, null, true);
        }

        /// <summary>
        /// 使用指定配置初始化本地化系统，可选地优先使用系统语言。
        /// </summary>
        private static bool InitializeInternal(
            LocalizationSettingsAsset localizationSettings,
            string preferredLocale,
            bool releaseRuntimeResources)
        {
            runtimeGeneration++;
            localeSwitchVersion++;
            if (releaseRuntimeResources)
                ReleaseRuntimeResources();
            database.Clear();
            warnedMissingKeys.Clear();
            warnedFallbackKeys.Clear();
            usedCategories.Clear();
            asyncPreloadAttempted = false;
            asyncReadyTask = null;
            asyncTableLoadTasks.Clear();
            failedTableLoads.Clear();
            warnedSynchronousUriAccess = false;
            manifestLoadAttempted = false;
            manifestAsyncLoadAttempted = false;
            manifestAsyncLoadTask = null;
            manifest = null;
            assetTableLoadAttempted = false;
            assetTableResourceLoaded = false;
            assetTable = null;
            warnedMissingAssets.Clear();
            initialized = false;
            Settings = localizationSettings;
            CurrentLocale = null;

            if (Settings == null)
            {
                LogUtil.Error("Localization", "LocalizationSettingsAsset 缺失，无法初始化本地化系统。");
                return false;
            }

            try
            {
                Settings.NormalizeConfiguration();
            }
            catch (Exception exception)
            {
                LogUtil.Error("Localization", $"本地化配置无效：{exception.Message}");
                return false;
            }

            if (!Settings.EnableLocalization)
            {
                LogUtil.Info("Localization", "本地化模块已关闭，跳过语言表加载。");
                initialized = true;
                return true;
            }

            CurrentLocale = ResolveInitialLocale(preferredLocale);
            database.SetLocaleContext(CurrentLocale, Settings.DefaultFallbackLocale);
            bool loadedAny = InitializeLoadingMode();
            initialized = true;

            if (Settings.LoadingMode != RuntimeLoadingMode.OnDemandModule
                && !loadedAny
                && Settings.Categories.Count > 0)
            {
                LogUtil.Warn("Localization", $"默认语言没有加载到任何语言表：{CurrentLocale}");
            }

            return true;
        }

        /// <summary>
        /// 从系统语言偏好、支持语言列表和默认语言中确定初始化语言。
        /// </summary>
        private static string ResolveInitialLocale(string preferredLocale)
        {
            if (!string.IsNullOrWhiteSpace(preferredLocale)
                && LocaleInfo.TryNormalize(preferredLocale, out string normalizedPreferred)
                && Settings.SupportsLocale(normalizedPreferred))
            {
                return normalizedPreferred;
            }

            return Settings.DefaultLocale;
        }

        /// <summary>
        /// 将 Unity 系统语言映射为框架支持的标准语言 ID。
        /// </summary>
        private static bool TryGetSystemLocale(out string localeCode)
        {
            localeCode = null;
            if (!SystemLanguageLocaleMap.TryGetValue(
                    Application.systemLanguage.ToString(),
                    out string mappedLocale))
            {
                return false;
            }

            return LocaleInfo.TryNormalize(mappedLocale, out localeCode);
        }

        /// <summary>
        /// 查询本地化文本。当前语言缺失时使用 Fallback；两者都缺失时返回 Key 并输出警告。
        /// </summary>
        public static string Get(string key)
        {
            if (TryGetForDisplay(key, out string value))
                return value;

            return key ?? string.Empty;
        }

        /// <summary>
        /// 获取当前语言对应的 Unity 资源。
        /// 当前语言没有资源时会回退到配置中的默认 Fallback 语言。
        /// 资源表资产位于 Resources 中，因此会随 Player 一起打包。
        /// </summary>
        public static UnityEngine.Object GetAsset(string key)
        {
            return TryGetAssetInternal(key, typeof(UnityEngine.Object), out UnityEngine.Object asset)
                ? asset
                : null;
        }

        /// <summary>
        /// 获取当前语言对应的指定类型 Unity 资源。
        /// </summary>
        public static T GetAsset<T>(string key) where T : UnityEngine.Object
        {
            return TryGetAssetInternal(key, typeof(T), out UnityEngine.Object asset)
                ? asset as T
                : null;
        }

        /// <summary>
        /// 尝试获取当前语言对应的指定类型 Unity 资源，不输出缺失资源日志。
        /// </summary>
        public static bool TryGetAsset<T>(string key, out T asset) where T : UnityEngine.Object
        {
            asset = null;
            if (!EnsureInitialized()
                || Settings == null
                || !Settings.EnableLocalization
                || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            LocalizationAssetTable catalog = GetAssetTable();
            if (catalog == null)
                return false;

            if (!TryGetAssetForLocale(catalog, key, CurrentLocale, out UnityEngine.Object rawAsset)
                && !string.Equals(CurrentLocale, Settings.DefaultFallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                TryGetAssetForLocale(
                    catalog,
                    key,
                    Settings.DefaultFallbackLocale,
                    out rawAsset);
            }

            asset = rawAsset as T;
            return asset != null;
        }

        /// <summary>
        /// 异步查询本地化文本。移动端 StreamingAssets 和 OnDemandModule 应优先使用此入口。
        /// </summary>
        public static async UniTask<string> GetAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            if (!await EnsureAsyncReady(cancellationToken)
                || Settings == null
                || !Settings.EnableLocalization
                || string.IsNullOrWhiteSpace(key))
            {
                if (Settings == null || Settings.EnableLocalization)
                    WarnMissingKey(key);
                return key ?? string.Empty;
            }

            await EnsureCategoryLoadedForKeyAsync(key, cancellationToken);
            if (TryGetForDisplayWithoutLoading(key, out string value))
                return value;

            WarnMissingKey(key);
            return key;
        }

        /// <summary>
        /// 尝试查询本地化文本，不输出缺失 Key 日志。
        /// </summary>
        public static bool TryGet(string key, out string value)
        {
            value = null;

            if (!EnsureInitialized()
                || Settings == null
                || !Settings.EnableLocalization
                || string.IsNullOrWhiteSpace(key))
                return false;

            EnsureCategoryLoadedForKey(key);

            return database.TryGet(key, out value);
        }

        /// <summary>
        /// 同步查询用于界面显示的文本，并在按需模式下先加载对应分类。
        /// </summary>
        private static bool TryGetForDisplay(string key, out string value)
        {
            value = null;
            if (!EnsureInitialized()
                || Settings == null
                || !Settings.EnableLocalization
                || string.IsNullOrWhiteSpace(key))
            {
                if (Settings == null || Settings.EnableLocalization)
                    WarnMissingKey(key);
                return false;
            }

            EnsureCategoryLoadedForKey(key);
            return TryGetForDisplayWithoutLoading(key, out value);
        }

        /// <summary>
        /// 只查询当前数据库，不触发磁盘或网络加载，并记录缺失/Fallback 警告。
        /// </summary>
        private static bool TryGetForDisplayWithoutLoading(string key, out string value)
        {
            value = null;
            string fallbackLocale = Settings?.DefaultFallbackLocale;

            if (!database.TryGet(
                    CurrentLocale,
                    fallbackLocale,
                    key,
                    out value,
                    out bool usedFallback))
            {
                WarnMissingKey(key);
                return false;
            }

            if (usedFallback)
            {
                string warningKey = BuildWarningKey(key);
                if (warnedFallbackKeys.Add(warningKey))
                {
                    LogUtil.Warn(
                        "Localization",
                        $"当前语言缺少本地化 Key，已使用 Fallback：Key={key}，" +
                        $"当前语言={CurrentLocale}，Fallback={fallbackLocale ?? "<未设置>"}");
                }
            }

            return true;
        }

        /// <summary>
        /// 异步查询并格式化本地化文本。
        /// </summary>
        public static async UniTask<string> FormatAsync(
            string key,
            CancellationToken cancellationToken = default,
            params object[] arguments)
        {
            string template = await GetAsync(key, cancellationToken);
            if (string.Equals(template, key ?? string.Empty, StringComparison.Ordinal)
                && !database.TryGet(key, out _))
                return template;

            if (LocalizationFormatter.TryFormat(template, arguments, out string value, out string error))
                return value;

            LogUtil.Warn("Localization", $"本地化文本格式化失败：Key={key} → {error}");
            return template;
        }

        /// <summary>
        /// 查询并使用位置参数格式化本地化文本，例如："击败了 {0} 个敌人"。
        /// </summary>
        public static string Format(string key, params object[] arguments)
        {
            if (!TryGetForDisplay(key, out string template))
                return key ?? string.Empty;

            if (LocalizationFormatter.TryFormat(template, arguments, out string value, out string error))
                return value;

            LogUtil.Warn("Localization", $"本地化文本格式化失败：Key={key} → {error}");
            return template;
        }

        /// <summary>
        /// 查询并使用命名参数格式化本地化文本，例如："击败了 {count} 个敌人"。
        /// </summary>
        public static string Format(
            string key,
            params (string Name, object Value)[] namedArguments)
        {
            if (!TryGetForDisplay(key, out string template))
                return key ?? string.Empty;

            if (LocalizationFormatter.TryFormat(template, namedArguments, out string value, out string error))
                return value;

            LogUtil.Warn("Localization", $"本地化文本格式化失败：Key={key} → {error}");
            return template;
        }

        /// <summary>
        /// 加载指定主分类的当前语言表。
        /// 按模块按需模式下会同时加载默认 Fallback 语言的同一分类。
        /// </summary>
        public static bool LoadCategory(string category)
        {
            if (!EnsureInitialized() || Settings == null || !Settings.EnableLocalization)
                return false;

            bool loadedAny = LoadCategory(category, CurrentLocale);
            if (Settings.LoadingMode == RuntimeLoadingMode.OnDemandModule
                && !string.Equals(CurrentLocale, Settings.DefaultFallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                loadedAny |= LoadCategory(category, Settings.DefaultFallbackLocale);
            }

            if (loadedAny)
                usedCategories.Add(category.Trim());

            return loadedAny;
        }

        /// <summary>
        /// 异步加载指定主分类的当前语言表。
        /// 按模块按需模式下会同时加载默认 Fallback 语言的同一分类。
        /// </summary>
        public static async UniTask<bool> LoadCategoryAsync(
            string category,
            CancellationToken cancellationToken = default)
        {
            if (!EnsureInitialized() || Settings == null || !Settings.EnableLocalization)
                return false;

            bool loadedAny = await LoadCategoryAsync(category, CurrentLocale, cancellationToken);
            if (Settings.LoadingMode == RuntimeLoadingMode.OnDemandModule
                && !string.Equals(CurrentLocale, Settings.DefaultFallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                loadedAny |= await LoadCategoryAsync(
                    category,
                    Settings.DefaultFallbackLocale,
                    cancellationToken);
            }

            if (loadedAny)
                usedCategories.Add(category.Trim());

            return loadedAny;
        }

        /// <summary>
        /// 异步切换当前运行时语言。
        /// 语言包模式会等待当前语言和 Fallback 语言加载完成后再触发语言变化事件。
        /// </summary>
        public static async UniTask<bool> SwitchLocaleAsync(
            string localeCode,
            CancellationToken cancellationToken = default)
        {
            if (!EnsureInitialized() || Settings == null || !Settings.EnableLocalization)
                return false;

            if (!TryResolveLocaleSwitch(localeCode, out string normalizedLocale))
                return false;

            int generation = runtimeGeneration;
            int switchVersion = ++localeSwitchVersion;
            string previousLocale = CurrentLocale;
            RuntimeLoadingMode loadingMode = Settings.LoadingMode;
            string fallbackLocale = Settings.DefaultFallbackLocale;
            if (string.Equals(previousLocale, normalizedLocale, StringComparison.OrdinalIgnoreCase))
            {
                database.SetLocaleContext(CurrentLocale, fallbackLocale);
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await EnsureAsyncReady(cancellationToken);
            if (!IsLocaleSwitchCurrent(generation, switchVersion))
                return false;

            if (loadingMode == RuntimeLoadingMode.OnDemandModule)
            {
                var categoriesToReload = new List<string>(usedCategories);
                foreach (string category in categoriesToReload)
                {
                    await LoadCategoryAsync(category, normalizedLocale, cancellationToken);
                    if (!IsLocaleSwitchCurrent(generation, switchVersion))
                        return false;

                    if (!string.Equals(normalizedLocale, fallbackLocale, StringComparison.OrdinalIgnoreCase))
                    {
                        await LoadCategoryAsync(
                            category,
                            fallbackLocale,
                            cancellationToken);
                        if (!IsLocaleSwitchCurrent(generation, switchVersion))
                            return false;
                    }
                }
            }

            if (loadingMode == RuntimeLoadingMode.OnDemandLocalePackage)
            {
                await LoadAllCategoriesAsync(normalizedLocale, cancellationToken);
                if (!IsLocaleSwitchCurrent(generation, switchVersion))
                    return false;

                if (!string.Equals(normalizedLocale, fallbackLocale, StringComparison.OrdinalIgnoreCase))
                {
                    await LoadAllCategoriesAsync(fallbackLocale, cancellationToken);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!IsLocaleSwitchCurrent(generation, switchVersion))
                return false;

            CommitLocaleSwitch(previousLocale, normalizedLocale);
            return true;
        }

        /// <summary>
        /// 同步切换当前运行时语言。返回时语言上下文和所需本地语言表已经更新。
        /// URI 形式的 StreamingAssets 无法同步读取，应改用 SwitchLocaleAsync。
        /// </summary>
        public static bool SwitchLocale(string localeCode)
        {
            if (!EnsureInitialized() || Settings == null || !Settings.EnableLocalization)
                return false;

            if (!TryResolveLocaleSwitch(localeCode, out string normalizedLocale))
                return false;

            string previousLocale = CurrentLocale;
            if (string.Equals(previousLocale, normalizedLocale, StringComparison.OrdinalIgnoreCase))
            {
                localeSwitchVersion++;
                database.SetLocaleContext(CurrentLocale, Settings.DefaultFallbackLocale);
                return true;
            }

            if (IsStreamingAssetsUri)
            {
                return RejectSynchronousUriAccess(
                    "当前平台的 StreamingAssets 是 URI，无法同步切换语言，请使用 SwitchLocaleAsync。");
            }

            int generation = runtimeGeneration;
            int switchVersion = ++localeSwitchVersion;

            LoadLocaleForSynchronousSwitch(normalizedLocale);
            if (!IsLocaleSwitchCurrent(generation, switchVersion))
                return false;

            CommitLocaleSwitch(previousLocale, normalizedLocale);
            return true;
        }

        private static bool TryResolveLocaleSwitch(string localeCode, out string normalizedLocale)
        {
            if (!LocaleInfo.TryNormalize(localeCode, out normalizedLocale))
            {
                LogUtil.Warn("Localization", $"无法切换到无效语言：{localeCode}");
                return false;
            }

            if (Settings.SupportsLocale(normalizedLocale))
                return true;

            LogUtil.Warn("Localization", $"语言不在支持列表中：{normalizedLocale}");
            return false;
        }

        private static bool IsLocaleSwitchCurrent(int generation, int switchVersion)
        {
            return generation == runtimeGeneration && switchVersion == localeSwitchVersion;
        }

        private static void LoadLocaleForSynchronousSwitch(string localeCode)
        {
            string fallbackLocale = Settings.DefaultFallbackLocale;
            if (Settings.LoadingMode == RuntimeLoadingMode.OnDemandModule)
            {
                var categoriesToReload = new List<string>(usedCategories);
                for (int i = 0; i < categoriesToReload.Count; i++)
                {
                    string category = categoriesToReload[i];
                    LoadCategory(category, localeCode);
                    if (!string.Equals(localeCode, fallbackLocale, StringComparison.OrdinalIgnoreCase))
                        LoadCategory(category, fallbackLocale);
                }

                return;
            }

            // LoadAll 初始化后通常已经具备这些表；再次调用可以补齐缺失或被卸载的表。
            LoadAllCategories(localeCode);
            if (!string.Equals(localeCode, fallbackLocale, StringComparison.OrdinalIgnoreCase))
                LoadAllCategories(fallbackLocale);
        }

        private static void CommitLocaleSwitch(string previousLocale, string normalizedLocale)
        {
            CurrentLocale = normalizedLocale;
            database.SetLocaleContext(CurrentLocale, Settings.DefaultFallbackLocale);

            if (Settings.LoadingMode != RuntimeLoadingMode.LoadAll
                && !string.Equals(previousLocale, Settings.DefaultFallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                database.RemoveLocale(previousLocale);
            }

            NotifyLocaleChanged(previousLocale, CurrentLocale);
        }

        /// <summary>
        /// 卸载指定分类的语言表。
        /// 按模块按需模式下会同时卸载当前语言和默认 Fallback 语言。
        /// </summary>
        public static bool UnloadCategory(string category)
        {
            if (!EnsureInitialized() || Settings == null || !Settings.EnableLocalization)
                return false;

            if (!LocalizationPath.IsSafeCategory(category))
                return false;

            string normalizedCategory = category.Trim();

            bool removed = database.RemoveTable(CurrentLocale, normalizedCategory);
            failedTableLoads.Remove(BuildTableLoadKey(CurrentLocale, normalizedCategory));
            if (Settings.LoadingMode == RuntimeLoadingMode.OnDemandModule
                && !string.Equals(CurrentLocale, Settings.DefaultFallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                removed |= database.RemoveTable(Settings.DefaultFallbackLocale, normalizedCategory);
                failedTableLoads.Remove(BuildTableLoadKey(Settings.DefaultFallbackLocale, normalizedCategory));
            }

            if (removed)
                usedCategories.Remove(normalizedCategory);

            return removed;
        }

        /// <summary>
        /// 卸载指定语言的全部分类。当前语言和 Fallback 语言由调用方决定是否保留。
        /// </summary>
        public static int UnloadLocale(string localeCode)
        {
            if (!EnsureInitialized() || Settings == null || !Settings.EnableLocalization)
                return 0;

            if (!LocaleInfo.TryNormalize(localeCode, out string normalizedLocale))
                return 0;

            RemoveFailedTableLoads(normalizedLocale);
            return database.RemoveLocale(normalizedLocale);
        }

        /// <summary>
        /// 清理当前运行时语言表。
        /// </summary>
        public static void Clear()
        {
            runtimeGeneration++;
            localeSwitchVersion++;
            ReleaseRuntimeResources();
            database.Clear();
            Settings = null;
            CurrentLocale = null;
            initialized = false;
            warnedMissingKeys.Clear();
            warnedFallbackKeys.Clear();
            usedCategories.Clear();
            asyncPreloadAttempted = false;
            asyncReadyTask = null;
            manifestLoadAttempted = false;
            manifestAsyncLoadAttempted = false;
            manifestAsyncLoadTask = null;
            manifest = null;
            assetTableLoadAttempted = false;
            assetTableResourceLoaded = false;
            assetTable = null;
            warnedMissingAssets.Clear();
            asyncTableLoadTasks.Clear();
            failedTableLoads.Clear();
            warnedSynchronousUriAccess = false;
        }

        /// <summary>
        /// 查询资源表并校验资源实际类型；公开资源 API 统一通过此入口处理 Fallback 和日志。
        /// </summary>
        private static bool TryGetAssetInternal(
            string key,
            Type expectedType,
            out UnityEngine.Object asset)
        {
            asset = null;
            if (!EnsureInitialized()
                || Settings == null
                || !Settings.EnableLocalization
                || string.IsNullOrWhiteSpace(key))
            {
                WarnMissingAsset(key, expectedType, "本地化模块未启用或 Key 为空。", false);
                return false;
            }

            LocalizationAssetTable catalog = GetAssetTable();
            if (catalog == null)
            {
                WarnMissingAsset(key, expectedType, "资源本地化表未找到。", false);
                return false;
            }

            if (!TryGetAssetForLocale(catalog, key, CurrentLocale, out asset)
                && !string.Equals(CurrentLocale, Settings.DefaultFallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                TryGetAssetForLocale(
                    catalog,
                    key,
                    Settings.DefaultFallbackLocale,
                    out asset);
            }

            if (asset == null)
            {
                WarnMissingAsset(key, expectedType, "当前语言和 Fallback 都没有资源引用。", true);
                return false;
            }

            if (expectedType != typeof(UnityEngine.Object)
                && !expectedType.IsInstanceOfType(asset))
            {
                WarnMissingAsset(
                    key,
                    expectedType,
                    $"资源类型不匹配，实际类型为 {asset.GetType().Name}。",
                    true);
                asset = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 从资源表读取指定语言的资源，不执行 Fallback。
        /// </summary>
        private static bool TryGetAssetForLocale(
            LocalizationAssetTable catalog,
            string key,
            string localeCode,
            out UnityEngine.Object asset)
        {
            asset = null;
            return catalog != null
                && !string.IsNullOrWhiteSpace(localeCode)
                && catalog.TryGetAsset(key, localeCode, out asset)
                && asset != null;
        }

        /// <summary>
        /// 延迟加载并缓存资源本地化表，避免没有使用资源功能时提前加载资产。
        /// </summary>
        private static LocalizationAssetTable GetAssetTable()
        {
            if (assetTableLoadAttempted)
                return assetTable;

            assetTableLoadAttempted = true;
            assetTable = LoadAssetTableResource();
            return assetTable;
        }

        /// <summary>
        /// 通过资源管理器加载本地化配置资产。
        /// 该入口只负责 Unity Resources 中的 ScriptableObject；语言 JSON 仍由文件数据源读取。
        /// </summary>
        private static LocalizationSettingsAsset LoadSettingsResource()
        {
            // 顶层默认配置入口先释放本地化上一次持有的引用，再获取新引用。
            ReleaseRuntimeResources();
            try
            {
                LocalizationSettingsAsset settings = ResManager.Instance.Load<LocalizationSettingsAsset>(
                    BuildResourcesPath(LocalizationPath.SettingsResourcesPath));
                if (settings != null)
                {
                    settingsResourceLoaded = true;
                    return settings;
                }

                // 资源管理器可能在配置缺失时只返回 null 而不抛异常，同样使用 Unity Resources 兜底。
                LocalizationSettingsAsset fallback = Resources.Load<LocalizationSettingsAsset>(
                    LocalizationPath.SettingsResourcesPath);
                if (fallback != null)
                {
                    settingsResourceLoaded = false;
                    LogUtil.Warn("Localization", "资源管理器未返回本地化配置，已回退到 Unity Resources。");
                    return fallback;
                }

                settingsResourceLoaded = false;
                return null;
            }
            catch (Exception exception)
            {
                // 编辑器脚本或资源管理器尚未准备好时，直接从 Unity Resources 读取配置。
                // 配置资产本身位于固定 Resources 路径，这个兜底不会依赖 GlobalSettingsAsset。
                LocalizationSettingsAsset fallback = Resources.Load<LocalizationSettingsAsset>(
                    LocalizationPath.SettingsResourcesPath);
                if (fallback != null)
                {
                    settingsResourceLoaded = false;
                    LogUtil.Warn(
                        "Localization",
                        $"资源管理器加载本地化配置失败，已回退到 Unity Resources：{exception.GetBaseException().Message}");
                    return fallback;
                }

                // 配置资源或全局资源系统尚未准备好时，初始化应返回失败而不是把底层空引用抛给业务层。
                settingsResourceLoaded = false;
                LogUtil.Error(
                    "Localization",
                    $"加载本地化配置资源失败：{exception.GetBaseException().Message}");
                return null;
            }
        }

        /// <summary>
        /// 通过资源管理器加载资源本地化目录资产。
        /// </summary>
        private static LocalizationAssetTable LoadAssetTableResource()
        {
            try
            {
                LocalizationAssetTable catalog = ResManager.Instance.Load<LocalizationAssetTable>(
                    BuildResourcesPath(LocalizationPath.AssetTableResourcesPath));
                if (catalog != null)
                {
                    assetTableResourceLoaded = true;
                    return catalog;
                }

                LocalizationAssetTable fallback = Resources.Load<LocalizationAssetTable>(
                    LocalizationPath.AssetTableResourcesPath);
                if (fallback != null)
                {
                    assetTableResourceLoaded = false;
                    LogUtil.Warn("Localization", "资源管理器未返回本地化资源表，已回退到 Unity Resources。");
                    return fallback;
                }

                assetTableResourceLoaded = false;
                return null;
            }
            catch (Exception exception)
            {
                LocalizationAssetTable fallback = Resources.Load<LocalizationAssetTable>(
                    LocalizationPath.AssetTableResourcesPath);
                if (fallback != null)
                {
                    assetTableResourceLoaded = false;
                    LogUtil.Warn(
                        "Localization",
                        $"资源管理器加载本地化资源表失败，已回退到 Unity Resources：{exception.GetBaseException().Message}");
                    return fallback;
                }

                assetTableResourceLoaded = false;
                LogUtil.Warn(
                    "Localization",
                    $"加载本地化资源表失败：{exception.GetBaseException().Message}");
                return null;
            }
        }

        /// <summary>
        /// 将 Resources 下的相对路径转换为资源管理器使用的 res:// 路径。
        /// </summary>
        private static string BuildResourcesPath(string resourcesPath)
        {
            return $"res://{resourcesPath}";
        }

        /// <summary>
        /// 释放本地化模块通过 ResManager 持有的资源引用。
        /// 不强制删除缓存，避免影响其他模块对同一配置资产的引用。
        /// </summary>
        private static void ReleaseRuntimeResources()
        {
            if (settingsResourceLoaded)
            {
                settingsResourceLoaded = false;
                try
                {
                    ResManager.Instance.UnloadAsset<LocalizationSettingsAsset>(
                        BuildResourcesPath(LocalizationPath.SettingsResourcesPath));
                }
                catch (Exception exception)
                {
                    LogUtil.Warn("Localization", $"释放本地化配置资源失败：{exception.Message}");
                }
            }

            if (assetTableResourceLoaded)
            {
                assetTableResourceLoaded = false;
                try
                {
                    ResManager.Instance.UnloadAsset<LocalizationAssetTable>(
                        BuildResourcesPath(LocalizationPath.AssetTableResourcesPath));
                }
                catch (Exception exception)
                {
                    LogUtil.Warn("Localization", $"释放本地化资源表失败：{exception.Message}");
                }
            }
        }

        /// <summary>
        /// 按语言、Key、期望类型和原因去重记录资源缺失警告。
        /// </summary>
        private static void WarnMissingAsset(
            string key,
            Type expectedType,
            string reason,
            bool includeLocaleContext)
        {
            string warningKey = $"{CurrentLocale ?? string.Empty}|{Settings?.DefaultFallbackLocale ?? string.Empty}|"
                + $"{expectedType?.FullName ?? string.Empty}|{key ?? string.Empty}|{reason}";
            if (!warnedMissingAssets.Add(warningKey))
                return;

            string localeContext = includeLocaleContext
                ? $"当前语言={CurrentLocale ?? "<未设置>"}，Fallback={Settings?.DefaultFallbackLocale ?? "<未设置>"}，"
                : string.Empty;
            LogUtil.Warn(
                "Localization",
                $"本地化资源缺失：{localeContext}Key={key}，期望类型={expectedType?.Name ?? "Object"}，原因={reason}");
        }

        /// <summary>
        /// 按当前语言上下文去重记录缺失文本 Key 警告。
        /// </summary>
        private static void WarnMissingKey(string key)
        {
            string warningKey = BuildWarningKey(key);
            if (!warnedMissingKeys.Add(warningKey))
                return;

            LogUtil.Warn(
                "Localization",
                $"本地化 Key 缺失：当前语言={CurrentLocale ?? "<未设置>"}，" +
                $"Fallback={Settings?.DefaultFallbackLocale ?? "<未设置>"}，将显示 Key：{key}");
        }

        /// <summary>
        /// 构造缺失/Fallback 日志去重所需的上下文标识。
        /// </summary>
        private static string BuildWarningKey(string key)
        {
            return $"{CurrentLocale ?? string.Empty}|{Settings?.DefaultFallbackLocale ?? string.Empty}|{key ?? string.Empty}";
        }

        /// <summary>
        /// 逐个调用语言切换订阅者，隔离单个组件异常，确保其他组件继续刷新。
        /// </summary>
        private static void NotifyLocaleChanged(string previousLocale, string currentLocale)
        {
            Action<string, string> handlers = OnLocaleChanged;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                if (!(invocationList[i] is Action<string, string> handler))
                    continue;

                try
                {
                    handler(previousLocale, currentLocale);
                }
                catch (Exception exception)
                {
                    // 单个绑定组件异常不应阻止其他组件刷新。
                    LogUtil.Error(
                        "Localization",
                        $"本地化语言切换通知失败：{exception.Message}");
                }
            }
        }

        /// <summary>
        /// 同步加载指定语言配置的全部分类，并返回是否至少成功加载一张表。
        /// </summary>
        private static bool LoadAllCategories(string localeCode)
        {
            bool loadedAny = false;
            IReadOnlyList<string> categories = Settings.Categories;

            for (int i = 0; i < categories.Count; i++)
                loadedAny |= LoadCategory(categories[i], localeCode);

            return loadedAny;
        }

        /// <summary>
        /// 按配置的加载策略执行同步初始化；URI 平台将预加载交给异步入口。
        /// </summary>
        private static bool InitializeLoadingMode()
        {
            if (IsStreamingAssetsUri
                && Settings.LoadingMode != RuntimeLoadingMode.OnDemandModule)
            {
                LogUtil.Info(
                    "Localization",
                    "当前平台的 StreamingAssets 是 URI，预加载将通过异步接口执行。\n" +
                    "请使用 InitializeAsync、GetAsync 或 FinkLocalizedText 的自动异步刷新。");
                return false;
            }

            switch (Settings.LoadingMode)
            {
                case RuntimeLoadingMode.LoadAll:
                    return LoadAllSupportedLocales();

                case RuntimeLoadingMode.OnDemandLocalePackage:
                {
                    bool loadedCurrent = LoadAllCategories(CurrentLocale);
                    if (!string.Equals(CurrentLocale, Settings.DefaultFallbackLocale, StringComparison.OrdinalIgnoreCase))
                        loadedCurrent |= LoadAllCategories(Settings.DefaultFallbackLocale);
                    return loadedCurrent;
                }

                case RuntimeLoadingMode.OnDemandModule:
                    return false;

                default:
                    LogUtil.Warn("Localization", $"未知加载模式：{Settings.LoadingMode}，将使用模块按需加载。");
                    return false;
            }
        }

        /// <summary>
        /// 当前平台的 StreamingAssets 是否只能通过 UnityWebRequest 访问。
        /// Android/WebGL 等 URI 平台不能使用同步文件 API。
        /// </summary>
        private static bool IsStreamingAssetsUri =>
            LocalizationPath.GetStreamingDataRoot().IndexOf("://", StringComparison.Ordinal) >= 0;

        /// <summary>
        /// 确保当前加载策略所需的异步预加载只执行一次，并共享并发调用结果。
        /// </summary>
        private static async UniTask<bool> EnsureAsyncReady(CancellationToken cancellationToken)
        {
            if (!EnsureInitialized() || Settings == null)
                return false;

            int generation = runtimeGeneration;

            if (!Settings.EnableLocalization)
                return true;

            if (Settings.LoadingMode == RuntimeLoadingMode.OnDemandModule
                || asyncPreloadAttempted)
                return true;

            if (asyncReadyTask != null)
                return await asyncReadyTask;

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            asyncReadyTask = completion.Task;
            try
            {
                bool loadedAny;
                switch (Settings.LoadingMode)
                {
                    case RuntimeLoadingMode.LoadAll:
                        loadedAny = await LoadAllSupportedLocalesAsync(cancellationToken);
                        break;

                    case RuntimeLoadingMode.OnDemandLocalePackage:
                        loadedAny = await LoadAllCategoriesAsync(CurrentLocale, cancellationToken);
                        if (!string.Equals(
                                CurrentLocale,
                                Settings.DefaultFallbackLocale,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            loadedAny |= await LoadAllCategoriesAsync(
                                Settings.DefaultFallbackLocale,
                                cancellationToken);
                        }
                        break;

                    default:
                        loadedAny = false;
                        break;
                }

                if (generation != runtimeGeneration)
                {
                    completion.TrySetResult(false);
                    return false;
                }

                bool result = loadedAny || Settings.Categories.Count == 0;
                asyncPreloadAttempted = true;
                completion.TrySetResult(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled();
                asyncReadyTask = null;
                throw;
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                asyncReadyTask = null;
                throw;
            }
        }

        /// <summary>
        /// 异步初始化配置和语言表。移动端或使用 StreamingAssets URI 时应从启动流程调用。
        /// </summary>
        public static async UniTask<bool> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            if (!Initialize())
                return false;

            return await EnsureAsyncReady(cancellationToken);
        }

        /// <summary>
        /// 同步按需模式下根据 Key 前缀或 Manifest 定位并加载分类。
        /// </summary>
        private static void EnsureCategoryLoadedForKey(string key)
        {
            if (Settings == null
                || !Settings.EnableLocalization
                || Settings.LoadingMode != RuntimeLoadingMode.OnDemandModule
                || string.IsNullOrWhiteSpace(key))
                return;

            string preferredCategory = TryResolveCategoryFromKey(key, out string category)
                ? category
                : null;
            if (preferredCategory == null
                && EnsureManifestLoaded()
                && manifest.TryGetCategory(key, out string manifestCategory)
                && IsConfiguredCategory(manifestCategory))
            {
                preferredCategory = manifestCategory;
            }
            if (preferredCategory != null)
            {
                bool loaded = LoadCategoryForLookup(preferredCategory);
                if (loaded && database.TryGet(key, out _))
                    usedCategories.Add(preferredCategory);
                return;
            }

            IReadOnlyList<string> categories = Settings.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                string candidateCategory = categories[i];
                if (!LoadCategoryForLookup(candidateCategory))
                    continue;

                if (database.TryGet(key, out _))
                {
                    usedCategories.Add(candidateCategory);
                    return;
                }
            }
        }

        /// <summary>
        /// 异步按需模式下根据 Key 前缀或 Manifest 定位并加载分类。
        /// </summary>
        private static async UniTask<bool> EnsureCategoryLoadedForKeyAsync(
            string key,
            CancellationToken cancellationToken)
        {
            if (Settings == null
                || !Settings.EnableLocalization
                || Settings.LoadingMode != RuntimeLoadingMode.OnDemandModule
                || string.IsNullOrWhiteSpace(key))
                return false;

            int generation = runtimeGeneration;

            string preferredCategory = null;
            if (TryResolveCategoryFromKey(key, out string prefixCategory))
            {
                preferredCategory = prefixCategory;
            }
            else if (await EnsureManifestLoadedAsync(cancellationToken))
            {
                if (generation != runtimeGeneration)
                    return false;

                if (manifest.TryGetCategory(key, out string manifestCategory)
                    && IsConfiguredCategory(manifestCategory))
                {
                    preferredCategory = manifestCategory;
                }
            }

            if (generation != runtimeGeneration)
                return false;

            if (preferredCategory != null)
            {
                bool loaded = await LoadCategoryForLookupAsync(preferredCategory, cancellationToken);
                if (generation != runtimeGeneration)
                    return false;
                if (loaded && database.TryGet(key, out _))
                    usedCategories.Add(preferredCategory);
                return loaded;
            }

            IReadOnlyList<string> categories = Settings.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                string candidateCategory = categories[i];
                if (!await LoadCategoryForLookupAsync(candidateCategory, cancellationToken))
                    continue;

                if (generation != runtimeGeneration)
                    return false;

                if (database.TryGet(key, out _))
                {
                    usedCategories.Add(candidateCategory);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 为同步 Key 查询加载当前语言及其 Fallback 对应的分类。
        /// </summary>
        private static bool LoadCategoryForLookup(string category)
        {
            bool loadedAny = LoadCategory(category, CurrentLocale);
            if (Settings.LoadingMode == RuntimeLoadingMode.OnDemandModule
                && !string.Equals(CurrentLocale, Settings.DefaultFallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                loadedAny |= LoadCategory(category, Settings.DefaultFallbackLocale);
            }

            return loadedAny;
        }

        /// <summary>
        /// 为异步 Key 查询加载当前语言及其 Fallback 对应的分类。
        /// </summary>
        private static async UniTask<bool> LoadCategoryForLookupAsync(
            string category,
            CancellationToken cancellationToken)
        {
            if (Settings == null)
                return false;

            int generation = runtimeGeneration;
            RuntimeLoadingMode loadingMode = Settings.LoadingMode;
            string currentLocale = CurrentLocale;
            string fallbackLocale = Settings.DefaultFallbackLocale;
            bool loadedAny = await LoadCategoryAsync(category, currentLocale, cancellationToken);
            if (generation != runtimeGeneration)
                return false;

            if (loadingMode == RuntimeLoadingMode.OnDemandModule
                && !string.Equals(currentLocale, fallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                loadedAny |= await LoadCategoryAsync(
                    category,
                    fallbackLocale,
                    cancellationToken);
            }

            return generation == runtimeGeneration && loadedAny;
        }

        /// <summary>
        /// 通过配置分类前缀解析完整 Key 所属的主分类。
        /// 当分类名称存在前缀关系（例如 ui 与 ui.menu）时，优先最长匹配，
        /// 避免配置列表顺序改变 Key 的归属。
        /// </summary>
        private static bool TryResolveCategoryFromKey(string key, out string category)
        {
            category = null;
            if (Settings?.Categories == null || string.IsNullOrWhiteSpace(key))
                return false;

            string normalizedKey = key.Trim();
            IReadOnlyList<string> categories = Settings.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                string candidate = categories[i];
                if (!IsConfiguredCategory(candidate)
                    || (!string.Equals(normalizedKey, candidate, StringComparison.OrdinalIgnoreCase)
                        && !normalizedKey.StartsWith(candidate + ".", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (category == null || candidate.Length > category.Length)
                    category = candidate;
            }

            return category != null;
        }

        /// <summary>
        /// 验证清单或调用方给出的分类仍在当前配置中。
        /// 清单可能来自旧数据版本；忽略过期分类后，查询仍可回退到逐分类探测。
        /// </summary>
        private static bool IsConfiguredCategory(string category)
        {
            if (!LocalizationPath.IsSafeCategory(category) || Settings?.Categories == null)
                return false;

            IReadOnlyList<string> categories = Settings.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                if (string.Equals(categories[i], category.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 在支持同步文件访问的平台加载运行时 Manifest。
        /// </summary>
        private static bool EnsureManifestLoaded()
        {
            if (manifestLoadAttempted)
                return manifest != null;

            manifestLoadAttempted = true;
            string manifestPath = LocalizationPath.GetManifestPath(
                LocalizationPath.GetStreamingDataRoot());
            if (!dataSource.TryReadManifest(manifestPath, out string json)
                || string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                manifest = JsonUtility.FromJson<LocalizationManifest>(json.TrimStart('\uFEFF'));
                return manifest is { version: > 0 };
            }
            catch (Exception exception)
            {
                LogUtil.Warn("Localization", $"本地化清单解析失败，将回退到分类探测：{exception.Message}");
                manifest = null;
                return false;
            }
        }

        /// <summary>
        /// 异步加载并缓存运行时 Manifest，供 URI 平台定位分类文件。
        /// </summary>
        private static async UniTask<bool> EnsureManifestLoadedAsync(
            CancellationToken cancellationToken)
        {
            if (manifest != null)
                return true;
            if (manifestAsyncLoadTask != null)
                return await manifestAsyncLoadTask;
            if (manifestAsyncLoadAttempted)
                return false;

            int generation = runtimeGeneration;
            manifestAsyncLoadAttempted = true;
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            manifestAsyncLoadTask = completion.Task;
            string manifestPath = LocalizationPath.GetManifestPath(
                LocalizationPath.GetStreamingDataRoot());
            try
            {
                string json = await dataSource.ReadManifestAsync(
                    manifestPath,
                    cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                {
                    completion.TrySetResult(false);
                    return false;
                }

                if (generation != runtimeGeneration)
                {
                    completion.TrySetResult(false);
                    return false;
                }

                bool loaded;
                try
                {
                    manifest = JsonUtility.FromJson<LocalizationManifest>(
                        json.TrimStart('\uFEFF'));
                    loaded = manifest is { version: > 0 };
                }
                catch (Exception exception)
                {
                    LogUtil.Warn(
                        "Localization",
                        $"本地化清单解析失败，将回退到分类探测：{exception.Message}");
                    manifest = null;
                    loaded = false;
                }

                completion.TrySetResult(loaded);
                return loaded;
            }
            catch (OperationCanceledException)
            {
                manifestAsyncLoadAttempted = false;
                manifestAsyncLoadTask = null;
                completion.TrySetCanceled();
                throw;
            }
            catch (Exception exception)
            {
                LogUtil.Warn(
                    "Localization",
                    $"读取本地化清单失败，将回退到分类探测：{exception.Message}");
                manifest = null;
                completion.TrySetResult(false);
                return false;
            }
            finally
            {
                if (manifestAsyncLoadTask == completion.Task)
                    manifestAsyncLoadTask = null;
            }
        }

        /// <summary>
        /// 按配置顺序同步加载所有支持语言的全部分类。
        /// </summary>
        private static bool LoadAllSupportedLocales()
        {
            bool loadedAny = false;
            IReadOnlyList<LocaleInfo> locales = Settings.SupportedLocales;

            for (int i = 0; i < locales.Count; i++)
            {
                LocaleInfo locale = locales[i];
                if (locale != null)
                    loadedAny |= LoadAllCategories(locale.Code);
            }

            return loadedAny;
        }

        /// <summary>
        /// 按配置顺序异步加载所有支持语言的全部分类。
        /// </summary>
        private static async UniTask<bool> LoadAllSupportedLocalesAsync(
            CancellationToken cancellationToken)
        {
            bool loadedAny = false;
            IReadOnlyList<LocaleInfo> locales = Settings.SupportedLocales;

            for (int i = 0; i < locales.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LocaleInfo locale = locales[i];
                if (locale != null)
                    loadedAny |= await LoadAllCategoriesAsync(locale.Code, cancellationToken);
            }

            return loadedAny;
        }

        /// <summary>
        /// 同步读取并提交一张“语言 + 分类”表；已加载或 URI 平台会安全跳过。
        /// </summary>
        private static bool LoadCategory(string category, string localeCode)
        {
            if (IsStreamingAssetsUri)
                return RejectSynchronousUriAccess(
                    "当前平台的 StreamingAssets 是 URI，语言表必须通过异步接口加载。");

            if (!LocalizationPath.IsSafeCategory(category) || !LocaleInfo.TryNormalize(localeCode, out string normalizedLocale))
            {
                LogUtil.Error("Localization", $"无效的语言表路径：分类={category}，语言={localeCode}");
                return false;
            }

            if (database.IsTableLoaded(normalizedLocale, category))
                return true;

            string loadKey = BuildTableLoadKey(normalizedLocale, category);
            if (failedTableLoads.Contains(loadKey))
                return false;

            if (!dataSource.TryRead(
                    LocalizationPath.GetCandidateRoots(Settings),
                    category,
                    normalizedLocale,
                    Settings.JsonFileNamePattern,
                    out LocalizationDataFile fileData))
            {
                LogUtil.Warn(
                    "Localization",
                    $"语言文件不存在：分类={category}，语言={normalizedLocale}。" +
                    "请在编辑器中保存语言表并同步运行时副本后重试。");
                failedTableLoads.Add(loadKey);
                return false;
            }

            if (!database.TryAddJson(
                    fileData.Json,
                    fileData.SourcePath,
                    normalizedLocale,
                    category,
                    out string error))
            {
                LogUtil.Error("Localization", $"加载语言文件失败：{fileData.SourcePath} → {error}");
                failedTableLoads.Add(loadKey);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 异步读取并提交一张“语言 + 分类”表，合并并发请求并校验运行时世代。
        /// </summary>
        private static async UniTask<bool> LoadCategoryAsync(
            string category,
            string localeCode,
            CancellationToken cancellationToken)
        {
            if (!LocalizationPath.IsSafeCategory(category)
                || !LocaleInfo.TryNormalize(localeCode, out string normalizedLocale))
            {
                LogUtil.Error("Localization", $"无效的语言表路径：分类={category}，语言={localeCode}");
                return false;
            }

            if (database.IsTableLoaded(normalizedLocale, category))
                return true;

            int generation = runtimeGeneration;
            string loadKey = BuildTableLoadKey(normalizedLocale, category);
            if (failedTableLoads.Contains(loadKey))
                return false;
            if (asyncTableLoadTasks.TryGetValue(loadKey, out Task<bool> existingTask))
                return await existingTask;

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            asyncTableLoadTasks[loadKey] = completion.Task;

            try
            {
                bool loaded = await LoadCategoryAsyncCore(
                    category,
                    normalizedLocale,
                    generation,
                    cancellationToken);
                completion.TrySetResult(loaded);
                return loaded;
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled();
                throw;
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                throw;
            }
            finally
            {
                if (asyncTableLoadTasks.TryGetValue(loadKey, out Task<bool> currentTask)
                    && currentTask == completion.Task)
                {
                    asyncTableLoadTasks.Remove(loadKey);
                }
            }
        }

        /// <summary>
        /// 执行单张语言表的实际异步读取、解析和数据库提交。
        /// </summary>
        private static async UniTask<bool> LoadCategoryAsyncCore(
            string category,
            string normalizedLocale,
            int generation,
            CancellationToken cancellationToken)
        {
            if (generation != runtimeGeneration)
                return false;

            if (database.IsTableLoaded(normalizedLocale, category))
                return true;

            LocalizationDataFile fileData = await dataSource.ReadAsync(
                LocalizationPath.GetCandidateRoots(Settings),
                category,
                normalizedLocale,
                Settings.JsonFileNamePattern,
                cancellationToken);
            if (generation != runtimeGeneration)
                return false;

            if (fileData == null)
            {
                LogUtil.Warn(
                    "Localization",
                    $"语言文件不存在：分类={category}，语言={normalizedLocale}。" +
                    "请在编辑器中保存语言表并同步运行时副本后重试。");
                if (!IsStreamingAssetsUri)
                    failedTableLoads.Add(BuildTableLoadKey(normalizedLocale, category));
                return false;
            }

            // 同步切换可能已在异步读取期间提交了同一张表；此时复用现有结果。
            if (database.IsTableLoaded(normalizedLocale, category))
                return true;

            if (!database.TryAddJson(
                    fileData.Json,
                    fileData.SourcePath,
                    normalizedLocale,
                    category,
                    out string error))
            {
                LogUtil.Error("Localization", $"加载语言文件失败：{fileData.SourcePath} → {error}");
                if (!IsStreamingAssetsUri)
                    failedTableLoads.Add(BuildTableLoadKey(normalizedLocale, category));
                return false;
            }

            return true;
        }

        /// <summary>
        /// 构造异步表加载任务的稳定字典键。
        /// </summary>
        private static string BuildTableLoadKey(string normalizedLocale, string category)
        {
            return normalizedLocale + "\u001f" + (category ?? string.Empty).Trim();
        }

        /// <summary>
        /// 统一拒绝 URI 平台上的同步读取，并确保同一周期只提示一次。
        /// </summary>
        private static bool RejectSynchronousUriAccess(string message)
        {
            if (!warnedSynchronousUriAccess)
            {
                LogUtil.Warn("Localization", message);
                warnedSynchronousUriAccess = true;
            }

            return false;
        }

        /// <summary>
        /// 清除指定语言的失败加载标记，使切换语言后可以重新尝试读取。
        /// </summary>
        private static void RemoveFailedTableLoads(string normalizedLocale)
        {
            string prefix = normalizedLocale + "\u001f";
            var keysToRemove = new List<string>();
            foreach (string key in failedTableLoads)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    keysToRemove.Add(key);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
                failedTableLoads.Remove(keysToRemove[i]);
        }

        /// <summary>
        /// 异步加载指定语言的全部分类。
        /// </summary>
        private static async UniTask<bool> LoadAllCategoriesAsync(
            string localeCode,
            CancellationToken cancellationToken)
        {
            bool loadedAny = false;
            IReadOnlyList<string> categories = Settings.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                loadedAny |= await LoadCategoryAsync(
                    categories[i],
                    localeCode,
                    cancellationToken);
            }

            return loadedAny;
        }

    }
}
