using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Interactables;
using ScriptableObjects.Rooms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rooms.Editor
{
    [CustomEditor(typeof(RoomManager))]
    public class RoomManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _roomLoadWaitTimeProp;
        private SerializedProperty _roomLoggingProp;

        private SerializedProperty _startingRoomProp;
        private SerializedProperty _startingRoomEnteringFromProp;
        private SerializedProperty _loadRoomTypeProp;

        private SerializedProperty _roomsProp;
        private SerializedProperty _roomConfigsProp;
        private SerializedProperty _currentRoomProp;

        private int _selectedIndex;
        private readonly string[] _availableOptions = {};
        private readonly string[] _availableOptionsNicified = {};

        private MethodInfo _playerToRoomMethodInfo;

        private void OnEnable()
        {
            _roomLoadWaitTimeProp = serializedObject.FindProperty("roomLoadWaitTime");
            _roomLoggingProp = serializedObject.FindProperty("roomLogging");

            _startingRoomProp = serializedObject.FindProperty("startingRoom");
            _startingRoomEnteringFromProp = serializedObject.FindProperty("startingRoomEnteringFrom");
            _loadRoomTypeProp = serializedObject.FindProperty("loadRoomType");

            _roomsProp = serializedObject.FindProperty("rooms");
            _roomConfigsProp = serializedObject.FindProperty("roomConfigs");
            _currentRoomProp = serializedObject.FindProperty("currentRoom");

            // (_availableOptions, _availableOptionsNicified) =
            //     GetAvailableRooms((RoomType)_startingRoomProp.enumValueIndex);
            _selectedIndex = EditorPrefs.GetInt("RoomManager_StartingRoomSelectedDoor");

            FindRoomConfigs();
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt("RoomManager_StartingRoomSelectedDoor", _selectedIndex);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_roomLoadWaitTimeProp);

            EditorGUILayout.PropertyField(_loadRoomTypeProp);
            if (_loadRoomTypeProp.enumValueIndex == (int)InitRoomLoadType.StartingRoom)
            {
                // EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(_startingRoomProp);
                EditorGUILayout.PropertyField(_startingRoomEnteringFromProp);

                // if (EditorGUI.EndChangeCheck())
                // {
                //     (_availableOptions, _availableOptionsNicified) =
                //         GetAvailableRooms((RoomType)_startingRoomProp.enumValueIndex);
                //
                //     Enum.TryParse(_availableOptions[0], out RoomType startingDoor);
                //     _startingRoomEnteringFromProp.enumValueIndex = (int)startingDoor;
                //     _selectedIndex = 0;
                // }

                // if (_availableOptions.Length <= 0)
                // {
                //     GUIStyle labelStyle = new GUIStyle();
                //     labelStyle.alignment = TextAnchor.MiddleCenter;
                //     labelStyle.normal.textColor = Color.red;
                //     EditorGUILayout.LabelField("Room does not have any doors! Cannot spawn here!", labelStyle);
                // }
                // else
                // {
                // EditorGUI.BeginChangeCheck();
                // _selectedIndex = EditorGUILayout.Popup(_startingRoomEnteringFromProp.displayName,
                //     _selectedIndex, _availableOptionsNicified);
                // if (EditorGUI.EndChangeCheck())
                // {
                //     Enum.TryParse(_availableOptions[_selectedIndex], out RoomType startingDoor);
                //     _startingRoomEnteringFromProp.enumValueIndex = (int)startingDoor;
                // }
                // }
            }

            EditorGUILayout.PropertyField(_roomLoggingProp);

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_roomConfigsProp);
            EditorGUILayout.PropertyField(_currentRoomProp);
            EditorGUI.BeginDisabledGroup(false);

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            foreach (RoomType roomType in Enum.GetValues(typeof(RoomType)))
            {
                if (GUILayout.Button("Player  ->  " + ObjectNames.NicifyVariableName(roomType.ToString())))
                {
                    if (_playerToRoomMethodInfo == null)
                    {
                        _playerToRoomMethodInfo = ((RoomManager)serializedObject.targetObject).GetType()
                            .GetMethod("PlayerToRoom", BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    _playerToRoomMethodInfo?.Invoke(serializedObject.targetObject, new object[] { roomType });
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private Tuple<string[], string[]> GetAvailableRooms(RoomType selectedRoom)
        {
            RoomManager roomManager = (RoomManager)serializedObject.targetObject;

            var availableRooms = new List<string>();
            var availableRoomsNicified = new List<string>();
            if (roomManager)
            {
                Room currentRoom = roomManager.GetRoom(selectedRoom);

                if (currentRoom)
                {
                    foreach (Door door in currentRoom.Doors())
                    {
                        availableRooms.Add(door.GetConnectingRoom().ToString());
                        availableRoomsNicified.Add(ObjectNames.NicifyVariableName(door.GetConnectingRoom().ToString()));
                    }
                }
            }

            return new Tuple<string[], string[]>(availableRooms.ToArray(), availableRoomsNicified.ToArray());
        }

        private void FindRoomConfigs()
        {
            var foundAssets = FindAssetsByType<RoomConfig>(new[] { "Assets/Scriptable Objects/Rooms" });
            if (foundAssets == null || !foundAssets.Any())
            {
                Debug.LogWarning(
                    "RoomConfigs not found at path 'Assets/Scriptable Objects/Rooms'. Were they moved? Attempting to search the entire asset database instead...");
                foundAssets = FindAssetsByType<RoomConfig>();
                if (foundAssets == null || !foundAssets.Any())
                {
                    Debug.LogError(
                        "RoomConfigs cannot be found! Are they missing from the project? RoomManager cannot function without them!");
                    return;
                }
            }


            _roomConfigsProp.ClearArray();
            int i = 0;
            foreach (RoomConfig asset in foundAssets)
            {
                _roomConfigsProp.InsertArrayElementAtIndex(i);
                _roomConfigsProp.GetArrayElementAtIndex(i).objectReferenceValue = asset;

                i++;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private IEnumerable<T> FindAssetsByType<T>(string[] foldersToSearch = null) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T)}", foldersToSearch);
            foreach (string t in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(t);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset)
                {
                    yield return asset;
                }
            }
        }
    }
}