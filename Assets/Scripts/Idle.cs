using UnityEngine;

public class Idle : MonoBehaviour
{
    [Header("Bob")]
    [Range(0f, 0.3f)] public float bobAmplitude = 0.05f;
    [Range(0f, 3f)]   public float bobSpeed = 1.0f;

    [Header("Sway")]
    [Range(0f, 8f)]   public float swayAngle = 1.2f;
    [Range(0f, 3f)]   public float swaySpeed = 0.63f;

    Vector3 startPos;

    void Awake() => startPos = transform.localPosition;

       void Update()
    {
        float t = Time.time;
        transform.localPosition = startPos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);
        transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * swaySpeed) * swayAngle);
    }
}