using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Random = UnityEngine.Random;

public enum ECompassDirection
{
    north = 0,
    northeast,
    east,
    southeast,
    south,
    southwest,
    west,
    northwest,
    max,
}

public class TM_Utilities : MonoBehaviour
{
    private static TM_Utilities _instance;

    private static TM_Utilities Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TM_Utilities>();

                if (_instance != null)
                {
                    return _instance;
                }

                GameObject go = new GameObject("TM_Utilities");
                _instance = go.AddComponent<TM_Utilities>();
            }

            return _instance;
        }
    }

    /// <summary>
    /// You can connect this to your game, and cause screenshake this way. Send a float to your camera and it does whatever.
    /// </summary>
    private static Action<float> cameraScreenshakeAction;

    /// <summary>
    /// You can connect this to your game if you want the camera to do full screen fades.
    /// Color = destination color
    /// float = duration of the fade
    /// bool = if true, clear the fade on end. or don't, it's your code! 
    /// </summary>
    private static Action<Color, float, bool> cameraFadeAction;

    private static Dictionary<Vector2Int, ECompassDirection> compassDirectionDict;

    private static Dictionary<ECompassDirection, Vector2> directionToNormalizedVector;


    /// <summary>
    /// Given a direction, return a normalized Vector2. Not a Vector2Int! 
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    public static Vector2 GetNormalizedVectorFromDirection(ECompassDirection dir)
    {
        if (directionToNormalizedVector == null)
        {
            directionToNormalizedVector = new Dictionary<ECompassDirection, Vector2>();

            directionToNormalizedVector[ECompassDirection.north] = new Vector2(0, 1);
            directionToNormalizedVector[ECompassDirection.northeast] = new Vector2(1, 1).normalized;
            directionToNormalizedVector[ECompassDirection.east] = new Vector2(1, 0);
            directionToNormalizedVector[ECompassDirection.southeast] = new Vector2(1, -1).normalized;
            directionToNormalizedVector[ECompassDirection.south] = new Vector2(0, -1);
            directionToNormalizedVector[ECompassDirection.southwest] = new Vector2(-1, -1).normalized;
            directionToNormalizedVector[ECompassDirection.west] = new Vector2(-1, 0);
            directionToNormalizedVector[ECompassDirection.northwest] = new Vector2(-1, 1).normalized;
        }

        return directionToNormalizedVector[dir];
    }

    /// <summary>
    /// Take a Vector2Int and determine which compass direction this maps to. Any size V2I is ok, it'll be scaled down.
    /// Values that don't match a direction directly (such as (3,1)) will be quantized.
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public static ECompassDirection GetDirectionFromVector(Vector2Int v)
    {
        if (compassDirectionDict == null)
        {
            compassDirectionDict = new Dictionary<Vector2Int, ECompassDirection>();
            compassDirectionDict[Vector2Int.up] = ECompassDirection.north;
            compassDirectionDict[Vector2Int.up + Vector2Int.right] = ECompassDirection.northeast;
            compassDirectionDict[Vector2Int.right] = ECompassDirection.east;
            compassDirectionDict[Vector2Int.down + Vector2Int.right] = ECompassDirection.southeast;
            compassDirectionDict[Vector2Int.down] = ECompassDirection.south;
            compassDirectionDict[Vector2Int.down + Vector2Int.left] = ECompassDirection.southwest;
            compassDirectionDict[Vector2Int.left] = ECompassDirection.west;
            compassDirectionDict[Vector2Int.up + Vector2Int.left] = ECompassDirection.northwest;
        }
        

        if (compassDirectionDict.ContainsKey(v))
            return compassDirectionDict[v];
                
        v.x = Math.Sign(v.x);
        v.y = Math.Sign(v.y);
        return compassDirectionDict[v];
    }

    /// <summary>
    /// Determine the direction represented by this x/y delta.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static ECompassDirection GetDirectionFromValues(int x, int y)
    {
        return GetDirectionFromVector(new Vector2Int(x, y));
    }

    /// <summary>
    /// Doesn't work yet, rip
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    public static ECompassDirection GetDirectionFromVector(Vector2 dir)
    {
        var dirNormalized = dir.normalized;
        
        var angleInRadians = Math.Atan2(dir.y, dir.x);

        var angleInDegrees = angleInRadians * Mathf.Rad2Deg;
        angleInDegrees += 360f;
        angleInDegrees %= 360f;
        
        DebugConsole.Log("Angles: " + angleInRadians + " rads, " + angleInDegrees + " degrees");
        int quantizedValue = (int)(8 * (angleInDegrees / 360.0f) + 8) % 8;

        return (ECompassDirection) quantizedValue;
    }
    

    public static Vector2 Rotate2DVector(Vector2 rotateMe, float fAngleInDegrees)
    {
        Vector2 retVec = new Vector2();
        float rads = (fAngleInDegrees / 360.0f) * 6.28f;

        retVec.x = rotateMe.x * Mathf.Cos(rads) - rotateMe.y * Mathf.Sin(rads);
        retVec.y = rotateMe.x * Mathf.Sin(rads) + rotateMe.y * Mathf.Cos(rads);
        return retVec;
    }

    public static void PointSpriteInDirection(Vector2 vDirection, GameObject go)
    {
        Vector3 rotation = go.transform.rotation.eulerAngles;
        rotation.z = Vector2.Angle(Vector2.up, vDirection);
        if (vDirection.x > 0)
            rotation.z *= -1.0f;

        go.transform.rotation = Quaternion.Euler(rotation);
    }

    public static IEnumerator ShakeGraphicObject(float fLifetime, float fStartShakeStremf, RectTransform rt)
    {
        float fTime = 0f;
        Vector2 vOrigin = rt.anchoredPosition;

        while (fTime < fLifetime)
        {
            float fStremf = Mathf.Lerp(fStartShakeStremf, 0.1f, fTime / fLifetime);
            Vector2 vDelta = UnityEngine.Random.insideUnitCircle.normalized * fStremf;
            rt.anchoredPosition = vOrigin + vDelta;
            fTime += Time.deltaTime;
            yield return null;
        }
    }
    
    /// <summary>
    /// Align a quad between two points and make it look like a beam, or an energy arc, or something. Pew pew.
    /// </summary>
    /// <param name="beamStart">World location of one side of the beam</param>
    /// <param name="beamEnd">World location of the other</param>
    /// <param name="beamWidth"></param>
    /// <param name="beamMR">The actual mesh we're stretching and positioning.</param>
    /// <param name="beamMaterial">The mesh's material, which is hopefully an instance</param>
    /// <param name="beamScrollMinMax">Min/Max values on the x scroll speed of the beam texture.</param>
    public static void AlignBeamBetweenTwoPoints(Vector3 beamStart, Vector3 beamEnd, float beamWidth,
        MeshRenderer beamMR, Material beamMaterial, Vector2 beamScrollMinMax)
    {
        //Get the vector between us and them.
        var beamDelta = beamEnd - beamStart;
        var beamDist = beamDelta.magnitude;
		
        beamMR.enabled = true;
        var beamTForm = beamMR.transform;

        //place the quad at the mid point between us and them, and rotate it to face correctly along the vector
        var projectileCenter = beamStart + beamDelta * 0.5f;
        beamTForm.position = projectileCenter;
        beamTForm.right = beamDelta.normalized;

        var rotEuler = beamTForm.rotation.eulerAngles;
        rotEuler.y = 0f;
        beamTForm.rotation = Quaternion.Euler(rotEuler);
		
        //scale the quad to match the distance between us and them
        var scale = beamTForm.localScale;
        scale.x = beamDist;
        scale.y = beamWidth;
        beamTForm.localScale = scale;

        //Scale the texture on the quad and rotate through it
        //todo: have the divisor for beam distance be an editor variable.
        beamMaterial.mainTextureScale = new Vector2(beamDist / 4, 1);
        var offset = beamMaterial.mainTextureOffset;
        offset.x -= Time.smoothDeltaTime * UnityEngine.Random.Range(beamScrollMinMax.x, beamScrollMinMax.y);
        beamMaterial.mainTextureOffset = offset;

    }

    /// <summary>
    /// Align a quad between two points and make it look like a beam, or an energy arc, or something. Pew pew.
    /// </summary>
    /// <param name="beamStart">World location of one side of the beam</param>
    /// <param name="beamEnd">World location of the other</param>
    /// <param name="beamWidth"></param>
    /// <param name="beamMR">The actual mesh we're stretching and positioning.</param>
    /// <param name="beamMaterial">The mesh's material, which is hopefully an instance</param>
    /// <param name="beamScrollMinMax">Min/Max values on the x scroll speed of the beam texture.</param>
    /// <param name="beamProjectile">A projectile that will match the shape and orientation of the beam</param>
    public static void AlignBeamBetweenTwoPointsWithProjectile(Vector3 beamStart, Vector3 beamEnd, float beamWidth,
        MeshRenderer beamMR, Material beamMaterial, Vector2 beamScrollMinMax, TM_Projectile beamProjectile)
    {
        AlignBeamBetweenTwoPoints(beamStart, beamEnd, beamWidth, beamMR, beamMaterial, beamScrollMinMax);

        //now position the projectile
        var pTform = beamProjectile.transform;
        var beamTForm = beamMR.transform;

        var beamDelta = beamEnd - beamStart;
        
        beamProjectile.SetDirectionAndUpdateRotation(beamDelta.normalized);
        pTform.position = beamTForm.position;
        var scale = beamTForm.localScale;
        scale.x = beamDelta.magnitude;
        scale.y = beamWidth;
        pTform.localScale = new Vector3(scale.y, scale.x, 1);

    }

    public static object Debug_TestVectorDirections(string[] args)
    {
        //just run through a bunch of numbers and see what works
        for (float x = -30.0f; x <= 30.0f; x+= 2.0f)
        {
            for (float y = -30.0f; y <= 30.0f; y += 2.0f)
            {
                var resultDirection = GetDirectionFromVector(new Vector2(x, y));
                DebugConsole.Log("Vector " + x + ", " + y + " is direction " + resultDirection);
            }
        }

        return "done";
    }

    /// <summary>
    /// Connect this to your game's camera code to cause shaking
    /// </summary>
    /// <param name="shakeDuration"></param>
    public static void CameraScreenshake(float shakeDuration)
    {    
        cameraScreenshakeAction?.Invoke(shakeDuration);
    }

    /// <summary>
    /// This action should let your camera do whatever it needs to make shake happen.
    /// </summary>
    /// <param name="screenshakeAction"></param>
    public static void SetCameraScreenshakeAction(Action<float> screenshakeAction)
    {
        cameraScreenshakeAction = screenshakeAction;
    }

    /// <summary>
    /// Launch one gib in a given direction
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="direction"></param>
    /// <param name="force"></param>
    /// <param name="spriteName"></param>
    public static void LaunchGib(Vector2 origin, Vector2 direction, float force, string spriteName)
    {
        LaunchGib(origin, direction, force, AssetLoader.GetSprite(spriteName));
    }

    /// <summary>
    /// Launch one gib in a given direction
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="direction"></param>
    /// <param name="force"></param>
    /// <param name="sprite"></param>
    public static void LaunchGib(Vector2 origin, Vector2 direction, float force, Sprite sprite)
    {
        var gibGO = PoolManager.Instantiate("prefab_gib");
        gibGO.transform.position = origin;

        if (gibGO.TryGetComponent(out TM_Gibs gib))
        {
            gib.Launch(direction, force, sprite);
        }
    }

    /// <summary>
    /// Launch multiple gibs at once in a circular burst
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="forceMinMax"></param>
    /// <param name="numGibs"></param>
    /// <param name="spriteName"></param>
    public static void LaunchGibBurst(Vector2 origin, Vector2 forceMinMax, int numGibs, string spriteName)
    {
        var sprite = AssetLoader.GetSprite(spriteName);
        LaunchGibBurst(origin, forceMinMax, numGibs, sprite);

    }

    /// <summary>
    /// Launch multiple gibs at once in a circular burst
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="forceMinMax"></param>
    /// <param name="numGibs"></param>
    /// <param name="sprite"></param>
    public static void LaunchGibBurst(Vector2 origin, Vector2 forceMinMax, int numGibs, Sprite sprite)
    {
        for (int t = 0; t < numGibs; t++)
        {
            var randomDirection = Random.insideUnitCircle.normalized;
            LaunchGib(origin, randomDirection, Random.Range(forceMinMax.x, forceMinMax.y), sprite);
        }
    }
    
    /// <summary>
    /// Launch gibs in a cone from a given direction. 
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="coneDirection"></param>
    /// <param name="coneAngle"></param>
    /// <param name="forceMinMax"></param>
    /// <param name="numGibs"></param>
    /// <param name="spriteName"></param>
    public static void LaunchGibInCone(Vector2 origin, Vector2 coneDirection, float coneAngle, Vector2 forceMinMax,
     int numGibs, string spriteName)
    {
        var sprite = AssetLoader.GetSprite(spriteName);
        var halfAngle = coneAngle * 0.5f;
        for (int t = 0; t < numGibs; t++)
        {
            var gibDirection = coneDirection;
            gibDirection = Rotate2DVector(gibDirection, Random.Range(-halfAngle, halfAngle));
            LaunchGib(origin, gibDirection, Random.Range(forceMinMax.x, forceMinMax.y), sprite);
        }
    }

    
    /// <summary>
    /// Connect this to your game's camera code to allow fading.
    /// </summary>
    public static void FadeCameraTo(Color fadeColor, float fadeDuration, bool shouldClearFadeOnEnd)
    {
        cameraFadeAction?.Invoke(fadeColor, fadeDuration, shouldClearFadeOnEnd);
    }
    
    /// <summary>
    /// This action should let your camera fade colors over time.
    /// </summary>
    /// <param name="fadeAction"></param>
    public static void SetFadeCameraAction(Action<Color, float, bool> fadeAction)
    {
        cameraFadeAction = fadeAction;
    }
}

