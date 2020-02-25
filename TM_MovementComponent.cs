
using UnityEngine;
using UnityEngine.Experimental.AI;

public class TM_MovementComponent : MonoBehaviour
{
    [Tooltip("Turn this on to prevent this unit from stacking on top of other units.")]
    [SerializeField]
    private bool shouldPushAwayFromOthersOfType;

    [Tooltip("All outside impulses applied to this unit are multiplied by this. 1.0 is immobile. > 1.0 is weird, so don't. " +
             "Less than 0 makes the unit lighter.")]
    [SerializeField]
    protected float outsideImpulseReduction;
    
    protected Vector3 desiredMoveThisFrame;
    protected Vector2 _vGoalPosition;

    protected Vector2 directMove;
    protected Vector2 combinedImpulse;
    protected float impulseDecay;

    protected bool bWasStuck = false;

    protected bool bWasAskedToStopThisTick = false;

    protected GameObject objectIMightBeStuckOn;
    protected float fMightBeStuckTimer;

    /// <summary>
    /// my collide lide, my collide
    /// </summary>
    private Collider2D myCollider;

    /// <summary>
    /// Things stupid enough to hit my collision
    /// </summary>
    private static RaycastHit2D[] hits = new RaycastHit2D[5];
    
    /// <summary>
    /// Used when I check to see if I'm overlapping other units of my type.
    /// </summary>
    private static Collider2D[] contactColliders = new Collider2D[5];

    private static ContactFilter2D worldCollisionFilter;
    private static ContactFilter2D otherUnitsCollisionFilter;

    public virtual void SetStuckOnThis( GameObject obj )
    {
        if (obj != objectIMightBeStuckOn)
        {
            fMightBeStuckTimer = 0f;
        }
        objectIMightBeStuckOn = obj;
    }

    public virtual void SetNotStuck()
    {
        objectIMightBeStuckOn = null;
    }

    public virtual void ActuallyMoveThisFrame()
    {
        
    }

