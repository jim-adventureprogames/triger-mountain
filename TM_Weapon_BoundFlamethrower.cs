using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityMock.Debug;

/// <summary>
/// A bound flamethrower is just like the flamethrower from Mercs!
/// https://www.youtube.com/watch?v=dyNE2sF6r2w#t=10m30s
///
/// Projectiles do not move on their own with this weapon, but instead live, die, and position themselves
/// according to player rotation and holding down the fire button.
///
///	* We don't consume ammo, though maybe we could be on a timer?
/// * The projectiles stay alive as long as the player is holding down the fire button
/// 
/// </summary>
public class TM_Weapon_BoundFlamethrower : TM_Weapon_ContinuousFire
{
	/// <summary>
	/// The hot hot flames. Or cold cold ice, whatever.
	/// </summary>
	private TM_Projectile[] boundProjectiles;
	
	/// <summary>
	/// Keep track of where the weapon was facing over time, and move the far away flame orbs towards
	/// those spots. This lets us whip our flame back and forth.
	/// </summary>
	protected List<float> cachedHeadingValues;
	
	[Header("Bound Flame Stuff!")]
	
	[SerializeField]
	[Tooltip("How many fooms should exist in this line")]
	protected int maxProjectiles;

	[SerializeField]
	[Tooltip("How long should the line be")]
	protected float lineDistance;
	
	[SerializeField] 
	[Tooltip("The base foom object")]
	protected TM_Projectile projectilePrefab;
	
	
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
	/// When we stop firing the weapon, there is a window during which we need to update the projectile positions
	/// as the flames die down. While this flag is false, we update.
	/// </summary>
	protected bool allProjectilesDisabled;



	protected float lastCachedHeadingTime;
	
	[SerializeField] 
	[Tooltip("How many seconds between taking snapshots of weapon heading? Hint: a small number.")]
	protected float timeBetweenHeadingCaches;
	

	[SerializeField] 
	[Tooltip("The chain we will use as a base for all the foom we will deliver.")]
	protected TM_ChainOfProjectiles chainPrefab;

	protected List<TM_ChainOfProjectiles> myChains;

	private bool bChainsInitialized = false;

	/// <summary>
	/// When we turn the fire on and off, we need to make sure we're only doing one of those at a time.
	/// </summary>
	private Coroutine flameActivationCoroutine;
	
	
	
	/// <inheritdoc />
	protected override void Initialize()
	{
		base.Initialize();
		myChains = new List<TM_ChainOfProjectiles>();
		AddNewChain();
		for(int t=2; t <= LinkedNozzles; t++)
			AddNewChain();

	}

	protected void AddNewChain()
	{
		if (chainPrefab == null)
		{
			Debug.LogError("Hey, no chainPrefab in flamethrower weapon " + name + ".");
			return;
		}
		
		var newChain = PoolManager.Instantiate(chainPrefab).GetComponent<TM_ChainOfProjectiles>();	
		myChains.Add(newChain);

		if (bChainsInitialized)
		{
			newChain.InitializeChain(maxProjectiles, 0, projectilePrefab, owningCombatComponent,
				this);
		}

		bNozzleCountDirty = true;
	}
	
	/// <summary>
	/// Pretty different from the base weapon. We don't tick down shots or do typical burst/clip stuff.
	/// We just want to make sure that the flames are following us.
	/// </summary>
	protected override void Update()
	{
		base.Update();

		if (bChainsInitialized)
		{
			MaintainProjectileDistanceAndPosition();
		}
	}

	protected override void OnReleaseTrigger()
	{
		TurnOffFlames();
	}
	
	
	public override void StopBurstImmediate()
	{
		base.StopBurstImmediate();
		TurnOffFlames(true);
	}
	
	/// <summary>
	/// Hide and disable the bound projectiles
	/// </summary>
	private void TurnOffFlames(bool rtfn = false)
	{
		foreach (var chain in myChains)
		{
			chain.DeactivateChain(rtfn);
		}
	}

