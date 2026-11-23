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
        EditorGUILayout.PropertyField(skills.FindPropertyRelative("Q"), new GUIContent("Q · 그림자 가위", "조준 거리, 가위의 공격 범위와 타이밍, 적중 후 공격 속도 증가를 설정합니다."), true);
        EditorGUILayout.PropertyField(skills.FindPropertyRelative("W"), new GUIContent("W · 영감 표식", "표식을 붙일 수 있는 거리, 완성까지 걸리는 시간, 폭발 가능 시간을 설정합니다."), true);
        EditorGUILayout.PropertyField(skills.FindPropertyRelative("E"), new GUIContent("E · 그림자 돌진", "E1의 돌진과 은신, E2의 발동 사거리와 착지 거리를 설정합니다."), true);
        EditorGUILayout.PropertyField(skills.FindPropertyRelative("R"), new GUIContent("R · 걸작", "R1의 진입 사거리와 R2의 탈출 거리 및 시간을 설정합니다."), true);
        EditorGUIUtility.labelWidth = previousLabelWidth;
        serializedObject.ApplyModifiedProperties();

        if (!GUILayout.Button(new GUIContent("스킬 기본값으로 되돌리기", "Q·W·E·R 설정을 프로젝트 기본값으로 복원합니다. 위쪽의 시작 스킬 레벨은 별도 설정입니다."))) return;
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
