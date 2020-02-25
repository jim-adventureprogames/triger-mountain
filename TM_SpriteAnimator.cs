using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Object = System.Object;
using Random = System.Random;

/// <summary>
/// A point in a frame that can be grabbed for reference by game code to draw stuff there.
/// </summary>
[System.Serializable]
public class TM_AnimSocket
{
    [SerializeField]
    private string name;
    
    [SerializeField]
    private Vector2Int location;

    public string Name => name;
    public Vector2Int Location => location;
    
    public TM_AnimSocket(string name, int x, int y)
    {
        this.name = name;
        location = new Vector2Int(x,y);
    }
    
    public TM_AnimSocket(string name, Vector2Int loc)
    {
        this.name = name;
        location = loc;
    }

    public void SetLocation(int x, int y)
    {
        location = new Vector2Int(x,y);
    }
    
    public void SetLocation(Vector2Int loc)
    {
        location = loc;
    }
}


[System.Serializable]
public class TM_AnimFrame
{
    [SerializeField]
    public Sprite sprite;
    [SerializeField]
    public float frametime;
    [SerializeField]
    public string eventToFire;
    [SerializeField]
    public AudioClip clip;

    /// <summary>
    /// This list is modified by the editor and serialized
    /// </summary>
    [SerializeField]
    public List<TM_AnimSocket> sockets = new List<TM_AnimSocket>();

    /// <summary>
    /// This list is built at runtime from the socket list in the editor
    /// </summary>
    private Dictionary<string, TM_AnimSocket> socketLookupDictionary;

    private bool dictionaryInitialized = false;

    public TM_AnimFrame()
    {

    }

    /// <summary>
    /// Don't call this outside of the editor
    /// </summary>
    /// <param name="name"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    public void SetSocket(string name, int x, int y)
    {
        if (sockets == null)
        {
            sockets = new List<TM_AnimSocket>();
        }

        if (!dictionaryInitialized)
            InitializeSocketDictionary();

        TM_AnimSocket s = null;
        if (socketLookupDictionary.TryGetValue(name, out s))
        {
            s.SetLocation(x,y);   
        }
        else
        {
            s = new TM_AnimSocket(name, x, y);
            sockets.Add(s);
            socketLookupDictionary[name] = s;
        }
    }
    
    /// <summary>
    /// Don't call this outside of editor code.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="loc"></param>
    public void SetSocket(string name, Vector2Int loc)
    {
        SetSocket(name, loc.x, loc.y);
    }

    public Vector2Int GetSocketLocation(string socketName)
    {
        if (!dictionaryInitialized)
            InitializeSocketDictionary();

        if (socketLookupDictionary == null || !socketLookupDictionary.TryGetValue(socketName, out var s))
            return Vector2Int.zero;   

        return s.Location;
    }

    void InitializeSocketDictionary()
    {
        socketLookupDictionary = new Dictionary<string, TM_AnimSocket>();
        if (sockets != null)
        {
            foreach (var s in sockets)
            {
                socketLookupDictionary[s.Name] = s;
            }
        }

        dictionaryInitialized = true;
    }

}

[System.Serializable]
public class TM_SpriteAnim
{
    [SerializeField]
    public string Name;
    [SerializeField]
    public EShepAnimBehavior actionOnEnd;
    [SerializeField]
    public List<TM_AnimFrame> Frames;
    [SerializeField]
    public List<float> listBranchAnims_Chance;
    [SerializeField]
    public List<string> listBranchAnims_Name;
    [SerializeField]
    public string nextAnim;
    [SerializeField]
    public int loopThisManyTimes;


    public TM_SpriteAnim()
    {
        Frames = new List<TM_AnimFrame>();
        listBranchAnims_Chance = new List<float>();
        listBranchAnims_Name = new List<string>();
    }

    public Vector2Int GetSocketLocation(string socketName, int frameIdx)
    {
        if( frameIdx < 0 || frameIdx >= Frames.Count)
            return Vector2Int.zero;

        return Frames[frameIdx].GetSocketLocation(socketName);
    }

    public void SetSocket(string name, int frameIdx, int x, int y)
    {
        if( frameIdx < 0 || frameIdx >= Frames.Count)
        {
            return;
        }

        Frames[frameIdx].SetSocket(name, x,y);
    }

    public void SetSocket(string name, int frameIdx, Vector2Int loc)
    {
        SetSocket(name, frameIdx, loc.x, loc.y);
    }
}

public enum EShepAnimBehavior
{

    loop = 0,
    play_then_random_branch,
    play_once,
    play_once_and_hide,
    play_once_and_disable_go,
    play_once_and_destroy,
    play_once_then_next,
    loop_count_and_then_next,
    MAX,

}

[System.Serializable]
public class TM_SpriteAnimator : MonoBehaviour 
{
    [HideInInspector]
    public TM_SpriteAnim currentAnimation;

    [HideInInspector]
    public SpriteRenderer spriteRenderer;

