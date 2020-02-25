using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Rewired;

public class TM_Projectile : MonoBehaviour
{
    public enum ProjectileOnHitBehavior
    {
        destroy = 0,
        bounce_off_world,
        bounce_off_anything,
        drill_through_enemies,
        no_change,
    }
    
    [Header("Motion and Shape")]
    [Tooltip("World direction we're pointing for movement")]
    [SerializeField]
    protected Vector3 direction;
    public Vector3 Direction => direction;

    [Tooltip("Range min/max of our launch speed")]
    [SerializeField]
    protected Vector2 speedMinMax;
    
    [Tooltip("Force sprite to face up at all times.")]
    [SerializeField]
    protected bool shouldAlwaysRotateUp;

    [Tooltip("Perhaps this projectile is always spinnin'?")]
    [SerializeField]
    protected float constantRotationSpeedDegrees;

    [Tooltip("VFX for trails and lights if we need them")]
    [SerializeField]
    protected GameObject vfxPrefab;

    [Tooltip("For things like smoke trails, where we don't want them to vanish instantly, make this non-zero")]
    [SerializeField]
    protected float secondsToLeaveParticleInWorldAfterCollection;

    /// <summary>
    /// Set at run time if we want to lock a projectile in a given facing after launch
    /// </summary>
    protected Vector3 alwaysFaceThisDirection;
    protected bool lockFacing;

    /// <summary>
    /// We only need the name of the prefab to do the instantiation because we have a pool.
    /// </summary>
    protected string vfxPrefabName;

    
    protected float currentSpeed;
    /// <summary>
    /// Speed in units per second
    /// </summary>
    public float Speed => currentSpeed;

    [Tooltip("Size multiplier to apply each second of life. 1.0 == 100%")]
    [SerializeField]
    protected float sizeDeltaOverTime;

    [SerializeField]
    private Sprite sprite;

    [Header("Combat Information")]
    
    [Tooltip("Unmodified damage on hit, subject may take different amount.")]
    [SerializeField]
    protected float damageOnHit = 1.0f;
    public float Damage => damageOnHit;

    [Tooltip("Force applied to targets on hit.")]
    [SerializeField]
    protected float forceOnHit = 10.0f;
    public float ForceOnHit => forceOnHit;
    
    [Tooltip("How long will this impulse apply to the target? ")]
    [SerializeField]
    protected float forceDecayPerSecond = 0.1f;
    public float ForceDecayPerSecond => forceDecayPerSecond;

    [Tooltip("Prefab to instantiate at location of hit. Use for visual effects and audio.")]
    [SerializeField]
    protected GameObject prefab_impact;

    [Tooltip("Projectile will despawn after this long, value of zero will become a default, -1 infinite.")]
    [SerializeField]
    protected float destroyAfterTime;
    protected float lifetime;

    [Tooltip("What happens to this projectile when it hits something?")]
    [SerializeField]
    public ProjectileOnHitBehavior behaviorOnHit;

    /// <summary>
    /// When we hit something, cache the collision information so that we don't need to pass it all through
    /// hither and yon.
    /// </summary>
    private Vector2 lastCollisionNormal;

    [Tooltip("If we persist in the world after hitting something, but don't want to damage every frame, set this as a delay. " +
             "If the projectile has Drill behavior, this will be the drill delay duration.")]
    public float damageEveryThisOften;


    [Tooltip("If we have Drill behavior, we slowdown (or even stop!) between hits. This multiplier is applied to our speed.")]
    public float drillPauseSpeedMultiplier = 0.1f;

    /// <summary>
    /// Turn this on when I start the pause so I don't re-pause during a pause. This is my anti-pause-pause clause. 
    /// </summary>
    protected bool inDrillPauseState;
    
    /// <summary>
    /// World time of the last time we hit a target and damaged it. Set it during LateUpdate so that when the timer rolls over,
    /// multiple targets in the area can still be hit.
    /// </summary>
    protected float lastDamageTime;

