using UnityEngine;

public class FireScreenDriver : MonoBehaviour
{
    [Header("Material")]
    public Material fireMaterial;      // the material on the Full Screen Pass feature

    [Header("Temperature Range")]
    public float warningTemp = 70f;    // fire starts appearing here
    public float maxTemp = 100f;       // full intensity here

    [Header("Smoothing")]
    public float riseSpeed = 2.5f;     // how fast the effect catches up when heating
    public float fallSpeed = 1.2f;     // slower on the way down — heat lingers

    static readonly int AmountID = Shader.PropertyToID("_FireAmount");

    float current;

    void Awake() => Apply(0f);

    void OnDisable() => Apply(0f);     // don't leave the material hot in the project file

    /// Call this every frame with your current temperature.
    public void SetTemperature(float temp)
    {
        float target = Mathf.InverseLerp(warningTemp, maxTemp, temp);
        float speed = target > current ? riseSpeed : fallSpeed;

        current = Mathf.MoveTowards(current, target, speed * Time.deltaTime);
        Apply(current);
    }

    void Apply(float v)
    {
        if (fireMaterial != null) fireMaterial.SetFloat(AmountID, v);
    }
}