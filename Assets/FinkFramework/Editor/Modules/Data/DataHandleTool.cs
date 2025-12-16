using System;
using FinkFramework.Runtime.Settings;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Data
{
    /// <summary>
    /// 数据全流程处理工具（清空 → 生成类 → 等待编译(可选) → 导出 → 汇总打印）
    /// </summary>
    public static class DataHandleTool
    {
        private const string KEY_STATE = "Fink_HandleState";      // 存储最终状态
        private const string KEY_STAGE = "Fink_HandleStage";      // 当前阶段标志
        private const string KEY_GEN_RESULT = "Fink_GenResult";   // 生成阶段记录

        [Serializable]
        private class HandleState
        {
            public int genSuccess, genTotal;
            public int saveSuccess, saveTotal;
        }

        private enum Stage
        {
            None = 0,
            Cleared = 1,
            Generated = 2,     // 仅内部输出使用
            PendingCompile = 3,// 仅内部输出使用
            Exported = 4,
        }

        // ========== 一键处理数据 主函数 ==========
        public static void HandleAllData()
        {
            // 清空导出数据
            DataCleanTool.ClearExportedData();
            EditorPrefs.SetInt(KEY_STAGE, (int)Stage.Cleared);

            // 生成数据类
            var (genSuccess, genTotal) = DataGenTool.GenerateAllData(true);

            // 判断是否需要等待编译
            bool needWaitCompile = !GlobalSettings.Current.CSharpUseExternal;
            
            if (needWaitCompile)
            {
                // 内部路径 → 会触发编译 → 必须记录中间状态
                var genState = new HandleState { genSuccess = genSuccess, genTotal = genTotal };
                EditorPrefs.SetString(KEY_GEN_RESULT, JsonUtility.ToJson(genState));
                EditorPrefs.SetInt(KEY_STAGE, (int)Stage.Generated);
                // 内部输出 -> 会触发编译，需等待
                if (EditorApplication.isCompiling)
                {
                    EditorPrefs.SetInt(KEY_STAGE, (int)Stage.PendingCompile);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    return;
                }

                // 没有触发编译 → 直接导出
                DoExport(genState);
            }
            else
            {
                // 外部路径 → 不会触发编译 → 不需要记录任何 EditorPrefs
                var genState = new HandleState { genSuccess = genSuccess, genTotal = genTotal };
                DoExport(genState);
            }
        }

        // ========== 编辑器 reload 时恢复流程 ==========
        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            if (EditorApplication.isCompiling) return;

            Stage stage = (Stage)EditorPrefs.GetInt(KEY_STAGE, 0);

            switch (stage)
            {
                case Stage.PendingCompile:
                {
                    var genState = JsonUtility.FromJson<HandleState>(EditorPrefs.GetString(KEY_GEN_RESULT, "{}"));
                    DoExport(genState);
                    break;
                }
                case Stage.None:
                case Stage.Cleared:
                case Stage.Generated:
                case Stage.Exported:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // ========== 执行数据导出 ==========
        private static void DoExport(HandleState genState)
        {
            var (saveSuccess, saveTotal) = DataExportTool.ExportAllData(true);

            var state = new HandleState
            {
                genSuccess = genState.genSuccess,
                genTotal = genState.genTotal,
                saveSuccess = saveSuccess,
                saveTotal = saveTotal
            };

            EditorPrefs.SetString(KEY_STATE, JsonUtility.ToJson(state));
            EditorPrefs.SetInt(KEY_STAGE, (int)Stage.Exported);

            // 立即触发最终打印
            PrintFinalLog();
        }

        // ========== 最终汇总打印 ==========
        private static void PrintFinalLog()
        {
            if (!EditorPrefs.HasKey(KEY_STATE)) return;

            var state = JsonUtility.FromJson<HandleState>(EditorPrefs.GetString(KEY_STATE));
            EditorPrefs.DeleteKey(KEY_STATE);
            EditorPrefs.DeleteKey(KEY_STAGE);
            EditorPrefs.DeleteKey(KEY_GEN_RESULT);

            LogUtil.Success("DataExportTool", "导出数据清空完成！");
            bool result = true;

            if (state.genSuccess != state.genTotal)
            {
                result = false;
                LogUtil.Error("DataGenTool", $"部分数据生成失败！状态: {state.genSuccess}/{state.genTotal}");
            }
            else
                LogUtil.Success("DataGenTool", $"数据代码生成完成！状态: {state.genSuccess}/{state.genTotal}");

            if (state.saveSuccess != state.saveTotal)
            {
                result = false;
                LogUtil.Error("DataExportTool", $"部分数据解析失败！状态: {state.saveSuccess}/{state.saveTotal}");
            }
            else
                LogUtil.Success("DataExportTool", $"数据导出存储完成！状态: {state.saveSuccess}/{state.saveTotal}");

            if (result)
                LogUtil.Success("DataHandleTool", "全部数据处理完成！");
            else
                LogUtil.Warn("DataHandleTool", "数据处理完成，但存在部分失败。");
            EditorApplication.delayCall += AssetDatabase.Refresh;
        }
    }
}
