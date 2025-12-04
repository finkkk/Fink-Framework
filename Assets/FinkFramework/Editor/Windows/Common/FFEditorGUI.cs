using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Windows.Common
{
    public static class FFEditorGUI
    {
        public static void Center(System.Action content)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            content?.Invoke();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public static void Separator(float thickness = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, thickness);
            Color c = EditorGUIUtility.isProSkin
                ? new Color(0.3f, 0.3f, 0.3f)
                : new Color(0.6f, 0.6f, 0.6f);
            EditorGUI.DrawRect(rect, new Color(0.6f, 0.6f, 0.6f));
        }
    }
}