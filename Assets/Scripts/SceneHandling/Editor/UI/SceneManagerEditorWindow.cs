using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Utils.ObservableCollections;
using Debug = UnityEngine.Debug;

namespace SceneHandling.Editor.UI
{
    public class SceneManagerEditorWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset rootView;
        [SerializeField] private VisualTreeAsset sceneTemplate;

        private ListView _sceneAssetListView;
        private ListView _managedScenesListView;
        private ListView _sceneReferenceDependenciesListView;
        private Button _saveButton;
        private Button _loadButton;
        private Button _clearButton;
        private Button _initButton;

        private Button _openSettingsExplorerButton;
        private Button _openSettingsFileButton;

        private Button _removeListItemsButton;
        private Button _printSceneListSizeButton;

        private Toggle _enabledSceneHandling;

        private List<ManagedSceneTemplate> _sceneTemplatesList;

        private List<ManagedSceneReference> _selectedSceneDependencies;

        internal static SceneManagerEditorWindow Instance { get; private set; }

        [MenuItem("Tools/Scene Manager/Open Scene Manager")]
        private static void ShowWindow()
        {
            SceneManagerEditorWindow window = GetWindow<SceneManagerEditorWindow>();
            window.titleContent = new GUIContent("Scene Manager");
        }

        private void CreateGUI()
        {
            Instance = this;

            TemplateContainer template = rootView.Instantiate();
            template.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
            rootVisualElement.Add(template);

            #region Scene Assets View

            _sceneAssetListView = rootVisualElement.Q<ListView>("sceneAssetList");
            _sceneAssetListView.makeItem = () => new ObjectField
            {
                allowSceneObjects = false, objectType = typeof(SceneAsset), style = { paddingBottom = 4 }
            };
            _sceneAssetListView.itemsSource = SceneManagerSettings.Instance.scenes.GetList();
            _sceneAssetListView.bindItem = (element, i) =>
            {
                ((ObjectField)element).value = SceneManagerSettings.Instance.scenes[i];
                element.Q(className: "unity-object-field__input").SetEnabled(false);
            };

            #endregion

            #region Managed Scenes View

            _sceneTemplatesList =
                SceneManagerSettings.Instance.managedScenes?.Select((scene, _) => new ManagedSceneTemplate(scene))
                    ?.ToList() ?? new List<ManagedSceneTemplate>();

            _managedScenesListView = rootVisualElement.Q<ListView>("managedSceneList");
            _managedScenesListView.makeItem = sceneTemplate.Instantiate;
            _managedScenesListView.itemsSource = _sceneTemplatesList;
            _managedScenesListView.bindItem = (element, i) =>
            {
                if (_sceneTemplatesList.ElementAtOrDefault(i) is not ManagedSceneTemplate managedSceneTemplate)
                {
                    managedSceneTemplate = new ManagedSceneTemplate();
                    _sceneTemplatesList[i] = managedSceneTemplate;
                }

                managedSceneTemplate.CreateGUI(element);
            };
            _managedScenesListView.itemsRemoved += indices =>
            {
                foreach (int index in indices)
                {
                    if (_sceneTemplatesList[index] is ManagedSceneTemplate managedSceneTemplate)
                    {
                        managedSceneTemplate.OnDestroy();
                    }
                }
            };
            _managedScenesListView.selectionChanged += objects =>
            {
                _selectedSceneDependencies.Clear();
                foreach (object selectedObj in objects)
                {
                    if (selectedObj is ManagedSceneTemplate managedSceneTemplate && managedSceneTemplate.managedScene)
                    {
                        // int index = _sceneTemplatesList.IndexOf(managedSceneTemplate);

                        Stopwatch timer = Stopwatch.StartNew();
                        var task =
                            SceneDependencyResolver.GetDependantsAsync(managedSceneTemplate.managedScene);
                        task.GetAwaiter().OnCompleted(() =>
                        {
                            foreach (var entry in task.Result)
                            {
                                // Debug.Log($"{entry.Key}: {entry.Value.ListToString()}");
                                foreach (string value in entry.Value)
                                {
                                    _selectedSceneDependencies.Add(new ManagedSceneReference(AssetRefType.Scene,
                                        managedSceneTemplate.managedScene, entry.Key, value));
                                }
                            }

                            timer.Stop();
                            if (_selectedSceneDependencies.Count == 0)
                            {
                                Debug.LogWarning(
                                    $"Could not find any dependencies for {typeof(ManagedScene).Name}: {managedSceneTemplate.managedScene.SceneName}");
                            }

                            _sceneReferenceDependenciesListView.itemsSource = _selectedSceneDependencies;
                            _sceneReferenceDependenciesListView.Rebuild();
                        });

                        // var dependants = SceneDependencyResolver.GetDependants(managedSceneTemplate.managedScene);
                        // foreach (var entry in dependants)
                        // {
                        //     Debug.Log($"{entry.Key}: {entry.Value.ListToString()}");
                        //     foreach (string value in entry.Value)
                        //     {
                        //         _selectedSceneDependencies.Add(new ManagedSceneReference(AssetRefType.Scene,
                        //             managedSceneTemplate.managedScene, entry.Key, value));
                        //     }
                        // }
                        //
                        // if (_selectedSceneDependencies.Count == 0)
                        // {
                        //     Debug.LogWarning(
                        //         $"Could not find any dependencies for {typeof(ManagedScene).Name}: {managedSceneTemplate.managedScene.Name}");
                        // }

                        timer.Stop();
                        Debug.Log($"Elapsed time (ms): {timer.ElapsedMilliseconds}");

                        break;
                    }
                }

                _sceneReferenceDependenciesListView.itemsSource = _selectedSceneDependencies;
                _sceneReferenceDependenciesListView.Rebuild();
            };

            #endregion

            BindListListeners();

            #region Scene Reference Dependencies View

            _selectedSceneDependencies = new List<ManagedSceneReference>();
            _sceneReferenceDependenciesListView = rootVisualElement.Q<ListView>("sceneReferenceList");

            _sceneReferenceDependenciesListView.makeItem = () =>
            {
                VisualElement visualElement = new VisualElement { name = "inputBlocker" };
                visualElement.AddToClassList("collection-view__item--disabled");

                // visualElement.AddManipulator(new Clickable(OnSceneReferenceClicked));
                visualElement.RegisterCallback<MouseDownEvent>(OnSceneReferenceClicked);

                ObjectField objectField = new ObjectField
                {
                    allowSceneObjects = false, objectType = typeof(SceneAsset), style = { paddingBottom = 4 }
                };

                visualElement.Add(objectField);

                return visualElement;
            };
            _sceneReferenceDependenciesListView.bindItem = (element, i) =>
            {
                ((ObjectField)element.hierarchy[0]).value = _selectedSceneDependencies[i].SceneAsset;
                element.hierarchy[0].SetEnabled(false);
                VisualElement inputField = element.hierarchy[0].Q(className: "unity-object-field__object");
                inputField.pickingMode = PickingMode.Ignore;
            };

            #endregion

            #region Toolbar Buttons

            _saveButton = rootVisualElement.Q<Button>("saveSettings");
            _loadButton = rootVisualElement.Q<Button>("reloadSettings");
            _clearButton = rootVisualElement.Q<Button>("clearSettings");
            _initButton = rootVisualElement.Q<Button>("initSettings");

            _saveButton.clicked += () =>
            {
                SceneManagerSettings.Instance.Save();

                ReloadLists();
            };
            _loadButton.clicked += () =>
            {
                SceneManagerSettings.Instance.Load();

                ReloadLists();
            };
            _clearButton.clicked += () =>
            {
                UnbindListListeners();
                SceneManagerSettings.Instance.Create();
                BindListListeners();

                _sceneAssetListView.itemsSource = new List<SceneAsset>();
                _sceneAssetListView.Rebuild();

                _sceneTemplatesList.Clear();
                _managedScenesListView.Rebuild();
            };
            _initButton.clicked += () =>
            {
                UnbindListListeners();
                SceneManagerSettings.Instance.ResetState();
                SceneManagerSettings.Instance.scenes = new ObservableList<SceneAsset>(AssetDatabase
                    .FindAssets("t:SceneAsset")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<SceneAsset>).ToList());
                SceneManagerSettings.Instance.Save();
                BindListListeners();

                ReloadLists();
            };

