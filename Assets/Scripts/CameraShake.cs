using UnityEngine;

// This single line is the magic trick. 
// It forces this script to run AFTER your camera follow script has finished moving the camera!
[DefaultExecutionOrder(9999)] 
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;
    
    private float shakeTimeRemaining;
    private float shakePower;
    
    // We use this to remember how far we pushed the camera last frame
    private Vector3 lastShakeOffset = Vector3.zero;

    void Awake()
    {
        Instance = this;
    }

    public void Shake(float duration, float magnitude)
    {
        shakeTimeRemaining = duration;
        shakePower = magnitude;
    }

    void LateUpdate()
    {
        // 1. Un-shake the camera from the previous frame so it doesn't drift away
        transform.position -= lastShakeOffset;

        if (shakeTimeRemaining > 0)
        {
            // 2. Calculate a brand new random shake
            float x = Random.Range(-1f, 1f) * shakePower;
            float y = Random.Range(-1f, 1f) * shakePower;
            
            lastShakeOffset = new Vector3(x, y, 0);

            // 3. Apply the shake ON TOP of wherever the follow script just moved the camera
            transform.position += lastShakeOffset;

            shakeTimeRemaining -= Time.deltaTime;
        }
        else
        {
            lastShakeOffset = Vector3.zero;
        }
    }
}