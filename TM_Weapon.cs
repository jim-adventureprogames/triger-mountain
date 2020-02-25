using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Random = UnityEngine.Random;

public class TM_Weapon : MonoBehaviour
{
    [SerializeField]
    protected TM_CombatComponent owningCombatComponent;
    public TM_CombatComponent OwningCombatComponent => owningCombatComponent;


    [Tooltip("Offset from origin from which to launch attacks. Typically the end of the weapon barrel")]
    public Vector2 fireOriginOffset;

    [Space(12)]
    [Header("Weapon Cooldown")]
    [Tooltip("Minimum number of seconds after starting a burst to prevent further bursts.")]
    public float BurstCooldown;
    protected float burstCooldownRemaining;

    /// <summary>
    /// A percent value that indicates how much faster/slower we're firing, so we can tool our animations
    /// </summary>
    protected float totalSpeedModifier;

    [Space(12)]
    [Header("Burst Fire")]
    [Tooltip("Number of fire events generated from one pull of the trigger.")]
    public int ShotsPerBurst;
    [Tooltip("Delay between each fire event generated")]
    public float DelayBetweenShots;
    private int shotsLeftInBurst;
    protected float shotDelay;

    [Tooltip("Use for spread fire to launch multiple projectiles per fire event")]
    public List<float> MultipleFireAngles;
    
    [Tooltip("Use for linked fire that launches multiple shots in the same direction. The value must be 2 or greater.")]
    public int LinkedShotCount;
    

    [Tooltip("Inaccuracy measured in degrees of variance. Use for spray weapons.")]
    public float ShotInaccuracy;

    public float ScaledSize;

    [Header("Ammo")]
    public bool LimitedAmmo;
    public int MaximumAmmo;
    public int AmmoPerShot;
    protected Action<TM_CombatComponent> actionOnAmmoEmpty;
    protected int currentAmmo;

    [Header("Flashy Bullshit")]
    public TM_SpriteAnimator MySpriteAnimator;
    public bool ScreenshakeOnFire;
    public string AnimOnStartBurst;
    public string AnimOnStartFire;
    public string AnimOnEndFire;
    public float KickbackImpulseWhenFired;
    public AudioClip clip_launchClip;

    public bool isFiring { get; protected set; }
    
    /// <summary>
    /// True if the owner of this weapon is disabled and firing should not happen under any circumstance.
    /// </summary>
    protected bool ownerDisabledDoNotFire;


    /// <summary>
    /// use this if the game needs to pre-determine where a projectile will land, specifically in the case of
    /// tossed things like grenades or mortars.
    /// </summary>
    protected Func<TM_Projectile, Vector2> funcDetermineProjectileDestination;
    
    protected static LayerMask playerCollisionMask;
    protected static LayerMask monsterCollisionMask; 
    protected static LayerMask worldCollisionMask;

    /// <summary>
    /// Weapons that might fire when we fire. 
    /// </summary>
    protected HashSet<TM_AttachedSubWeaponInfo> attachedSubWeapons;

    /// <summary>
    /// Collections of modifiers to our core information -- fire speed, accuracy, etc.
    /// </summary>
    protected Dictionary<string, List<Dictionary<string, object>>> attachedModifiers;

    /// <summary>
    /// Store all the core values we have, so that modifiers can be added and removed without permanently
    /// messing with our numbers.
    /// </summary>
    protected Dictionary<string, object> cachedUnmodifiedValues;
    
    /// <summary>
    /// If this weapon needs to run some bespoke code when it hits something, attack that here. That code probably exists
    /// in your game layer above Triger Mountain, so the [object] key in this dictionary can be something from there
    /// that holds the information.
    /// </summary>
    public Dictionary<object, Action<object, TM_Weapon, TM_CombatComponent>> specialActionOnHit { get; private set; }


    protected void Awake()
    {
        Initialize();
    }