    private bool shouldSetLastDamageTimeThisTick;
    
    public bool passThroughWorldCollision;

    protected bool bPayloadSpent;

    protected float fDeliverPayloadTick;

    protected GameObject targetObject;

    public AudioClip launchClip;
    public AudioClip impactClip;
    protected AudioSource audioSource;

    protected GameObject myParticle;

    [HideInInspector] public TM_CombatComponent owningCombatComponent;

    //#todo: Replace "ApplyFreezeTimer" with more general system for effects on hit.
    //public float ApplyFreezeTimer;

    public float radiusForGrenadeDamageOnDeath;

    public TM_Weapon weaponThatFiredMe { get; protected set; }
    private bool hasWeaponThatFiredMe;
    

    /// <summary>
    /// A cool method of moving a projectile to a given point while allowing it to curve
    /// </summary>
    protected TM_SproggiwoodCoinMotionComponent mySprogComponent;
    protected bool usesSprogMotion;

    /// <summary>
    /// Flag that indicates who I belong to
    /// </summary>
    private int myFaction;
    public int Faction => myFaction;

    /// <summary>
    /// We don't want to evaporate mid frame
    /// </summary>
    protected bool shouldReturnToPoolThisFrame;

    /// <summary>
    /// Call when I am done and returned to the pool
    /// </summary>
    private List<Action<TM_Projectile>> onReturnToPool;

    /// <summary>
    /// I draw thing
    /// </summary>
    //#todo: TM_SpriteAnimator herp
    protected SpriteRenderer mySR;

    protected Vector2 lastImpactPoint;

    /// <summary>
    /// If this projectile does foom up with grenade damage, don't do so more than once.
    /// </summary>
    private bool hasDetonatedGrenadeDamage;


    /// <summary>
    /// A GameObject outside of the TM scope that is responsible for this projectile. 
    /// </summary>
    public GameObject gameLevelOwner;

    
    
    
    public virtual void Awake()
    {
        Initialize();
    }

    protected virtual void Initialize()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
            audioSource.volume = TM_AudioManager.GetSFXVolume();

        currentSpeed = UnityEngine.Random.Range(speedMinMax.x, speedMinMax.y);

        mySR = GetComponent<SpriteRenderer>();  
        
        if (sprite == null && mySR != null)
        {
            sprite = mySR.sprite;
        }

        transform.localScale = Vector3.one;

