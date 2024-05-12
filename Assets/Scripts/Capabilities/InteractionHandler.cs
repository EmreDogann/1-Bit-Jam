using System;
using Controllers;
using Events;
using Inspect;
using Inspect.Views;
using Interactables;
using Interaction;
using Items;
using Rooms;
using ScriptableObjects;
using ScriptableObjects.Rooms;
using UnityEngine;

namespace Capabilities
{
    [RequireComponent(typeof(Controller))]
    public class InteractionHandler : MonoBehaviour, IInteractor
    {
        [SerializeField] private ItemEventListener itemInteractedEvent;

        [SerializeField] private GuidReference<PickupView> pickupViewRef;
        [SerializeField] private GuidReference<ItemUserView> itemUserViewRef;

        [SerializeField] private Inventory inventory;

        private bool _interactionActive;

        private void Awake()
        {
            _interactionActive = true;
        }

        private void OnEnable()
        {
            Door.OnRoomSwitching += OnRoomSwitching;
            Room.OnRoomActivate += OnRoomActivate;

            itemInteractedEvent.Response.AddListener(OnItemInteract);
        }

        private void OnDisable()
        {
            Door.OnRoomSwitching -= OnRoomSwitching;
            Room.OnRoomActivate -= OnRoomActivate;

            itemInteractedEvent.Response.RemoveListener(OnItemInteract);
        }

        private void OnItemInteract(IItem item)
        {
            if (item != null)
            {
                _interactionActive = false;

                if (pickupViewRef.Component)
                {
                    pickupViewRef.Component.SetupPickup(item, wasConfirmed =>
                    {
                        if (wasConfirmed)
                        {
                            inventory.AddItem(item);
                            item.Pickup();
                        }

                        _interactionActive = true;
                    });
                    ViewManager.Instance.Show(pickupViewRef.Component);
                }
                else
                {
                    Debug.LogError(nameof(PickupView) +
                                   " not found! Aborting <color=green>[Item Pickup]</color> operation...");
                }
            }
        }

        private void OnRoomSwitching(RoomType roomType, float transitionTime, Action callback)
        {
            _interactionActive = false;
        }

        private void OnRoomActivate(RoomData roomData)
        {
            _interactionActive = true;
        }

        public ItemUserInteractionType ResolveInteraction(IItemUser itemUser, ItemUserView viewOverride = null)
        {
            if (!itemUserViewRef.Component)
            {
                Debug.LogError(nameof(ItemUserView) +
                               " not found! Aborting <color=green>[Item Use]</color> operation...");
                return ItemUserInteractionType.Default;
            }

            ItemUserView currentView = viewOverride != null ? viewOverride : itemUserViewRef.Component;
            if (itemUser != null)
            {
                if (itemUser.IsExpectingItem(out ItemInfoSO expectedItem) && inventory.ContainsItemType(expectedItem))
                {
                    currentView.SetupItemUserView(b =>
                    {
                        if (itemUser.IsExpectingItem(out ItemInfoSO expectedItem))
                        {
                            IItem itemToGive = inventory.TryGetItem(expectedItem);
                            if (itemToGive != null)
                            {
                                itemUser.TryItem(itemToGive);
                            }
                        }
                    }, itemUser.GetCameraAngle());
                    ViewManager.Instance.Show(itemUserViewRef.Component);
                    return ItemUserInteractionType.GiveItem;
                }

                if (itemUser.HasItem())
                {
                    currentView.SetupItemUserView(b =>
                    {
                        IItem itemToTake = itemUser.TryTakeItem();
                        inventory.AddItem(itemToTake);
                    }, itemUser.GetCameraAngle());
                    ViewManager.Instance.Show(itemUserViewRef.Component);
                    return ItemUserInteractionType.TakeItem;
                }
            }

            return ItemUserInteractionType.Default;
        }
    }
}