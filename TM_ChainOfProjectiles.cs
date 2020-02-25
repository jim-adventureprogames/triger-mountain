using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A collection of projectiles chained one after the other. The chain keeps track of previous weapon headings so that
/// you can get the whip effect that Jim so craved. The flame thrower from arcade/MegaDrive classic MERCS is the inspiration
/// for this, but I bet before I'm done I will have a chain with spikey balls or something. 
/// </summary>
public class TM_ChainOfProjectiles : MonoBehaviour
{
    /// <summary>
    /// The hot hot flames. Or cold cold ice, whatever.
    /// </summary>
    private TM_Projectile[] boundProjectiles;

    private int chainLinkCount;
	
    /// <summary>
    /// Keep track of where the weapon was facing over time, and move the far away flame orbs towards
    /// those spots. This lets us whip our flame back and forth.
    /// </summary>
    protected List<float> cachedHeadingValues;

    protected bool bInitialized;



    protected float lastCachedHeadingTime;
	
    [SerializeField] 
    [Tooltip("How many seconds between taking snapshots of weapon heading? Hint: a small number.")]
    protected float timeBetweenHeadingCaches;

    /// <summary>
    /// When we turn the fire on and off, we need to make sure we're only doing one of those at a time.
    /// </summary>
    private Coroutine flameActivationCoroutine;


	/// <summary>
	/// The chain may take additional time to activate / deactivate, we need to track the actual state of all the links
	/// rather than rely on the weapon's on/off state.
	/// </summary>
    private bool allLinksDisabled = true;
    
	[SerializeField] 
	[Tooltip("Fwooomph")]
	protected AudioClip clip_startFire;

	[SerializeField] 
	[Tooltip("ooooooooshhhhooooooshhoooooooshh")]
	protected AudioClip clip_loopFire;
	/// <summary>
	/// Keep track of the handle for this looping sound.
	/// </summary>
	protected int loopFireIdx;

	[SerializeField] 
	[Tooltip("shwfwip")]
	protected AudioClip clip_endFire;

	/// <summary>
	/// When we are activated, all of our links should be facing one direction, and not the left over direction
	/// from the previous activation.
	/// </summary>
	protected bool bForceLinksToForwardOnThisUpdate;

	/// <summary>
	/// For weapons with multiple chains that fire in different directions.
	/// </summary>
	[HideInInspector] 
	public float rotationFromForward;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

	/// <summary>
	/// Set up the chain -- nothing works until you call this.
	/// </summary>
	/// <param name="numLinks">How many links will the chain have?</param>
	/// <param name="headingRadians">The initial forward value for the chain</param>
	/// <param name="projectilePrefab"></param>
	/// <param name="owningCombatComponent"></param>
	/// <param name="owningWeapon"></param>
    public void InitializeChain(int numLinks, float headingRadians, TM_Projectile projectilePrefab, TM_CombatComponent owningCombatComponent,
							    TM_Weapon owningWeapon)
    {
    	gameObject.SetActive(true);
    
	    if (boundProjectiles != null)
	    {
		    for (int t = 0; t < boundProjectiles.Length; t++)
		    {
			    boundProjectiles[t].ReturnToPool();
		    }
	    }

	    chainLinkCount = numLinks;
	    cachedHeadingValues = new List<float>();
	    for (int t = 0; t < chainLinkCount; t++)
	    {
		    cachedHeadingValues.Add(headingRadians);
	    }
	    
		boundProjectiles = new TM_Projectile[chainLinkCount];
	    for (int t = 0; t < chainLinkCount; t++)
	    {
		    GameObject go = PoolManager.Instantiate(projectilePrefab.gameObject);
		    TM_Projectile p = go.GetComponent<TM_Projectile>();
		    p.CopyFromArchetype(projectilePrefab);
		    p.SetInfoWithoutLaunch(owningCombatComponent, owningWeapon);
		    boundProjectiles[t] = p;
	    }

	    bInitialized = true;
    }

	/// <summary>
	/// Get one of the projectiles from the list. Please don't delete it.
	/// </summary>
	/// <param name="idx"></param>
	/// <returns></returns>
    public TM_Projectile GetProjectileFromChain(int idx)
    {
	    if (!bInitialized || idx < 0 || idx >= boundProjectiles.Length)
		    return null;

	    return boundProjectiles[idx];
    }

	/// <summary>
	/// Store a new forward angle at the start of the chain, move the rest down the chain for whippy bippy action.
	/// </summary>
	/// <param name="angleForFwd"></param>
	public void CacheNewHeadingValue(float angleForFwd)
	{
		for (int t = chainLinkCount - 1; t > 0; t--)
		{
			cachedHeadingValues[t] = cachedHeadingValues[t - 1];
		}

		cachedHeadingValues[0] = angleForFwd;
	}