    protected virtual void Initialize()
    {
        playerCollisionMask = LayerMask.GetMask("player_collision");
        monsterCollisionMask = LayerMask.GetMask("monster_collision");
        worldCollisionMask = LayerMask.GetMask("world_collision");

        attachedSubWeapons = new HashSet<TM_AttachedSubWeaponInfo>();
        attachedModifiers = new Dictionary<string, List<Dictionary<string, object>>>();
        specialActionOnHit = new Dictionary<object, Action<object, TM_Weapon, TM_CombatComponent>>();
    }
    
    // Use this for initialization
    protected virtual void Start()
    {
        shotsLeftInBurst = ShotsPerBurst;
        currentAmmo = MaximumAmmo;
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (isFiring)
        {
            TickDownShots();
        }
        else if (burstCooldownRemaining > 0.0f)
        {
            burstCooldownRemaining -= Time.deltaTime;
        }
    }

    public void SetOwningCombatComponent(TM_CombatComponent cc)
    {
        owningCombatComponent = cc;
    }

    public virtual void StopBurstImmediate()
    {
        isFiring = false;
        ResetBurst();
        burstCooldownRemaining = BurstCooldown;
        
        if (!string.IsNullOrEmpty(AnimOnEndFire))
        {
            MySpriteAnimator.animRateAdjustment = totalSpeedModifier;
            MySpriteAnimator.StartAndStopCurrent(AnimOnEndFire);
        }
    }

    /// <summary>
    /// Shuffle this up, because you may not want every weapon in a group to fire at the same exact time when spawned.
    /// </summary>
    public virtual void RandomizeRemainingCooldown()
    {
        burstCooldownRemaining = BurstCooldown * Random.value;
    }

    /// <summary>
    /// Attempt to the fire weapon. If it is on cooldown, out of ammo, or any other bollox, it won't fire,
    /// so don't worry about that, just point and shoot.
    /// </summary>
    /// <returns></returns>
    public virtual bool PullTrigger()
    {
        //Don't re-start the burst if we're already firing
        if (isFiring == true)
            return false;

        //if we're on burst cooldown, don't fire
        if (burstCooldownRemaining > 0)
            return false;

        Fire();
        
        //if this is the first blast of our burst, maybe we have an anim to play
        if (shotsLeftInBurst == ShotsPerBurst && !string.IsNullOrEmpty(AnimOnStartBurst))
        {
            MySpriteAnimator.StartAndStopCurrent(AnimOnStartBurst);
        }
        
        shotsLeftInBurst--;
        if (shotsLeftInBurst <= 0)
        {
            StopBurstImmediate();
        }
        else
        {
            isFiring = true;
        }

        burstCooldownRemaining = BurstCooldown;

        return true;
    }

    protected void ResetBurst()
    {
        shotsLeftInBurst = ShotsPerBurst;
        shotDelay = 0.0f;
    }

    private void TickDownShots()
    {
        shotDelay -= Time.deltaTime;
        if (shotDelay <= 0.0f)
        {
            shotDelay = DelayBetweenShots;
            Fire();
            shotsLeftInBurst--;
            if (shotsLeftInBurst <= 0)
            {
                StopBurstImmediate();
            }
        }
    }

