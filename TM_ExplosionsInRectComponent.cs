using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Call a bunch of effects (not JUST explosions, but they rule) to happen in a given area.
/// </summary>
public class TM_ExplosionsInRectComponent : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Game objects to spawn at random. They should play particles and clean themselves up.")]
    private List<GameObject> explosionObjects;
    
    [SerializeField]
    [Tooltip("How often should foom and doom happen?")]
    private float timeBetweenExplosions;
    
    [SerializeField]
    [Tooltip("And for how long?")]
    private float totalLifetime;

    [SerializeField]
    [Tooltip("Optional last effect to play once we are done fooming and dooming.")]
    private GameObject finalParticleAtCenter;
    
    [Space(12)]
    [Header("Gibs on End")]
    [SerializeField]
    [Tooltip("Should we launch gibs at the end of our run? If so, set a sprite here.")]
    private Sprite gibSprite;

    [SerializeField] private int minGibs;
    [SerializeField] private int maxGibs;
    [SerializeField] private float minForce;
    [SerializeField] private float maxForce;

    [Space(12)]
    [Header("Screen fade during explosion")]
    [SerializeField]
    [Tooltip("Should we fade the screen to a certain color over time during this?")]
    private bool shouldFade;
    [SerializeField] private Color fadeColor;
    [SerializeField] private float fadeStartTime;
    [SerializeField] private float fadeDuration;
    [Tooltip("Important! If this isn't true, your fade will stay there until set otherwise.")]
    [SerializeField] private bool shouldClearFadeOnEnd;

    private bool bAlreadyCalledFadeThisRun;

    private bool bActive;
    private float lifetime;
    private float timeToNextExplosion;
    private Rect foomArea;

    // Update is called once per frame
    void Update()
    {
        if (!bActive)
            return;

        // don't do this. Why you do this?
        if (timeBetweenExplosions <= 0f)
            return;
            
        lifetime += Time.deltaTime;
        timeToNextExplosion += Time.deltaTime;
        while (timeToNextExplosion > timeBetweenExplosions)
        {
            //play a foom
            PlayEffectInAreaAtRandom();
            
            //tick down the clock
            timeToNextExplosion -= timeBetweenExplosions;
        }

        // done now
        if (lifetime >= totalLifetime)
        {
            if (finalParticleAtCenter != null)
            {
                //do this foom at center
                PlayFinalEffectInCenter();
            }
            
            //clean ourselves up
            PoolManager.ReturnToPool(gameObject);
        }

        //if we are to fade, begin so here.
        if ( shouldFade && 
             !bAlreadyCalledFadeThisRun &&
             lifetime > fadeStartTime)
        {
            bAlreadyCalledFadeThisRun = true;
            TM_Utilities.FadeCameraTo(fadeColor, fadeDuration, shouldClearFadeOnEnd);
        }

    }

    /// <summary>
    /// Play something somewhere in our zone.
    /// </summary>
    void PlayEffectInAreaAtRandom()
    {
        Vector2 foomLoc = foomArea.min;
        foomLoc.x += foomArea.width * Random.value;
        foomLoc.y += foomArea.height * Random.value;

        var foomGO = PoolManager.Instantiate(explosionObjects[Random.Range(0, explosionObjects.Count)]);

        foomGO.transform.position = foomLoc;
    }

    /// <summary>
    /// The last big foomaloom, if needed.
    /// </summary>
    void PlayFinalEffectInCenter()
    {
        var finalGO = PoolManager.Instantiate(finalParticleAtCenter);
        finalGO.transform.position = foomArea.center;

        if (gibSprite != null)
        {
            TM_Utilities.LaunchGibBurst( foomArea.center, new Vector2(minForce, maxForce),
            Random.Range(minGibs, maxGibs +1), gibSprite.name );
        }
    }

    /// <summary>
    /// Start the show, and make sure to send in an area to play all the explosions in.
    /// </summary>
    /// <param name="effectArea"></param>
    public void SetEffectAreaAndActivate(Rect effectArea)
    {
        foomArea = effectArea;
        bActive = true;
        timeToNextExplosion = timeBetweenExplosions;
    }

    /// <summary>
    /// Clean ourselves up so we behave correctly if brought back.
    /// </summary>
    void OnReturnToPool()
    {
        bActive = false;
        lifetime = 0f;
        bAlreadyCalledFadeThisRun = false;
    }

    private void OnEnable()
    {
        bActive = false;
        lifetime = 0f;
        bAlreadyCalledFadeThisRun = false;
    }
}
