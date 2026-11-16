using System.Collections.Generic;
using FinkFramework.Editor.Common;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Audio;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    /// <summary>音乐和音效的共用配置界面，输出只列出当前 Mixer 分组。</summary>
    [CustomEditor(typeof(AudioSettingsAsset))]
    internal sealed class AudioSettingsAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty enabledProperty, mixerProperty, masterProperty, musicProperty, soundProperty;
        private List<AudioMixerGroupEditorUtility.Entry> groups;
        private AudioMixer currentMixer;
        private bool dirty = true;
        private bool validationDirty = true;
        private bool initialGroupCheck = true;
        private string validationError;

        private void OnEnable()
        {
            enabledProperty = serializedObject.FindProperty("EnableAudioModule");
            mixerProperty = serializedObject.FindProperty("CustomMixer");
            masterProperty = serializedObject.FindProperty("MasterGroup");
            musicProperty = serializedObject.FindProperty("MusicGroup");
            soundProperty = serializedObject.FindProperty("SoundGroup");
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
        }

        private void Invalidate()
        {
            dirty = validationDirty = true;
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawModuleSettings();
            GUILayout.Space(FFEditorStyles.SectionSpacing);

            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(mixerProperty, new GUIContent("自定义 Mixer"));
            bool mixerChanged = EditorGUI.EndChangeCheck();
            var mixer = mixerProperty.objectReferenceValue as AudioMixer;
            if (dirty || groups == null || currentMixer != mixer)
            {
                groups = AudioMixerGroupEditorUtility.Read(mixer);
                currentMixer = mixer;
                dirty = false;
            }
            if (mixer)
            {
                if ((initialGroupCheck && !masterProperty.objectReferenceValue) || mixerChanged)
                    masterProperty.objectReferenceValue = AudioMixerGroupEditorUtility.FindCommonParent(groups,
                        musicProperty.objectReferenceValue as AudioMixerGroup, soundProperty.objectReferenceValue as AudioMixerGroup)
                        ?? groups.Find(entry => !entry.Parent)?.Group;
                DrawGroupPopup(masterProperty, "全局输出分组");
                DrawGroupPopup(musicProperty, "音乐输出分组");
                DrawGroupPopup(soundProperty, "音效输出分组");
                if (GUILayout.Button("刷新分组列表")) Invalidate();
            }
            else EditorGUILayout.HelpBox("使用框架内置 Mixer 的 Master / Music / SFX 分组。", MessageType.Info);
            initialGroupCheck = false;
            EditorGUILayout.HelpBox("音乐和音效输出须位于全局分组内。播放器音量＝全局音量 × 音乐或音效音量，无需暴露 Mixer 参数。其他分组、效果和 Snapshot 由用户自行管理。", MessageType.Info);
            if (serializedObject.ApplyModifiedProperties())
            {
                if (Application.isPlaying) AudioManager.ShutdownIfDisabled();
                validationDirty = true;
                AssetDatabase.SaveAssetIfDirty(target);
            }
            if (validationDirty) ValidateConfiguration();
            if (!string.IsNullOrEmpty(validationError)) EditorGUILayout.HelpBox(validationError, MessageType.Error);
            GUILayout.EndVertical();
        }

        private void DrawModuleSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader("模块配置", "控制框架音乐和音效播放服务是否启用。");
            enabledProperty.boolValue = EditorGUILayout.Toggle(
                new GUIContent("启用音频模块", "关闭时不创建音频服务、不注册帧回调、不加载播放资源。"),
                enabledProperty.boolValue);
            if (!enabledProperty.boolValue) EditorGUILayout.HelpBox("音频模块已关闭。", MessageType.Info);
            GUILayout.EndVertical();
        }

        private void DrawGroupPopup(SerializedProperty property, string label)
        {
            var options = property == masterProperty ? groups : groups.FindAll(entry => AudioMixerGroupEditorUtility.IsWithin(
                groups, masterProperty.objectReferenceValue as AudioMixerGroup, entry.Group));
            var group = property.objectReferenceValue as AudioMixerGroup;
            int selected = options.FindIndex(g => g.Group == group) + 1;
            var labels = new List<string> { selected == 0 && group ? $"不在当前输出范围：{group.name}" : "请选择分组" };
            foreach (var entry in options) labels.Add(entry.Path);
            int next = EditorGUILayout.Popup(label, selected, labels.ToArray());
            if (next != selected) property.objectReferenceValue = next == 0 ? null : options[next - 1].Group;
        }

        private void ValidateConfiguration()
        {
            validationDirty = false;
            validationError = null;
            var audio = (AudioSettingsAsset)target;
            if (!audio.EnableAudioModule) return;
            var global = CreateInstance<GlobalSettingsAsset>();
            try
            {
                global.AudioSettings = audio;
                var fallback = audio.CustomMixer ? null : Resources.Load<AudioMixer>("FinkFramework/Audio/MasterMixer");
                if (AudioSettingsRuntimeLoader.TryResolve(global, fallback, out _, out validationError))
                    AudioMixerGroupEditorUtility.ValidateHierarchy(audio, out validationError);
            }
            finally { DestroyImmediate(global); }
        }
    }
}