	public void UpdateChain(float chainDistance, Vector3 startPosition, float angleForFwdRadians, 
	float maxRotationSpeedRadians)
	{
		if (allLinksDisabled)
			return;
			
		// If this is our first update, everybody line right up
		if (bForceLinksToForwardOnThisUpdate)
		{
			cachedHeadingValues = new List<float>();
			for (int t = 0; t < chainLinkCount; t++)
				cachedHeadingValues.Add(angleForFwdRadians);
		}
		// Otherwise, cache the heading every now and then. 
		else if (Time.realtimeSinceStartup - lastCachedHeadingTime > timeBetweenHeadingCaches)
		{
			CacheNewHeadingValue(angleForFwdRadians);
			lastCachedHeadingTime = Time.realtimeSinceStartup;
		}

	
		//A guess at the required spacing between projectiles.
		var naiveDelta = chainDistance / chainLinkCount;

		for (int t = 0; t < chainLinkCount; t++)
		{
			//this indicates how far down the line we are with each projectile.
			var completenessRatio = (t+1) / (float)chainLinkCount;

			var p = boundProjectiles[t];
			var ptform = p.transform;
			var thisProjectilesRadius = naiveDelta * t;
			
			//the closer the projectile is to the beginning, the shorter that distance should be.
			var scaledValue = (0.5f + completenessRatio * 0.5f);
			thisProjectilesRadius *= scaledValue;
			ptform.localScale = new Vector3(scaledValue + 0.1f, scaledValue + 0.1f, 1);
			
			//This is the current line delta from us to them. Make sure this distance is
			//unchanging. The radius of the projectile's circle doesn't change. The position
			//on the circle does.
			
			//this is our current vector away from the muzzle. We need to move towards the actual
			//muzzle direction
			Vector2 toProjectile = (ptform.position - startPosition).normalized;
			Vector2 cached = toProjectile;
			
			//this is TO the current projectile
			var angleForTo = Mathf.Atan2(toProjectile.y, toProjectile.x);

			//this is our cached value so we can snake along like a hissy ass snake
			angleForFwdRadians = cachedHeadingValues[t];
			
			//lie about it
			angleForFwdRadians += rotationFromForward * Mathf.Deg2Rad;

			if (angleForTo < 0)
			{
				angleForTo = Mathf.PI*2 + angleForTo;
			}

			var angleDelta = angleForFwdRadians - angleForTo;

			var absOneDirection = Mathf.Abs(angleDelta);
			var absOtherDirection = 6.28f - absOneDirection;
			
			//do we need to reverse the movement?
			if (absOtherDirection < absOneDirection)
			{
				//Debug.Log("Bigass move requested, " + angleDelta + " rads. ");
				//we do!
				if (angleDelta > 0f)
				{
					angleDelta -= Mathf.PI * 2.0f;
				}
				else if (angleDelta < 0f)
				{
					angleDelta += Mathf.PI * 2.0f;
				}
				
				//Debug.Log("But now I'm only moving " + angleDelta + " rads, pow!");
			}

			//watch for tricksy crap -- if we are rotating past a 0/360 point, this number enjankens
			//if (angleDelta > Mathf.PI )
			//{
			//	angleDelta = -6.28f + angleDelta;
			//}
			//else if (angleDelta < -3.14f)
			//{
			//	angleDelta = 6.28f + Mathf.Abs(angleDelta);
			//}
			
			var degTo = angleForTo * Mathf.Rad2Deg;
			var degFwd = angleForFwdRadians * Mathf.Rad2Deg;
			var degLerp = angleDelta * Mathf.Rad2Deg;
			
			//limit our rotation speed based on distance
			var rotationCap = maxRotationSpeedRadians * Time.deltaTime;
			angleDelta = Mathf.Clamp(angleDelta, -rotationCap, rotationCap);
			angleForTo += angleDelta;
			
			//after all that... 
			if (bForceLinksToForwardOnThisUpdate)
			{
				angleForTo = angleForFwdRadians;
			}

			var newHeading = new Vector2(Mathf.Cos(angleForTo), Mathf.Sin(angleForTo));
			var goalPosition = startPosition + (Vector3)newHeading * thisProjectilesRadius;

			//you live here now.
			ptform.position = goalPosition;
			p.SetDirectionAndUpdateRotation(newHeading);
		}

		bForceLinksToForwardOnThisUpdate = false;
	}

	public void DeactivateChain(bool rtfn)
	{
		if (rtfn)
		{
			for (int t = 0; t < chainLinkCount; t++)
			{
				var p = boundProjectiles[t];
				var go = p.gameObject;
				go.SetActive(false);
			}
			
			TM_AudioManager.StopLoopingClip(loopFireIdx);
		}
		else
		{
			if( flameActivationCoroutine != null )
				StopCoroutine(flameActivationCoroutine);
	
			flameActivationCoroutine = StartCoroutine(CascadingLinkDeactivation(0.2f));
		}
	}

	public void ActivateChain()
	{
		if( flameActivationCoroutine != null )
			StopCoroutine(flameActivationCoroutine);

		flameActivationCoroutine = StartCoroutine(CascadingLinkActivation(0.2f));

		//make sure all links are facing the correct way
		cachedHeadingValues = new List<float>();

		bForceLinksToForwardOnThisUpdate = true;

	}
	
	protected IEnumerator CascadingLinkActivation(float activateDuration)
	{
		allLinksDisabled = false;
		TM_AudioManager.PlayClip(clip_startFire);

		
		float timePerFlame = activateDuration / boundProjectiles.Length;
		int numActivated = 0;
		while (numActivated < boundProjectiles.Length)
		{
			var p = boundProjectiles[numActivated];
			p.gameObject.SetActive(true);
			p.GetComponent<TM_SpriteAnimator>().StartIfNotRunning("start");
			yield return new WaitForSeconds(timePerFlame);
			numActivated++;
		}

		loopFireIdx = TM_AudioManager.PlayLoopingClip(clip_loopFire);
	}
	
	protected IEnumerator CascadingLinkDeactivation(float activateDuration)
	{
		TM_AudioManager.StopLoopingClip(loopFireIdx);
		
		TM_AudioManager.PlayClip(clip_endFire);
		float timePerFlame = activateDuration / boundProjectiles.Length;
		int numRemaining = boundProjectiles.Length - 1;
		while (numRemaining >= 0)
		{
			var p = boundProjectiles[numRemaining];
			p.GetComponent<TM_SpriteAnimator>().StartIfNotRunning("die");
			yield return new WaitForSeconds(timePerFlame);
			numRemaining--;
		}

		allLinksDisabled = true;
	}

	void OnReturnToPool()
	{
		gameObject.SetActive(false);
	}

}
