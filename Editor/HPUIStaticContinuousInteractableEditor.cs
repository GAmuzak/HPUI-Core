using System.Collections.Generic;
using System.Linq;
using ubco.ovilab.HPUI.Interaction;
using UnityEditor;

namespace ubco.ovilab.HPUI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(HPUIStaticContinuousInteractable))]
    public class HPUIStaticContinuousInteractableEditor : HPUIBaseInteractableEditor
    {
        private HPUIStaticContinuousInteractable t;
        private SerializedProperty staticMesh;
        private SerializedProperty meshXResolution;
        private SerializedProperty flippedAndNormalisedCoords;
        
        protected override List<string> EventPropertyNames => base.EventPropertyNames.Union(new List<string>()
        {
            "staticMesh",
            "meshXResolution",
            "flippedAndNormalisedCoords"
        }).ToList();
        
        protected override void OnEnable()
        {
            base.OnEnable();
            t = (HPUIStaticContinuousInteractable)target;
            staticMesh = serializedObject.FindProperty("staticHPUIMesh");
            meshXResolution = serializedObject.FindProperty("meshXResolution");
            flippedAndNormalisedCoords = serializedObject.FindProperty("flippedAndNormalisedCoords");
        }

        protected override void DrawProperties()
        {
            base.DrawProperties();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Static Mesh Configurations", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(staticMesh);
            EditorGUILayout.PropertyField(meshXResolution);
            EditorGUILayout.PropertyField(flippedAndNormalisedCoords);
        }
    }
}