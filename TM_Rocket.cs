using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A TM_Rocket behaves like a TM_Projectile, except for a small period before launch where the speed and direction
/// may be different. This allows the rocket to launch, do one thing, then switch into a different mode with a greater
/// speed or different animation, whatever.
/// </summary>
public class TM_Rocket : TM_Projectile
{
	private Coroutine launchCoroutine;
	private bool inAssaultPhase;

	private Vector2 storedAttackDirection;

	/// <summary>
	/// If true, the rocket will slowly reduce speed during the initial drift phase.
	/// </summary>
	private bool shouldSlowdownDuringDrift;

	private ProjectileOnHitBehavior storedBehavior;
	
	/// <inheritdoc />
	protected override void Initialize()
	{
		base.Initialize();
		inAssaultPhase = false;
		storedAttackDirection = Vector2.zero;
	}

	/// <summary>
	/// Sets our direction but does not change our facing.
	/// </summary>
	/// <param name="dir"></param>
	public void SetDriftDirection(Vector2 dir)
	{
		direction = dir;
	}

	/// <summary>
	/// Set data and start a coroutine that ticks down time to launch into attack phase, racing forward at the realest of speeds.
	/// </summary>
	/// <param name="delay"></param>
	/// <param name="attackSpeed"></param>
	/// <param name="attackDirection"></param>
	/// <param name="slowdownDuringDrift"></param>
	/// <returns></returns>
	public void BeginCountdownToLaunchPhase(float delay, float attackSpeed, Vector2 attackDirection, bool slowdownDuringDrift = false)
	{
		//point at the way we're going to eventually launch.
		storedAttackDirection = attackDirection;
		transform.up = attackDirection;
		shouldSlowdownDuringDrift = slowdownDuringDrift;
		storedBehavior = behaviorOnHit;
		behaviorOnHit = ProjectileOnHitBehavior.bounce_off_anything;
		
		
		launchCoroutine = StartCoroutine(CountdownToAttackPhase(delay, attackSpeed, attackDirection));
	}

	/// <summary>
	/// Count down until it is time to launch into attack phase, racing forward at the realest of speeds.
	/// </summary>
	/// <param name="delay"></param>
	/// <param name="attackSpeed"></param>
	/// <param name="attackDirection"></param>
	/// <returns></returns>
	IEnumerator CountdownToAttackPhase(float delay, float attackSpeed, Vector2 attackDirection)
	{
		yield return new WaitForSeconds(delay);
		
		SetDirectionAndUpdateRotation(attackDirection);
		currentSpeed = attackSpeed;
		
		//play visual effect?
		//play sound?

		inAssaultPhase = true;
		behaviorOnHit = storedBehavior;
	}

	/// <summary>
	/// Make sure our launch coroutine is stopped if it hasn't been already.
	/// </summary>
	public void	OnReturnToPool()
	{
		StopCoroutine(launchCoroutine);
		inAssaultPhase = false;
		Debug.Log("Rocket returning to pool!");
	}

	/// <inheritdoc />
	public override void UpdateRotation()
	{
		if (inAssaultPhase)
		{
			base.UpdateRotation();
			return;
		}
		//we are drifting off, maybe we should be slowing down.
		if (shouldSlowdownDuringDrift)
		{
			currentSpeed *= 0.98f;
		}
		
		transform.up = storedAttackDirection;
	}

	
}