	/// <summary>
	/// Keep all the shots lined up and have them try to follow the player's facing.
	/// </summary>
	private void MaintainProjectileDistanceAndPosition(bool initializePositions = false)
	{
		var tform = transform;
		var fwd = tform.up.normalized;
		var muzzlePosition = tform.position; // plus some stuff...
		
		//the actual muzzle position exists at origin + MuNgLo's Constant away from whatever this is a stream joke.
		var adjustedOffset = TM_Utilities.Rotate2DVector(fireOriginOffset, tform.rotation.eulerAngles.z);
		muzzlePosition.x += adjustedOffset.x;
		muzzlePosition.y += adjustedOffset.y;
		
		var angleForFwd = Mathf.Atan2(fwd.y, fwd.x);
		if (angleForFwd < 0)
		{
			angleForFwd = Mathf.PI*2 + angleForFwd;
		}
		var maxRotationSpeed = Mathf.PI * 2.0f * 4.0f;

		foreach (var chain in myChains)
		{
			chain.UpdateChain(lineDistance, muzzlePosition, angleForFwd, maxRotationSpeed);
		}

		return;
		
		/*
		for (int t = 0; t < numProjectiles; t++)
		{
			//this indicates how far down the line we are with each projectile.
			var completenessRatio = (t+1) / (float)numProjectiles;

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
			Vector2 toProjectile = (ptform.position - muzzlePosition).normalized;
			Vector2 cached = toProjectile;
			
			//this is TO the current projectile
			var angleForTo = Mathf.Atan2(toProjectile.y, toProjectile.x);

			//this is our cached value so we can snake along like a hissy ass snake
			angleForFwd = cachedHeadingValues[t];

			if (angleForTo < 0)
			{
				angleForTo = Mathf.PI*2 + angleForTo;
			}

			var angleDelta = angleForFwd - angleForTo;

			var absOneDirection = Mathf.Abs(angleDelta);
			var absOtherDirection = 6.28f - absOneDirection;
			
			//do we need to reverse the movement?
			if (absOtherDirection < absOneDirection)
			{
				Debug.Log("Bigass move requested, " + angleDelta + " rads. ");
				//we do!
				if (angleDelta > 0f)
				{
					angleDelta -= Mathf.PI * 2.0f;
				}
				else if (angleDelta < 0f)
				{
					angleDelta += Mathf.PI * 2.0f;
				}
				
				Debug.Log("But now I'm only moving " + angleDelta + " rads, pow!");
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
			var degFwd = angleForFwd * Mathf.Rad2Deg;
			var degLerp = angleDelta * Mathf.Rad2Deg;
			
			//limit our rotation speed based on distance
			var rotationCap = maxRotationSpeed * Time.deltaTime;
			angleDelta = Mathf.Clamp(angleDelta, -rotationCap, rotationCap);
			angleForTo += angleDelta;
			
			//after all that... 
			if (initializePositions)
			{
				angleForTo = angleForFwd;
			}

			var newHeading = new Vector2(Mathf.Cos(angleForTo), Mathf.Sin(angleForTo));
			var goalPosition = muzzlePosition + (Vector3)newHeading * thisProjectilesRadius;

			//you live here now.
			ptform.position = goalPosition;
			p.SetDirectionAndUpdateRotation(newHeading);

			// debug log biggest projectile with change
			if (t == numProjectiles - 1)
			{
				Debug.Log("I am the last foom in the chain, I was supposed to move " + angleDelta + " radians. " + cached + " -> " + toProjectile);
				Debug.Log("My to angle is " + degTo + " but I need to be " + degFwd + ", lerping " + degLerp + " degrees.");
			}
		}
		*/
	}



	/// <inheritdoc />
	protected override void Fire()
	{
		//todo: Make sure we track effects on our weapon that may fire every X seconds, such as the forest density reducer
		if (isFiring)
			return;

		isFiring = true;

		if (!bChainsInitialized)
		{
			foreach (var chain in myChains)
			{
				chain.InitializeChain(maxProjectiles, 0, projectilePrefab, owningCombatComponent,
					this);
			}

			bChainsInitialized = true;
		}

		if (bNozzleCountDirty)
		{
			SortAndSizeNozzlesByCount(Math.Max(LinkedNozzles, 1));
		}
		
		foreach (var chain in myChains)
		{
			chain.ActivateChain();
		}
	}
	/// <summary>
	/// Position and size the nozzles correctly so that if we have more than one, they look cool coming out of the front of
	/// the weapon.
	/// </summary>
	protected override void SortAndSizeNozzlesByCount(int nozzleCount)
	{
		//the count may be less than the number of beams we have, or it could be more!
		if (nozzleCount > myChains.Count)
		{
			while( myChains.Count < nozzleCount )
				AddNewChain();
		}
		
		for (int t = 0; t < myChains.Count; t++)
		{
			var chain = myChains[t];
			//any that we're not using, turn off
			if (t >= nozzleCount)
			{
				chain.enabled = false;
			}
			//otherwise, turn them on
			else
			{
				chain.enabled = true;
			}
		}
		
		//create patterns for multiple throwers
		if (nozzleCount == 1)
		{
			myChains[0].rotationFromForward = 0f;
		}
		//slight V formation
		else if (nozzleCount == 2)
		{
			myChains[0].rotationFromForward = -4f;
			myChains[1].rotationFromForward = 4f;
					
		}
		//larger spread, still a solid wedge
		else if (nozzleCount == 3)
		{
		}
		//bigger flame fan, whip it back and forth
		else
		{
			myChains[0].rotationFromForward = -6f;
			myChains[1].rotationFromForward = -18f;
			myChains[2].rotationFromForward = 6f;
			myChains[3].rotationFromForward = 18f;
		}

		bNozzleCountDirty = false;
	}


	/// <summary>
	/// What are we supposed to instantiate when we fire?
	/// </summary>
	/// <returns></returns>
	public override string GetProjectilePrefabName()
	{
		return "projectile_boundflamethrower_default";
	}
}
