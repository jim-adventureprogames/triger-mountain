using System.Collections;
using System.Collections.Generic;
using TwitchLib.Api.Models.v5.Games;
using UnityEngine;

public class TM_Weapon_Hitscan : TM_Weapon
{
	private static RaycastHit2D[] railgunHitResults = new RaycastHit2D[32];

	[Header("Hitscan Stuff!")]
	[SerializeField]
	[Tooltip("Should this weapon hit all targets in a trace, or just the first one?")]
	protected bool shouldPenetrateTargets;

	[SerializeField]
	protected GameObject prefabImpact;

	[SerializeField]
	protected GameObject prefabMuzzle;

	[SerializeField]
	protected GameObject prefabTrail;

	[SerializeField] 
	[Tooltip("Not actually spawned, just used for game data.")]
	protected GameObject projectileInfoPrefab;

	protected TM_Projectile projectileInfo;

	/// <inheritdoc />
	protected override void Start()
	{
		base.Start();
		projectileInfo = projectileInfoPrefab.GetComponent<TM_Projectile>();
	}

	/// <inheritdoc />
	protected override TM_Projectile FireOneShot(float deviateAngle, Vector2 muzzleOffset)
	{
		if (ownerDisabledDoNotFire)
		{
			return null;
		}
		
		if (LimitedAmmo)
		{
			currentAmmo -= AmmoPerShot;
			if (currentAmmo < 0)
			{
				ownerDisabledDoNotFire = true;
				StopBurstImmediate();
				actionOnAmmoEmpty(owningCombatComponent);
				return null;
			}
		}

		if (ScreenshakeOnFire)
		{
			owningCombatComponent.ShakeScreen();
		}

		var tform = transform;
		var towardTarget = tform.up.normalized;

		if (KickbackImpulseWhenFired != 0f)
		{
			owningCombatComponent.KickbackOnWeaponFire(towardTarget, KickbackImpulseWhenFired);
		}
		
		//fire direction might deviate
		if (deviateAngle != 0)
		{
			Vector2 vRotMe = towardTarget;
			vRotMe = TM_Utilities.Rotate2DVector(vRotMe, deviateAngle);
			towardTarget.x = vRotMe.x;
			towardTarget.y = vRotMe.y;
		}
		

		//pew pew 
		var muzzle = GetWeaponFirePosition();
		var numHits = Physics2D.RaycastNonAlloc(muzzle, towardTarget, railgunHitResults, 100.0f,
			playerCollisionMask | monsterCollisionMask | worldCollisionMask);

		//hits should always be at least 1, cause if we miss everything we still tag the bounds of the arena?
		
		//track where the shot ends
		var shotEndLocation = muzzle;

		//who did we hit?
		for(int hitIdx =0; hitIdx < numHits; hitIdx++)
		{
			var hit = railgunHitResults[hitIdx];
			var hitGO = hit.collider.gameObject;
			
			//if this is world collision, the shot ends here.
			if (((1 << hitGO.layer) & worldCollisionMask) > 0)
			{
				shotEndLocation = hit.point;
				break;
			}
			
			//let's check our projectile against the target's faction.
			var enemyCombatant = hitGO.GetComponent<TM_CombatComponent>();
			if (enemyCombatant != null &&
			    enemyCombatant.Faction != owningCombatComponent.Faction)
			{
				projectileInfo.gameLevelOwner = owningCombatComponent.gameObject;
				enemyCombatant.OnHitByProjectile(projectileInfo, hit.point);

				if (!shouldPenetrateTargets)
				{
					shotEndLocation = hit.point;
					break;
				}
			}
		}
		
		//play the impact prefab 
		var go = PoolManager.Instantiate(prefabImpact);
		go.transform.position = shotEndLocation;
		go.transform.up = towardTarget * -1.0f;
		
		//muzzle prefab
		go = PoolManager.Instantiate(prefabMuzzle);
		go.transform.position = muzzle;
		go.transform.up = towardTarget;
		
		//attach muzzle flash to self.
		go.transform.parent = tform;
		
		//pew pew line
		go = PoolManager.Instantiate(prefabTrail);
		var trail = go.GetComponent<TM_HitscanTrailFX>();
		if (trail != null)
		{
			trail.SetLineInformation(muzzle, shotEndLocation, 0.2f);
			trail.transform.parent = tform;
		}

		
		

		return null;
	}
}
