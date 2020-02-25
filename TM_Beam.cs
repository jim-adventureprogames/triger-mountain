using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An in-world beam that is fired by a TM_Beam_Weapon. Doesn't update on its own, but has a collection of objects
/// that do.
/// </summary>
public class TM_Beam : MonoBehaviour
{
    /// <summary>
    /// The quad that is our beam in the world
    /// </summary>
    public MeshRenderer beamMR;
   
    /// <summary>
    /// What plays at the origin of the beam
    /// </summary>
    public GameObject muzzleFlashGO;
    private TM_SpriteAnimator muzzleFlashAnim;
    
    /// <summary>
    /// What plays at the point of impact where the beam hits the world
    /// </summary>
    public GameObject impactAnimGO;
    private TM_SpriteAnimator impactAnim;
    
    protected Material beamMaterial;

    /// <summary>
    /// The world object that causes the damage.
    /// </summary>
    [HideInInspector]
    public TM_Projectile projectile;

    /// <summary>
    /// For weapons with multiple beams that fire in different directions.
    /// </summary>
    [HideInInspector] 
    public Vector2 offsetFromMuzzlePosition;

    /// <summary>
    /// For weapons with multiple beams that fire in different directions.
    /// </summary>
    [HideInInspector] 
    public float rotationFromForward;

    [Tooltip("Often overriden by the weapon may be changed due to mods or other conditions.")]
    public float beamWidth;
    
    [Tooltip("What speed should the beam texture scroll? Set them to be the same for smoove beam, otherwise it's jumpy")]
    public Vector2 beamScrollMinMax;


    public void Initialize(TM_Projectile projectilePrefab, TM_CombatComponent owningCombatComponent, TM_Weapon owningWeapon)
    {
        GameObject go = PoolManager.Instantiate(projectilePrefab.gameObject);
        projectile = go.GetComponent<TM_Projectile>();
        projectile.CopyFromArchetype(projectilePrefab);
        projectile.SetInfoWithoutLaunch(owningCombatComponent, owningWeapon);
        
        //Create new copy of material so we can F with it.
        beamMaterial = Instantiate(beamMR.material);
        beamMR.material = beamMaterial;
        
        beamMR.sortingOrder = 0;
        beamMR.sortingLayerName = "character";

    }
    
    public void TurnBeamOn()
    {
        gameObject.SetActive(true);
        beamMR.enabled = true;
        projectile.gameObject.SetActive(true);


        if (muzzleFlashGO)
        {
            muzzleFlashGO.SetActive(true);
            muzzleFlashAnim = muzzleFlashGO.GetComponent<TM_SpriteAnimator>();
            muzzleFlashAnim.StartIfNotRunning("idle");
        }
		
        if (impactAnimGO)
        {
            impactAnimGO.SetActive(true);
            impactAnim = impactAnimGO.GetComponent<TM_SpriteAnimator>();
            impactAnim.StartIfNotRunning("idle");
        }
        
    }

    /// <summary>
    /// Hide the beam from the world.
    /// </summary>
    public void TurnBeamOff()
    {
        gameObject.SetActive(false);
        beamMR.enabled = false;
        projectile.gameObject.SetActive(false);

        if (muzzleFlashGO)
        {
            muzzleFlashGO.SetActive(false);
        }

        if (impactAnimGO)
        {
            impactAnimGO.SetActive(false);
        }
    }

    /// <summary>
    /// Ask TM_Utilities to do the math that positions us correctly.
    /// </summary>
    /// <param name="beamStart"></param>
    /// <param name="beamEnd"></param>
    public void AlignBetweenTwoPoints(Vector2 beamStart, Vector2 beamEnd)
    {
        TM_Utilities.AlignBeamBetweenTwoPointsWithProjectile(beamStart, beamEnd, beamWidth, beamMR, beamMaterial, beamScrollMinMax, projectile);
        
        var beamDelta = beamEnd - beamStart;
        if (impactAnim)
        {
            var impactTForm = impactAnim.transform;
            impactTForm.position = beamEnd;
            impactTForm.transform.up = beamDelta.normalized;
        }

        if (muzzleFlashAnim)
        {
            var muzzleTForm = muzzleFlashAnim.transform;
            muzzleTForm.position = beamStart;
            muzzleTForm.transform.up = beamDelta.normalized;
        }        

    }
}
