using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Audio;
using Interactables;
using Lights;
using MyBox;
using ScriptableObjects.Rooms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Rooms
{
    public class RoomData
    {
        public SplineContainer RoomPath;
        public int PathIndex;
        public Transform StartingPosition;
        public float LightFadeDuration;
        public Collider2D CameraBounds;
    }

    public class Room : MonoBehaviour
    {
        [SerializeField] private RoomConfig roomConfig;

        [Separator("Lights")]
        [SerializeField] private readonly float lightFadeDuration = 1.0f;
        [field: SerializeReference] public bool LightsOn { get; private set; } = true;

        [Separator("Runtime Room Data")]
        [SerializeField] private Collider2D cameraBounds;
        [SerializeField] private List<Door> roomDoors;
        [SerializeField] private List<ControlLight> controllableLights;
        [SerializeField] private SplineContainer roomPath;

        public static event Action<Room> OnRoomCreate;
        public static event Action<Room> OnRoomDestroy;
        public static event Action<bool, float> OnLightsSwitch;
        public static event Action<RoomData> OnRoomPrepare;
        public static event Action<RoomData> OnRoomActivate;
        public static event Action<RoomData> OnRoomDeactivate;

        private BoxCollider _roomBounds;

        private enum RoomLoggingState
        {
            Prepare,
            Activate,
            Deactivate
        }

        private bool _roomLogging;

        public void SetRoomLogging(bool loggingEnabled)
        {
            _roomLogging = loggingEnabled;
        }

        private void TriggerRoomLogging(RoomLoggingState loggingState, bool isStateFinished)
        {
            if (!_roomLogging)
            {
                return;
            }

            switch (loggingState)
            {
                case RoomLoggingState.Prepare:
                    if (!isStateFinished)
                    {
                        Debug.Log("Preparing room <color=orange>" + RoomType() + "</color>...");
                    }
                    else
                    {
                        Debug.Log("Room <color=orange>" + RoomType() + "</color> prepared!");
                    }

                    break;
                case RoomLoggingState.Activate:
                    if (!isStateFinished)
                    {
                        Debug.Log("Activating room <color=green>" + RoomType() + "</color>...");
                    }
                    else
                    {
                        Debug.Log("Room <color=green>" + RoomType() + "</color> activated!");
                    }

                    break;
                case RoomLoggingState.Deactivate:
                    if (!isStateFinished)
                    {
                        Debug.Log("Deactivating room <color=red>" + RoomType() + "</color>...");
                    }
                    else
                    {
                        Debug.Log("Room <color=red>" + RoomType() + "</color> deactivated!");
                    }

                    break;
            }
        }

        private void Awake()
        {
            if (roomDoors == null)
            {
                roomDoors = GetComponentsInChildren<Door>().ToList();
            }

            if (controllableLights == null)
            {
                controllableLights = GetComponentsInChildren<ControlLight>().ToList();
            }

            // Active then deactivate lights that start disabled so that their awake functions can get called.
            foreach (ControlLight controlLight in controllableLights)
            {
                if (!controlLight.gameObject.activeSelf)
                {
                    controlLight.gameObject.SetActive(true);
                    controlLight.gameObject.SetActive(false);
                }
            }

            var roomBoundsCollider = GetComponentsInChildren<BoxCollider>();

            foreach (BoxCollider bounds in roomBoundsCollider)
            {
                if (bounds.gameObject.CompareTag("RoomBounds"))
                {
                    _roomBounds = bounds;
                    break;
                }
            }

            roomPath = GetComponentInChildren<SplineContainer>();

            if (_roomBounds == null)
            {
                Debug.LogError(
                    $"{name} Error: Room bounds not found! Please create a BoxCollider game object" +
                    "tagged \"RoomBounds\" that encapsulates the entire room.");
            }

            OnRoomCreate?.Invoke(this);
        }

        private void OnDestroy()
        {
            OnRoomDestroy?.Invoke(this);
        }

        private void OnValidate()
        {
            var doorsFound = GetComponentsInChildren<Door>();
            if (doorsFound.Length != roomDoors.Count)
            {
                foreach (Door door in doorsFound)
                {
                    if (!roomDoors.Contains(door))
                    {
                        roomDoors.Add(door);
                    }
                }
            }

            var lightsFound = GetComponentsInChildren<ControlLight>();
            if (lightsFound.Length != controllableLights.Count)
            {
                foreach (ControlLight controlLight in lightsFound)
                {
                    if (!controllableLights.Contains(controlLight))
                    {
                        controllableLights.Add(controlLight);
                    }
                }
            }

            roomPath = GetComponentInChildren<SplineContainer>();
        }

        public Vector3 DoorSpawnPoint(int doorIndex)
        {
            return roomDoors[doorIndex].GetSpawnPoint().position;
        }

        public RoomType RoomType()
        {
            return roomConfig.roomType;
        }

        public Collider2D CameraBounds()
        {
            return cameraBounds;
        }

        public bool ContainsPoint(Vector3 point)
        {
            return _roomBounds.bounds.Contains(point);
        }

        public void PrepareRoom(RoomConfig exitingRoom)
        {
            TriggerRoomLogging(RoomLoggingState.Prepare, false);

            foreach (ControlLight controlLight in controllableLights.Where(roomLight =>
                         roomLight.CanBeControlledByRoom()))
            {
                controlLight.TurnOffLight();
            }

            Door selectedDoor = null;
            if (exitingRoom)
            {
                foreach (Door door in roomDoors)
                {
                    if (door.GetConnectingRoom() != exitingRoom.roomType)
                    {
                        continue;
                    }

                    selectedDoor = door;

                    break;
                }
            }
            else
            {
                selectedDoor = roomDoors[0];
            }

            OnRoomPrepare?.Invoke(new RoomData
            {
                RoomPath = roomPath,
                PathIndex = NearestSplineIndex(selectedDoor.GetSpawnPoint().position),
                StartingPosition = selectedDoor.GetSpawnPoint(),
                LightFadeDuration = lightFadeDuration,
                CameraBounds = cameraBounds
            });

            TriggerRoomLogging(RoomLoggingState.Prepare, true);
        }

        public List<RoomAmbience> RoomAmbiences()
        {
            return roomConfig.roomAmbiences;
        }

        public List<Door> Doors()
        {
            return roomDoors;
        }

        public void ActivateRoom(bool setPlayerPosition)
        {
            TriggerRoomLogging(RoomLoggingState.Activate, false);

            if (LightsOn)
            {
                foreach (ControlLight controlLight in controllableLights.Where(roomLight =>
                             roomLight.CanBeControlledByRoom()))
                {
                    controlLight.TurnOnLight(lightFadeDuration);
                }

                OnLightsSwitch?.Invoke(true, lightFadeDuration);
            }

            // Uses first door
            if (roomDoors.Count > 0)
            {
                OnRoomActivate?.Invoke(new RoomData
                {
                    RoomPath = roomPath,
                    PathIndex = setPlayerPosition ? NearestSplineIndex(roomDoors[0].GetSpawnPoint().position) : 0,
                    StartingPosition = setPlayerPosition ? roomDoors[0].GetSpawnPoint() : null,
                    LightFadeDuration = lightFadeDuration,
                    CameraBounds = cameraBounds
                });
            }

            foreach (RoomAmbience roomAmbience in roomConfig.roomAmbiences)
            {
                roomAmbience.audio.Play2D(true, 2.0f);
            }

            TriggerRoomLogging(RoomLoggingState.Activate, true);
        }

        public void ActivateRoom(RoomConfig exitingRoom, bool setPlayerPosition = true)
        {
            TriggerRoomLogging(RoomLoggingState.Activate, false);

            if (LightsOn)
            {
                foreach (ControlLight controlLight in controllableLights.Where(roomLight =>
                             roomLight.CanBeControlledByRoom()))
                {
                    controlLight.TurnOnLight(lightFadeDuration);
                }

                OnLightsSwitch?.Invoke(true, lightFadeDuration);
            }

            Door selectedDoor = null;
            if (exitingRoom)
            {
                foreach (Door door in roomDoors)
                {
                    if (door.GetConnectingRoom() != exitingRoom.roomType)
                    {
                        continue;
                    }

                    door.PlayClosingAudio();
                    selectedDoor = door;

                    break;
                }
            }
            else
            {
                selectedDoor = roomDoors[0];
            }

            OnRoomActivate?.Invoke(new RoomData
            {
                RoomPath = roomPath,
                PathIndex = setPlayerPosition ? NearestSplineIndex(selectedDoor.GetSpawnPoint().position) : 0,
                StartingPosition = setPlayerPosition ? selectedDoor.GetSpawnPoint() : null,
                LightFadeDuration = lightFadeDuration,
                CameraBounds = cameraBounds
            });

            foreach (RoomAmbience roomAmbience in roomConfig.roomAmbiences)
            {
                roomAmbience.audio.Play2D(true, 2.0f);
            }

            TriggerRoomLogging(RoomLoggingState.Activate, true);
        }

        public Coroutine DeactivateRoom(RoomType exitingRoom)
        {
            TriggerRoomLogging(RoomLoggingState.Deactivate, false);

            foreach (Door door in roomDoors)
            {
                if (door.GetConnectingRoom() != exitingRoom)
                {
                    continue;
                }

                OnRoomDeactivate?.Invoke(new RoomData
                {
                    RoomPath = roomPath,
                    PathIndex = NearestSplineIndex(door.GetSpawnPoint().position),
                    StartingPosition = door.GetSpawnPoint(),
                    LightFadeDuration = lightFadeDuration,
                    CameraBounds = cameraBounds
                });
                break;
            }

            foreach (RoomAmbience roomAmbience in roomConfig.roomAmbiences)
            {
                roomAmbience.audio.Stop(AudioHandle.Invalid, true, 2.0f);
            }

            TriggerRoomLogging(RoomLoggingState.Deactivate, true);

            return StartCoroutine(WaitForLightsOff(lightFadeDuration));
        }

        public void ControlLightState(LightState state)
        {
            foreach (ControlLight controlLight in controllableLights.Where(roomLight =>
                         roomLight.CanBeControlledByRoom()))
            {
                controlLight.ChangeLightState(state);
            }
        }

        public Coroutine ControlLights(bool turnOn, float duration)
        {
            LightsOn = turnOn;

            if (turnOn)
            {
                return StartCoroutine(WaitForLightsOn(duration));
            }

            return StartCoroutine(WaitForLightsOff(duration));
        }

        private IEnumerator WaitForLightsOff(float duration)
        {
            foreach (ControlLight controlLight in controllableLights.Where(roomLight =>
                         roomLight.CanBeControlledByRoom()))
            {
                controlLight.TurnOffLight(duration);
                OnLightsSwitch?.Invoke(false, duration);
            }

            foreach (ControlLight controlLight in controllableLights.Where(roomLight =>
                         roomLight.CanBeControlledByRoom()))
            {
                while (controlLight.IsOn())
                {
                    yield return null;
                }
            }
        }

        private IEnumerator WaitForLightsOn(float duration)
        {
            foreach (ControlLight controlLight in controllableLights.Where(roomLight =>
                         roomLight.CanBeControlledByRoom()))
            {
                controlLight.TurnOnLight(duration);
                OnLightsSwitch?.Invoke(true, duration);
            }

            foreach (ControlLight controlLight in controllableLights.Where(roomLight =>
                         roomLight.CanBeControlledByRoom()))
            {
                while (!controlLight.IsOn())
                {
                    yield return null;
                }
            }
        }

        private int NearestSplineIndex(Vector3 point)
        {
            float closestDistance = Mathf.Infinity;
            int closestIndex = 0;

            int index = 0;
            foreach (Spline path in roomPath.Splines)
            {
                float distance = SplineUtility.GetNearestPoint(path, roomPath.transform.InverseTransformPoint(point),
                    out float3 _, out float _);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = index;
                }

                index++;
            }

            return closestIndex;
        }
    }
}