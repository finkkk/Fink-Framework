using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Settings.Loaders
{
    public static class ResBackendSettingsEditorLoader
    {
         private const string BaseDir = "Assets/FinkFramework/Runtime/Resources/FinkFramework/Settings/ResBackends";

         public static AssetBundleBackendSettingsAsset GetOrCreateAssetBundleSettings(GlobalSettingsAsset global)
         {
             EnsureBaseDir();

             if (!global.AssetBundleSettings)
             {
                 global.AssetBundleSettings =
                     LoadOrCreate<AssetBundleBackendSettingsAsset>(
                         "AssetBundleBackendSettings.asset");

                 EditorUtility.SetDirty(global);
             }

             return global.AssetBundleSettings;
         }

         public static AddressablesBackendSettingsAsset
             GetOrCreateAddressablesSettings(GlobalSettingsAsset global)
         {
             EnsureBaseDir();

             if (!global.AddressablesSettings)
             {
                 global.AddressablesSettings =
                     LoadOrCreate<AddressablesBackendSettingsAsset>(
                         "AddressablesBackendSettings.asset");

                 EditorUtility.SetDirty(global);
             }

             return global.AddressablesSettings;
         }
         
         private static T LoadOrCreate<T>(string fileName)
             where T : ScriptableObject
         {
             string path = $"{BaseDir}/{fileName}";
             var asset = AssetDatabase.LoadAssetAtPath<T>(path);

             if (!asset)
             {
                 asset = ScriptableObject.CreateInstance<T>();
                 AssetDatabase.CreateAsset(asset, path);
                 AssetDatabase.SaveAssets();
             }

             return asset;
         }

         private static void EnsureBaseDir()
         {
             if (!AssetDatabase.IsValidFolder(BaseDir))
             {
                 AssetDatabase.CreateFolder(
                     "Assets/FinkFramework/Runtime/Resources/FinkFramework/Settings",
                     "ResBackends");
             }
         }
    }
}