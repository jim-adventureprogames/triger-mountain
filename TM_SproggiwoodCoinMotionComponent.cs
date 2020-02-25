using UnityEngine;
using System.Collections;

public class TM_SproggiwoodCoinMotionComponent : MonoBehaviour
{
    public Vector3 StartPosition;
    public Vector3 EndPosition;
    public Vector3 LaunchVector;
    public float LaunchSpeed;

    public float Lifetime;

    private bool bLaunched;
    private float fTimeSinceLaunch;

	// Use this for initialization
	void Start () 
    {
	
	}
	
    public void Launch( Vector3 start, Vector3 end, Vector3 launchDir, float launchSpeed, float fTime )
    {
        StartPosition = start;
        EndPosition = end;
        LaunchVector = launchDir;
        LaunchSpeed = launchSpeed;

        Lifetime = fTime;

        fTimeSinceLaunch = 0;
        bLaunched = true;
    }

	// Update is called once per frame
	void Update () 
    {
	    if( bLaunched )
        {
            fTimeSinceLaunch += Time.deltaTime;

        }
	}

    public bool ArrivedAtGoal()
    {
        return fTimeSinceLaunch >= Lifetime;
    }

    public Vector3 GetCurrentPosition()
    {
        float fDelta = fTimeSinceLaunch / Lifetime;

        //straight line position
        Vector3 vStraightLinePos = Vector3.Lerp(StartPosition, EndPosition, fDelta );

        //fake launch vector
        Vector3 vFakeLaunchVector = StartPosition + ( LaunchVector * LaunchSpeed * fTimeSinceLaunch );

        //true position
        return Vector3.Lerp(vFakeLaunchVector, vStraightLinePos, fDelta);
    }

	public void Reset()
	{
		bLaunched = false;
		fTimeSinceLaunch = 0;
	}
}
