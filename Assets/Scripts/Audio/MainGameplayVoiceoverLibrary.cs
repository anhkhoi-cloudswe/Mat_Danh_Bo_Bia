using System;
using UnityEngine;

/// <summary>
/// Thư viện ánh xạ <see cref="MainGameplayVoiceKey"/> → AudioClip cho lời thoại main gameplay.
/// Phải đặt trong một thư mục Resources và đặt tên "MainGameplayVoiceoverLibrary" để
/// <see cref="MainGameplayVoiceover"/> nạp được qua Resources.Load.
/// </summary>
[CreateAssetMenu(fileName = "MainGameplayVoiceoverLibrary", menuName = "AnhBoBia/Main Gameplay Voiceover Library")]
public sealed class MainGameplayVoiceoverLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public MainGameplayVoiceKey key;
        public AudioClip clip;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    public Entry[] Entries
    {
        get => entries;
        set => entries = value ?? Array.Empty<Entry>();
    }
}
