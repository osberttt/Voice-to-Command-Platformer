using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMovement))]
public class PlayerMovementEditor : Editor
{
    PlayerMovement p;

    void OnEnable()
    {
        p = (PlayerMovement)target;
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();

        DrawDefaultInspector();

        if (EditorGUI.EndChangeCheck())
        {
            if (Application.isPlaying)
            {
                p.RecalculateParameters();

                EditorUtility.SetDirty(p);
            }
        }
    }
}