using UnityEngine;

public static class MusicPlayer
{
    static AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { source = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        if (source != null) return;
        var audio = Resources.Load<GameAudio>("GameAudio");
        if (audio == null || audio.music == null) return;

        var go = new GameObject("Music");
        Object.DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.clip = audio.music;
        source.loop = true;
        source.volume = audio.musicVolume;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.Play();
    }

    public static void SetVolume(float volume) { if (source != null) source.volume = Mathf.Clamp01(volume); }
}
