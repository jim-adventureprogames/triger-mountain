using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// A parent class for weapons that fire consistently, every frame, as long as the trigger is held down. Things like
/// machineguns do not qualify, as they are burst fire weapons with delays between shots. This is instead for constant
/// aggression such as flamethrowers or energy beams.
/// </summary>
public class TM_Weapon_ContinuousFire : TM_Weapon
{
	public enum EContinuousFireChargeState
	{
		ready = 0,
		firing,
		recovering_from_empty,
	}

	[Header("Continuous Fire")] 

	[SerializeField]
	[Tooltip("If > 0, this weapon can only fire until the fuel is empty.")]
	protected float maxFuelAmount;

	[SerializeField]
	[Tooltip("If this weapon is fuel driven, how much fuel does it use per second?")]
	protected float fuelBurnPerSecond;

	[SerializeField]
	[Tooltip("When the weapon is not firing, does it regain fuel automatically?")]
	protected float fuelRechargePerSecond;
	
	[SerializeField]
	[Tooltip("If this weapon is emptied of fuel, does it need to wait until a full recharge before it can fire again?")]
	protected bool doesWeaponShutDownWhenEmptiedUntilRefilled;
	
	[SerializeField]
	[Tooltip("Does the weapon refuel speed change if it is recovering from a fully empty tank?")]
	protected float refuelSpeedMultiplierFromEmpty;

	[SerializeField]
	[Tooltip("Should code that asks this weapon for numbers see the inverse, so it looks like a heat/cooldown deal?")]
	protected bool shouldPretendHeatCooldown;

	protected EContinuousFireChargeState chargeState;

	protected float currentFuel;

	[SerializeField]
	[Tooltip("This value allows a weapon to fire multiple beams or gouts of flame or whatever. Similar to LinkedShots in the parent class. Why not used LinkedShots for all weapons? You can if you want to.")]
	protected int LinkedNozzles;

	/// <summary>
	/// Call this function when the lockdown has ended because the fuel has been fully restored.
	/// </summary>
	protected Action<TM_Weapon, object[]> onFuelLockdownOver;
	protected object[] args_onFuelLockdownOver;

	/// <inheritdoc />
	protected override void Initialize()
	{
		base.Initialize();
		currentFuel = maxFuelAmount;
	}

	/// <summary>
    /// Track the held down state, when this and the current state don't match, it might be time to turn off the gun.
    /// </summary>
    protected bool triggerHeldDown;

	/// <summary>
	/// If true, we need to reshape and position the nozzles this frame.
	/// </summary>
	protected bool bNozzleCountDirty = false;

	protected override void Update()
    {
	    if (isFiring)
	    {
		    //If we were holding the trigger down before, check to see if we are still doing so!
		    if (!triggerHeldDown)
		    {
			    //oh, we must have let go of the trigger!
			    isFiring = false;
			    OnReleaseTrigger();
			    if (chargeState != EContinuousFireChargeState.recovering_from_empty)
			    {
				    chargeState = EContinuousFireChargeState.ready;
			    }
		    }
		    else
		    {
			    //we are firing and holding down the trigger
			    if (maxFuelAmount > 0)
			    {
				    currentFuel -= fuelBurnPerSecond * Time.deltaTime;
				    
				    //did we just burn out?
				    if (currentFuel <= 0)
				    {
					    isFiring = false;
					    OnReleaseTrigger();
					    if (doesWeaponShutDownWhenEmptiedUntilRefilled)
					    {
						    chargeState = EContinuousFireChargeState.recovering_from_empty;
						    //todo: let game code know so that an effect can be played 
					    }
					    else
					    {
						    chargeState = EContinuousFireChargeState.ready;
					    }
				    }
					else
				    {
					    chargeState = EContinuousFireChargeState.firing;
				    }
			    }
		    }
	    }
	    else //not firing this frame, so let's recover.
	    {
		    var recoverAmount = fuelRechargePerSecond * Time.deltaTime;
		    if (chargeState == EContinuousFireChargeState.recovering_from_empty)
		    {
			    recoverAmount += recoverAmount * refuelSpeedMultiplierFromEmpty;
		    }

		    currentFuel += recoverAmount;
		    if (currentFuel >= maxFuelAmount)
		    {
			    currentFuel = maxFuelAmount;
			    
			    //recharg'd
			    if (chargeState == EContinuousFireChargeState.recovering_from_empty)
			    {
				    //todo: tell the game the weapon has recovered.
				    onFuelLockdownOver?.Invoke(this, args_onFuelLockdownOver);
			    }

			    chargeState = EContinuousFireChargeState.ready;
		    }
	    }

	    //This will be set to true when the trigger is detected.
	    triggerHeldDown = false;
    }

