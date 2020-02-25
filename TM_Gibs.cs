using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An object that bounces around the world using default Unity physics. Fades after a given time, may spawn effects
/// on impact with other world objects.
/// </summary>
public class TM_Gibs : MonoBehaviour
{
    [SerializeField]
    [Tooltip("How long should this object exist in the world?")]
    protected float lifetime;
    
    [SerializeField]
    [Tooltip("How long should the fadeout take before it vanishes? This value is subtracted from lifetime to determine" +
             "when the fade begins.")]
    protected float fadeAfter;
    
    [SerializeField]
    [Tooltip("If this gib should spawn something when it hits another physics object, add that here.")]
    protected GameObject prefab_SpawnOnImpact;

	protected float timeAlive = 0f;
    protected SpriteRenderer mySR;

    protected Rigidbody2D myRB;

    protected bool bInitialized = false;
    
    // Start is called before the first frame update
    void Awake()
    {
		Initialize();
    }

    void Initialize()
    {
		mySR = GetComponent<SpriteRenderer>();
		myRB = GetComponent<Rigidbody2D>();
		bInitialized = true;
    }

    // Update is called once per frame
    void Update()
    {
	    timeAlive += Time.deltaTime;
	    if (timeAlive >= lifetime)
	    {
		    //all done
		    PoolManager.ReturnToPool(gameObject);
		    return;
	    }

	    if (timeAlive > fadeAfter)
	    {
		    var delta = (timeAlive - fadeAfter) / (lifetime - fadeAfter);
		    var alpha = 1.0f - delta;
		    var color = mySR.color;
		    color.a = alpha;
		    mySR.color = color;
		    
	    }
    }
	
    public void Launch(Vector2 launchDirection, float launchForce, Sprite sprite)
    {
	    if( !bInitialized )
		    Initialize();

	    mySR.sprite = sprite;
	    myRB.AddForce(launchDirection * launchForce);

	    timeAlive = 0f;
    }

    private void OnEnable()
    {
	    timeAlive = 0f;
	    var color = mySR.color;
	    color.a = 1.0f;
	    mySR.color = color;
    }
}
