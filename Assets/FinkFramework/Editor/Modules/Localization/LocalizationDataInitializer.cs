using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FinkFramework.Runtime.Localization;
using UnityEditor;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 根据本地化配置创建分类目录和语言 JSON 文件。
    /// </summary>
    public static class LocalizationDataInitializer
    {
        /// <summary>
        /// 菜单入口：按当前配置创建缺失的分类目录和语言 JSON。
        /// </summary>
        public static void InitializeFromMenu()
        {
            LocalizationSettingsAsset settings = LocalizationSettingsEditorLoader.LoadOrCreate();
            if (settings == null)
            {
                EditorUtility.DisplayDialog("创建失败", "LocalizationSettingsAsset 不存在，无法创建缺失语言文件。", "确定");
                return;
            }

            EditorUtility.DisplayDialog(Initialize(settings, out string message) ? "创建完成" : "创建失败", message, "确定");
        }

        /// <summary>
        /// 创建缺少的语言表文件，不覆盖任何已有文件。
        /// </summary>
        public static bool Initialize(LocalizationSettingsAsset settings, out string message)
        {
            if (settings == null)
            {
                message = "LocalizationSettingsAsset 为空。";
                return false;
            }

            settings.NormalizeConfiguration();
            if (settings.Categories == null || settings.Categories.Count == 0)
            {
                message = "当前没有配置主分类，请先添加 UI、Common 或 Battle 等分类。";
                return false;
            }

            if (settings.SupportedLocales == null || settings.SupportedLocales.Count == 0)
            {
                message = "当前没有配置支持语言，请检查本地化配置中的固定语言列表。";
                return false;
            }

            if (!LocalizationDataSyncUtility.PrepareSourceDataRoot(
                    out string prepareMessage))
            {
                message = prepareMessage;
                return false;
            }

            string dataRoot = LocalizationPath.GetSourceDataRoot();
            int createdCount = 0;
            int existingCount = 0;
            var errors = new List<string>();

            try
            {
                Directory.CreateDirectory(dataRoot);

                foreach (string category in settings.Categories)
                {
                    string categoryDirectory = LocalizationPath.GetCategoryDirectory(dataRoot, category);
                    if (string.IsNullOrEmpty(categoryDirectory))
                    {
                        errors.Add($"无效分类：{category}");
                        continue;
                    }

                    Directory.CreateDirectory(categoryDirectory);
                    foreach (LocaleInfo locale in settings.SupportedLocales)
                    {
                        if (locale == null || !LocaleInfo.TryNormalize(locale.Code, out string normalizedLocale))
                        {
                            errors.Add($"无效语言：{locale?.Code}");
                            continue;
                        }

                        string filePath = LocalizationPath.GetLanguageFilePath(
                            dataRoot,
                            category,
                            normalizedLocale,
                            settings.JsonFileNamePattern);
                        if (string.IsNullOrEmpty(filePath))
                        {
                            errors.Add($"无法生成语言文件路径：分类={category}，语言={normalizedLocale}");
                            continue;
                        }

                        if (File.Exists(filePath))
                        {
                            existingCount++;
                            continue;
                        }

                        LocalizationEditorFileUtil.WriteUtf8TextAtomic(filePath, "{}\n");
                        createdCount++;
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add($"写入语言表失败：{exception.Message}");
            }

            if (errors.Count == 0
                && !LocalizationDataSyncUtility.Sync(settings, out string manifestMessage))
            {
                errors.Add(manifestMessage);
            }

            AssetDatabase.Refresh();

            if (errors.Count > 0)
            {
                message = BuildMessage(createdCount, existingCount, errors);
                return false;
            }

            message =
                $"已创建 {createdCount} 个语言表文件，保留 {existingCount} 个已有文件。\n" +
                $"数据目录：{dataRoot}\n" +
                "运行时清单已同步。";
            return true;
        }

        private static string BuildMessage(
            int createdCount,
            int existingCount,
            IReadOnlyList<string> errors)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"已创建 {createdCount} 个语言表文件，保留 {existingCount} 个已有文件。\n");
            for (int i = 0; i < errors.Count && i < 8; i++)
                builder.AppendLine(errors[i]);

            if (errors.Count > 8)
                builder.AppendLine($"其余 {errors.Count - 8} 个错误已省略。");

            return builder.ToString();
        }
    }
}
