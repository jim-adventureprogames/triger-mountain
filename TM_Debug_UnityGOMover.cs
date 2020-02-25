using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a unity gameobject move around if you just wanna test stuff.
/// </summary>
public class TM_Debug_UnityGOMover : MonoBehaviour
{
    public Vector3 moveDir;
    public float speed;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += Time.deltaTime * speed * moveDir;
    }
}
