using System;
using System.Collections.Generic;
using DarkTonic.MasterAudio;
using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static DarkTonic.MasterAudio.MasterAudio;
using Cysharp.Threading.Tasks;

// 音效播放的設定資料
public class AudioPlaySet
{
    public bool Loop = false;  // PlayOneTrackByName   PlayEffectByName 使用
    public float Volume = 1f;  // PlayOneTrackByName   PlayEffectByName 使用
    public float Crossfade = 0f;  // PlayOneTrackByName 使用
    public float Pitch = 1f;  // PlayEffectByName 使用
    // 播放音效時，背景音樂針對設定變小聲
    public float BgmVolume = 0.5f;  // PlayEffectByName 使用
    public bool CheckRepeat = false;  // PlayEffectByName 使用
    public AudioSource Audio = null;  // PlayEffectByName 使用
    public float Delay = 0f;  // PlayEffectByName 使用
}

public class AudioManager : Singleton<AudioManager>
{
    // 音效播放的狀態資料
    struct SfxPlayData
    {
        public string name;
        public float PlayTime;
        public PlaySoundResult audio;
        public float bgmVolume;
        public Action<string> OnFxEnd;
    }
    private const string _musicGroupName = "MusicList";
    [Header("預載 Audio 資源的 Label: preload_audio")]
    [ShowInInspector, PropertyOrder(-1), ReadOnly]
    private const string _preload_audio = "preload_audio";
    public static string currentMusicName = "";

    // 目前播放中的音效
    private static List<SfxPlayData> sfxPlayDatas = new List<SfxPlayData>();
    private static bool _isAudioManagerInit = false;
    private static Tween _bgmFadeTween;

    public static float MusicVolume
    {
        get
        {
            return MasterAudio.PlaylistMasterVolume;
        }
        set
        {
            MasterAudio.PlaylistMasterVolume = value;
        }
    }

    public static float SFXVolume
    {
        get
        {
            return MasterAudio.MasterVolumeLevel;
        }
        set
        {
            MasterAudio.MasterVolumeLevel = value;
        }
    }

    protected static void Initialized()
    {
        if (_isAudioManagerInit)
        {
            return;
        }

        // OnSoundFinished += OnSfxEnd;
        ClearMusicData();
        ClearSFXData();
        LoadAudioBundles();

        _isAudioManagerInit = true;

        // // 讀取playlist addressable 音效資料
        // Playlist playlist = MasterAudio.GrabPlaylist(_musicGroupName);
    }

    void Start()
    {
        Initialized();
    }

    void Update()
    {
        OnSfxEnd();
    }

    void OnDestroy()
    {
        this.gameObject.SetActive(false);
        // StopBGM();
        _isAudioManagerInit = false;
    }

    private static void LoadAudioBundles()
    {
        // 先查詢是否存在任何帶有該 Label 的資源，避免空載入
        var locationsHandle = Addressables.LoadResourceLocationsAsync(_preload_audio, typeof(AudioClip));
        locationsHandle.Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || handle.Result.Count == 0)
            {
                Addressables.Release(handle);
                return;
            }

