using System;
using System.Collections.Generic;
using UnityEngine;

public enum MainGameplayVoiceKey
{
    None,
    NgaDay1Welcome,
    NgaDay1Receive,
    ShipperDay1Welcome,
    ShipperDay1Receive,
    FanDay1Welcome,
    FanDay1Receive,
    PlayerDay1MorningEnd,
    XamMinhNight1,
    BanhMiNight1,
    PlayerPickupLighter,
    PlayerReturnLighter,
    HuyReturnLighter,
    HuyWarning,
    PlayerInspectTrash,
    PlayerAfterNgaDay2,
    NgaDay2Welcome,
    NgaDay2Receive,
    ShipperDay2Welcome,
    ShipperDay2Receive,
    MeLiuDay2Welcome,
    MeLiuDay2Receive,
    PlayerAskMeLiu,
    PlayerDetectMeLiu,
    PlayerAfterMeLiu,
    PlayerNight2Intro,
    PlayerNight2Surveillance,
    PlayerNight2EvidenceComplete,
    PlayerNight2CallTeam,
    NarratorDay1MorningIntro,
    PlayerNight1Intro,
    PlayerNight1ObserveHuy,
    PlayerDay2MorningIntro,
    PlayerAfterShipperDay2,
    PlayerNight2CallTeamConfirmed,
    PlayerAfterSearchSpawns,
    PlayerCollectFirstEvidence,
    PlayerCompleteTrashPhase1
}

/// <summary>Plays the authored voice lines used by the main gameplay story.</summary>
public sealed class MainGameplayVoiceover : MonoBehaviour
{
    private const string LibraryResourceName = "MainGameplayVoiceoverLibrary";
    private static MainGameplayVoiceover instance;

    private readonly Dictionary<MainGameplayVoiceKey, AudioClip> clips = new();
    private AudioSource source;

    public static MainGameplayVoiceover Instance
    {
        get
        {
            if (instance == null) CreateRuntimeInstance();
            return instance;
        }
    }

    public float RemainingTime => source != null && source.isPlaying
        ? Mathf.Max(0f, source.clip.length - source.time)
        : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRuntimeInstance()
    {
        if (instance != null) return;
        var go = new GameObject("MainGameplayVoiceover");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MainGameplayVoiceover>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        MainGameplayVoiceoverLibrary library = Resources.Load<MainGameplayVoiceoverLibrary>(LibraryResourceName);
        if (library == null)
        {
            Debug.LogWarning($"[Voiceover] Missing Resources/{LibraryResourceName}.asset.");
            return;
        }

        foreach (MainGameplayVoiceoverLibrary.Entry entry in library.Entries)
        {
            if (entry.key != MainGameplayVoiceKey.None && entry.clip != null)
                clips[entry.key] = entry.clip;
        }
    }

    public float Play(MainGameplayVoiceKey key)
    {
        if (key == MainGameplayVoiceKey.None) return 0f;
        if (!clips.TryGetValue(key, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning($"[Voiceover] No AudioClip assigned for {key}.");
            return 0f;
        }

        source.Stop();
        source.clip = clip;
        source.Play();
        return clip.length;
    }

    public static float PlayLine(MainGameplayVoiceKey key) => Instance.Play(key);
}
