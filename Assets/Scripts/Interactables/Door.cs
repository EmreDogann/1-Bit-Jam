using System;
using Audio;
using Cinemachine;
using DG.Tweening;
using Events;
using Inspect.Views.Triggers;
using Items;
using MyBox;
using ScriptableObjects;
using ScriptableObjects.Rooms;
using UnityEngine;

namespace Interactables
{
    public class Door : MonoBehaviour, IItemUser
    {
        [Separator("Room Settings")]
        [SerializeField] private RoomConfig connectingRoomConfig;
        [SerializeField] private RoomType connectingRoom;
        [SerializeField] private Transform spawnPoint;

        [Separator("Audio")]
        [SerializeField] private AudioSO turningValueAudio;
        [SerializeField] private AudioSO closingDoorAudio;
        [SerializeField] private AudioSO lockingAudio;
        [SerializeField] private AudioSO doorLockedAudio;

        [field: SerializeReference] public bool PlayConnectingRoomAmbience { get; private set; }

        [Separator("Open Animation")]
        [SerializeField] private Transform turningValve;
        [SerializeField] private float valveTurnEulerAngle = 25;
        [SerializeField] private float valveTurnDuration = 1.0f;
        [SerializeField] private Ease valveTurnEasing = Ease.Linear;

        [SerializeField] private float openEulerAngle = 25;
        [SerializeField] private float openDuration = 1.0f;
        [SerializeField] private Ease openEasing = Ease.Linear;

        [Separator("Inspection")]
        [SerializeField] private ItemInfoSO expectedItem;
        [SerializeField] private bool isInspectable;
        [SerializeField] private CinemachineVirtualCamera doorCameraAngle;
        [SerializeField] private ViewTrigger missingHandleViewTrigger;
        [SerializeField] private ViewTrigger lockedDoorViewTrigger;

        [Separator("Door State")]
        [ReadOnly] [SerializeField] private bool isLocked;

        [Separator("Events")]
        [SerializeField] private BoolEventChannelSO interactionBlocker;

        private bool _handleRemoved;
        private Vector3 _valveStartingRotation;
        private Vector3 _startingRotation;
        private Transform _child;

        private Sequence _doorOpenSequence;

        public static event Action<RoomConfig, float, Action> OnRoomSwitching;

        private void Awake()
        {
            _child = transform.GetChild(0);
            _startingRotation = _child.localEulerAngles;
            _valveStartingRotation = turningValve.localEulerAngles;

            _doorOpenSequence = DOTween.Sequence();
            _doorOpenSequence
                .Append(turningValve
                    .DOLocalRotate(new Vector3(_valveStartingRotation.x, _valveStartingRotation.y, valveTurnEulerAngle),
                        valveTurnDuration)
                    .SetEase(valveTurnEasing))
                .Insert(valveTurnDuration * 0.9f, _child
                    .DOLocalRotate(new Vector3(_startingRotation.x, openEulerAngle, _startingRotation.z), openDuration)
                    .SetEase(openEasing)
                    .OnComplete(() =>
                    {
                        OnRoomSwitching?.Invoke(connectingRoomConfig, -1.0f, () =>
                        {
                            // _doorOpenSequence.SmoothRewind();
                            interactionBlocker?.Raise(false);
                        });
                    }))
                .SetAutoKill(false)
                .Pause();
        }

        private void OnDestroy()
        {
            _doorOpenSequence.Kill();
        }

        public RoomType GetConnectingRoom()
        {
            return connectingRoom;
        }

        public RoomConfig GetConnectingRoomConfig()
        {
            return connectingRoomConfig;
        }

        public Transform GetSpawnPoint()
        {
            return spawnPoint;
        }

        public void PlayClosingAudio()
        {
            closingDoorAudio.Play(transform.position);
        }

        public void OpenDoor()
        {
            if (_handleRemoved)
            {
                missingHandleViewTrigger.TriggerView();
                // ViewManager.Instance.Show(missingHandleInspectViewTrigger);
                return;
            }

            if (!_doorOpenSequence.IsPlaying())
            {
                if (isLocked)
                {
                    doorLockedAudio.Play(transform.position);
                }

                if (isLocked || _handleRemoved)
                {
                    return;
                }

                interactionBlocker?.Raise(true);
                turningValueAudio.Play(transform.position);
                _doorOpenSequence.PlayForward();
            }
        }

        public void SetLocked(bool locked, bool playLockedSound = true)
        {
            isLocked = locked;
            if (playLockedSound)
            {
                lockingAudio.Play(transform.position);
            }
        }

        [ButtonMethod]
        private void LockDoor()
        {
            SetLocked(true);
        }

        [ButtonMethod]
        private void UnlockDoor()
        {
            SetLocked(false);
        }

        [ButtonMethod]
        private void RemoveHandle()
        {
            turningValve.gameObject.SetActive(false);
            _handleRemoved = true;
            isLocked = false;
            isInspectable = true;
        }

        public CinemachineVirtualCamera GetCameraAngle()
        {
            return doorCameraAngle;
        }

        public string GetMessage()
        {
            return "";
        }

        public bool IsInspectable()
        {
            return isInspectable;
        }

        public bool IsExpectingItem(out ItemInfoSO item)
        {
            item = expectedItem;
            return _handleRemoved;
        }

        public bool HasItem()
        {
            return false;
        }

        public bool TryItem(IItem item)
        {
            bool isExpectingItem = IsExpectingItem(out ItemInfoSO itemInfo);
            if (isExpectingItem && item.GetItemInfo() == itemInfo)
            {
                item.Consume();

                turningValve.gameObject.SetActive(true);
                _handleRemoved = false;
                isLocked = false;
                isInspectable = false;
                // TODO: Play placing handle audio

                return true;
            }

            return false;
        }

        public IItem TryTakeItem()
        {
            return null;
        }
    }
}