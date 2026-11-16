using FinkFramework.Editor.Common;
using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    /// <summary>音频模块项目设置入口，与配置 Inspector 共用编辑和校验逻辑。</summary>
    public sealed class AudioSettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset global;
        private SerializedObject globalObject;
        private UnityEditor.Editor audioEditor;
        private Vector2 scroll;

        public AudioSettingsProvider(string path, SettingsScope scope) : base(path, scope) { }

        /// <summary>创建 Fink Framework 的 Audio 设置页。</summary>
        /// <returns>音频模块的项目设置页。</returns>
        [SettingsProvider]
        public static SettingsProvider CreateProvider() => new AudioSettingsProvider("Project/Fink Framework/Audio", SettingsScope.Project)
        {
            label = "Audio System",
            keywords = new[] { "Fink", "Audio", "Mixer", "Music", "SFX", "音频", "音乐", "音效", "音量" }
        };

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            global = GlobalSettingsEditorLoader.LoadOrCreate();
            globalObject = new SerializedObject(global);
        }

        public override void OnDeactivate()
        {
            if (audioEditor) Object.DestroyImmediate(audioEditor);
            audioEditor = null;
        }

        public override void OnGUI(string searchContext)
        {
            if (!global)
            {
                OnActivate(searchContext, null);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(10);
            FFEditorGUI.Center(() => GUILayout.Label("音频配置 (Audio Settings)", FFEditorStyles.Title));
            GUILayout.Space(8);
            EditorGUILayout.LabelField("配置音频模块开关、Mixer，以及全局、音乐和音效的输出分组。", FFEditorStyles.Description);
            GUILayout.Space(12);

            globalObject.Update();
            EditorGUILayout.PropertyField(globalObject.FindProperty("AudioSettings"), new GUIContent("音频配置资产"));
            if (globalObject.ApplyModifiedProperties()) AssetDatabase.SaveAssets();
            var audio = AudioSettingsEditorLoader.GetOrCreateAudioSettings(global);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("定位配置资产")) EditorGUIUtility.PingObject(audio);
            if (GUILayout.Button("定位 Mixer"))
            {
                var mixer = audio.CustomMixer ? audio.CustomMixer : Resources.Load<AudioMixer>("FinkFramework/Audio/MasterMixer");
                if (mixer) EditorGUIUtility.PingObject(mixer);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            UnityEditor.Editor.CreateCachedEditor(audio, typeof(AudioSettingsAssetEditor), ref audioEditor);
            audioEditor.OnInspectorGUI();
            GUILayout.Space(20);
            FFEditorGUI.Center(() => GUILayout.Label("Copyright © 2025 Fink Framework", FFEditorStyles.Footer));
            EditorGUILayout.EndScrollView();
        }
    }
}
