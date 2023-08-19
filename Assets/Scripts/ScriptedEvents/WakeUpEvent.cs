using Audio;
using DG.Tweening;
using Events;
using Lights;
using MyBox;
using Rooms;
using UnityEngine;

namespace ScriptedEvents
{
    public class WakeUpEvent : MonoBehaviour, IScriptedEvent
    {
        // Move the player into position, play Ship explosion noise, play alarm, play low oxygen voice, fade to normal.
        [SerializeField] private bool triggerOnAwake;
        [ConditionalField(nameof(triggerOnAwake))] [SerializeField] private float triggerDelay;
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private RoomType startingRoom;

        [Separator("Transforms")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform doorTransform;

        [Separator("Audio")]
        [SerializeField] private SubmarineSoundScape submarineSoundScape;
        [SerializeField] private AudioSO explosionSound;
        [SerializeField] private AudioSO doorOpenSound;
        [SerializeField] private AudioSO lowOxygenVoiceSound;

        [Separator("Events")]
        [SerializeField] private BoolEventChannelSO pauseEvent;

        private Sequence _wakeUpSequence;

        private void Start()
        {
            // Initialize starting room
            roomManager.GetRoom(startingRoom).ControlLights(false, 0.0f);

            _wakeUpSequence = DOTween.Sequence();
            _wakeUpSequence
                .AppendCallback(() => { pauseEvent.Raise(true); })
                // .AppendCallback(() => { LightManager.Instance.ToggleLights(false, 0.0f); })
                .AppendCallback(() => { roomManager.SwitchRoom(startingRoom); })
                .AppendInterval(triggerDelay)
                .AppendCallback(() =>
                {
                    Vector3 playerTransformPosition = playerTransform.position;
                    playerTransformPosition.x = 22;
                    playerTransform.position = playerTransformPosition;
                })
                .AppendCallback(() =>
                {
                    // Play ship explosion noise
                    submarineSoundScape.TriggerExplosion(true);
                })
                .AppendInterval(0.1f)
                .AppendCallback(() =>
                {
                    // Play ship explosion noise
                    // submarineSoundScape.TriggerExplosion(true);
                    explosionSound.Play(playerTransform.position);
                })
                .AppendInterval(0.05f)
                .AppendCallback(() =>
                {
                    // Play ship explosion noise
                    submarineSoundScape.TriggerExplosion(true);
                })
                .AppendInterval(1.5f)
                .AppendCallback(() =>
                {
                    // play Low Oxygen Voice
                    doorOpenSound.Play(doorTransform.position);
                })
                // .AppendInterval(doorOpenSound.GetPlaybackInfo(AudioHandle.Invalid).CurrentClipDuration)
                .AppendInterval(1.0f)
                .AppendCallback(() =>
                {
                    lowOxygenVoiceSound.Play(playerTransform.position);
                    // Fade to normal lights
                    // LightControl.OnLightControl?.Invoke(true, 1.0f);
                })
                .AppendInterval(1.0f)
                .AppendCallback(() => { LightManager.Instance.ChangeLightColor(LightState.Alarm, 0.4f); })
                .AppendInterval(1.5f)
                .AppendCallback(() => { pauseEvent.Raise(false); })
                .SetUpdate(true)
                .Pause();

            if (triggerOnAwake)
            {
                _wakeUpSequence.Play();
            }
        }

        [ButtonMethod]
        public void PlaySequence()
        {
            _wakeUpSequence.Play();
        }
    }
}