using System;
using System.Collections.Generic;
using Audio;
using MyBox;
using SceneHandling;
using UnityEngine;

namespace ScriptableObjects.Rooms
{
    public enum RoomType
    {
        CaptainQuarters,
        CrewQuarters,
        Engine,
        Hallway,
        Navigation,
        Torpedo
    }

    [Serializable]
    public class RoomAmbience
    {
        public AudioSO audio;
        public bool playInConnectingRooms;
        [ConditionalField(nameof(playInConnectingRooms))] public bool useOriginalAudioVolume = true;
        [ConditionalField(nameof(playInConnectingRooms))] public float connectingRoomVolume = 1.0f;
    }

    [CreateAssetMenu(fileName = "New Room Configuration", menuName = "Rooms/Room Config", order = 0)]
    public class RoomConfig : ScriptableObject
    {
        public RoomType roomType;
        public List<RoomAmbience> roomAmbiences;
        public ManagedScene owningScene;
    }
}