using UnityEngine;

public class Breathe : MonoBehaviour
{
    public float amount = 0.035f;
    public float speed = 1.1f;          // match Idle's bobSpeed
    public float phaseOffset = 1.57f;   // quarter cycle behind the bob

    Vector3 startScale;

    void Awake() => startScale = transform.localScale;

    void Update()
    {
        float b = Mathf.Sin(Time.time * speed + phaseOffset) * amount;
        transform.localScale = new Vector3(
            startScale.x * (1f - b),
            startScale.y * (1f + b),
            startScale.z);
    }
}