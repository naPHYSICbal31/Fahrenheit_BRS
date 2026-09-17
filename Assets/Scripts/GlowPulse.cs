using UnityEngine;

public class GlowPulse : MonoBehaviour
{
    public float minScale = 1.55f;
    public float maxScale = 1.68f;
    public float speed = 0.4f;

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        float s = Mathf.Lerp(minScale, maxScale, t);
        transform.localScale = new Vector3(s, s, 1f);
    }
}