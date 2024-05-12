using Attributes;
using Rooms;
using ScriptableObjects.Rooms;
using UnityEngine;

namespace xNode.Nodes.Lights
{
    [NodeWidth(400)]
    [CreateNodeMenu("Actions/Lights/Room Lights")]
    public class RoomLightsNode : BaseNode
    {
        [NodeEnum] [SerializeField] private RoomType roomType;
        [SerializeField] private GuidReference<RoomManager> roomManager;
        [Space]
        [SerializeField] private bool turnLightsOn;
        [SerializeField] private float switchDuration = 2.0f;

        public override void Execute()
        {
            if (roomManager.Component)
            {
                roomManager.Component.GetRoom(roomType).ControlLights(turnLightsOn, switchDuration);
            }

            NextNode("exit");
        }
    }
}