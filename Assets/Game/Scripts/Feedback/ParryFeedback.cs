using Core.Timing;
using Gameplay.Player;
using UnityEngine;

namespace Gameplay.Feedback
{
    /// <summary>
    /// Makes parries feel good: hit-stop, screen shake, sound, and slow motion on a perfect parry.
    /// Put it on the player next to <see cref="PlayerParry"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerParry))]
    [DisallowMultipleComponent]
    public sealed class ParryFeedback : MonoBehaviour
    {
        [Header("Normal Parry")]
        [SerializeField, Min(0f)]
        [Tooltip("Real seconds the game freezes on a parry.")]
        private float hitStop = 0.05f;

        [SerializeField, Range(0f, 1f)]
        private float shake = 0.35f;

        [SerializeField]
        private AudioClip parryClip;

        [Header("Perfect Parry")]
        [SerializeField, Min(0f)]
        private float perfectHitStop = 0.08f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Time scale during the slow motion after a perfect parry.")]
        private float perfectSlowScale = 0.3f;

        [SerializeField, Min(0f)]
        [Tooltip("Real seconds of slow motion after a perfect parry (starts after the hit-stop).")]
        private float perfectSlowDuration = 0.4f;

        [SerializeField, Range(0f, 1f)]
        private float perfectShake = 0.6f;

        [SerializeField]
        [Tooltip("Played on a perfect parry. Falls back to the normal parry clip if empty.")]
        private AudioClip perfectClip;

        [Header("Audio")]
        [SerializeField]
        [Tooltip("Defaults to an AudioSource on this object, added automatically if missing.")]
        private AudioSource audioSource;

        [SerializeField, Range(0f, 1f)]
        private float volume = 0.8f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Random pitch variation so repeated parries don't sound identical.")]
        private float pitchVariation = 0.08f;

        private PlayerParry parry;

        private void Awake()
        {
            parry = GetComponent<PlayerParry>();

            if (audioSource == null && !TryGetComponent(out audioSource))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        private void OnEnable()
        {
            parry.Parried += HandleParried;
        }

        private void OnDisable()
        {
            parry.Parried -= HandleParried;
        }

        private void HandleParried(ParryEvent parryEvent)
        {
            if (parryEvent.IsPerfect)
            {
                GameTime.HitStop(perfectHitStop);
                GameTime.SlowMotion(perfectSlowScale, perfectSlowDuration);
                CameraShaker.Shake(perfectShake);
                Play(perfectClip != null ? perfectClip : parryClip, 1.15f);
            }
            else
            {
                GameTime.HitStop(hitStop);
                CameraShaker.Shake(shake);
                Play(parryClip, 1f);
            }
        }

        private void Play(AudioClip clip, float basePitch)
        {
            if (clip == null || audioSource == null)
            {
                return;
            }

            audioSource.pitch = basePitch + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