    protected virtual void Fire()
    {
        float fModifiedInaccuracy = Random.Range(-ShotInaccuracy, ShotInaccuracy);

        //check for linked fire -- if we are firing 2 or more shots in a linked burst,
        //use the correct offsets
        if (LinkedShotCount >= 2)
        {
            /*
             *        
             *                  ^ ^ ^ ^
             *                   ^ ^ ^
             *                    ^ ^
             *                     ^
             * 
             */

            float spaceBetweenLinkedShots = 0.2f;
            float startingXOffset = spaceBetweenLinkedShots * -0.5f * (LinkedShotCount - 1);
            Vector2 initialOffset = new Vector2(startingXOffset, 0);
            
            for (int t = 0; t < LinkedShotCount; t++)
            {
                var shotOffset = initialOffset;
                shotOffset.x += t * spaceBetweenLinkedShots;
                FireOneShot(fModifiedInaccuracy, shotOffset);
            }
        }
        //otherwise, fire one shot dead ahead from the center.
        else
        {
            FireOneShot(fModifiedInaccuracy, Vector2.zero);
        }
        
        //now check for spread fire, which adds more shots that fire off in different directions.
        if (MultipleFireAngles != null && MultipleFireAngles.Count > 0)
        {
            foreach (float f in MultipleFireAngles)
            {
                fModifiedInaccuracy = Random.Range(-ShotInaccuracy, ShotInaccuracy);
                FireOneShot(f + fModifiedInaccuracy, Vector2.zero);
            }
        }

        TM_AudioManager.PlayClip(clip_launchClip);

        shotDelay = DelayBetweenShots;

        //If there are any guns linked to us, shoot them.
        foreach (var subInfo in attachedSubWeapons)
        {
            subInfo.TryProc();
        }

    }

    /// <summary>
    /// Fire a single projectile. If it is a linked shot, it will likely have a separate offset from the muzzle.
    /// It may also have a deviation angle to represent inaccurate fire.
    /// </summary>
    /// <param name="deviateAngle"></param>
    /// <param name="offsetFromMuzzle"></param>
    /// <returns></returns>
    protected virtual TM_Projectile FireOneShot(float deviateAngle, Vector2 offsetFromMuzzle)
    {
        //overriden in children
        return null;
    }

    public void SetActionOnAmmoDeplete(Action<TM_CombatComponent> action)
    {
        actionOnAmmoEmpty = action;
    }

    public float GetAmmoRatio()
    {
        if (!LimitedAmmo)
        {
            return 1.0f;
        }

        return (float)currentAmmo / (float)MaximumAmmo;
    }

    /// <summary>
    /// The location where we are aiming a tossed projectile.
    /// </summary>
    /// <returns></returns>
    public Vector2 GetTossedProjectileDestination(TM_Projectile p)
    {
        if (funcDetermineProjectileDestination != null)
        {
            return funcDetermineProjectileDestination(p);
        }

        //if we've cached something, use that value.
        if (owningCombatComponent.cachedTargetingLocationFromOutside != Vector2.zero)
            return owningCombatComponent.cachedTargetingLocationFromOutside;
        
        //do some rough guesstimate bullshit
        return owningCombatComponent.transform.position + p.Direction * 5.0f;
    }