            _openSettingsExplorerButton = rootVisualElement.Q<Button>("openSettingsExplorer");
            _openSettingsExplorerButton.clicked += () =>
            {
                EditorUtility.RevealInFinder(SceneManagerSettings.Instance.TryGetFilePathDirectory() + "/");
            };

            _openSettingsFileButton = rootVisualElement.Q<Button>("openSettingsFile");
            _openSettingsFileButton.clicked += () =>
            {
                Application.OpenURL(SceneManagerSettings.Instance.TryGetFilePath(true));
            };

            _removeListItemsButton = rootVisualElement.Q<Button>("removeListItems");
            _removeListItemsButton.clicked += () =>
            {
                SceneManagerSettings.Instance.scenes.RemoveRange(0, 10);
                SceneManagerSettings.Instance.Save();

                ReloadLists();
            };

            _printSceneListSizeButton = rootVisualElement.Q<Button>("printSceneListSize");
            _printSceneListSizeButton.clicked += () =>
            {
                Debug.Log($"Scene List Size {SceneManagerSettings.Instance.scenes.Count}");
            };

            #endregion

            #region Footer

            _enabledSceneHandling = rootVisualElement.Q<Toggle>("enabledSceneHandling");
            _enabledSceneHandling.RegisterValueChangedCallback(evt =>
            {
                SceneManagerSettings.Instance.EnableSceneHandling = evt.newValue;
                ToolbarExtender.Repaint();
            });

