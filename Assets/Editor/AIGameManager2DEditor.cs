using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIGameManager2D))]
public class AIGameManager2DEditor : Editor
{
    SerializedProperty _nodes;
    SerializedProperty _links;

    void OnEnable()
    {
        _nodes = serializedObject.FindProperty("_navigationNodes");
        _links = serializedObject.FindProperty("_navigationLinks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        DrawPropertiesExcluding(serializedObject, "m_Script", "_navigationNodes", "_navigationLinks");

        bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUI.DisabledScope(playing))
        {
            EditorGUILayout.PropertyField(_nodes, new GUIContent("Nodos de navegación"), true);
            EditorGUILayout.PropertyField(_links, new GUIContent("Conexiones entre nodos"), true);
        }

        EditorGUILayout.HelpBox(playing
            ? "Salí de Play Mode para editar los nodos y sus conexiones."
            : "Arrastrá los nodos numerados en Scene. Cada conexión une los nodos X e Y en ambos sentidos.",
            MessageType.Info);

        if (serializedObject.ApplyModifiedProperties())
            SceneView.RepaintAll();
    }

    void OnSceneGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        serializedObject.Update();
        Vector3 origin = ((AIGameManager2D)target).transform.position;
        using (new Handles.DrawingScope(Color.cyan))
        {
            for (int i = 0; i < _links.arraySize; i++)
            {
                Vector2Int link = _links.GetArrayElementAtIndex(i).vector2IntValue;
                if ((uint)link.x >= _nodes.arraySize || (uint)link.y >= _nodes.arraySize)
                    continue;

                Vector2 from = _nodes.GetArrayElementAtIndex(link.x).vector2Value;
                Vector2 to = _nodes.GetArrayElementAtIndex(link.y).vector2Value;
                Handles.DrawLine(origin + (Vector3)from, origin + (Vector3)to);
            }
        }

        using (new Handles.DrawingScope(Color.magenta))
        {
            for (int i = 0; i < _nodes.arraySize; i++)
            {
                SerializedProperty node = _nodes.GetArrayElementAtIndex(i);
                Vector3 position = origin + (Vector3)node.vector2Value;
                float size = HandleUtility.GetHandleSize(position) * 0.08f;

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider2D(position, Vector3.forward,
                    Vector3.right, Vector3.up, size, Handles.DotHandleCap, Vector2.zero);
                if (EditorGUI.EndChangeCheck())
                    node.vector2Value = (Vector2)(moved - origin);

                Handles.Label(moved + Vector3.right * size, $"Nodo {i}");
            }
        }

        if (serializedObject.ApplyModifiedProperties())
            Repaint();
    }
}
