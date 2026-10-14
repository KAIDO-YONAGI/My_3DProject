using Core.Scripts.Character.Presentation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterVisual))]
public sealed class CharacterVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var visual = (CharacterVisual)target;
        if (!visual.TryValidate(visual.InputCamera != null, out string issue))
            EditorGUILayout.HelpBox(issue, MessageType.Warning);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("保存当前发辫初始偏移"))
            {
                Undo.RecordObject(visual, "保存发辫初始偏移");
                if (visual.CaptureBonePose())
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                    EditorUtility.SetDirty(visual);
                }
                else
                {
                    Debug.LogWarning("请指定头骨和发辫根节点，并在模型初始姿势下保存偏移。", visual);
                }
            }
        }
    }
}
