using UnityEngine;

public class WavyVisual : MonoBehaviour
{
    [Header("Wave Settings")]
    public float waveSpeed = 20f;  // How fast it wiggles
    public float waveSize = 0.15f; // How far it moves side-to-side

    private Vector3 startLocalPos;

    void Start()
    {
        // Remember where the object started relative to the parent
        startLocalPos = transform.localPosition;
    }

    void Update()
    {
        // Calculate the Sine wave offset
        float offset = Mathf.Sin(Time.time * waveSpeed) * waveSize;
        
        // Apply the wiggle to the Y axis (up and down relative to the snowball)
        transform.localPosition = startLocalPos + new Vector3(0f, offset, 0f);
    }
}