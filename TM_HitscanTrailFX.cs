
using UnityEngine;

public class TM_HitscanTrailFX : MonoBehaviour
{
	private float lifetimeMax;
	private float lifetime;

	private LineRenderer myLine;
	private Material myMat;

	void Awake()
	{
		myLine = GetComponent<LineRenderer>();
		if (myMat == null)
		{
			myMat = Instantiate(myLine.material);
			myLine.material = myMat;
		}
	}

	private void Update()
	{
		lifetime -= Time.deltaTime;
		if (lifetime <= 0f)
		{
			myLine.enabled = false;
			enabled = false;
			var tform = transform;
			tform.parent = null;
			tform.position = Vector3.zero;
			tform.rotation = Quaternion.identity;
			return;
		}

		myMat.SetFloat("_Decay", lifetime / lifetimeMax);
	}

	/// <summary>
	/// Place this into position and set it to fade over time.
	/// </summary>
	/// <param name="start"></param>
	/// <param name="end"></param>
	/// <param name="fadetime"></param>
	public void SetLineInformation(Vector3 start, Vector3 end, float fadetime)
	{
		myLine.SetPosition(0, start);
		myLine.SetPosition(1, end);

		myLine.startColor = Color.white;
		myLine.endColor = Color.white;

		lifetime = fadetime;
		lifetimeMax = fadetime;
		
		myLine.enabled = true;
		enabled = true;
	}
	
}
