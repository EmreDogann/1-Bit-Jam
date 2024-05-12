using UnityEngine;

namespace xNode.Nodes
{
    [NodeWidth(400)]
    [CreateNodeMenu("Actions/Set To Transform")]
    public class SetTransformNode : BaseNode
    {
        public enum TransformSpace
        {
            Global,
            Local
        }

        [SerializeField] private TransformSpace targetPositionSpace;
        [SerializeField] private TransformSpace targetRotationSpace;

        [SerializeField] private GameObject gameObject;
        [SerializeField] private Transform targetTransform;

        public override void Execute()
        {
            gameObject.transform.position = targetPositionSpace == TransformSpace.Global
                ? targetTransform.position
                : targetTransform.localPosition;
            gameObject.transform.rotation = targetRotationSpace == TransformSpace.Global
                ? targetTransform.rotation
                : targetTransform.localRotation;
            gameObject.transform.localScale = targetTransform.localScale;

            NextNode("exit");
        }
    }
}