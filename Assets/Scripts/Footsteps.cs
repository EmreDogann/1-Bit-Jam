using System.Collections;
using Cinemachine;
using MyBox;
using ScriptableObjects;
using Surface;
using UnityEngine;

public class Footsteps : MonoBehaviour
{
    [SerializeField] private LayerMask collisionDetectionLayerMask;
    [SerializeField] private float collisionRadius;
    [SerializeField] private bool play3D;

    [Separator("Camera")]
    public bool shakeCameraOnStep;
    [ConditionalField(nameof(shakeCameraOnStep))]
    [SerializeField] private CinemachineIndependentImpulseListener impulseListener;

    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private ScreenShakeProfile screenShakeProfile;

    [Separator("Water Interaction Settings")]
    [SerializeField] private GameObject footstepMesh;
    [SerializeField] private float meshLifetime;

    private readonly RaycastHit[] _hits = new RaycastHit[5];
    private Coroutine _coroutine;
    private Material _footstepMaterial;

    private void Awake()
    {
        if (footstepMesh)
        {
            footstepMesh.SetActive(false);
            _footstepMaterial = footstepMesh.GetComponent<MeshRenderer>().sharedMaterial;
            _footstepMaterial.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        }
    }

    private void OnFootstep()
    {
        TriggerFootstep();
    }

    private void TriggerFootstep()
    {
        int hitNum = Physics.SphereCastNonAlloc(transform.position + Vector3.up * (collisionRadius + 0.5f),
            collisionRadius,
            Vector3.down, _hits,
            collisionRadius,
            collisionDetectionLayerMask);

        if (hitNum <= 0)
        {
            return;
        }

        for (int i = 0; i < hitNum; i++)
        {
            RaycastHit hit = _hits[i];
            if (hit.transform.TryGetComponent(out ISteppable steppableComponent))
            {
                if (!play3D)
                {
                    steppableComponent.GetSurfaceData().surfaceSound.Play2D();
                }
                else
                {
                    steppableComponent.GetSurfaceData().surfaceSound.Play(transform.position);
                }

                if (shakeCameraOnStep && screenShakeProfile && impulseSource)
                {
                    if (impulseListener)
                    {
                        impulseSource.m_ImpulseDefinition.m_ImpulseDuration = screenShakeProfile.impactTime;
                        impulseSource.m_DefaultVelocity = screenShakeProfile.defaultVelocity;
                        impulseListener.m_ReactionSettings.m_AmplitudeGain = screenShakeProfile.listenerAmplitude;
                        impulseListener.m_ReactionSettings.m_FrequencyGain = screenShakeProfile.listenerFrequency;
                        impulseListener.m_ReactionSettings.m_Duration = screenShakeProfile.listenerDuration;

                        impulseSource.GenerateImpulseAtPositionWithVelocity(transform.position,
                            screenShakeProfile.defaultVelocity * screenShakeProfile.impactForce);
                    }
                }
            }
        }

        if (footstepMesh)
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }

            _coroutine = StartCoroutine(ToggleFootstep());
        }
    }

    private IEnumerator ToggleFootstep()
    {
        _footstepMaterial.color = new Color(0.05f, 0.0f, 0.0f, 1.0f);

        footstepMesh.SetActive(true);
        yield return meshLifetime == 0 ? new WaitForEndOfFrame() : new WaitForSeconds(meshLifetime);
        footstepMesh.SetActive(false);
        _footstepMaterial.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
    }
}