    protected Vector3 GetWeaponFirePosition()
    {
        var retPosition = transform.position;
        
        //if there's an offset, use that instead
        if (fireOriginOffset != Vector2.zero)
        {
            Vector2 adjustedOffset = TM_Utilities.Rotate2DVector(fireOriginOffset, transform.rotation.eulerAngles.z);
            adjustedOffset += adjustedOffset*ScaledSize;
            retPosition.x += adjustedOffset.x;
            retPosition.y += adjustedOffset.y;
        }

        return retPosition;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="subweapon"></param>
    /// <param name="chance">% chance with each main weapon fire to fire this weapon</param>
    /// <param name="minTimeBetweenProcs">At least this much time must pass between procs</param>
    public void AttachSubWeapon(TM_Weapon subweapon, float chance, float minTimeBetweenProcs)
    {
        var subInfo = new TM_AttachedSubWeaponInfo();
        subInfo.SetWeaponInfo(subweapon, chance, minTimeBetweenProcs, owningCombatComponent);
        var subTrans = subweapon.transform;
        subTrans.SetParent(transform);
        subTrans.localPosition = Vector3.zero;
        subTrans.localRotation = Quaternion.identity;
        
        attachedSubWeapons.Add(subInfo);
        
    }


    /// <summary>
    /// Changes some values on the weapon's core firing attributes. Multiple mods can be added.
    /// See below for information on what [string,object] pairs are required to make changes.
    /// </summary>
    /// <param name="modName"></param>
    /// <param name="newMod"></param>
    public void AddWeaponModifier(string modName, Dictionary<string, object> newMod)
    {
        //first, make sure we've stored our core values
        if (cachedUnmodifiedValues == null)
        {
            CacheDefaultValues();
        }

        //hang on to it
        if (!attachedModifiers.ContainsKey(modName))
        {
            attachedModifiers[modName] = new List<Dictionary<string, object>>();
        }
        attachedModifiers[modName].Add(newMod);
        
        //go to work
        RebuildWeaponStatsWithModifiers();
    }

    /// <summary>
    /// Pull a modifier off the weapon, and recalculate the values.
    /// </summary>
    /// <param name="modName"></param>
    public void RemoveWeaponModifier(string modName)
    {
        //see if we have this mod in our collection.
        if (attachedModifiers.ContainsKey(modName))
        {
            //remove one instance from the list.
            var modList = attachedModifiers[modName];
            
            //this is a list of identical modifiers. Hopefully :) 
            if (modList.Count > 0)
            {
                modList.RemoveAt(modList.Count -1);
            }
            
            //if we removed the last one, clean up
            if (modList.Count == 0)
            {
                attachedModifiers.Remove(modName);
            }
        }
        
        RebuildWeaponStatsWithModifiers();
    }


    /// <summary>
    /// Recalculate our stats based on modifiers we have attached.
    /// </summary>
    protected virtual void RebuildWeaponStatsWithModifiers()
    {
        //set base values
        MultipleFireAngles = new List<float>();
        var cachedList =  cachedUnmodifiedValues["MultipleFireAngles"] as List<float>;
        foreach( var f in cachedList )
        {
            MultipleFireAngles.Add(f);
        }
        
        DelayBetweenShots = (float)cachedUnmodifiedValues["DelayBetweenShots"];
        ShotsPerBurst = (int)cachedUnmodifiedValues["ShotsPerBurst"];
        BurstCooldown = (float)cachedUnmodifiedValues["BurstCooldown"];
        ShotInaccuracy = (float)cachedUnmodifiedValues["ShotInaccuracy"];
        ScaledSize = (float)cachedUnmodifiedValues["ScaledSize"];
        MaximumAmmo = (int)cachedUnmodifiedValues["MaximumAmmo"];
        LinkedShotCount = (int) cachedUnmodifiedValues["LinkedShotCount"];
        
        totalSpeedModifier = 0f;
        
        //add in mods
        foreach (var keyAndList in attachedModifiers)
        {
            var modList = keyAndList.Value;
            foreach (var mod in modList)
            {
                ApplySingleMod(mod);
            }
        }
    }
    
    /// <summary>
    /// Store the defaults we set for any value that can be modified by modifiers.
    /// </summary>
    protected virtual void CacheDefaultValues()
    {
        cachedUnmodifiedValues = new Dictionary<string, object>();
        
        var cachedAngles = new List<float>();
        foreach (var f in MultipleFireAngles)
        {
            cachedAngles.Add(f);
        }
        cachedUnmodifiedValues["MultipleFireAngles"] = cachedAngles;
        cachedUnmodifiedValues["DelayBetweenShots"] = DelayBetweenShots;
        cachedUnmodifiedValues["ShotsPerBurst"] = ShotsPerBurst;
        cachedUnmodifiedValues["BurstCooldown"] = BurstCooldown;
        cachedUnmodifiedValues["ShotInaccuracy"] = ShotInaccuracy;
        cachedUnmodifiedValues["ScaledSize"] = ScaledSize;
        cachedUnmodifiedValues["MaximumAmmo"] = MaximumAmmo;
        cachedUnmodifiedValues["LinkedShotCount"] = LinkedShotCount;
        
    }

    /// <summary>
    /// Use this function to apply changes to various stats about the weapon. Also use this function as a reference
    /// to what key values do what. Trying to keep this generic without making a TM_WeaponMod class... but maybe I'll
    /// do that too.
    ///
    /// If you want an effect that only fires sometimes, make that a separate TM_Weapon and attach it via the SubWeapon
    /// code chain above.
    /// </summary>
    /// <param name="mods"></param>
    public virtual void ApplySingleMod(Dictionary<string, object> mods)
    {
        //MultipleFireAngles is a list of additional shots to fire per shot, with a given offset.
        //So "-5" will fire an additional shot at a -5 degree offset from the main shot.
        //Adding "0" will make an additional shot with no change in origin. This is ok if the weapon
        //has a natural spread.
        //
        //Use the syntax "additional_shots", "[angle]|[angle]|[angle]" with from 1 to X angles, separated by |
    
        if (mods.ContainsKey("additional_shots"))
        {
            var shotInfo = mods["additional_shots"].ToString();
            var splitFloat = shotInfo.Split( '|');
            foreach (var newAngle in splitFloat)
            {
                MultipleFireAngles.Add(float.Parse(newAngle));
            }
        }

        //DelayBetweenShots is a spacing between shots in a burst. If each trigger pull results in something like a
        //three round burst, you can reduce the time between each shot in the burst.
        //
        //If the weapon is single fire, this will instead reduce the time between bursts. 
        //This is a multiplier value, not a direct adjustment. Negative numbers make the delay smaller.
        //Please remember to scale your values so that 1.0 == 100% change
        //
        //Use the syntax "change_shot_delay", "[change%]"
        if (mods.ContainsKey("change_shot_delay"))
        {
            var delayAdjuster = float.Parse(mods["change_shot_delay"].ToString());
            if (ShotsPerBurst == 1)
            {
                BurstCooldown += BurstCooldown * delayAdjuster;
            }
            else
            {
                DelayBetweenShots += DelayBetweenShots * delayAdjuster;
            }

            //Delay adjuster is a - value for faster shots, so subtract it here.
            totalSpeedModifier -= delayAdjuster;
        }

        //Shots per burst indicate how many times the weapon fires with each trigger pull.
        //If the weapon doesn't already fire in burst mode, this will convert it to burst mode
        //with a delay between shots in the burst that you set in the mod, as well as a new delay
        //between bursts. If the weapon is already in burst mode, you cannot change the delay with this mod setting,
        //use the one above.
        //
        //Use the syntax "change_shots_per_burst", "[count change]|[new shot delay]|[new burst delay]"
        //
        if (mods.ContainsKey("change_shots_per_burst"))
        {
            var alreadyInBurstMode = ShotsPerBurst > 1;
            var splitsies = mods["change_shots_per_burst"].ToString().Split('|');
            if (alreadyInBurstMode)
            {
                ShotsPerBurst += int.Parse(splitsies[0]);
            }
            //If the weapon is not burst fire, and we didn't get a new delay time, this mod is invalid
            else if( splitsies.Length > 2)
            {
                ShotsPerBurst = Math.Max(ShotsPerBurst += int.Parse(splitsies[0]), 0);
                DelayBetweenShots = float.Parse(splitsies[1]);
                BurstCooldown = float.Parse(splitsies[2]);
            }
        }
        
        //BurstCooldown is the spacing between bursts -- allowable trigger pulls. If a weapon is single shot,
        //this effectively changes the delay between shots. 
        //
        //This is a multiplier, not a direct adjustment. Negative numbers indicate faster firing.
        //
        //Use the syntax "change_burst_delay", "[adjusted burst delay]"
        //Please remember to scale your values so that 1.0 == 100% change
        if (mods.ContainsKey("change_burst_delay"))
        {
            var delayAdjuster = float.Parse(mods["change_burst_delay"].ToString());
            BurstCooldown += BurstCooldown * delayAdjuster;
        }
        
        //ShotInaccuracy is a degree value that creates a firing cone for the weapon.
        //0 means dead accurate fire, any other number is a cone.
        //
        //This number is a multiplier. If the weapon has an inaccuracy of 0, and the modifier is positive,
        //it will be given an inaccuracy of 5 to start before the modifier is applied.
        //
        //Use the syntax "change_accuracy" "[adjustment%]". Positive numbers make the weapon more accurate.
        //Please remember to scale your values so that 1.0 == 100% change
        if (mods.ContainsKey("change_accuracy"))
        {
            var accuracyAdjustmentChange = float.Parse(mods["change_accuracy"].ToString());
            accuracyAdjustmentChange *= -1.0f;

            if (ShotInaccuracy == 0 && accuracyAdjustmentChange > 0)
            {
                ShotInaccuracy = 5f;
            }

            ShotInaccuracy += ShotInaccuracy * accuracyAdjustmentChange;
        }
        
        //ScaledSize changes the size of the projectile. Don't use this for hitscan weapons, it won't work.
        //This is a direct modifer that will add to the existing value, if any.
        //
        //Use the syntax "change_size","[new size modifier]". Positive modifiers make the shots bigger.
        //Please remember to scale your values so that 1.0 == 100% change
        if (mods.ContainsKey("change_size"))
        {
            ScaledSize = Math.Max(0, ScaledSize + float.Parse(mods["change_size"].ToString()));
        }
        
        //MaximumAmmo indicates how many shots this weapon can fire before it needs to be reloaded.
        //This is a percent modifier, not a direct adjustment. If a weapon isn't bound to ammo counts,
        //this modification won't do anything.
        //
        //Use the syntax "change_max_ammo","[max ammo adjustment]". Positive numbers make the max ammo bigger.
        //Please remember to scale your values so that 1.0 == 100% change
        if (mods.ContainsKey("change_max_ammo"))
        {
            MaximumAmmo += (int) (MaximumAmmo * float.Parse(mods["change_max_ammo"].ToString()));
        }
        
        //LinkedShots is a value that lets us fire multiple side-by-side shots that go in the same direction.
        //If this value is 2 or greater, that many shots will fire instead of just 1, and they will be offset
        //perpendicular to the muzzle direction.
        //
        //This value adds X number of linked shots to the weapon. If the weapon has none to start, it will end up
        //with at least one.
        //
        //Use the syntax "linked_shots", "[number of linked shots to add]".
        if (mods.ContainsKey("linked_shots"))
        {
            var extraLinks = int.Parse(mods["linked_shots"].ToString());
            if (LinkedShotCount < 2)
            {
                LinkedShotCount = 1 + extraLinks;
            }
            else
            {
                LinkedShotCount += extraLinks;
            }
        }

        
        //Weapon modifiers may have special code that gets called in your game code above Triger Mountain.
        //The parameters are
        // * an object from your game that you may use for data purposes
        // * the TM_Weapon that did the hitting.
        // * the TM_CombatComponent that did the receiving.
        //
        // for example, the "object" parameter could be a string of information, or an Item with data about some
        // special effect, or whatever.
        //
        //Use the syntax "on_hit_function", "[function name]"
        //You must also provide a key called "source_object" and a value of whatever.
        if (mods.ContainsKey("on_hit_function"))
        {
            var funcName = mods["on_hit_function"].ToString();
            object sourceObject;
            mods.TryGetValue("source_object", out sourceObject);
        
            if (!string.IsNullOrEmpty(funcName))
            {
                var methodInfo = typeof(WeaponHitEffectScripts).GetMethod(funcName);
                specialActionOnHit[sourceObject] = (Action<object, TM_Weapon, TM_CombatComponent>) Delegate.CreateDelegate(
                    typeof(Action<object, TM_Weapon, TM_CombatComponent>), methodInfo);
            }
        }
    }
    
    /// <summary>
    /// If a temporary modification was applied, use this Coroutine to pull it off when time is up.
    /// </summary>
    /// <param name="delay"></param>
    /// <returns></returns>
    public IEnumerator RemoveWeaponModAfterTime(string modName, float delay)
    {
        yield return new WaitForSeconds(delay);
        RemoveWeaponModifier(modName);
    }

    /// <summary>
    /// If we need to run some bespoke code when we hit a target, place it here. 
    /// </summary>
    /// <param name="invokingObject">An object that contains information about the action, probably from your
    /// game layer which is above Triger Mountain.</param>
    /// <param name="action"></param>
    public void AddSpecialActionOnHit(object invokingObject, Action<object, TM_Weapon, TM_CombatComponent> action)
    {
        specialActionOnHit[invokingObject] = action;
    }

    //todo: violation of TM/Game barrier here. Make DynamicGameVars into a TrigerMountain class
    /// <summary>
    /// If our weapon has specific dynamic changes that need to be made, those may be stored in the data,
    /// and we'll use that info here. 
    /// </summary>
    /// <param name="gameVars"></param>
    public virtual void HandleDynamicGameVars(DynamicGameVars gameVars)
    {
        
    }


}



/* 

Good code for tracking with 2D turrets

 *     public override void UpdateMotion()
    {
        Vector3 towardsPlayer = playerScript.transform.position - transform.position;
        towardsPlayer.Normalize();
        transform.right = Vector3.Cross(towardsPlayer, new Vector3(0, 0, 1));
    }

    public override void StayVertical()
    {
       
    }

    public override void UpdateAttacks()
    {
        Vector3 towardsPlayer = playerScript.transform.position - transform.position;

        towardsPlayer.x += -0.2f + Random.value*0.4f;
        towardsPlayer.Normalize();

        for (int t = 0; t < Attacks.Count(); t++)
        {
            Attacks[t].Update();

            if (transform.position.x < 12.0f)
            {
                float fShotSpeed = 15.0f + (5.5f * mcs.GetEnemySpeedMultiplier());
                Attacks[t].TryFire(transform.position, towardsPlayer, fShotSpeed);
            }
        }
    }
*/

/// <summary>
/// Not actually a weapon, but rather a connecting class to a different weapon that might fire when the main
/// weapon fires.
/// </summary>
public class TM_AttachedSubWeaponInfo
{
    /// <summary>
    /// The weapon that might fire.
    /// </summary>
    private TM_Weapon subWeapon;

