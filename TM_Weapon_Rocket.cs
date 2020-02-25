using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TM_Weapon_Rocket : TM_Weapon_Projectile
{
	[Header("Rocket Information")] 
	[SerializeField]
	[Tooltip("Drift speed before the rocket launches")]
	private Vector2 launchPhaseSpeedMinMax;

	[SerializeField]
	[Tooltip("Delay before entering attack phase")]
	private Vector2 timeUntilAttackPhaseMinMax;

	[SerializeField]
	[Tooltip("If true, the rocket will pick a direction on the circle to drift before going into attack phase")]
	private bool driftInRandomDirection;

	[SerializeField]
	[Tooltip("If true, the rocket will gradually slow to a stand still during the pre-assault drift phase.")]
	private bool rocketShouldSlowDuringDrift;
	
	
	public override string GetProjectilePrefabName()
	{
		return "projectile_rocket";
	}
	
	/// <summary>
	/// After firing one shot, the Rocket gets placed into its initial stage, which is usually a slower motion
	/// that does not represent full launch speed.
	/// </summary>
	/// <param name="deviateAngle"></param>
	/// <param name="offsetFromMuzzle"></param>
	/// <returns></returns>
	protected override TM_Projectile FireOneShot(float deviateAngle, Vector2 offsetFromMuzzle)
	{
		var rocket = base.FireOneShot(deviateAngle, offsetFromMuzzle) as TM_Rocket;

		if (rocket == null)
		{
			return null;
		}

		var storedDirection = rocket.Direction;
		var storedSpeed = rocket.Speed;
		
		//change the speed and direction, change the facing, and set a timer for actual rocket launch.
		if (driftInRandomDirection)
		{
			rocket.SetDriftDirection(Random.insideUnitCircle);
		}
		
		rocket.SetSpeed(Random.Range(launchPhaseSpeedMinMax.x, launchPhaseSpeedMinMax.y));

		//Tick down to launch
		rocket.BeginCountdownToLaunchPhase(Random.Range(timeUntilAttackPhaseMinMax.x, timeUntilAttackPhaseMinMax.y),
		   storedSpeed,storedDirection, rocketShouldSlowDuringDrift);
		
		return rocket;
	}

}
