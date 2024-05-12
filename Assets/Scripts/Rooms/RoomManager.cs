using System;
using System.Collections;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Interactables;
using Lights;
using MyBox;
using SceneHandling;
using ScriptableObjects.Rooms;
using UnityEngine;
using Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rooms
{
    public enum InitRoomLoadType
    {
        None,
        StartingRoom,
        RoomContainingPlayer
    }

    public class RoomManager : MonoBehaviour
    {
        [SerializeField] private float roomLoadWaitTime;

        [Separator("Room Settings")]
        [SerializeField] private InitRoomLoadType loadRoomType;
        [SerializeField] private RoomType startingRoom;
        [SerializeField] private RoomType startingRoomEnteringFrom;

        [SerializeField] private bool roomLogging;

        [SerializeField] private List<Room> rooms;
        [ReadOnly] [SerializeField] private Room currentRoom;

        private SerializedDictionary<RoomType, RoomConfig> _roomTypeToRoomConfig;
        private bool _switchingInProgress;

        private void Awake()
        {
            Room.OnRoomCreate += OnRoomLoaded;
            Room.OnRoomDestroy += OnRoomDestroy;
        }

        private void OnRoomLoaded(Room loadedRoom)
        {
            bool containsRoom = false;
            foreach (Room room in rooms)
            {
                if (room.RoomType() == loadedRoom.RoomType())
                {
                    containsRoom = true;
                    break;
                }
            }

            if (!containsRoom)
            {
                rooms.Add(loadedRoom);
            }
        }

        private void OnRoomDestroy(Room destroyRoom)
        {
            int index = 0;
            foreach (Room room in rooms)
            {
                if (room.RoomType() == destroyRoom.RoomType())
                {
                    break;
                }

                index++;
            }

            rooms.RemoveAt(index);
        }

        private void OnDestroy()
        {
            Room.OnRoomCreate -= OnRoomLoaded;
            Room.OnRoomDestroy -= OnRoomDestroy;
        }

        private void Start()
        {
            if (currentRoom != null || _switchingInProgress)
            {
                return;
            }

            GameObject player = GameState.Instance.GetPlayer;
            if (player == null)
            {
                Debug.LogError("ERROR: PlayerToRoom(): Player reference not found in GameState!");
                return;
            }

            switch (loadRoomType)
            {
                case InitRoomLoadType.None:
                    break;
                case InitRoomLoadType.StartingRoom:
                    currentRoom = GetRoom(startingRoom);
                    break;
                case InitRoomLoadType.RoomContainingPlayer:
                    currentRoom = GetRoomAtPoint(player.transform.position);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (loadRoomType != InitRoomLoadType.None && currentRoom == null)
            {
                Debug.LogError("Room Manager Error: Initial room not found. Maybe player is out of bounds?");
            }
            else
            {
                currentRoom.SetRoomLogging(roomLogging);
                currentRoom.ActivateRoom(startingRoomEnteringFrom, loadRoomType == InitRoomLoadType.StartingRoom);
                PlayDoorAmbiances(currentRoom.Doors());
            }
        }

        private void OnEnable()
        {
            Door.OnRoomSwitching += SwitchRoom;
            LightManager.OnLightControl += OnLightControl;
            LightManager.OnChangeState += OnLightStateChange;
        }

        private void OnDisable()
        {
            Door.OnRoomSwitching -= SwitchRoom;
            LightManager.OnLightControl -= OnLightControl;
            LightManager.OnChangeState -= OnLightStateChange;
        }

        public Room GetRoom(RoomType roomType)
        {
            return rooms.Find(x => x.RoomType() == roomType);
        }

        public Room GetRoomAtPoint(Vector3 point)
        {
            return rooms.Find(x => x.ContainsPoint(point));
        }

        private void OnLightStateChange(LightData data)
        {
            if (currentRoom != null)
            {
                currentRoom.ControlLightState(data.State);
            }
        }

        private void OnLightControl(bool turnOn, float duration)
        {
            StartCoroutine(WaitForLights(turnOn, duration));
        }

        private IEnumerator WaitForLights(bool turnOn, float duration)
        {
            if (currentRoom)
            {
                yield return currentRoom.ControlLights(turnOn, duration);
            }
        }

        public void SwitchRoom(RoomType roomType, float transitionWaitTime = -1.0f, Action roomSwitchedCallback = null)
        {
            StartCoroutine(TransitionRooms(roomType, transitionWaitTime, roomSwitchedCallback));
        }

        private IEnumerator WaitForRoomLoad(RoomType roomType, Ref<Room> targetRoom)
        {
            _roomTypeToRoomConfig.TryGetValue(roomType, out RoomConfig roomConfig);

            bool isLoadFinished = false;
            if (roomConfig != null && roomConfig.owningScene != null)
            {
                SceneManager.LoadSceneAsync(roomConfig.owningScene.SceneName, false,
                    _ => isLoadFinished = true);
            }

            Debug.Log("Waiting for room " + roomType + " to load...");

            while (!isLoadFinished)
            {
                yield return null;
            }

            Debug.Log("Room " + roomType + " is loaded!");

            targetRoom.Value = UnityEngine.SceneManagement.SceneManager.GetSceneByName(roomConfig.owningScene.SceneName)
                .GetRootGameObjects()[0]
                .GetComponent<Room>();
        }

        private IEnumerator TransitionRooms(RoomType newRoomType, float roomLoadWaitTimeOverride,
            Action roomSwitchedCallback)
        {
            _switchingInProgress = true;
            string currentScene = currentRoom.gameObject.scene.path;

            if (roomLogging)
            {
                Debug.Log("Switching rooms: " + currentRoom.RoomType() + " -> " + newRoomType);
            }

            Room newRoom = null;
            foreach (Room room in rooms)
            {
                if (room.RoomType() == newRoomType)
                {
                    newRoom = room;
                    break;
                }
            }

            yield return currentRoom.DeactivateRoom(newRoomType);

            StopDoorAmbiances(currentRoom.Doors());

            // If target room cannot be found, load into memory and wait for it to be available.
            if (newRoom == null)
            {
                var loadedRoomRef = new Ref<Room>();
                yield return WaitForRoomLoad(newRoomType, loadedRoomRef);

                newRoom = loadedRoomRef.Value;
                newRoom.SetRoomLogging(roomLogging);
            }

            newRoom.PrepareRoom(currentRoom.RoomType());

            yield return new WaitForSecondsRealtime(roomLoadWaitTimeOverride > 0.0f
                ? roomLoadWaitTimeOverride
                : roomLoadWaitTime);

            newRoom.ActivateRoom(currentRoom.RoomType());

            PlayDoorAmbiances(newRoom.Doors());

            roomSwitchedCallback?.Invoke();
            currentRoom = newRoom;

            SceneManager.UnloadSceneAsync(currentScene);
            _switchingInProgress = false;
        }

        private void PlayDoorAmbiances(List<Door> doors)
        {
            foreach (Door door in doors)
            {
                if (!door.PlayConnectingRoomAmbience)
                {
                    continue;
                }

                var roomAmbiences = _roomTypeToRoomConfig[door.GetConnectingRoom()].roomAmbiences;

                foreach (RoomAmbience roomAmbience in roomAmbiences)
                {
                    if (roomAmbience.playInConnectingRooms)
                    {
                        if (roomAmbience.useOriginalAudioVolume)
                        {
                            roomAmbience.audio.Play(door.transform.position, true, 0.5f);
                        }
                        else
                        {
                            roomAmbience.audio.Play(door.transform.position, true, 0.5f,
                                roomAmbience.connectingRoomVolume);
                        }
                    }
                }
            }
        }

        private void StopDoorAmbiances(List<Door> doors)
        {
            foreach (Door door in doors)
            {
                if (!door.PlayConnectingRoomAmbience)
                {
                    continue;
                }

                var roomAmbiences = _roomTypeToRoomConfig[door.GetConnectingRoom()].roomAmbiences;

                foreach (RoomAmbience roomAmbience in roomAmbiences)
                {
                    if (roomAmbience.playInConnectingRooms)
                    {
                        roomAmbience.audio.StopAll();
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall += () => { _roomTypeToRoomConfig = LoadAllRooms(); };
        }

        private static SerializedDictionary<RoomType, RoomConfig> LoadAllRooms()
        {
            string[] guids =
                AssetDatabase.FindAssets("t:" + typeof(RoomConfig), new[] { "Assets/Scriptable Objects/Rooms" });
            int count = guids.Length;
            var dict = new SerializedDictionary<RoomType, RoomConfig>();

            for (int n = 0; n < count; n++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[n]);
                RoomConfig roomConfig = AssetDatabase.LoadAssetAtPath<RoomConfig>(path);
                dict[roomConfig.roomType] = roomConfig;
            }

            return dict;
        }

        public static event Action<Collider2D> OnPlayerSwitchingRoomsEditor;

        private void PlayerToRoom(RoomType roomType)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("ERROR: Player reference not found in GameState!");
                return;
            }

            bool roomFound = false;
            foreach (Room room in rooms)
            {
                if (room.RoomType() == roomType)
                {
                    // Set player to spawn point of first door in the room's list.
                    player.transform.position = room.DoorSpawnPoint(0);
                    OnPlayerSwitchingRoomsEditor?.Invoke(room.CameraBounds());

                    roomFound = true;
                    break;
                }
            }

            if (!roomFound)
            {
                Debug.LogError("ERROR: Room not found!");
            }
        }

#endif
    }
}