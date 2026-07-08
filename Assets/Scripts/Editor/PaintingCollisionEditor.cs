#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PaintingCollision))]
public class PaintingCollisionEditor : Editor
{
    Editor materialEditor;

    void OnDisable()
    {
        if (materialEditor != null)
            DestroyImmediate(materialEditor);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        PaintingCollision paintingCollision = (PaintingCollision)target;
        SurfaceMaterial material = paintingCollision.surfaceMaterial;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Surface Material Parameters", EditorStyles.boldLabel);

        if (material == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Surface Material above (e.g. WetLand from Assets/SciptableObjects/).\n" +
                "Create new: Right-click in Project → Create → Fluid → Surface Material",
                MessageType.Info);

            if (GUILayout.Button("Assign WetLand (if present)"))
            {
                string[] guids = AssetDatabase.FindAssets("WetLand t:SurfaceMaterial");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    paintingCollision.surfaceMaterial =
                        AssetDatabase.LoadAssetAtPath<SurfaceMaterial>(path);
                    EditorUtility.SetDirty(paintingCollision);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "WetLand not found",
                        "Could not find WetLand.asset under Assets/SciptableObjects/.",
                        "OK");
                }
            }

            return;
        }

        if (materialEditor == null || materialEditor.target != material)
            CreateCachedEditor(material, null, ref materialEditor);

        EditorGUI.indentLevel++;
        materialEditor.OnInspectorGUI();
        EditorGUI.indentLevel--;

        if (GUI.changed)
        {
            EditorUtility.SetDirty(material);
        }
    }
}
#endif
