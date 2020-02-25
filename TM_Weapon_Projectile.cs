using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TM_Weapon_Projectile : TM_Weapon
{
	[Header("Projectile Info")]
	
	[Tooltip("Pew pew!")]
	public GameObject Prefab_Projectile;
	private TM_Projectile projectileArchetype;
    
	[Tooltip("Multiplier for base projectile speed")]
	public float projectileSpeedMultiplier = 1.0f;

	//Vector used to generate a scalar value that increases the shot speed based on the movement of the owner
	protected Vector2 projectileVelocityAdjustmentFromOwnerMovement;
	// Called at the moment of instantiation
	void Awake()
	{
		Initialize();
	}

	// Initialize stuff here, not in Awake
	protected override void Initialize()
	{
		base.Initialize();
	}
	
    // Start vis called before the first frame update
    protected override void Start()
    {
	    base.Start();
	    projectileArchetype = Prefab_Projectile.GetComponent<TM_Projectile>();
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
    }
	
    protected override TM_Projectile FireOneShot(float deviateAngle, Vector2 offsetFromMuzzle)
    {
        if (ownerDisabledDoNotFire)
        {
            return null;
        }

        if (ScreenshakeOnFire)
        {
            owningCombatComponent.ShakeScreen();
        }

        if (KickbackImpulseWhenFired != 0f)
        {
            owningCombatComponent.KickbackOnWeaponFire(transform.up.normalized, KickbackImpulseWhenFired);
        }

        if (!string.IsNullOrEmpty(AnimOnStartFire))
        {
	        MySpriteAnimator.animRateAdjustment = totalSpeedModifier;
	        MySpriteAnimator.StartAndStopCurrent(AnimOnStartFire);
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

		
        GameObject go = PoolManager.Instantiate(GetProjectilePrefabName());
        TM_Projectile p = go.GetComponent<TM_Projectile>();
        p.CopyFromArchetype(projectileArchetype);


        Vector3 vProjectilePosition = transform.position;
        //if there's an offset, use that instead
        if (fireOriginOffset != Vector2.zero)
        {
	        var muzzlePlusOffset = fireOriginOffset + offsetFromMuzzle;
            Vector2 vAdjustedOffset = TM_Utilities.Rotate2DVector(muzzlePlusOffset, transform.rotation.eulerAngles.z);
            vAdjustedOffset += vAdjustedOffset*ScaledSize;
            vProjectilePosition.x += vAdjustedOffset.x;
            vProjectilePosition.y += vAdjustedOffset.y;
        }
        
        //cheating and putting them up front for sorting
        vProjectilePosition.z = -5;

        p.transform.position = vProjectilePosition;
        p.SetSpeed(p.Speed * projectileSpeedMultiplier);

        Vector3 towardsTarget = transform.up.normalized;

        //Override tracking        
        //if (vForceBurstToThisVector != Vector3.zero)
        //    towardsTarget = vForceBurstToThisVector.normalized;

        if (deviateAngle != 0)
        {
            Vector2 vRotMe = towardsTarget;
            vRotMe = TM_Utilities.Rotate2DVector(vRotMe, deviateAngle);
            towardsTarget.x = vRotMe.x;
            towardsTarget.y = vRotMe.y;
        }

        p.SetDirectionAndUpdateRotation(towardsTarget);

        //if we have an adjustment vector, use it to change the speed of the projectile
        Vector2 vNormalizedAdjustment = projectileVelocityAdjustmentFromOwnerMovement.normalized;
        float fAdjustedSpeed = projectileVelocityAdjustmentFromOwnerMovement.magnitude;

        //how much of the adjustment do we want to use
        float bonusSpeed = Vector2.Dot(vNormalizedAdjustment, p.Direction)*fAdjustedSpeed;
        //Debug.Log("fMultiplier is " + fMultiplier + " when dotting " + vNormalizedAdjustment + " with " + p.vDirection + " times " + fAdjustedSpeed);
        p.SetSpeed( p.Speed + bonusSpeed);

        p.Launch(p.transform.position, owningCombatComponent, this);

        //get big if we are big
        Vector3 vScale = p.gameObject.transform.localScale;
        vScale += vScale*ScaledSize;
        p.gameObject.transform.localScale = vScale;

        return p;
    }

    /// <summary>
    /// What are we supposed to instantiate when we fire?
    /// </summary>
    /// <returns></returns>
    public virtual string GetProjectilePrefabName()
    {
	    return "projectile_default";
    }
    
    public void SetVelocityAdjustmentVector(Vector2 vAdjustment)
    {
	    projectileVelocityAdjustmentFromOwnerMovement = vAdjustment;
    }

}
