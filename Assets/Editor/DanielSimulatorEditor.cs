using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(DanielSimulator)), CanEditMultipleObjects]
internal sealed class DanielSimulatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "skills");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("다니엘 스킬 설정", EditorStyles.boldLabel);

        var skills = serializedObject.FindProperty("skills");
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = Mathf.Clamp(EditorGUIUtility.currentViewWidth * .58f, 150f, 270f);
        EditorGUILayout.PropertyField(skills.FindPropertyRelative("Q"), new GUIContent("Q · 그림자 가위"), true);
        EditorGUILayout.PropertyField(skills.FindPropertyRelative("W"), new GUIContent("W · 영감 표식"), true);
        EditorGUILayout.PropertyField(skills.FindPropertyRelative("E"), new GUIContent("E · 그림자 돌진"), true);
        EditorGUILayout.PropertyField(skills.FindPropertyRelative("R"), new GUIContent("R · 걸작"), true);
        EditorGUIUtility.labelWidth = previousLabelWidth;
        serializedObject.ApplyModifiedProperties();

        if (!GUILayout.Button("스킬 기본값으로 되돌리기")) return;
        Undo.RecordObjects(targets, "Reset Daniel skill settings");
        foreach (DanielSimulator simulator in targets)
        {
            simulator.ResetSkillSettings();
            PrefabUtility.RecordPrefabInstancePropertyModifications(simulator);
            EditorUtility.SetDirty(simulator);
        }
        serializedObject.Update();
    }
}

[InitializeOnLoad]
internal static class DanielArenaPreview
{
    const HideFlags PreviewFlags = HideFlags.DontSave | HideFlags.NotEditable;
    static readonly HashSet<Object> assets = new HashSet<Object>();
    static GameObject root;
    static DanielSimulator owner;

    static DanielArenaPreview()
    {
        EditorSceneManager.sceneOpened += (_, _) => QueueRefresh();
        EditorSceneManager.activeSceneChangedInEditMode += (_, _) => QueueRefresh();
        EditorApplication.hierarchyChanged += QueueRefresh;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        EditorApplication.quitting += Clear;
        QueueRefresh();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) Clear();
        if (state == PlayModeStateChange.EnteredEditMode) QueueRefresh();
    }

    static void QueueRefresh()
    {
        EditorApplication.delayCall -= Refresh;
        EditorApplication.delayCall += Refresh;
    }

    static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var scene = SceneManager.GetActiveScene();
        if (owner && root && owner.gameObject.scene == scene && owner.isActiveAndEnabled) return;
        Clear();
        if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) return;

        foreach (var simulator in Object.FindObjectsByType<DanielSimulator>(FindObjectsSortMode.None))
        {
            if (simulator.gameObject.scene != scene || !simulator.isActiveAndEnabled) continue;
            owner = simulator;
            break;
        }
        if (!owner) return;

        root = new GameObject("Daniel Arena Preview") { hideFlags = PreviewFlags };
        SceneManager.MoveGameObjectToScene(root, scene);
        var builder = root.AddComponent<DanielSimulator>();
        foreach (var material in builder.BuildEditorPreview()) Track(material);
        Object.DestroyImmediate(builder);
        foreach (var item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.hideFlags = PreviewFlags;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials) Track(material);
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) Track(filter.sharedMesh);
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    static void Track(Object asset)
    {
        if (!asset || EditorUtility.IsPersistent(asset)) return;
        assets.Add(asset);
        asset.hideFlags = PreviewFlags;
    }

    static void Clear()
    {
        EditorApplication.delayCall -= Refresh;
        if (root) Object.DestroyImmediate(root);
        foreach (var asset in assets)
            if (asset) Object.DestroyImmediate(asset);
        assets.Clear();
        root = null;
        owner = null;
    }
}
