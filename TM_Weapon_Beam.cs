using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TM_Weapon_Beam : TM_Weapon_ContinuousFire
{
	private static RaycastHit2D[] beamHitResults = new RaycastHit2D[32];
	 
	[Header("Beam Information")]
	[SerializeField] 
	[Tooltip("The prefab for the projectile that lives under the laser quad")]
	protected TM_Projectile projectilePrefab;

	[SerializeField] 
	[Tooltip("Width of the beam in unity units.")]
	protected float beamWidth;
	
	[SerializeField] 
	[Tooltip("Does this beam go through enemies or stop when it hits them?")]
	protected bool shouldPenetrateTargets;

	/// <summary>
	/// True if the beam is visible and damaging people
	/// </summary>
	protected bool beamsActive;

	[SerializeField]
	protected GameObject BeamPrefab;

	/// <summary>
	/// All the beams we control when we fire.
	/// </summary>
	protected List<TM_Beam> myBeams;
	
	private bool beamsInitialized = false;

	// Initialize stuff here, not in Awake
	protected override void Initialize()
	{
		base.Initialize();
		beamsInitialized = false;
		myBeams = new List<TM_Beam>();
		
		//always have at least one
		AddBeamToCollection();
		
		for(int t=2; t <= LinkedNozzles; t++)
			AddBeamToCollection();
	}

	protected void AddBeamToCollection()
	{
		if (BeamPrefab == null)
			return;
			
		var newBeamGO = Instantiate(BeamPrefab);
		var newBeam = newBeamGO.GetComponent<TM_Beam>();
		myBeams.Add(newBeam);

		// If we are adding this mid-run, initialize it right now. Otherwise we're still pooling, and don't.
		if (beamsInitialized)
		{
			newBeam.Initialize(projectilePrefab, owningCombatComponent, this);
		}

		bNozzleCountDirty = true;
	}

	/// <inheritdoc />
	protected override void RebuildWeaponStatsWithModifiers()
	{
		base.RebuildWeaponStatsWithModifiers();
	}

	/// <summary>
	/// Position and size our beams correctly so that if we have more than one, they look cool coming out of the front of
	/// the weapon.
	/// </summary>
	protected override void SortAndSizeNozzlesByCount(int nozzleCount)
	{
		//the count may be less than the number of beams we have, or it could be more!
		if (nozzleCount > myBeams.Count)
		{
			while( myBeams.Count < nozzleCount )
				AddBeamToCollection();
		}
		
		//position beams accordingly
		var newBeamWidth = beamWidth * Mathf.Max(1.0f - (0.25f * (nozzleCount - 1)), 0.5f);
		float spaceBetweenLinkedShots = 0.3f;
		float startingXOffset = spaceBetweenLinkedShots * -0.5f * (nozzleCount - 1);
		float muzzleScale = newBeamWidth / beamWidth;
		
		for (int t = 0; t < myBeams.Count; t++)
		{
			var beam = myBeams[t];
			//any that we're not using, turn off
			if (t >= nozzleCount)
			{
				beam.enabled = false;
			}
			//otherwise, turn them on
			else
			{
				beam.enabled = true;
				beam.offsetFromMuzzlePosition = new Vector2(startingXOffset + t* spaceBetweenLinkedShots, 0f);
				beam.beamWidth = newBeamWidth;
				//beam.rotationFromForward = t * 72.0f;
				if( beam.muzzleFlashGO != null )
				{	
					var tform = beam.muzzleFlashGO.transform;
					tform.localScale = new Vector3(muzzleScale,muzzleScale,muzzleScale); 	
				}
			}
		}

		bNozzleCountDirty = false;
	}

	void OnReturnToPool()
	{
	
	}

	/// <inheritdoc />
	protected override void Fire()
	{
		//todo: Make sure we track effects on our weapon that may fire every X seconds, such as the forest density reducer
		if (isFiring)
			return;

		if (!beamsInitialized)
		{
			beamsInitialized = true;
			foreach( var b in myBeams )
				b.Initialize(projectilePrefab, owningCombatComponent, this);
		}

		if (bNozzleCountDirty)
		{
			SortAndSizeNozzlesByCount(Math.Max(LinkedNozzles, 1));
		}

		isFiring = true;
		beamsActive = true;
		foreach( var b in myBeams )
			b.TurnBeamOn();
			
	}

	void UpdateBeamPositionAndSize()
	{
		var tform = transform;
		var fwd = tform.up.normalized;
		var baseMuzzlePosition = tform.position; // plus some stuff...
		
		//some hax
		if (LinkedNozzles == 4)
		{
			myBeams[0].rotationFromForward = Mathf.PingPong(Time.realtimeSinceStartup * 10.0f, 5.0f);
			myBeams[3].rotationFromForward = Mathf.PingPong(Time.realtimeSinceStartup * 10.0f, 5.0f) * -1.0f;
		}

		foreach (var beam in myBeams)
		{
			//the actual muzzle position exists at origin + MuNgLo's Constant away from whatever this is a stream joke.
			var adjustedOffset = TM_Utilities.Rotate2DVector(fireOriginOffset + beam.offsetFromMuzzlePosition,
			 tform.rotation.eulerAngles.z + beam.rotationFromForward );

			Vector2 muzzlePosition = (Vector2)baseMuzzlePosition + adjustedOffset;
			Vector2 myFwd = TM_Utilities.Rotate2DVector(fwd, beam.rotationFromForward);
			var beamStart = muzzlePosition;
			
			//trace from the weapon muzzle out.
			//stop at 
			// world?
			// enemy?
			// a point that is a preset distance from the muzzle? 
			//depends on the projectile. 
			var numHits = Physics2D.RaycastNonAlloc(muzzlePosition, myFwd, beamHitResults, 100.0f,
				playerCollisionMask | monsterCollisionMask | worldCollisionMask);
				
			//track where the shot ends
			var beamEnd = muzzlePosition;

			//who did we hit?
			for (int hitIdx = 0; hitIdx < numHits; hitIdx++)
			{
				var hit = beamHitResults[hitIdx];
				var hitGO = hit.collider.gameObject;
			
				//if this is world collision, the shot ends here.
				if (((1 << hitGO.layer) & worldCollisionMask) > 0)
				{
					beamEnd = hit.point;
					break;
				}
			
				//let's check our projectile against the target's faction.
				var enemyCombatant = hitGO.GetComponent<TM_CombatComponent>();
				if (enemyCombatant != null &&
				    enemyCombatant.Faction != owningCombatComponent.Faction)
				{
					if (!shouldPenetrateTargets)
					{
						beamEnd = hit.point;
						break;
					}
				}
			}

			beam.AlignBetweenTwoPoints(beamStart, beamEnd);
			//TM_Utilities.AlignBeamBetweenTwoPointsWithProjectile(beamStart, beamEnd, beamWidth, beamMR, beamMaterial, beamScrollMinMax, projectile);
		}
	}


    // Update is called once per frame
    protected override void Update()
    {
	    base.Update();

	    if (beamsActive)
	    {
		    UpdateBeamPositionAndSize();
	    }
    }

    /// <inheritdoc />
    protected override void OnReleaseTrigger()
    {
	    base.OnReleaseTrigger();
	    beamsActive = false;

	    foreach( var b in myBeams )
		    b.TurnBeamOff();

    }

    /// <inheritdoc />
    public override void StopBurstImmediate()
    {
	    base.StopBurstImmediate();
	    OnReleaseTrigger();
    }

    // Best to do final positioning / render stuff here
	void LateUpdate()
	{
		
	}
	
	// Clean up whatever memory hogging resources you created
	void OnDestroy()
	{
		
	}

	/// <inheritdoc />
	public override bool PullTrigger()
	{
		return base.PullTrigger();
	}
}