            #endregion
        }


        private void OnSceneReferenceClicked(EventBase evt)
        {
            if (_sceneReferenceDependenciesListView.selectedItem is ManagedSceneReference managedSceneReference)
            {
                _sceneReferenceDependenciesListView.ClearSelection();
                Scene? scene = null;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    Scene tempScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (tempScene.path.Equals(managedSceneReference.AssetRefPath))
                    {
                        scene = tempScene;
                        break;
                    }
                }

                scene ??= EditorSceneManager.OpenScene(managedSceneReference.AssetRefPath,
                    OpenSceneMode.Additive);

                bool objectRefFound = false;
                foreach (GameObject rootObj in scene.Value.GetRootGameObjects())
                {
                    foreach (Transform transform in rootObj.GetComponentsInChildren<Transform>(true))
                    {
                        if (GlobalObjectId.GetGlobalObjectIdSlow(transform.gameObject).targetObjectId ==
                            ulong.Parse(managedSceneReference.ObjectRefID))
                        {
                            objectRefFound = true;
                            EditorGUIUtility.PingObject(transform.gameObject);
                            break;
                        }
                    }

                    if (objectRefFound)
                    {
                        break;
                    }
                }
            }

            evt.StopImmediatePropagation();
        }

        private void ReloadLists()
        {
            // _sceneTemplatesList =
            //     SceneManagerSettings.Instance.managedScenes?.Select((scene, i) => new ManagedSceneTemplate(scene, i))
            //         .ToList() ?? new List<ManagedSceneTemplate>();
            // _managedScenesListView.itemsSource = _sceneTemplatesList;
            _managedScenesListView.Rebuild();

            _sceneAssetListView.itemsSource = SceneManagerSettings.Instance.scenes.GetList();
            _sceneAssetListView.Rebuild();
        }

        private void ScenesList_CollectionChanged(in NotifyCollectionChangedEventArgs<SceneAsset> evt)
        {
            _sceneAssetListView.itemsSource = SceneManagerSettings.Instance.scenes.GetList();
            _sceneAssetListView.Rebuild();
        }

        private void ManagedScenesList_CollectionChanged(in NotifyCollectionChangedEventArgs<ManagedScene> evt)
        {
            Debug.Log(evt.Action);
            if (evt.Action == NotifyCollectionChangedAction.Remove)
            {
                Debug.Log($"Item removed from ManagedScenes: {evt.OldItem}");
                EditorApplication.delayCall += () => _managedScenesListView.Rebuild();
            }
            else if (evt.Action == NotifyCollectionChangedAction.Add)
            {
                if (evt.IsSingleItem)
                {
                    int index = evt.NewStartingIndex;
                    EditorApplication.delayCall += () => _managedScenesListView.RefreshItem(index);
                }
            }
        }

        private void OnDestroy()
        {
            UnbindListListeners();
        }

        private void BindListListeners()
        {
            SceneManagerSettings.Instance.scenes.CollectionChanged += ScenesList_CollectionChanged;
            SceneManagerSettings.Instance.managedScenes.CollectionChanged += ManagedScenesList_CollectionChanged;
        }

        private void UnbindListListeners()
        {
            SceneManagerSettings.Instance.scenes.CollectionChanged -= ScenesList_CollectionChanged;
            SceneManagerSettings.Instance.managedScenes.CollectionChanged -= ManagedScenesList_CollectionChanged;
        }
    }
}