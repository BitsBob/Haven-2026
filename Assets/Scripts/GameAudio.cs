using UnityEngine;

public class GameAudio : ScriptableObject
{
    [Header("Music")]
    [Tooltip("Looping background track.")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    [Header("Player")]
    [Tooltip("One is picked at random for each throw.")]
    public AudioClip[] throwClips;
    public AudioClip jump;
    public AudioClip playerDeath;

    [Header("Enemies")]
    public AudioClip slimeHit;
    public AudioClip slimeDeath;
    public AudioClip skeletonDeath;
    public AudioClip batDeath;

    [Header("World")]
    [Tooltip("Played when a tower is activated.")]
    public AudioClip towerOpen;
    public AudioClip waveVictory;

    [Header("Mix")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Tooltip("Enemy sounds fade out with distance and are silent beyond this many metres.")]
    public float hearingRange = 70f;
}