    private void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        worldCollisionFilter.layerMask = LayerMask.GetMask("world_collision");
        worldCollisionFilter.useLayerMask = true;
    }

    public virtual void UpdateDeathFrenzy()
    {

    }

    public virtual void Update()
    {
        if (objectIMightBeStuckOn != null)
        {
            fMightBeStuckTimer += Time.deltaTime;
            if (fMightBeStuckTimer > 0.1f)
            {
                EscapeStuckCollision();
            }
        }
        else
        {
            fMightBeStuckTimer = 0f;
        }
    }

    protected virtual void EscapeStuckCollision()
    {
        fMightBeStuckTimer = 0f;
    }

    public virtual void PrepareNewGame()
    {

    }

    /// <summary>
    /// Cause this unit to move via outside force. Force is adjusted by outside impulse reduction, a way to simulate
    /// our weight.
    /// </summary>
    /// <param name="unitsPerSecond"></param>
    /// <param name="fPercentDecayPerSecond"></param>
    public virtual void AddImpulse(Vector2 unitsPerSecond, float fPercentDecayPerSecond)
    {
        unitsPerSecond *= 1.0f - outsideImpulseReduction;
        
        combinedImpulse += unitsPerSecond;
        impulseDecay = fPercentDecayPerSecond;
    }

    public virtual void SetDirectMove(Vector2 vUnitsThisFrame)
    {
        directMove = vUnitsThisFrame;
    }

    public virtual void StopAllMotion()
    {
        combinedImpulse = Vector2.zero;
        impulseDecay = 0f;
        bWasAskedToStopThisTick = true;
    }

    /// <summary>
    /// We have all our impulses and inputs applied, now math out where we're gonna go, and
    /// bounce off anything we need to bounce off of.
    /// </summary>
    /// <returns></returns>
    public virtual Vector2 CalculateMoveThisFrame(float speedMultiplier = 0)
    {
        var currentPosition = transform.position;
        
        if (bWasAskedToStopThisTick)
        {
            bWasAskedToStopThisTick = false;
            desiredMoveThisFrame = Vector3.zero;
        }

        desiredMoveThisFrame = combinedImpulse * Time.deltaTime;
        if (impulseDecay > 0f)
        {
            combinedImpulse -= (combinedImpulse * impulseDecay * Time.deltaTime);
            if (combinedImpulse.sqrMagnitude <= 0.05f)
            {
                combinedImpulse = Vector2.zero;
                impulseDecay = 0f;
            }
        }

        desiredMoveThisFrame += (Vector3) directMove * (speedMultiplier + 1.0f);
        directMove = Vector2.zero;
        
        //if we're overlapping other units like ourselves, perhaps we should push away
        if (shouldPushAwayFromOthersOfType)
        {
            desiredMoveThisFrame += CalculatePushAwayFromOthers();
        }

        //Now that we've figured out where we want to go, see if we can do so.
        return CalculateWorldCollisionAndReturnNewPosition(currentPosition, 
            currentPosition + desiredMoveThisFrame);
        
    }

    public bool WasIStuckThisTick()
    {
        return bWasStuck;
    }

    /// <summary>
    /// Determine if we're colliding with others like ourselves, and if so try to move away.
    /// </summary>
    /// <returns></returns>
    public virtual Vector3 CalculatePushAwayFromOthers()
    {
        var myPosition = transform.position;
        var returnVec = Vector3.zero;
        
        otherUnitsCollisionFilter.layerMask = 1 << gameObject.layer;
        otherUnitsCollisionFilter.useLayerMask = true;
        var hitCount = myCollider.GetContacts(otherUnitsCollisionFilter,contactColliders);
        if (hitCount > 0)
        {
            for (int t = 0; t < hitCount; t++)
            {
                var c = contactColliders[t];
                if (c.gameObject == gameObject)
                {
                    continue;
                }

                var deltaAway =  myPosition - c.transform.position;
                deltaAway.Normalize();
                if (deltaAway == Vector3.zero)
                {
                    //uh
                    deltaAway = UnityEngine.Random.onUnitSphere;
                }
                returnVec += deltaAway * Time.deltaTime;
            }
        }

        return returnVec;
    }

    /// <summary>
    /// Check the math of where we were, where we want to be, and what we can do about it if we are
    /// touching something in the world that we should not be able to enter.
    /// </summary>
    /// <param name="oldPosition"></param>
    /// <param name="desiredPosition"></param>
    /// <returns></returns>
    public virtual Vector2 CalculateWorldCollisionAndReturnNewPosition(Vector2 oldPosition, Vector2 desiredPosition)
    {
        var deltaToNew = desiredPosition - oldPosition;
        var distToNew = deltaToNew.magnitude;

        if (distToNew == 0f)
        {
            return desiredPosition;
        }

        //What am I touching?
        var hitCount = myCollider.Cast(deltaToNew.normalized,worldCollisionFilter, hits, distToNew);
        if (hitCount > 0)
        {
            //we hit something trying to move
            var deltaHorizontal = deltaToNew;
            var deltaVertical = deltaToNew;

            deltaHorizontal.y = 0;
            deltaVertical.x = 0;
            
            //check horizontal
            hitCount = myCollider.Cast(deltaHorizontal.normalized, worldCollisionFilter, hits, 
                Mathf.Abs(deltaHorizontal.x));
            if (hitCount > 0)
            {
                hitCount = myCollider.Cast(deltaVertical.normalized, worldCollisionFilter, hits, 
                    Mathf.Abs(deltaVertical.y));
                if (hitCount > 0)
                {
                    //we can't move, do something else, uh oh.
                    return oldPosition;
                }

                //we can move vertically!
                return oldPosition + deltaVertical;
            }

            //we can move from side to side 🎶
            //(O Christmas Tree was playing on winamp when I wrote this)
            return oldPosition + deltaHorizontal;
        }
        
        //we can move just fine.        
        return desiredPosition;
    }
    public virtual Vector3 GetDesiredMove()
    {
        return desiredMoveThisFrame;
    }

    public virtual void SetGoalPosition(Vector2 vGoal)
    {
        _vGoalPosition = vGoal;
    }
}
