using UnityEngine.Audio;
using UnityEngine;

public class AudioPlayer : MonoBehaviour
{
    public AudioSource source;
    public AudioClip[] sfxClips;

    public void PlaySFX(int index)
    {
        if(index >= 0 && index < sfxClips.Length)
        {
            source.clip = sfxClips[index];
            source.Play();
        }
    }
}