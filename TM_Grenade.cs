using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class TM_Grenade : TM_Projectile
{
	[Header("Grenade Info")] 
	[SerializeField]
	private float maxTossHeight;

	[SerializeField] 
	private float scaleSizeAtMaxHeight;

	[SerializeField] 
	[Tooltip("Min/Max drift from destination when fired.")]
	private Vector2 inaccuracyRange;

	/// <summary>
	/// Fakey value that simulates our height off the ground.
	/// </summary>
	private float height;
	
	/// <summary>
	/// Where we started
	/// </summary>
	private Vector2 launchPosition;
	
	/// <summary>
	/// Where we plan on landing.
	/// </summary>
	private Vector2 landingDestination;

	/// <summary>
	/// Cache this cause we'll ask for it all the time.
	/// </summary>
	private Vector2 towardsDestination;

	private Rigidbody2D myRB;

	/// <summary>
	/// Ask the game what to do when we know where we are going.
	/// </summary>
	public static Action<TM_Grenade, Vector2> onDetermineDestination;

	//#todo: Have a code hook that calls something when the grenade lands
	/// <summary>
	/// If this grenade should spawn a projectile when it lands, do that here.
	/// </summary>
	public GameObject prefab_projectileOnLanding;

	/// <summary>
	/// Maybe this grenade places an entity in the world?
	/// </summary>
	public string prefabCreatureOnLandingName;

	/// <inheritdoc />
	protected override void Initialize()
	{
		base.Initialize();

		myRB = GetComponent<Rigidbody2D>();
	}

	/// <inheritdoc />
	public override void CopyFromArchetype(TM_Projectile arc)
	{
		base.CopyFromArchetype(arc);
		var arcGrenado = arc as TM_Grenade;

		maxTossHeight = arcGrenado.maxTossHeight;
		scaleSizeAtMaxHeight = arcGrenado.scaleSizeAtMaxHeight;
		inaccuracyRange = arcGrenado.inaccuracyRange;

		prefab_projectileOnLanding = arcGrenado.prefab_projectileOnLanding;
		prefabCreatureOnLandingName = arcGrenado.prefabCreatureOnLandingName;
	}

	/// <inheritdoc />
	public override void Update()
	{
		//update as normal
		base.Update();
		
		//if time is up, and we have something else to spawn, place it here.
		if (lifetime >= destroyAfterTime )
		{
			if (prefab_projectileOnLanding != null)
			{
				var newProjectile = PoolManager.Instantiate(prefab_projectileOnLanding).GetComponent<TM_Projectile>();

				//make sure this projectile is just like us.
				newProjectile.Launch(transform.position, owningCombatComponent, weaponThatFiredMe);
			}

			if (!string.IsNullOrEmpty(prefabCreatureOnLandingName))
			{
				CombatManager.SpawnNewMonster(prefabCreatureOnLandingName, transform.position, 0f,
					(CombatFaction)Faction);
			}

			//just in case
			shouldReturnToPoolThisFrame = true;
		}
	}

	/// <inheritdoc />
	public override void UpdateMovement()
	{
		base.UpdateMovement();
		
		//our height is based on how long we've been alive.
		var lifeRatio = lifetime / destroyAfterTime;
		height = maxTossHeight * Mathf.Sin(lifeRatio * Mathf.PI);
		
		//don't collide if we're in the air
		myRB.simulated = height > 1.0f;

	}

	/// <inheritdoc />
	public override void UpdateSize()
	{
		//change size based on height from ground
		var scaleSize = Mathf.Lerp(1, scaleSizeAtMaxHeight, height / maxTossHeight);
		transform.localScale = Vector3.one * scaleSize;

	}

	/// <inheritdoc />
	public override void Launch(Vector3 worldPosition, TM_CombatComponent owner, TM_Weapon firingWeapon)
	{
		base.Launch(worldPosition, owner, firingWeapon);
		
		//determine where we are and where we are going
		launchPosition = worldPosition;
		landingDestination = firingWeapon.GetTossedProjectileDestination(this);

		//some noise
		var offsetDelta = Random.Range(inaccuracyRange.x, inaccuracyRange.y);
		landingDestination += Random.insideUnitCircle * offsetDelta;
		
		towardsDestination = landingDestination - launchPosition;
		
		//our speed and the distance we're flying should tell us how long we have to live.
		var distToDest = towardsDestination.magnitude;
		destroyAfterTime = distToDest / currentSpeed;
		SetDirectionAndUpdateRotation(towardsDestination);
		
		//let the world know where we'll end up
		onDetermineDestination(this, landingDestination);
	}

	/// <inheritdoc />
	public override void OnCollisionEnter2D(Collision2D hitObject)
	{
		//If we're up off the ground, don't worry about collision
		if (height > 1.0f)
		{
			return;
		}
		
		base.OnCollisionEnter2D(hitObject);
	}
}
