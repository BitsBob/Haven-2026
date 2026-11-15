using System.Collections.Generic;
using UnityEngine;

public static class Sfx
{
    const int Voices = 14;

    static GameAudio audio;
    static AudioSource[] sources;
    static int next;
    static readonly Dictionary<AudioClip, float> lastPlayed = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { audio = null; sources = null; next = 0; lastPlayed.Clear(); }

    static bool Ready()
    {
        if (sources != null) return audio != null;
        audio = Resources.Load<GameAudio>("GameAudio");
        var go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);
        sources = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            sources[i] = go.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
            sources[i].ignoreListenerPause = true;
        }
        return audio != null;
    }

    public static void Throw()
    {
        if (!Ready() || audio.throwClips == null || audio.throwClips.Length == 0) return;
        Play(audio.throwClips[Random.Range(0, audio.throwClips.Length)], 0.7f, 0.1f, 0.03f);
    }

    public static void Jump() { if (Ready()) Play(audio.jump, 0.6f, 0.06f, 0.05f); }
    public static void PlayerDeath() { if (Ready()) Play(audio.playerDeath, 1f, 0f, 0.5f); }
    public static void TowerOpen() { if (Ready()) Play(audio.towerOpen, 1f, 0f, 0.5f); }
    public static void WaveVictory() { if (Ready()) Play(audio.waveVictory, 0.9f, 0f, 1f); }

    public static void EnemyHit(EnemyType type, Vector3 position)
    {
        if (Ready() && type == EnemyType.Normal) PlayAt(audio.slimeHit, position, 0.45f, 0.15f, 0.07f);
    }

    public static void EnemyDeath(EnemyType type, Vector3 position)
    {
        if (!Ready()) return;
        AudioClip clip = type == EnemyType.Heavy ? audio.skeletonDeath : (type == EnemyType.Flying ? audio.batDeath : audio.slimeDeath);
        PlayAt(clip, position, 0.7f, 0.15f, 0.06f);
    }

    static void PlayAt(AudioClip clip, Vector3 position, float volume, float pitchSpread, float minGap)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float distance = Vector3.Distance(cam.transform.position, position);
        if (distance >= audio.hearingRange) return;
        float falloff = 1f - distance / audio.hearingRange;
        Play(clip, volume * falloff * falloff, pitchSpread, minGap);
    }

    static void Play(AudioClip clip, float volume, float pitchSpread, float minGap)
    {
        if (clip == null) return;
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(clip, out float last) && now - last < minGap) return;
        lastPlayed[clip] = now;

        AudioSource source = sources[next];
        next = (next + 1) % Voices;
        source.pitch = 1f + Random.Range(-pitchSpread, pitchSpread);
        source.PlayOneShot(clip, volume * audio.sfxVolume);
    }
}