	/// <inheritdoc />
	public override void RandomizeRemainingCooldown()
	{
		base.RandomizeRemainingCooldown();
		chargeState = EContinuousFireChargeState.recovering_from_empty;
		currentFuel = maxFuelAmount * Random.value;
	}

	/// <inheritdoc />
    public override bool PullTrigger()
    {
	    //if the weapon can't be fired right now, don't count a trigger pull.
	    //todo: play a sound? make a whatever?
	    if (chargeState == EContinuousFireChargeState.recovering_from_empty)
	    {
		    return false;
	    }
	    
	    triggerHeldDown = true;

	    //if we are already firing, then don't worry
	    if (isFiring)
	    {
		    return true;
	    }
	
	    //oh hey, we weren't firing and now we are. 
	    Fire();

	    return true;
    }

    /// <summary>
    /// What are we supposed to instantiate when we fire?
    /// </summary>
    /// <returns></returns>
    public virtual string GetProjectilePrefabName()
    {
	    return "projectile_default";
    }

	/// <summary>
	/// Account for logic that needs to happen every time a weapon fires -- we instead may just keep a timer going and
	/// proc those effects every x seconds.
	/// </summary>
    protected override void Fire()
    {
	    base.Fire();
    }

    protected virtual void OnReleaseTrigger() { }
    protected virtual void OnStartTrigger() { }
    protected virtual void OnMaintainTrigger() { }

    /// <summary>
    /// See what % foom we have left in the tank. Return the inverse if we're pretending to be a heat weapon.
    /// </summary>
    /// <returns></returns>
    public float GetFuelRemainingRatio()
    {
	    if (maxFuelAmount == 0f)
		    return 1.0f;
	    
	    var ratio = currentFuel / maxFuelAmount;
	    if (shouldPretendHeatCooldown)
		    return 1.0f - ratio;

	    return ratio;
    }

    //private void OnGUI()
    //{
	//    GUI.Label( new Rect(128,64,1000,32), "Fuel: " + currentFuel + " State: " + chargeState );
    //}


    /// <summary>
    /// Call this when the weapon has been locked down due to an empty tank and is now full again and ready to fire. 
    /// </summary>
    /// <param name="function"></param>
    /// <param name="args"></param>
    public void SetFunc_OnWeaponLockdownOver(Action<TM_Weapon, object[]> function, params object[] args)
    {
	    args_onFuelLockdownOver = args;
	    onFuelLockdownOver = function;
	    
    }

    /// <inheritdoc />
    protected override void RebuildWeaponStatsWithModifiers()
    {
	    LinkedNozzles = (int)cachedUnmodifiedValues["LinkedNozzles"];
	    base.RebuildWeaponStatsWithModifiers();
	    bNozzleCountDirty = true;
    }

    /// <inheritdoc />
    protected override void CacheDefaultValues()
    {
	    base.CacheDefaultValues();
	    cachedUnmodifiedValues["LinkedNozzles"] = LinkedNozzles;
    }

    protected virtual void SortAndSizeNozzlesByCount(int nozzleCount)
    {
	    
    }

    /// <inheritdoc />
    public override void ApplySingleMod(Dictionary<string, object> mods)
    {
	    base.ApplySingleMod(mods);
	    
	    //LinkedNozzles is a value that lets us fire multiple side-by-side beams or flames.
	    //If this value is 2 or greater, that many shots will fire instead of just 1, and they will be offset
	    //perpendicular to the muzzle direction.
	    //
	    //This value adds X number of linked shots to the weapon. If the weapon has none to start, it will end up
	    //with at least one.
	    //
	    //Use the syntax "linked_nozzles", "[number of linked nozzles to add]".
	    //
	    //This is very similar to LinkedShots in the parent class! This value is here in case your game wants to 
	    //make a distinction between modifiers for beams/gouts and regular shots, as one can be way more powerful than the other.
	    if (mods.ContainsKey("linked_nozzles"))
	    {
		    var extraLinks = int.Parse(mods["linked_nozzles"].ToString());
		    if (LinkedNozzles < 2)
		    {
			    LinkedNozzles = 1 + extraLinks;
		    }
		    else
		    {
			    LinkedNozzles += extraLinks;
		    }
	    }
    }
}
