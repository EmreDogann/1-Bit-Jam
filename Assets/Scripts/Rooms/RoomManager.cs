using System;
using System.Collections;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Interactables;
using Lights;
using MyBox;
using ScriptableObjects.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;
using SceneManager = SceneHandling.SceneManager;
#if UNITY_EDITOR
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
        [SerializeField] private RoomConfig startingRoom;
        [SerializeField] private RoomConfig startingRoomEnteringFrom;

        [SerializeField] private bool roomLogging;

        [SerializeField] private List<Room> rooms;
        [SerializeField] private List<RoomConfig> roomConfigs;
        [ReadOnly] [SerializeField] private Room currentRoom;
        [ReadOnly] [SerializeField] private RoomConfig currentRoomConfig;

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
            if (currentRoomConfig || _switchingInProgress)
            {
                return;
            }

            GameObject player = GameState.Instance.GetPlayer;
            if (!player)
            {
                Debug.LogError("ERROR: PlayerToRoom(): Player reference not found in GameState!");
                return;
            }

            RoomConfig startingRoomConfig = null;
            switch (loadRoomType)
            {
                case InitRoomLoadType.None:
                    break;
                case InitRoomLoadType.StartingRoom:
                    startingRoomConfig = startingRoom;
                    break;
                case InitRoomLoadType.RoomContainingPlayer:
                    // startingRoomConfig = GetRoomAtPoint(player.transform.position);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!startingRoomConfig)
            {
                Debug.LogError("Room Manager Error: Initial room not found.");
            }
            else
            {
                SwitchRoom(startingRoomConfig);
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

        public RoomConfig GetRoomConfig(RoomType roomType)
        {
            return roomConfigs.Find(x => x.roomType == roomType);
        }

        public Room GetRoomAtPoint(Vector3 point)
        {
            return rooms.Find(x => x.ContainsPoint(point));
        }

        private void OnLightStateChange(LightData data)
        {
            if (currentRoom)
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

        public void SwitchRoom(RoomConfig roomConfig, float transitionWaitTime = -1.0f,
            Action roomSwitchedCallback = null)
        {
            StartCoroutine(TransitionRooms(roomConfig, transitionWaitTime, roomSwitchedCallback));
        }

        private IEnumerator WaitForRoomLoad(RoomType roomType, Ref<Room> targetRoom)
        {
            _roomTypeToRoomConfig.TryGetValue(roomType, out RoomConfig roomConfig);
            yield return WaitForRoomLoad(roomConfig, targetRoom);
        }

        private IEnumerator WaitForRoomLoad(RoomConfig roomConfig, Ref<Room> targetRoom)
        {
            bool isLoadFinished = false;
            if (roomConfig && roomConfig.owningScene != null)
            {
                SceneManager.LoadSceneAsync(roomConfig.owningScene.SceneName, false,
                    _ => isLoadFinished = true);
            }

            Debug.Log("Waiting for room " + roomConfig.roomType + " to load...");

            while (!isLoadFinished)
            {
                yield return null;
            }

            Debug.Log("Room " + roomConfig.roomType + " is loaded!");

            targetRoom.Value = UnityEngine.SceneManagement.SceneManager.GetSceneByName(roomConfig.owningScene.SceneName)
                .GetRootGameObjects()[0]
                .GetComponent<Room>();

            // Cache this runtime instance of room.
            roomConfig.RuntimeRoom = targetRoom.Value;
        }

        private IEnumerator TransitionRooms(RoomConfig newRoomConfig, float roomLoadWaitTimeOverride,
            Action roomSwitchedCallback)
        {
            _switchingInProgress = true;

            if (currentRoomConfig && currentRoomConfig.RuntimeRoom)
            {
                if (roomLogging)
                {
                    Debug.Log("Switching rooms: " + currentRoom.RoomType() + " -> " + newRoomConfig.roomType);
                }

                yield return currentRoomConfig.RuntimeRoom.DeactivateRoom(newRoomConfig.roomType);

                StopDoorAmbiances(currentRoomConfig.RuntimeRoom.Doors());
            }

            Room newRoom;
            // If target room cannot be found, load into memory and wait for it to be available.
            if (!newRoomConfig.owningScene.IsLoaded)
            {
                var loadedRoomRef = new Ref<Room>();
                yield return WaitForRoomLoad(newRoomConfig, loadedRoomRef);

                newRoom = loadedRoomRef.Value;
            }
            else
            {
                if (!newRoomConfig.RuntimeRoom)
                {
                    newRoomConfig.RuntimeRoom = UnityEngine.SceneManagement.SceneManager
                        .GetSceneByName(newRoomConfig.owningScene.SceneName)
                        .GetRootGameObjects()[0]
                        .GetComponent<Room>();
                }

                newRoom = newRoomConfig.RuntimeRoom;
            }

            newRoom.SetRoomLogging(roomLogging);

            newRoom.PrepareRoom(currentRoomConfig);

            yield return new WaitForSecondsRealtime(roomLoadWaitTimeOverride > 0.0f
                ? roomLoadWaitTimeOverride
                : roomLoadWaitTime);

            newRoom.ActivateRoom(currentRoomConfig);

            PlayDoorAmbiances(newRoom.Doors());

            if (currentRoomConfig)
            {
                SceneManager.UnloadSceneAsync(currentRoomConfig.owningScene.ScenePath);
            }

            currentRoom = newRoom;
            currentRoomConfig = newRoomConfig;
            roomSwitchedCallback?.Invoke();

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

                var roomAmbiences = door.GetConnectingRoomConfig().roomAmbiences;

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

                var roomAmbiences = door.GetConnectingRoomConfig().roomAmbiences;

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
        public static event Action<Collider2D> OnPlayerSwitchingRoomsEditor;

        private void PlayerToRoom(RoomType roomType)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (!player)
            {
                Debug.LogError("ERROR: Player reference not found in GameState!");
                return;
            }

            foreach (RoomConfig roomConfig in roomConfigs)
            {
                if (roomConfig.roomType == roomType)
                {
                    Scene scene =
                        UnityEngine.SceneManagement.SceneManager.GetSceneByPath(roomConfig.owningScene.ScenePath);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        scene =
                            EditorSceneManager.OpenScene(roomConfig.owningScene.ScenePath, OpenSceneMode.Additive);
                    }

                    var rootGameObjects = scene.GetRootGameObjects();
                    Room room = rootGameObjects[0].GetComponent<Room>();

                    // Set player to spawn point of first door in the room's list.
                    player.transform.position = room.DoorSpawnPoint(0);
                    OnPlayerSwitchingRoomsEditor?.Invoke(room.CameraBounds());

                    // Player's collider takes a frame to update.
                    StartCoroutine(FocusSceneView(player.GetComponent<Collider>(), false));
                    break;
                }
            }
        }

        private IEnumerator FocusSceneView(Collider collider, bool instant)
        {
            yield return null;
            SceneView.lastActiveSceneView.Frame(collider.bounds, instant);
        }
#endif
    }
}