            // 有資源才進行預載
            Addressables.LoadAssetsAsync<AudioClip>(_preload_audio, _ => { /* 預載用，逐一載入時不需動作 */ });
            Addressables.Release(handle);
        };
    }

    public static void ClearMusicData()
    {
        currentMusicName = "";
    }

    public static void ClearSFXData()
    {
        sfxPlayDatas.Clear();
    }

    public static void CheckBgmVolume()
    {
        float bgmVolume = 1.0f;
        for (int i = 0; i < sfxPlayDatas.Count; ++i)
        {
            SfxPlayData data = sfxPlayDatas[i];
            if (bgmVolume > data.bgmVolume)
            {
                bgmVolume = data.bgmVolume;
            }
        }

        // if (Math.Abs(MusicVolume - bgmVolume) > 0.0001)
        // {
        //     MusicVolume = bgmVolume;
        //     LogUtils.Log("CheckBgmVolume MusicVolume change to " + MusicVolume);
        // }
    }

    // 音效播放完畢，將音效播放資料清除
    public void OnSfxEnd()
    {
        for (int i = 0; i < sfxPlayDatas.Count; ++i)
        {
            SfxPlayData data = sfxPlayDatas[i];
            if (data.audio == null || !data.audio.ActingVariation.IsPlaying)
            {
                // LogUtils.Log("AudioManager OnSfxEnd name[" + data.name + "]. RemoveAt[" + i + "]");
                sfxPlayDatas.RemoveAt(i);
                if (data.OnFxEnd != null)
                {
                    data.OnFxEnd(data.name);
                    // LogUtils.Log("AudioManager OnSfxEnd name[" + data.name + "].");
                }
                i--;
            }
        }

        CheckBgmVolume();
    }

    // 停止指定名稱的音效
    public static void StopEffectByName(string name)
    {
        for (int i = 0; i < sfxPlayDatas.Count; ++i)
        {
            SfxPlayData data = sfxPlayDatas[i];
            if (data.name.Equals(name))
            {
                // LogUtils.Log("Stop Effect[" + name + "]");
                MasterAudio.StopAllOfSound(name);
            }
        }
    }

    // 檢查音效是否重複播放
    private static bool CheckSfxRepeat(string name, float checkRepeatTime)
    {
        for (int i = 0; i < sfxPlayDatas.Count; ++i)
        {
            SfxPlayData data = sfxPlayDatas[i];
            // 名稱相同而且在短時間內再次播放
            if (data.name.Equals(name) && data.PlayTime >= (Time.time - checkRepeatTime))
            {
                // 時間內表示重複播放
                return true;
            }
        }

        return false;
    }

    public static void PlayEffectByName(string name, AudioPlaySet audioPlaySet, Action<string> endCallback = null)
    {
        if (audioPlaySet != null)
        {
            PlayEffectByName(name, audioPlaySet.Delay, audioPlaySet.Volume, audioPlaySet.Pitch,
                             audioPlaySet.Loop, audioPlaySet.CheckRepeat, 0.01f, audioPlaySet.BgmVolume, audioPlaySet.Audio, endCallback);
        }
        else
        {
            PlayEffectByName(name);
        }
    }

    // 播放指定名稱音效
    public static void PlayEffectByName(string name, float delay = 0f, float volume = 1f,
                                        float pitch = 1f, bool loop = false, bool checkRepeat = false, float checkRepeatTime = 0.01f,
                                        float bgmVolume = 0.5f, AudioSource src = null, Action<string> endCallback = null)
    {
        if (!_isAudioManagerInit)
        {
            Initialized();
        }

        // 音效設定中有設定不可以重複播放
        if (checkRepeat)
        {
            if (CheckSfxRepeat(name, checkRepeatTime))
            {
                // LogUtils.Log("Repeat play:" + name);
                return;
            }
        }

        // UpdateAudioLoopState(name, loop);

        // 找出對應名稱的音效
        SfxPlayData data = new SfxPlayData();
        data.audio = MasterAudio.PlaySound(name, volumePercentage: volume, pitch: pitch);

        if (data.audio != null && data.audio.ActingVariation != null)
            data.audio.ActingVariation.VarAudio.loop = loop;

        data.PlayTime = Time.time;
        data.name = name;
        data.bgmVolume = bgmVolume;
        data.OnFxEnd = endCallback;
        // LogUtils.Log("AudioManager Play SFX[" + name + "][" + data.audio + "].");

        sfxPlayDatas.Add(data);
        CheckBgmVolume();
    }

    // 播放指定名稱音樂
    public static void PlayOneTrackByName(string name, AudioPlaySet audioPlaySet)
    {
        if (audioPlaySet != null)
        {
            PlayOneTrackByName(name, audioPlaySet.Loop, audioPlaySet.Volume, audioPlaySet.Crossfade);
        }
        else
        {
            PlayOneTrackByName(name);
        }
    }

    private static void UpdateAudioLoopState(string name, bool loop)
    {
        var result = MasterAudio.PlaySound(name);

        if (result != null && result.ActingVariation != null)
        {
            result.ActingVariation.VarAudio.loop = loop;
        }

        // MasterAudioGroup audioGroup = MasterAudio.GrabGroup(name);
        // if (audioGroup == null)
        // {
        //     return;
        // }

        // if (loop)
        // {
        //     audioGroup.curVariationMode = MasterAudioGroup.VariationMode.LoopedChain;
        // }
        // else
        // {
        //     audioGroup.curVariationMode = MasterAudioGroup.VariationMode.Normal;
        // }
    }

    private static void UpdateMusicLoopState(Playlist playlist, string name, bool loop)
    {
        foreach (MusicSetting setting in playlist.MusicSettings)
        {
            if (setting.clip != null && setting.clip.name.Equals(name))
            {
                setting.isLoop = loop;
                return;
            }
        }
    }

    private static bool CheckMusicInPlaylist(Playlist playlist, string name)
    {
        foreach (MusicSetting setting in playlist.MusicSettings)
        {
            if (setting.clip != null && setting.clip.name.Equals(name))
            {
                return true;
            }
        }

        LogUtils.LogWarning("AudioManager Music Name [" + name + "] not found in playlist.");
        return false;
    }

    // 播放指定名稱音樂
    public static void PlayOneTrackByName(string name, bool loop = true, float volume = 1f, float crossfade = 1.0f)
    {
        if (!_isAudioManagerInit)
        {
            Initialized();
        }

        if (!string.IsNullOrEmpty(currentMusicName) && !currentMusicName.Equals(name))
        {
            // StopBGM(crossfade);
        }

        if (_bgmFadeTween != null)
            _bgmFadeTween.Kill();

        MusicVolume = volume;

        currentMusicName = name;

        PlaylistController playlistController = MasterAudio.OnlyPlaylistController;
        // 避免撥放到同一首音樂
        if (playlistController.CurrentPlaylistClip != null && currentMusicName.Equals(playlistController.CurrentPlaylistClip.name))
        {
            return;
        }

        MasterAudio.StartPlaylistOnClip(_musicGroupName, currentMusicName);

        // Playlist playlist = MasterAudio.GrabPlaylist(_musicGroupName);
        // 檢查檔名有沒有在PlayList裡面
        // if (CheckMusicInPlaylist(playlist, currentMusicName))
        // {
        //     MasterAudio.StartPlaylistOnClip(_musicGroupName, currentMusicName);
        //     // 設定是否loop
        //     UpdateMusicLoopState(playlist, currentMusicName, loop);
        // }

        //LogUtils.Log("StartPlaylist[" + _musicGroupName + "]  clip[" + currentMusicName + "]");
    }

    public static void StopBGM(float crossfade = 1.0f)
    {
        if (string.IsNullOrEmpty(currentMusicName))
        {
            return;
        }

        if (_bgmFadeTween != null)
            _bgmFadeTween.Kill();

        // BGM Fade Out
        _bgmFadeTween = DOTween.To(() => MusicVolume, x => MusicVolume = x, 0.0f, crossfade).OnComplete(() =>
        {
            MasterAudio.StopPlaylist();
            ClearMusicData();
            MusicVolume = 1;
        });

        // MasterAudio.StopPlaylist();
        // ClearMusicData();
    }

    public static void PauseMusic()
    {
        PlaylistController playlistController = MasterAudio.OnlyPlaylistController;
        if (playlistController.CurrentPlaylist == null)
        {
            return;
        }

        playlistController.PausePlaylist();
        // playlistController.CurrentPlaylistSource.pitch = 0.0f;
    }

    public static void ResumeMusic()
    {
        PlaylistController playlistController = MasterAudio.OnlyPlaylistController;
        if (playlistController.CurrentPlaylist == null)
        {
            return;
        }

        playlistController.UnpausePlaylist();
        // playlistController.CurrentPlaylistSource.pitch = 1.0f;
    }

    // 針對音樂靜音
    public static void MuteMusic(bool mute)
    {
        if (mute)
        {
            MasterAudio.MuteAllPlaylists();
        }
        else
        {
            MasterAudio.UnmuteAllPlaylists();
        }
    }

    // 針對音效靜音
    public static void MuteSFX(bool mute)
    {
        if (mute)
        {
            MasterAudio.MasterVolumeLevel = 0.0f;
        }
        else
        {
            MasterAudio.MasterVolumeLevel = 1.0f;
        }
    }

    [Button]
    public static void OnFadeInBGMVolume()
    {
        FadeBGMVolume(1.0f, 0.5f);
    }

    [Button]
    public static void OnFadeOutBGMVolume()
    {
        FadeBGMVolume(0.0f, 0.5f);
    }

    public static void FadeBGMVolume(float volume, float fadeTime)
    {
        if (string.IsNullOrEmpty(currentMusicName))
        {
            return;
        }

        LogUtils.Log($"FadeBGMVolume to {volume} in {fadeTime} seconds.");

        if (_bgmFadeTween != null)
            _bgmFadeTween.Kill();

        _bgmFadeTween = DOTween.To(() => MusicVolume, x => MusicVolume = x, volume, fadeTime);
    }
}