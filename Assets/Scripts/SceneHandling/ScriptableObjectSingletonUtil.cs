using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

// Adapted from: https://forum.unity.com/threads/how-to-add-custom-project-settings-to-your-project.857419/#post-7561627
// and subsequently: https://pastebin.com/W8Q12q7K
namespace SceneHandling
{
    public static class ScriptableObjectSingletonUtil
    {
#if UNITY_EDITOR
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        // private static void OnLoad()
        // {
        //     // Load all persistent ScriptableObject Singletons at runtime.
        //     LoadAll();
        // }
        //
        // public static void LoadAll()
        // {
        //     foreach (Type type in FindAllPersistentTypes())
        //     {
        //         CreateOrLoad(type);
        //     }
        // }

        private static IEnumerable<Type> FindAllPersistentTypes()
        {
            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<FilePathAttribute>();

            foreach (Type type in types)
            {
                if (typeof(ScriptableObjectSingletonBase).IsAssignableFrom(type) == false)
                {
                    Debug.LogWarning(
                        $"Type {type} needs to inherit from {typeof(ScriptableObjectSingletonBase)} in order to Accept the attribute {typeof(FilePathAttribute)}!");
                    continue;
                }

                yield return type;
            }
        }

        public static ScriptableObjectSingletonBase CreateOrLoad(Type type)
        {
            // Debug.Log($"Creating or Loading {type}");
            ScriptableObjectSingletonBase asset = Load(type);
            if (asset == null)
            {
                asset = Create(type);
            }

            return asset;
        }

        internal static ScriptableObjectSingletonBase Create(Type type)
        {
            Debug.Log($"Creating {type}");
            ScriptableObjectSingletonBase
                asset = ScriptableObject.CreateInstance(type) as ScriptableObjectSingletonBase;

            Setup(asset);
            Save(asset);
            return asset;
        }

        internal static ScriptableObjectSingletonBase Load(Type type)
        {
            string filePath = GetFilePath(type);
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            Debug.Log($"Loading {type}");

            ScriptableObjectSingletonBase asset =
                InternalEditorUtility.LoadSerializedFileAndForget(filePath).FirstOrDefault() as
                    ScriptableObjectSingletonBase;

            if (asset == null)
            {
                return null;
            }

            Setup(asset);
            return asset;
        }

        public static void Save(ScriptableObjectSingletonBase asset)
        {
            Type type = asset.GetType();
            string filePath = GetFilePath(type);

            if (!string.IsNullOrEmpty(filePath))
            {
                Debug.Log($"Saving {type.Name}");
                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName ?? string.Empty);
                }

                InternalEditorUtility.SaveToSerializedFileAndForget(new Object[]
                {
                    asset
                }, filePath, true);
            }
            else
            {
                Debug.LogWarning(
                    $"Saving has no effect. Your class '{type}' is missing the FilePathAttribute. Use this attribute to specify where to save your ScriptableSingleton.\nOnly call Save() and use this attribute if you want your state to survive between sessions of Unity.");
            }
        }

        private static string GetFilePath(Type type)
        {
            return FilePathAttribute.Retrieve(type).Filepath;
        }

        private static void Setup(ScriptableObjectSingletonBase asset)
        {
            asset.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
        }

        public class BuildPreProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
        {
            public int callbackOrder => -200;

            private const string TempBuildFolder = "Assets/SceneManager_BuildStep";

            public void OnPreprocessBuild(BuildReport report)
            {
                if (!Directory.Exists(TempBuildFolder))
                {
                    Directory.CreateDirectory(TempBuildFolder);
                }

                var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();

                foreach (Type type in FindAllPersistentTypes())
                {
                    FilePathAttribute attribute = type.GetCustomAttribute<FilePathAttribute>();
                    if (attribute.Scope == FilePathAttribute.UsageScope.EditorOnly)
                    {
                        continue;
                    }

                    string filePath = attribute.Filepath;
                    ScriptableObjectSingletonBase asset = CreateOrLoad(type);
                    if (asset.IncludeInBuild == false)
                    {
                        continue;
                    }

                    asset.hideFlags = HideFlags.None;
                    AssetDatabase.CreateAsset(asset, filePath);

                    preloadedAssets.Add(asset);
                }

                PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
            }

            public void OnPostprocessBuild(BuildReport report)
            {
                var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();

                foreach (Type type in FindAllPersistentTypes())
                {
                    FilePathAttribute attribute = type.GetCustomAttribute<FilePathAttribute>();
                    if (attribute.Scope == FilePathAttribute.UsageScope.EditorOnly)
                    {
                        continue;
                    }

                    string filePath = attribute.Filepath;
                    ScriptableObjectSingletonBase asset =
                        AssetDatabase.LoadAssetAtPath<ScriptableObjectSingletonBase>(filePath);

                    preloadedAssets.Remove(asset);
                    AssetDatabase.DeleteAsset(filePath);
                }

                PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());

                Directory.Delete(TempBuildFolder);
                File.Delete(TempBuildFolder + ".meta");

                AssetDatabase.Refresh();
            }
        }
#endif
    }
}