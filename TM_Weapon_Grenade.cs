using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TM_Weapon_Grenade : TM_Weapon_Projectile
{
	/// <summary>
	/// If this is set, we pass it to the projectile, so it knows what to spawn on impact.
	/// </summary>
	private string prefabCreatureOnLanding;


	/// <summary>
	/// If we're launching an object that spawns a creature, the prefab is generic and we need to decorate it
	/// with this. 
	/// </summary>
	private string projectileSpriteName;
	
	/// <inheritdoc />
	public override string GetProjectilePrefabName()
	{
		return "projectile_grenade";
	}
	
	public override void HandleDynamicGameVars(DynamicGameVars gameVars)
	{
        base.HandleDynamicGameVars(gameVars);
        prefabCreatureOnLanding = gameVars.GetString("spawn_on_impact");
        projectileSpriteName = gameVars.GetString("projectile_sprite");

	}

	/// <inheritdoc />
	protected override TM_Projectile FireOneShot(float deviateAngle, Vector2 offsetFromMuzzle)
	{
		var p = base.FireOneShot(deviateAngle, offsetFromMuzzle) as TM_Grenade;
		if (p != null)
		{
			if (!string.IsNullOrEmpty(prefabCreatureOnLanding))
			{
				p.prefabCreatureOnLandingName = prefabCreatureOnLanding;
			}

			if (!string.IsNullOrEmpty(projectileSpriteName))
			{
				p.SetSprite(projectileSpriteName);
			}
		}

		return p;
	}
}
