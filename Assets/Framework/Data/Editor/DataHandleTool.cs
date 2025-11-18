using System;
using Framework.Utils;
using UnityEditor;
using UnityEngine;

namespace Framework.Data.Editor
{
    /// <summary>
    /// 数据全流程处理工具（清空 → 生成类 → 编译 → 加密 → 汇总打印）
    /// </summary>
    public static class DataHandleTool
    {
        private const string KEY_STATE = "Fink_HandleState";      // 存储最终状态
        private const string KEY_STAGE = "Fink_HandleStage";      // 当前阶段标志
        private const string KEY_GEN_RESULT = "Fink_GenResult";   // 生成阶段记录

        [Serializable]
        private class HandleState
        {
            public int genSuccess, genTotal, saveSuccess, saveTotal;
        }

        private enum Stage
        {
            None = 0,
            Cleared = 1,
            Generated = 2,
            PendingEncrypt = 3,
            Encrypted = 4,
        }

        // ========== 一键处理数据 主函数 ==========
        public static void HandleAllData()
        {
            // 清空加密数据
            DataExportTool.ClearEncryptData();
            EditorPrefs.SetInt(KEY_STAGE, (int)Stage.Cleared);

            // 生成数据类
            var (genSuccess, genTotal) = DataGenTool.GenerateAllData(true);

            // 保存生成结果
            var state = new HandleState { genSuccess = genSuccess, genTotal = genTotal };
            EditorPrefs.SetString(KEY_GEN_RESULT, JsonUtility.ToJson(state));
            EditorPrefs.SetInt(KEY_STAGE, (int)Stage.Generated);

            // 检查是否触发编译
            if (EditorApplication.isCompiling)
            {
                EditorPrefs.SetInt(KEY_STAGE, (int)Stage.PendingEncrypt);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }
            // 若未触发编译，直接执行加密
            DoEncrypt();
        }

        // ========== 编译完成后自动恢复 ==========
        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            if (EditorApplication.isCompiling) return;

            Stage stage = (Stage)EditorPrefs.GetInt(KEY_STAGE, 0);

            switch (stage)
            {
                case Stage.PendingEncrypt:
                    DoEncrypt();
                    break;

                case Stage.Encrypted:
                    PrintFinalLog();
                    break;
                case Stage.None:
                case Stage.Cleared:
                case Stage.Generated:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // ========== 执行加密 ==========
        private static void DoEncrypt()
        {
            var genState = JsonUtility.FromJson<HandleState>(
                EditorPrefs.GetString(KEY_GEN_RESULT, "{}"));

            var (saveSuccess, saveTotal) = DataExportTool.EncryptAllData(true);

            var state = new HandleState
            {
                genSuccess = genState.genSuccess,
                genTotal = genState.genTotal,
                saveSuccess = saveSuccess,
                saveTotal = saveTotal
            };

            EditorPrefs.SetString(KEY_STATE, JsonUtility.ToJson(state));
            EditorPrefs.SetInt(KEY_STAGE, (int)Stage.Encrypted);

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

            LogUtil.Success("DataEncryptTool", "加密数据清空完成！");
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
                LogUtil.Error("DataEncryptTool", $"部分数据解析失败！状态: {state.saveSuccess}/{state.saveTotal}");
            }
            else
                LogUtil.Success("DataEncryptTool", $"数据加密存储完成！状态: {state.saveSuccess}/{state.saveTotal}");

            if (result)
                LogUtil.Success("DataHandleTool", "全部数据处理完成！");
            else
                LogUtil.Warn("DataHandleTool", "数据处理完成，但存在部分失败。");
            EditorApplication.delayCall += AssetDatabase.Refresh;
        }
    }
}
