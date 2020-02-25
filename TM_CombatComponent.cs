using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TM_CombatComponent : MonoBehaviour
{
    HashSet<Action<TM_Projectile, Vector2>> listOnHitByProjectile;
    
    /// <summary>
    /// Flag that indicates who I belong to
    /// </summary>
    private int myFaction;
    public int Faction => myFaction;

    [Tooltip("Just for now.")]
    public CombatFaction debug_SetFactionInEditor;


    /// <summary>
    /// Use this to let your game code update the TM code with things like mouse position, or your AI's specific 
    /// targeting location if you need that level of accuracy. For example, you can use this to make sure that
    /// player launched grenades always go to where the mouse is. 
    /// </summary>
    [HideInInspector]
    public Vector2 cachedTargetingLocationFromOutside;

    void Awake()
    {
        listOnHitByProjectile = new HashSet<Action<TM_Projectile, Vector2>>();
        myFaction = (int)debug_SetFactionInEditor;
    }

    public void AddOnHitFunction(Action<TM_Projectile, Vector2> func)
    {
        if( !listOnHitByProjectile.Contains(func))
            listOnHitByProjectile.Add(func);
    }

    /// <summary>
    /// Calls all the functions assigned to it. This function does not change the projectile state directly
    /// but the functions it calls may do so.
    /// </summary>
    /// <param name="proj"></param>
    /// <param name="hitLocation">World space location of impact</param>
    /// <returns></returns>
    public void OnHitByProjectile(TM_Projectile proj, Vector2 hitLocation)
    {
        foreach (var f in listOnHitByProjectile)
        {
            f(proj, hitLocation);
        }
    }

    public virtual Vector3 GetWorldPosition()
    {
        return transform.position;
    }
    
    /// <summary>
    /// Who do I belong to in this grand conflict?
    /// </summary>
    /// <param name="newFaction"></param>
    public void SetFaction(int newFaction)
    {
        myFaction = newFaction;
    }

    public virtual void ShakeScreen()
    {
        TM_Utilities.CameraScreenshake(0.1f);
    }

    public virtual void KickbackOnWeaponFire(Vector2 vFireDirection, float kickbackImpulseWhenFired)
    {
    }
}