#if UNITY_EDITOR
using UnityEngine;

[CreateAssetMenu(menuName = "Model Test/Simple Character Auto Config Cache", fileName = "SimpleCharacterAutoConfigCache")]
public sealed class SimpleCharacterAutoConfigCache : ScriptableObject
{
    public const string AssetPath = "Assets/Settings/SimpleCharacterAutoConfigCache.asset";

    [SerializeField] private SimpleCharacterMovementSettings movementSettings = new SimpleCharacterMovementSettings();
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private DirectionalAnimationSet runDirectionalSet = new DirectionalAnimationSet();
    [SerializeField] private DirectionalAnimationSet sprintDirectionalSet = new DirectionalAnimationSet();
    [SerializeField] private SimpleCharacterJumpAnimationSet jumpAnimationSet = new SimpleCharacterJumpAnimationSet();
    [SerializeField] private SimpleCharacterAnimationSettings animationSettings = new SimpleCharacterAnimationSettings();

    public SimpleCharacterMovementSettings MovementSettings => movementSettings;
    public AnimationClip IdleClip => idleClip;
    public DirectionalAnimationSet RunDirectionalSet => runDirectionalSet;
    public DirectionalAnimationSet SprintDirectionalSet => sprintDirectionalSet;
    public SimpleCharacterJumpAnimationSet JumpAnimationSet => jumpAnimationSet;
    public SimpleCharacterAnimationSettings AnimationSettings => animationSettings;
    public bool HasAnimationBindings =>
        idleClip != null
        || (runDirectionalSet != null && runDirectionalSet.HasValidSamples)
        || (sprintDirectionalSet != null && sprintDirectionalSet.HasValidSamples)
        || (jumpAnimationSet != null && jumpAnimationSet.HasAnyClip);

    public static SimpleCharacterAutoConfigCache LoadAssetIfExists()
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<SimpleCharacterAutoConfigCache>(AssetPath);
    }

    public static SimpleCharacterAutoConfigCache LoadOrCreateAsset()
    {
        SimpleCharacterAutoConfigCache cache = LoadAssetIfExists();
        if (cache != null)
        {
            return cache;
        }

        string folderPath = System.IO.Path.GetDirectoryName(AssetPath).Replace("\\", "/");
        EnsureFolder(folderPath);

        cache = CreateInstance<SimpleCharacterAutoConfigCache>();
        UnityEditor.AssetDatabase.CreateAsset(cache, AssetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        return cache;
    }

    public bool EditorOverwriteFrom(
        SimpleCharacterMovementSettings sourceMovementSettings,
        AnimationClip sourceIdleClip,
        DirectionalAnimationSet sourceRunDirectionalSet,
        DirectionalAnimationSet sourceSprintDirectionalSet,
        SimpleCharacterJumpAnimationSet sourceJumpAnimationSet,
        SimpleCharacterAnimationSettings sourceAnimationSettings)
    {
        bool changed = false;
        changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(sourceMovementSettings, movementSettings);

        if (idleClip != sourceIdleClip)
        {
            idleClip = sourceIdleClip;
            changed = true;
        }

        changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(sourceRunDirectionalSet, runDirectionalSet);
        changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(sourceSprintDirectionalSet, sprintDirectionalSet);
        changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(sourceJumpAnimationSet, jumpAnimationSet);
        changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(sourceAnimationSettings, animationSettings);
        return changed;
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        if (string.IsNullOrWhiteSpace(assetFolderPath) || UnityEditor.AssetDatabase.IsValidFolder(assetFolderPath))
        {
            return;
        }

        string[] parts = assetFolderPath.Split('/');
        string currentPath = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string nextPath = currentPath + "/" + parts[index];
            if (!UnityEditor.AssetDatabase.IsValidFolder(nextPath))
            {
                UnityEditor.AssetDatabase.CreateFolder(currentPath, parts[index]);
            }

            currentPath = nextPath;
        }
    }
}
#endif
