using System;
using System.IO;
using System.Runtime.Serialization;
using SceneHandling.Utility;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SceneHandling
{
    /// <summary>
    ///     A representation of a <see cref="SceneAsset" /> with additional information and functionality.
    /// </summary>
    /// <remarks>
    ///     <see cref="sceneAsset" /> field is ONLY to be used in Editor. Will be null at build/runtime!
    /// </remarks>
    [Serializable]
    public class ManagedScene
    {
        [SerializeField] private Object sceneAsset;
        [SerializeField] private string guid = GuidUtils.AllZeroGuid;

#if UNITY_EDITOR
        /// <summary>
        ///     Get the <see cref="SceneAsset" /> that is managed by this instance.
        /// </summary>
        /// <remarks>Only available in editor.</remarks>
        [IgnoreDataMember]
        public SceneAsset SceneAsset
        {
            get => sceneAsset as SceneAsset;
            internal set => sceneAsset = value;
        }
#endif

        /// <summary>
        ///     Is this <see cref="ManagedScene" /> assigned something?
        /// </summary>
        [IgnoreDataMember]
        public bool HasValue
        {
            get
            {
                if (!Guid.IsValidGuid())
                {
                    // internal exceptions should not be documented as part of the public API
                    throw new Exception($"GUID is invalid. GUID: {guid}");
                }

                return Guid != GuidUtils.AllZeroGuid;
            }
        }

        /// <summary>
        ///     GUID of the scene asset
        /// </summary>
        public string Guid => guid.GuardGuidAgainstNullOrWhitespace();

        /// <summary>
        ///     Path to the <see cref="SceneAsset" />
        /// </summary>
        [IgnoreDataMember]
        public string ScenePath
        {
            get
            {
                if (!HasValue)
                {
                    throw new Exception($"This {typeof(ManagedScene).Name} is not assigned a scene.");
                }

                if (!SceneGuidToPathMapProvider.GuidToPathMap.TryGetValue(Guid, out string pathFromMap))
                {
                    throw new Exception(
                        $"Given GUID is not found in the scene GUID to path map. GUID: '{guid}'"
                        + "\nThis can happen for these reasons:"
                        + "\n1. The asset with the given GUID either doesn't exist or is not a scene. To fix this, make sure you provide the GUID of a valid scene."
                        + "\n2. The scene GUID to path map is outdated.");
                }

                return pathFromMap;
            }
        }

        /// <summary>
        ///     Name of the scene asset. Without '.unity' extension.
        /// </summary>
        [IgnoreDataMember]
        public string SceneName => Path.GetFileNameWithoutExtension(ScenePath);

        /// <summary>
        ///     Creates a new <see cref="ManagedScene" /> which references the scene that has the given GUID.
        /// </summary>
        /// <param name="guid">GUID of the scene to reference.</param>
        /// <exception cref="Exception">Throws if the given GUID is null or empty.</exception>
        /// <exception cref="Exception">Throws if the given GUID is not found in the Scene GUID to Path map.</exception>
        /// <exception cref="Exception">(Editor-only) Throws if the asset is not found at the path that the GUID corresponds to.</exception>
        public ManagedScene(string guid)
        {
            SetReferencedScene(guid);
        }

        /// <summary>
        ///     Updated the guid (SceneAsset) this <see cref="ManagedScene" /> references.
        /// </summary>
        /// <remarks>Note, this updates the <see cref="sceneAsset" /> field in Editor only!</remarks>
        internal void SetReferencedScene(string newGuid)
        {
            if (string.IsNullOrWhiteSpace(newGuid))
            {
                throw new Exception(
                    $"Given GUID is null or whitespace. GUID: '{newGuid}'." +
                    "\nTo fix this, make sure you provide the GUID of a valid scene.");
            }

            if (!SceneGuidToPathMapProvider.GuidToPathMap.TryGetValue(newGuid, out string pathFromMap))
            {
                throw new Exception(
                    $"Given GUID is not found in the scene GUID to path map. GUID: '{newGuid}'"
                    + "\nThis can happen for these reasons:"
                    + "\n1. The asset with the given GUID either doesn't exist or is not a scene. To fix this, make sure you provide the GUID of a valid scene."
                    + "\n2. The scene GUID to path map is outdated.");
            }

            guid = newGuid;

#if UNITY_EDITOR
            Object foundAsset = AssetDatabase.LoadAssetAtPath<Object>(pathFromMap);

            if (!foundAsset)
            {
                throw new Exception(
                    $"The given GUID was found in the map, but the scene asset at the corresponding path could not be loaded. Path: '{pathFromMap}'."
                    + "\nThis can happen due to an outdated scene GUID to path map retaining scene assets that no longer exist.");
            }

            sceneAsset = foundAsset;
#endif
        }

        public static ManagedScene CreateFromScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new Exception(
                    $"Given path is null or whitespace. Path: '{path}'" +
                    "\nTo fix this, make sure you provide the path of a valid scene.");
            }

            if (!SceneGuidToPathMapProvider.PathToGuidMap.TryGetValue(path, out string guidFromMap))
            {
                throw new Exception(
                    $"Given path is not found in the scene GUID to path map. Path: '{path}'"
                    + "\nThis can happen for these reasons:"
                    + "\n1. The asset at the given path either doesn't exist or is not a scene. To fix this, make sure you provide the path of a valid scene."
                    + "\n2. The scene GUID to path map is outdated.");
            }

            return new ManagedScene(guidFromMap);
        }

        public static implicit operator bool(ManagedScene managedScene)
        {
            return managedScene != null;
        }
    }
}