    /// <summary>
    /// % chance the weapon fires when the main weapon's trigger is pulled.
    /// </summary>
    private float fireChance;

    /// <summary>
    /// If not zero, at least this much time must elapse before this subweapon will trigger again.
    /// </summary>
    private float minimumTimeBetweenProcs;

    private float lastProcTime;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="wep"></param>
    /// <param name="chance">% chance with each main weapon fire to fire this weapon</param>
    /// <param name="minTimeBetweenProcs">At least this much time must pass between procs</param>
    /// <param name="cc">The CombatComponent that owns the parent weapon.</param>
    public void SetWeaponInfo(TM_Weapon subweapon, float chance, float minTimeBetweenProcs, TM_CombatComponent cc)
    {
        subWeapon = subweapon;
        fireChance = chance;
        minimumTimeBetweenProcs = minTimeBetweenProcs;
        subweapon.SetOwningCombatComponent( cc );
    }

    public bool TryProc()
    {
        //check timer
        if (Time.realtimeSinceStartup - lastProcTime < minimumTimeBetweenProcs)
        {
            return false;
        }

        //roll dice
        if (Random.value > fireChance)
        {
            return false;
        }
        
        //try firing. If the weapon is on cooldown, we should not count this as a proc.
        if (!subWeapon.PullTrigger())
        {
            return false;
        }
        
        //we did it 
        lastProcTime = Time.realtimeSinceStartup;
        return true;

    }


}