        inDrillPauseState = false;

    }

    public virtual void Start()
    {
        
    }

    /// <summary>
    /// Called when we are pulled out of the pool and we have all our values assigned to us.
    /// </summary>
    /// <param name="arc"></param>
    public virtual void CopyFromArchetype(TM_Projectile arc)
    {
        //Copy over values
        speedMinMax = arc.speedMinMax;
        currentSpeed = UnityEngine.Random.Range(speedMinMax.x, speedMinMax.y);

        shouldAlwaysRotateUp = arc.shouldAlwaysRotateUp;
        sizeDeltaOverTime = arc.sizeDeltaOverTime;
        damageOnHit = arc.damageOnHit;
        forceOnHit = arc.forceOnHit;
        forceDecayPerSecond = arc.forceDecayPerSecond;
        prefab_impact = arc.prefab_impact;
        destroyAfterTime = arc.destroyAfterTime;
        behaviorOnHit = arc.behaviorOnHit;
        damageEveryThisOften = arc.damageEveryThisOften;
        passThroughWorldCollision = arc.passThroughWorldCollision;
        impactClip = arc.impactClip;
        launchClip = arc.launchClip;
        radiusForGrenadeDamageOnDeath = arc.radiusForGrenadeDamageOnDeath;
        constantRotationSpeedDegrees = arc.constantRotationSpeedDegrees;
        secondsToLeaveParticleInWorldAfterCollection = arc.secondsToLeaveParticleInWorldAfterCollection;

        //Copy the name of the prefab we use for fooming
        if (arc.vfxPrefab != null)
        {
            vfxPrefabName = arc.vfxPrefab.name;
        }
        else
        {
            vfxPrefabName = null;
        }
        //copy the sproggiwood stuff too
        
        //Set our own new timers/values based on data we just grabbed
        lifetime = 0f;
        
        //assume a max life of X seconds unless told otherwise.
        //if destroyAfterTime is < 0 in the editor, we must want to live forever. And who doesn't?
        if (destroyAfterTime == 0f)
        {
            destroyAfterTime = 10.0f;
        }

        mySR.sprite = arc.sprite;
        
        transform.localScale = Vector3.one;

        var myCircle = GetComponent<CircleCollider2D>();
        var arcCircle = arc.GetComponent<CircleCollider2D>();
        if (myCircle != null && arcCircle != null)
        {
            myCircle.radius = arcCircle.radius;
        }
        
        if( TryGetComponent(out Rigidbody2D myRB))
        {
            if (arc.TryGetComponent(out Rigidbody2D theirRB))
            {
                myRB.bodyType = theirRB.bodyType;
            }
        }

        if (TryGetComponent(out TM_SpriteAnimator myAnimator))
        {
            myAnimator.StopAnim();
            if (arc.TryGetComponent(out TM_SpriteAnimator arcAnimator))
            {
                myAnimator.CopyFrom(arcAnimator);
            }
        }


    }

    /// <summary>
    /// Points the projectile in a new direction, and adjusts our sprite to match.
    /// </summary>
    /// <param name="newDirection"></param>
    public void SetDirectionAndUpdateRotation(Vector3 newDirection)
    {
        direction = newDirection.normalized;
        UpdateRotation();
    }
    
    

    /// <summary>
    /// Change the speed to a new value, can exceed the min/max bounds.
    /// </summary>
    /// <param name="newSpeed"></param>
    public void SetSpeed(float newSpeed)
    {
        currentSpeed = newSpeed;
    }

    /// <summary>
    /// Change the amount of time this particle should live in the world.
    /// </summary>
    /// <param name="newLifespan"></param>
    public void SetLifespan(float newLifespan)
    {
        destroyAfterTime = newLifespan;
    }
    
    

    public virtual void Update()
    {
        UpdateMovement();
        UpdateRotation();
        UpdateSize();

        // Some projectiles live forever. Lucky? Cursed?
        if (destroyAfterTime >= 0f)
        {
            //tick down our ever-decreasing time to exist;
            lifetime += Time.deltaTime;
            if (lifetime >= destroyAfterTime)
            {
                shouldReturnToPoolThisFrame = true;
                return;
            }
        }

        //if (dontDestroyOnHit)
        //{
        //    fDeliverPayloadTick -= Time.deltaTime;
        //}
    }

    /// <summary>
    /// Handle anything that we have asked to transpire at the end of the frame.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private void LateUpdate()
    {
        if (shouldReturnToPoolThisFrame)
        {
            ReturnToPool();
            return;
        }

        if (shouldSetLastDamageTimeThisTick)
        {
            lastDamageTime = Time.realtimeSinceStartup;
            shouldSetLastDamageTimeThisTick = false;
        }
    }

    public virtual void UpdateSize()
    {
        if (sizeDeltaOverTime != 0.0f)
        {
            Vector3 vScale = transform.localScale;
            vScale.x += vScale.x * sizeDeltaOverTime * Time.deltaTime;
            vScale.y += vScale.y * sizeDeltaOverTime * Time.deltaTime;
            transform.localScale = vScale;
        }
    }

    public virtual void UpdateRotation()
    {
        if (lockFacing)
        {
            transform.up = alwaysFaceThisDirection;
        }
        else if (constantRotationSpeedDegrees == 0f)
        {
            transform.up = shouldAlwaysRotateUp ? Vector3.up : direction;
        }
        else
        {
            var oldRot = transform.rotation.eulerAngles;
            oldRot.z += constantRotationSpeedDegrees * Time.deltaTime;
            if (oldRot.z > 360.0f)
                oldRot.z -= 360.0f;
            else if (oldRot.z < -360.0f)
                oldRot.z += 360.0f;

            transform.rotation = Quaternion.Euler(oldRot);
        }
    }

    public virtual void UpdateMovement()
    {
        var oldPos = transform.position;
        var newPos = oldPos;
        
        if (usesSprogMotion)
        {
            newPos = mySprogComponent.GetCurrentPosition();
            direction = (newPos - oldPos).normalized;
            transform.position = newPos;
            
            if (mySprogComponent.ArrivedAtGoal())
            {
                DoImpact(newPos);
            }

            return;
        }

        newPos += Time.deltaTime*currentSpeed*direction;
        transform.position = newPos;
    }

    /// <summary>
    /// Called when summoned up out of the pool
    /// </summary>
    protected virtual void OnEnable()
    {
        //Clear out any of these calls from last time.
        onReturnToPool = new List<Action<TM_Projectile>>();

        lifetime = 0f;

        hasDetonatedGrenadeDamage = false;

        inDrillPauseState = false;

    }

    /// <summary>
    /// For special case weapons that don't use typical launch patterns, use this to make sure important information
    /// is still set.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="firingWeapon"></param>
    public virtual void SetInfoWithoutLaunch(TM_CombatComponent owner, TM_Weapon firingWeapon)
    {
        owningCombatComponent = owner;
        weaponThatFiredMe = firingWeapon;
        if (weaponThatFiredMe != null)
        {
            hasWeaponThatFiredMe = true;
        }
        SetFaction(owningCombatComponent.Faction);
        gameLevelOwner = owner.gameObject;
    }

    /// <summary>
    /// Places us in the world, and plays a launch sound if we have one.
    /// </summary>
    /// <param name="worldPosition"></param>
    /// <param name="owner"></param>
    /// <param name="firingWeapon"></param>
    public virtual void Launch(Vector3 worldPosition, TM_CombatComponent owner, TM_Weapon firingWeapon)
    {
        owningCombatComponent = owner;
        gameLevelOwner = owner.gameObject;

        transform.position = worldPosition;
        weaponThatFiredMe = firingWeapon;
        if (weaponThatFiredMe != null)
        {
            hasWeaponThatFiredMe = true;
        }

        TM_AudioManager.PlayClip(launchClip);

        //Create a particle if we need one
        if (!string.IsNullOrEmpty(vfxPrefabName))
        {
            //What if myParticle already exists? They have an auto pool-returner on them,
            //and thus should* be fine* if we abandon them here.

            myParticle = PoolManager.Instantiate(vfxPrefabName);
            myParticle.SetActive(true);
            myParticle.transform.parent = transform;
            myParticle.transform.localRotation = Quaternion.identity;
            myParticle.transform.localPosition = Vector3.zero;
            if( myParticle.TryGetComponent(out ParticleSystem ps ))
            {
                ps.Play(true);
            }
        
            //look for a tail
            if (myParticle.TryGetComponent(out TrailRenderer tail))
            {
                tail.Clear();
                tail.emitting = true;
            }
        }
        
        //Know who I am allied to!
        SetFaction(owningCombatComponent.Faction);
        
    }

    /// <summary>
    /// Called internally by physics calls for when we bump up against colliders.
    /// </summary>
    /// <param name="hitGO"></param>
    protected virtual void HandleObjectCollision(GameObject hitGO)
    {
        //Did we hit a combat component of some type?
        if (hitGO.TryGetComponent(out TM_CombatComponent cc))
        {
            OnHitCombatComponent(cc);
            return;
        }
        
        //Did we hit world collision?
        if (!passThroughWorldCollision &&
            hitGO.layer == LayerMask.NameToLayer("world_collision"))
        {
            //probably just blow up, but maybe bounce or something else
            OnHitWorldCollision(hitGO);
        }   
    }
    
    /// <summary>
    /// The projectile is touching something it can touch. 
    /// </summary>
    /// <param name="hitObject"></param>
    public virtual void OnCollisionEnter2D(Collision2D hitObject)
    {
        //save me for later
        lastCollisionNormal = hitObject.GetContact(0).normal;
        
        var hitGO = hitObject.gameObject;
        
        //where exactly did we touch?
        lastImpactPoint = hitObject.GetContact(0).point;
        
        HandleObjectCollision(hitGO);     
    }

    private void OnCollisionStay2D(Collision2D hitObject)
    {
        if (Time.realtimeSinceStartup - lastDamageTime < damageEveryThisOften)
            return;

        // we are going to hurt people this tick, yay!
        shouldSetLastDamageTimeThisTick = true;

        //todo: maybe not this! Could we want specific and different behavior for the first impact vs future ones?
        OnCollisionEnter2D(hitObject);
    }

    /// <summary>
    /// We've smacked into the world. Now what?
    /// </summary>
    /// <param name="objectWeHit">World thing we've touched</param>
    private void OnHitWorldCollision(GameObject objectWeHit)
    {
        //explode? bounce? leave a decal? 
        DoImpact(lastImpactPoint);
        ExecuteOnHitBehavior(true, objectWeHit);
    }

    /// <summary>
    /// This projectile scored a hit against a target, now what?
    /// </summary>
    public void OnSuccessfulHit(GameObject objectWeHit)
    {
        DoImpact(lastImpactPoint);
        ExecuteOnHitBehavior(false, objectWeHit);
    }

    /// <summary>
    /// We hit something? Do we clean up? Bounce? What do?
    /// </summary>
    protected void ExecuteOnHitBehavior(bool hitWasWorldCollision, GameObject objectWeHit)
    {
        switch (behaviorOnHit)
        {
            case ProjectileOnHitBehavior.destroy:
                shouldReturnToPoolThisFrame = true;
                break;
            case ProjectileOnHitBehavior.bounce_off_world:
                if (hitWasWorldCollision)
                {
                    DoBounce(objectWeHit);
                }
                break;
            case ProjectileOnHitBehavior.bounce_off_anything:
                DoBounce(objectWeHit);
                break;
            case ProjectileOnHitBehavior.drill_through_enemies:
                //turn off damage for a set amount of time
                //reduce speed for same amount of time
                //when that time is over, return to normal
                if (hitWasWorldCollision)
                {
                    shouldReturnToPoolThisFrame = true;
                }
                else
                {
                    StartDrillBehavior();
                }
                break;
            case ProjectileOnHitBehavior.no_change:
                //???
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// We are supposed to drill through enemies, and we hit one! Slow down a touch, 
    /// </summary>
    protected virtual void StartDrillBehavior()
    {
        //play an effect or sound?
        
        //pause.
        StartCoroutine(DrillPause_Coroutine());
    }

    protected virtual IEnumerator DrillPause_Coroutine()
    {
        if (inDrillPauseState)
            yield break;
            
        //allow us to move through enemies instead of bumping around them
        bool hasRB = false;
        if( TryGetComponent(out Rigidbody2D myRB ))
        {
            hasRB = true;
            myRB.bodyType = RigidbodyType2D.Kinematic;
        }

        inDrillPauseState = true;
        //turn settings down
        float oldSpeed = currentSpeed;
        SetSpeed(currentSpeed * drillPauseSpeedMultiplier);

        //wait
        yield return new WaitForSeconds(damageEveryThisOften);

        //turn them back on
        SetSpeed(oldSpeed);
        inDrillPauseState = false;
        
        
        //allow us to move through enemies instead of bumping around them
        if( hasRB )
        {
            //if we are gonna hit something again, stay kinematic so we can smoothly march through our foes.
            bool hittingAgain = false;
            if( TryGetComponent(out CircleCollider2D myCollider ))
            {
               RaycastHit2D[] hitResults = Physics2D.CircleCastAll(transform.position, myCollider.radius, direction, 0f);
               foreach (var hit in hitResults)
               {
                   if (hit.collider.gameObject == gameObject)
                   {
                       continue;
                   }

                   hittingAgain = true;
                   HandleObjectCollision(hit.collider.gameObject);
               }
            }

            //but otherwise, return to normal physics
            if (!hittingAgain)
            {
                myRB.bodyType = RigidbodyType2D.Dynamic;
            }
        }
    }

    /// <summary>
    /// Examine the position of the object we hit, and change our direction based on that relation
    /// </summary>
    /// <param name="???"></param>
    protected void DoBounce(GameObject objectWeHit )
    {
        var oldDir = direction;
        direction = Vector2.Reflect(direction, lastCollisionNormal);
        
        //hey remember that normal we cached
        //direction = -2.0f * Vector2.Dot(direction, lastCollisionNormal) * lastCollisionNormal - (Vector2)direction;
        
        //scoot along in that direction just a touch, 
        transform.position += direction * currentSpeed * 0.05f;
    }

    public void ReturnToPool()
    {
        //clear out our data?
        shouldReturnToPoolThisFrame = false;
        
        //are we drilling and need to undrill?
        if (inDrillPauseState)
        {
            if( TryGetComponent(out Rigidbody2D myRB ))
            {
                myRB.bodyType = RigidbodyType2D.Dynamic;
            }
        }
        
        //tell everyone it is time to go home
        foreach (var a in onReturnToPool)
        {
            a(this);
        }
        
        //if I have a particle
        if (myParticle != null)
        {
            if (secondsToLeaveParticleInWorldAfterCollection > 0f)
            {
                myParticle.transform.parent = null;
                if (myParticle.TryGetComponent(out ParticleSystem p))
                {
                    p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                PoolManager.ReturnToPoolAfterDelay(myParticle.gameObject, secondsToLeaveParticleInWorldAfterCollection);
            }
            else
            {
                PoolManager.ReturnToPool(myParticle.gameObject);
            }
        }

        gameLevelOwner = null;
        
        PoolManager.ReturnToPool(gameObject);
    }

    /// <summary>
    /// Send ourselves to the CombatComponent on the object we hit and see whats up.
    /// </summary>
    /// <param name="target">The CC we've hit. Shall not be null.</param>
    /// <returns></returns>
    protected virtual void OnHitCombatComponent(TM_CombatComponent target )
    {
        //If there isn't one, or there is and I don't care, do nothing.
        if (myFaction == target.Faction )
        {
            return;
        }
        
        //if not,
        target.OnHitByProjectile(this, lastImpactPoint); // this.transform.position);
        
        //do we have any special behavior to call?
        if( hasWeaponThatFiredMe && weaponThatFiredMe.specialActionOnHit.Count > 0)
        {
            foreach (var kvp in weaponThatFiredMe.specialActionOnHit)
            {
                kvp.Value(kvp.Key, weaponThatFiredMe, target);
            }
        }

        //Now we handle our bounce / hide / whatever behavior
        OnSuccessfulHit(target.gameObject);

    }

    public virtual void OnTriggerStay2D(Collider2D hitObject)
    {
        if (bPayloadSpent)
            return;

        if (fDeliverPayloadTick > 0.0f)
            return;

        fDeliverPayloadTick = damageEveryThisOften;
        DoImpact(transform.position);
    }

    /// <summary>
    /// Who do I belong to in this grand conflict?
    /// </summary>
    /// <param name="newFaction"></param>
    public void SetFaction(int newFaction)
    {
        myFaction = newFaction;
    }
    
    
    protected virtual void DoImpact(Vector3 pos)
    {
        if (prefab_impact != null)
        {
            SpawnImpactPrefab(pos);
        }

        if (impactClip != null)
        {
            TM_AudioManager.PlayClip(impactClip);
        }

        if (radiusForGrenadeDamageOnDeath > 0f)
        {
            ExplodeWithGrenadeDamage(radiusForGrenadeDamageOnDeath);
        }
    }

    /// <summary>
    /// Foom foom foom!
    /// </summary>
    /// <param name="pos"></param>
    protected virtual void SpawnImpactPrefab(Vector3 pos)
    {
        var go = PoolManager.Instantiate(prefab_impact);
        go.transform.position = pos;
        go.transform.rotation = transform.rotation;
    }

    protected virtual void ExplodeWithGrenadeDamage(float radius)
    {
        if (hasDetonatedGrenadeDamage)
            return;

        hasDetonatedGrenadeDamage = true;
        
        RaycastHit2D[] allTargets = Physics2D.CircleCastAll(transform.position, radius, Vector2.zero, 
            0f);

        for (int t = 0; t < allTargets.Length; t++)
        {
            RaycastHit2D hit = allTargets[t];
            GameObject hitObject = hit.collider.gameObject;
            var cc = hitObject.GetComponent<TM_CombatComponent>();
            if (cc != null)
            {
                OnHitCombatComponent(cc);
            }
        }
    }

    /// <summary>
    /// Register an action to be called when this projectile is done with life.
    /// Use this for game-specific events that happen outside of the purview of Triger Mountain code.
    /// </summary>
    /// <param name="a"></param>
    public void RegisterOnReturnToPoolAction(Action<TM_Projectile> a)
    {
        onReturnToPool.Add(a);
    }

    /// <summary>
    /// Change our sprite at runtime, sure
    /// </summary>
    /// <param name="spriteName"></param>
    public void SetSprite(string spriteName)
    {
        sprite = AssetLoader.GetSprite(spriteName);
        mySR.sprite = sprite;
    }

    /// <summary>
    /// Inform this projectile that it will always face a given direction, no matter where it turns or moves.
    /// </summary>
    /// <param name="dashDirection"></param>
    public void SetPermanentFacing(Vector3 dashDirection)
    {
        alwaysFaceThisDirection = dashDirection;
        lockFacing = true;
    }

    /// <summary>
    /// Maybe some crazy ass cutscene or power is going to ask for this.
    /// </summary>
    public void RemovePermanentFacing()
    {
        lockFacing = false;
    }
}

//Added this line rendering code from other LD games
/*

    void DrawLaserBetweenSelfAnd(GameObject target)
    {
        if (target == null)
            return;

        MeshRenderer mr = purpleLaserQuad.GetComponent<MeshRenderer>();
        mr.enabled = true;

        //Get the vector between us and them.
        Vector3 vLightningDelta = target.transform.position - laserBone.transform.position;
        float fDist = vLightningDelta.magnitude;

        //place the quad at the mid point between us and them, and rotate it to face correctly along the vector
        Vector3 lightningPos = laserBone.transform.position;
        vLightningDelta.Normalize();
        lightningPos += vLightningDelta * fDist * 0.5f;
        purpleLaserQuad.transform.position = lightningPos;
        purpleLaserQuad.transform.right = vLightningDelta;

        //scale the quad to match the distance between us and them, the variance here is for laser pulsing effect
        Vector3 pissss = purpleLaserQuad.transform.localScale;
        pissss.x = fDist;
        pissss.y = 0.1f + Random.value * 0.2f;
        purpleLaserQuad.transform.localScale = pissss;

        //Scale the texture on the quad and rotate through it
        Material lightningMaterial = purpleLaserQuad.GetComponent<MeshRenderer>().material;
        lightningMaterial.mainTextureScale = new Vector2(fDist / 4, 1);
        Vector2 vPants = lightningMaterial.mainTextureOffset;
        vPants.x += Time.smoothDeltaTime * Random.value * -12.05f;
        lightningMaterial.mainTextureOffset = vPants;

    }

*/