    [HideInInspector]
    public EShepAnimBehavior behavior;

    [SerializeField]
    public List<TM_SpriteAnim> animationList;
    public string StartAnimation;

    private AudioSource audioSource;

    float fTimer;
    int iFrameIndex;

    private bool bPlaying;
    
    /// <summary>
    /// May not need to track this separately from bPlaying, but maybe so. 
    /// </summary>
    private bool bIsPaused;

    public float fDelayBeforeStart;

    /// <summary>
    /// Some animations loop X times before moving to a new anim.
    /// </summary>
    private int loopCount;

    //Delegates on animation changes
    private List<Action<GameObject>> onAnimStartActions;
    private List<Action<GameObject>> onAnimEndActions;

    private bool bPlayStartAnimThisFrame;

    /// <summary>
    /// todo: pretty this up
    /// </summary>
    public float animRateAdjustment;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
    }

	// Use this for initialization
	void Start ()
	{
        fTimer = 0;
	    if (currentAnimation != null && currentAnimation.Frames != null && currentAnimation.Frames.Count > 0 )
	    {
	        fTimer = currentAnimation.Frames[0].frametime;
	    }

        bPlayStartAnimThisFrame = true;
    }

    /// <summary>
    /// Search the active animation for the socket position.
    /// </summary>
    /// <param name="socketName">socket we're lookin' for</param>
    /// <param name="frameIdx">pick a frame! -1 means choose the active frame</param>
    /// <returns></returns>
    public Vector2Int GetSocketLocation(string socketName, int frameIdx = -1)
    {
        if( currentAnimation == null )
            return Vector2Int.zero;
            
        if (frameIdx == -1)
            frameIdx = iFrameIndex;

        return currentAnimation.GetSocketLocation(socketName, frameIdx);
    }


    void PlayStartAnimationIfExists()
    {
        if (!string.IsNullOrEmpty(StartAnimation) && animationList != null)
        {
            for(int t=0; t < animationList.Count; t++)
                if( animationList[t] != null &&
                    animationList[t].Name == StartAnimation)
                    SetAnimation(animationList[t]);
        }
    }

    void OnTakeFromPool()
    {
        bPlayStartAnimThisFrame = true;
    }
    
    public void SetAnimation( TM_SpriteAnim newAnim )
    {
        currentAnimation = newAnim;
        PlayAnim();
    }

    public void SetAnimation(string strAnimName)
    {
        if (!string.IsNullOrEmpty(strAnimName) && animationList != null)
        {
            for (int t = 0; t < animationList.Count; t++)
                if (animationList[t] != null &&
                    animationList[t].Name == strAnimName)
                {
                    SetAnimation(animationList[t]);
                    return;
                }
        }

        //Debug.LogError("OH NO");
    }

    TM_SpriteAnim GetAnimByString(string strAnim)
    {
        if (!string.IsNullOrEmpty(strAnim) && animationList != null)
        {
            for (int t = 0; t < animationList.Count; t++)
                if (animationList[t] != null &&
                    animationList[t].Name == strAnim)
                    return animationList[t];
        }

        return null;
    }

    public void StartIfNotRunning(string strAnim)
    {
        //if we are playing this anim already, don't do squat
        TM_SpriteAnim maybeThis = GetAnimByString(strAnim);
        if (maybeThis == currentAnimation &&
            bPlaying)
        {
            return;
        }

        SetAnimation(strAnim);
    }

    public void StartAndStopCurrent(string strAnim)
    {
        StopAnim();
        currentAnimation = null;
        StartIfNotRunning(strAnim);
    }
    
    public void PlayAnim()
    {
        var frame = currentAnimation.Frames[0];
        if (currentAnimation != null && currentAnimation.Frames != null && currentAnimation.Frames.Count > 0)
        {
            fTimer = frame.frametime;
        }
        else
        {
            return;
        }

        iFrameIndex = 0;

        bPlaying = true;
        spriteRenderer.enabled = true;
        spriteRenderer.sprite = frame.sprite;
        if (frame.clip != null)
            TM_AudioManager.PlayClip(frame.clip);
    }

    public void StopAnim()
    {
        bPlaying = false;
    }

    public void Pause()
    {
        bPlaying = false;
        bIsPaused = true;
    }

    public void UnPause()
    {
        if (!bIsPaused)
            return;

        bIsPaused = false;
        
        //No need to start up an animation, ideally we had one playing and it should resume.
        //if there was not one playing, nothing bad happens here either. Hopefully.
        bPlaying = true;
    }


	void Update () 
    {
        if (bPlayStartAnimThisFrame)
        {
            PlayStartAnimationIfExists();
            bPlayStartAnimThisFrame = false;
        }
        
        if (currentAnimation == null)
            return;
	    if (!bPlaying)
	        return;

        var timeThisFrame = Time.deltaTime * (animRateAdjustment + 1.0f);

        if (fDelayBeforeStart > 0f)
        {
            spriteRenderer.enabled = false;
            fDelayBeforeStart -= timeThisFrame;
            if (fDelayBeforeStart <= 0f)
            {
                spriteRenderer.enabled = true;
            }
            return;
        }

        fTimer -= timeThisFrame;
        if( fTimer < 0 )
        {

            iFrameIndex++;
            if (iFrameIndex >= currentAnimation.Frames.Count) // Sprites.Count)
            {
                iFrameIndex = 0;
                switch (currentAnimation.actionOnEnd)
                {
                    case EShepAnimBehavior.loop:
                        //Debug.Log("Loopy doops! " + currentAnimation.Name);
                        break;
                    case EShepAnimBehavior.play_then_random_branch:
                        StartIfNotRunning(ChooseNextAnimFromBranches());
                        break;
                    case EShepAnimBehavior.play_once:
                        iFrameIndex = currentAnimation.Frames.Count - 1;
                        OnAnimEnd();
                        break;
                    case EShepAnimBehavior.play_once_and_hide:
                        spriteRenderer.enabled = false;
                        OnAnimEnd();
                        break;
                    case EShepAnimBehavior.play_once_and_disable_go:
                        OnAnimEnd();
                        gameObject.SetActive(false);
                        break;
                    case EShepAnimBehavior.play_once_and_destroy:
                        spriteRenderer.enabled = false;
                        bPlaying = false;
                        Destroy(gameObject,1.0f);
                        break;
                    case EShepAnimBehavior.play_once_then_next:
                        OnAnimEnd();
                        StartIfNotRunning(currentAnimation.nextAnim);
                        break;
                    case EShepAnimBehavior.loop_count_and_then_next:
                        loopCount++;
                        if (loopCount >= currentAnimation.loopThisManyTimes)
                        {
                            OnAnimEnd();
                            loopCount = 0;
                            StartIfNotRunning(currentAnimation.nextAnim);
                        }
                        break;
                }
            }

            if (bPlaying)
            {
                var newFrame = currentAnimation.Frames[iFrameIndex];
                fTimer = newFrame.frametime;
                spriteRenderer.sprite = newFrame.sprite;
                if (newFrame.clip != null)
                    TM_AudioManager.PlayClip(newFrame.clip);
            }

        }
	}

    /// <summary>
    /// Rolls dice and picks the next anim to play
    /// </summary>
    string ChooseNextAnimFromBranches()
    {
        var totalChance = 0f;
        foreach (var f in currentAnimation.listBranchAnims_Chance)
        {
            totalChance += f;
        }

        var roll = UnityEngine.Random.value * totalChance;
        for (int t = 0; t < currentAnimation.listBranchAnims_Chance.Count(); t++)
        {
            if (roll <= currentAnimation.listBranchAnims_Chance[t])
            {
                return currentAnimation.listBranchAnims_Name[t];
            }

            roll -= currentAnimation.listBranchAnims_Chance[t];
        }

        return "";
    }

    void OnAnimEnd()
    {
        bPlaying = false;
        
        if( onAnimEndActions != null )
        {
            for (int t = 0; t < onAnimEndActions.Count; t++)
            {
                onAnimEndActions[t](gameObject);
            }
        }
        if (onAnimEndActions != null)
        {
            onAnimEndActions.Clear();
        }
    }

    public void AddFunctionOnAnimStart(Action<GameObject> func)
    {
        AddActionToListInternal(ref onAnimStartActions, func);
    }

    public void AddFunctionOnAnimEnd(Action<GameObject> func)
    {
        AddActionToListInternal( ref onAnimEndActions, func);
    }

    void AddActionToListInternal(ref List<Action<GameObject>> myList, Action<GameObject> func)
    {
        if (myList == null)
            myList = new List<Action<GameObject>>();

        if (!myList.Contains(func))
            myList.Add(func);
    }

    /// <summary>
    /// Play a sound 
    /// </summary>
    /// <param name="c"></param>
    void PlayClip(AudioClip c)
    {
        
    }

    /// <summary>
    /// Copy all anims and information from a different animator.
    /// </summary>
    /// <param name="other"></param>
    public void CopyFrom(TM_SpriteAnimator other)
    {
        behavior = other.behavior;
        
        animationList.Clear();
        foreach (var anim in other.animationList)
        {
            animationList.Add(anim);
        }

        StartAnimation = other.StartAnimation;
        fDelayBeforeStart = other.fDelayBeforeStart;
        
        onAnimStartActions = new List<Action<GameObject>>();
        if( other.onAnimStartActions != null )
        {
            foreach (var action in other.onAnimStartActions)
            {
                onAnimStartActions.Add(action);
            }
        }
        
        onAnimEndActions = new List<Action<GameObject>>();
        if( other.onAnimEndActions != null )
        {
            foreach (var action in other.onAnimEndActions)
            {
                onAnimEndActions.Add(action);
            }
        }

        fTimer = 0f;
        iFrameIndex = 0;
    }
}

