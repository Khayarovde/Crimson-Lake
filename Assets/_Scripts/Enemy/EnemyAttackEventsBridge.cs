using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAttackEventsBridge : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackStartClip;
    [SerializeField] private AudioClip attackHitClip;
    [SerializeField] private AudioClip attackEndClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("FX")]
    [SerializeField] private GameObject attackWindupFx;
    [SerializeField] private GameObject attackHitFx;
    [SerializeField] private GameObject attackEndFx;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void OnAttackStarted()
    {
        PlayClip(attackStartClip);
        SetFxActive(attackWindupFx, true);
    }

    public void OnAttackHit()
    {
        PlayClip(attackHitClip);
        PulseFx(attackHitFx);
    }

    public void OnAttackFinished()
    {
        PlayClip(attackEndClip);
        SetFxActive(attackWindupFx, false);
        PulseFx(attackEndFx);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static void SetFxActive(GameObject fxObject, bool isActive)
    {
        if (fxObject == null)
            return;

        fxObject.SetActive(isActive);
    }

    private static void PulseFx(GameObject fxObject)
    {
        if (fxObject == null)
            return;

        fxObject.SetActive(false);
        fxObject.SetActive(true);
    }
}
