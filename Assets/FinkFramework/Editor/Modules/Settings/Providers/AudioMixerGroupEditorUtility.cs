using System;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine.Audio;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    /// <summary>只读列出指定 Mixer 的分组层级，不登记额外音轨。</summary>
    internal static class AudioMixerGroupEditorUtility
    {
        internal sealed class Entry
        {
            internal AudioMixerGroup Group;
            internal AudioMixerGroup Parent;
            internal string Path;
        }

        internal static List<Entry> Read(AudioMixer mixer)
        {
            var result = new List<Entry>();
            if (!mixer) return result;
            var master = new SerializedObject(mixer).FindProperty("m_MasterGroup")?.objectReferenceValue as AudioMixerGroup;
            var visited = new HashSet<AudioMixerGroup>();
            void Add(AudioMixerGroup group, string parent, AudioMixerGroup parentGroup)
            {
                if (!group || group.audioMixer != mixer || !visited.Add(group)) return;
                string path = string.IsNullOrEmpty(parent) ? group.name : parent + "/" + group.name;
                result.Add(new Entry { Group = group, Parent = parentGroup, Path = path });
                var children = new SerializedObject(group).FindProperty("m_Children");
                if (children == null || !children.isArray) return;
                for (int i = 0; i < children.arraySize; i++)
                    Add(children.GetArrayElementAtIndex(i).objectReferenceValue as AudioMixerGroup, path, group);
            }
            Add(master, string.Empty, null);
            foreach (var group in mixer.FindMatchingGroups(string.Empty)) Add(group, string.Empty, null);
            foreach (var duplicate in result.GroupBy(g => g.Path, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                foreach (var entry in duplicate)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(entry.Group, out string _, out long localId);
                    entry.Path += $" [{localId}]";
                }
            return result;
        }

        internal static bool IsWithin(List<Entry> groups, AudioMixerGroup master, AudioMixerGroup group)
        {
            if (!master || !group || !groups.Any(entry => entry.Group == master)
                || !groups.Any(entry => entry.Group == group)) return false;
            var visited = new HashSet<AudioMixerGroup>();
            while (group && visited.Add(group))
            {
                if (group == master) return true;
                group = groups.Find(entry => entry.Group == group)?.Parent;
            }
            return false;
        }

        internal static AudioMixerGroup FindCommonParent(List<Entry> groups, AudioMixerGroup music, AudioMixerGroup sound)
        {
            var visited = new HashSet<AudioMixerGroup>();
            while (music && visited.Add(music))
            {
                if (IsWithin(groups, music, sound)) return music;
                music = groups.Find(entry => entry.Group == music)?.Parent;
            }
            return null;
        }

        internal static bool ValidateHierarchy(AudioSettingsAsset audio, out string error)
        {
            error = null;
            if (!audio || !audio.EnableAudioModule || !audio.CustomMixer) return true;
            var groups = Read(audio.CustomMixer);
            if (!IsWithin(groups, audio.MasterGroup, audio.MusicGroup))
                error = "音乐输出分组必须是全局输出分组本身或其子分组，请在 Mixer 中调整层级或重新选择输出。";
            else if (!IsWithin(groups, audio.MasterGroup, audio.SoundGroup))
                error = "音效输出分组必须是全局输出分组本身或其子分组，请在 Mixer 中调整层级或重新选择输出。";
            return error == null;
        }
    }
}
