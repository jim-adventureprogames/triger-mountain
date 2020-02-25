using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// If your object has a sprite animator, and that animator has sockets, use this to attach things to the animation
/// and draw them where the sockets is.
/// </summary>
public class TM_SocketedSpritesComponent : MonoBehaviour
{
    [System.Serializable]
    public class SpriteToSocketInfo
    {
        public SpriteRenderer renderer;
        public string socketName;
    }

    public TM_SpriteAnimator attachedAnimator;

    private bool hasAttachedAnimator;
    
    public List<SpriteToSocketInfo> socketConnections = new List<SpriteToSocketInfo>();

    private Camera mainCam = null;

    // Start is called before the first frame update
    void Start()
    {
        if (attachedAnimator != null)
            hasAttachedAnimator = true;

        mainCam = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        if( hasAttachedAnimator )
        {
            //number of units from center of screen to top
            var pixelsInUnit = (Screen.height * 0.5f) / mainCam.orthographicSize;

            var unitsPerPx = 1f / 32f;
        
            //get our current sprite's location and size
            var myPos = transform.position;
            var activeSprite = attachedAnimator.spriteRenderer.sprite;

            var spriteRect = activeSprite.bounds;
            
            //find the bottom left in world units
            var spriteBottomLeft = spriteRect.min;
            
            //now add the socket delta pixels to world units.
            for (int t = 0; t < socketConnections.Count; t++)
            {
                var connection = socketConnections[t];
                
                //get the offset position of the socket
                var socketPixelOffset = attachedAnimator.GetSocketLocation(connection.socketName);
                Vector3 worldUnitOffset = new Vector3(socketPixelOffset.x * unitsPerPx, socketPixelOffset.y * unitsPerPx, 0f);

                //ok!
                connection.renderer.transform.position = myPos + spriteBottomLeft + worldUnitOffset;

            }
        }
    }
}
