using Attributes;
using Rooms;
using ScriptableObjects.Rooms;
using UnityEngine;

namespace xNode.Nodes
{
    [NodeWidth(400)]
    [CreateNodeMenu("Actions/Switch Room")]
    public class SwitchRoomNode : BaseNode
    {
        [NodeEnum] [SerializeField] private RoomType roomType;
        [SerializeField] private GuidReference<RoomManager> roomManager;
        [SerializeField] private float transitionTime = -1.0f;

        public override void Execute()
        {
            if (roomManager.Component)
            {
                roomManager.Component.SwitchRoom(roomType, transitionTime);
            }

            NextNode("exit");
        }
    }
}