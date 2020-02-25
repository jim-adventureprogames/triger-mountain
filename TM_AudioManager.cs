using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class TM_MusicTrack
{
    public string strName;
    public AudioClip music;
    public bool bShouldLoop;
}

public enum EMusicCurrentlyPlaying
{
    
}

public class TM_AudioManager : MonoBehaviour
{
    public static List<TM_MusicTrack> musicTracks;

    private static float fSFXVolume = 1.0f;
    private static float fMusicVolume = 1.0f;

    public static AudioSource mainMusicSource;

    private static TM_AudioManager _instance;

    private static List<AudioSource> sourceList;
    private static int iSourceIndex;

    /// <summary>
    /// Sources that play looping sounds. If we run out of these, we will make more at runtime or just say Deal With It
    /// </summary>
    private static List<AudioSource> loopingSources;

    
    private static float fUIClipTimer;

    private static Dictionary<string, AudioClip> loadedClips;

    private static TM_AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TM_AudioManager>();

                if (_instance != null)
                {
                    return _instance;
                }

                GameObject go = new GameObject("TM_AudioManager");
                _instance = go.AddComponent<TM_AudioManager>();
                Initialize();
                
            }

            return _instance;
        }
    }

    static void Initialize()
    {
        loadedClips = new Dictionary<string, AudioClip>();
        
    }

	public static void Start ()
	{
	}

    public static void InitSources()
    {
        GameObject go = new GameObject();
        go.transform.parent = Instance.transform;
        mainMusicSource = go.AddComponent<AudioSource>();

        sourceList = new List<AudioSource>();
        loopingSources = new List<AudioSource>();
        for (int t = 0; t < 32; t++)
        {
            go = new GameObject();
            go.transform.parent = Instance.transform;
            sourceList.Add(go.AddComponent<AudioSource>());

            go = new GameObject();
            go.transform.parent = Instance.transform;
            loopingSources.Add(go.AddComponent<AudioSource>());
        }
        
        

        fSFXVolume = PlayerPrefs.GetFloat("sfx_volume", 1.0f);
        fMusicVolume = PlayerPrefs.GetFloat("music_volume", 0.8f);
    }
	
    public void Update()
    {
        if (fUIClipTimer > 0f)
        {
            fUIClipTimer -= Time.deltaTime;
        }
	}

    public static void ChangeMusicVolume(float fNewVolume)
    {
        fMusicVolume = fNewVolume;
        mainMusicSource.volume = fNewVolume;
    }

    public static void ChangeSFXVolume(float fNewVolume)
    {
        fSFXVolume = fNewVolume;
    }

    public static float GetSFXVolume() { return fSFXVolume; }
    public static float GetMusicVolume() { return fMusicVolume; }


    /// <summary>
    /// If we've cached this clip, cool. If not, try and cache it and play it.
    /// </summary>
    /// <param name="clipPath"></param>
    public static void PlayClip(string clipPath)
    {
        AudioClip playMe;
        if (!loadedClips.TryGetValue(clipPath, out playMe))
        {
            playMe = Resources.Load<AudioClip>(clipPath);
            loadedClips[clipPath] = playMe;
        }
        
        PlayClip(playMe);
    }
    
    public static int PlayClip(AudioClip clip)
    {
        if (sourceList == null || clip == null)
            return -1;

        AudioSource hackSource = sourceList[iSourceIndex];
        iSourceIndex++;
        if (iSourceIndex >= sourceList.Count)
            iSourceIndex = 0;


        hackSource.volume = fSFXVolume;
        hackSource.pitch = Random.Range(0.9f, 1.1f);
        hackSource.PlayOneShot(clip);

        return iSourceIndex;
    }

    /// <summary>
    /// Play a clip that will not end until we tell it to.
    /// </summary>
    /// <param name="clip"></param>
    /// <returns></returns>
    public static int PlayLoopingClip(AudioClip clip)
    {
        if (loopingSources == null || clip == null)
            return -1;
        
        int idx = 0;
        foreach (var source in loopingSources)
        {
            if (!source.isPlaying)
            {
                source.volume = fSFXVolume;
                source.pitch = Random.Range(0.9f, 1.1f);
                source.clip = clip;
                source.loop = true;
                source.Play();
                return idx;
            }

            idx++;
        }

        return -1;
    }

    public static bool StopLoopingClip(int clipIdx)
    {
        if (clipIdx == -1 || clipIdx >= loopingSources.Count)
            return false;
        
        loopingSources[clipIdx].Stop();
        return true;
    }

    //Only allow for UIClips to play every X seconds to prevent maddening noise from
    //UI spamming
    public static void PlayUIClip(AudioClip clip)
    {
        if (fUIClipTimer > 0f)
            return;

        fUIClipTimer = 0.2f;
        PlayClip(clip);
    }


    public static void StopCurrentMusic()
    {
        mainMusicSource.Stop();
    }

    public static void PlayMusic(TM_MusicTrack track)
    {
        PlayMusicInternal(track);
    }

    public static void FadeOutMusic(float fFadeTime)
    {
        _instance.StartCoroutine(FadeOutMusicInternal(fFadeTime));
    }

    private static IEnumerator FadeOutMusicInternal(float fFadeTime)
    {
        float fTimer = fFadeTime;
        while (fTimer > 0f)
        {
            fTimer -= Time.deltaTime;
            mainMusicSource.volume = Mathf.Lerp(fTimer, fMusicVolume, fTimer/fFadeTime);
            yield return null;
        }

        mainMusicSource.Stop();
    }

    private static IEnumerator FadeAndSuspendCurrentMusicInternal(float fFadeTime)
    {
        float fTimer = fFadeTime;
        while (fTimer > 0f)
        {
            fTimer -= Time.deltaTime;
            mainMusicSource.volume = Mathf.Lerp(fTimer, fMusicVolume, fTimer / fFadeTime);
            yield return null;
        }

        mainMusicSource.Pause();
        mainMusicSource.transform.localScale = new Vector3(mainMusicSource.time,0,0);
    }


    private static void PlayMusicInternal(TM_MusicTrack track)
    {
        if (mainMusicSource.isPlaying)
            mainMusicSource.Stop();

        mainMusicSource.volume = fMusicVolume;
        mainMusicSource.clip = track.music;
        mainMusicSource.loop = track.bShouldLoop;
        mainMusicSource.time = 0f;
        mainMusicSource.Play();
    }
}
