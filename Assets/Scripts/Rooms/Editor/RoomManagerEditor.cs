using System;
using System.Collections.Generic;
using System.Reflection;
using Interactables;
using ScriptableObjects.Rooms;
using UnityEditor;
using UnityEngine;

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
        private SerializedProperty _currentRoomProp;

        private int _selectedIndex;
        private string[] _availableOptions = {};
        private string[] _availableOptionsNicified = {};

        private MethodInfo _playerToRoomMethodInfo;

        private void OnEnable()
        {
            _roomLoadWaitTimeProp = serializedObject.FindProperty("roomLoadWaitTime");
            _roomLoggingProp = serializedObject.FindProperty("roomLogging");

            _startingRoomProp = serializedObject.FindProperty("startingRoom");
            _startingRoomEnteringFromProp = serializedObject.FindProperty("startingRoomEnteringFrom");
            _loadRoomTypeProp = serializedObject.FindProperty("loadRoomType");

            _roomsProp = serializedObject.FindProperty("rooms");
            _currentRoomProp = serializedObject.FindProperty("currentRoom");

            (_availableOptions, _availableOptionsNicified) =
                GetAvailableRooms((RoomType)_startingRoomProp.enumValueIndex);
            _selectedIndex = EditorPrefs.GetInt("RoomManager_StartingRoomSelectedDoor");
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
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(_startingRoomProp);

                if (EditorGUI.EndChangeCheck())
                {
                    (_availableOptions, _availableOptionsNicified) =
                        GetAvailableRooms((RoomType)_startingRoomProp.enumValueIndex);

                    Enum.TryParse(_availableOptions[0], out RoomType startingDoor);
                    _startingRoomEnteringFromProp.enumValueIndex = (int)startingDoor;
                    _selectedIndex = 0;
                }

                if (_availableOptions.Length <= 0)
                {
                    GUIStyle labelStyle = new GUIStyle();
                    labelStyle.alignment = TextAnchor.MiddleCenter;
                    labelStyle.normal.textColor = Color.red;
                    EditorGUILayout.LabelField("Room does not have any doors! Cannot spawn here!", labelStyle);
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    _selectedIndex = EditorGUILayout.Popup(_startingRoomEnteringFromProp.displayName,
                        _selectedIndex, _availableOptionsNicified);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Enum.TryParse(_availableOptions[_selectedIndex], out RoomType startingDoor);
                        _startingRoomEnteringFromProp.enumValueIndex = (int)startingDoor;
                    }
                }
            }

            EditorGUILayout.PropertyField(_roomLoggingProp);

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);

            EditorGUILayout.PropertyField(_roomsProp);
            EditorGUI.BeginDisabledGroup(true);
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
    }
}