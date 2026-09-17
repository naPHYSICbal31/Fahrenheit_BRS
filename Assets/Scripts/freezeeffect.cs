using UnityEngine;
using UnityEngine.Events;

// Bridges TemperatureBar -> FrostEffect (visual) and applies damage while freezing.
// Attach this to the same camera that has FrostEffect, or anywhere in the scene -
// just wire up the references in the Inspector.
public class ColdExposureEffect : MonoBehaviour
{
    [Header("Sources")]
    public TemperatureBar tempBar;
    public FrostEffect frost;

    [Header("Frost Visual")]
    [Tooltip("How fast FrostAmount chases its target value, in units/sec. Higher = snappier.")]
    public float frostLerpSpeed = 1.5f;

    [Header("Damage")]
    [Tooltip("Health lost per second while IsFreezing is true.")]
    public float damagePerSecond = 5f;

    [Tooltip("Fired with a whole-number damage amount once enough fractional damage has accumulated. Wire this to PlayerHealth.TakeDamage in the Inspector.")]
    public UnityEvent<int> onFreezeDamage;

    float currentFrostAmount;
    float damageAccumulator; // holds fractional damage between frames until it reaches a whole point

    void Reset()
    {
        // Convenience auto-wire when the component is first added.
        if (tempBar == null) tempBar = FindObjectOfType<TemperatureBar>();
        if (frost == null) frost = GetComponent<FrostEffect>();
    }

    float debugTimer;

    void Update()
    {
        if (tempBar == null)
        {
            Debug.LogWarning("[ColdExposureEffect] Temp Bar is not assigned.");
            return;
        }
        if (frost == null)
        {
            Debug.LogWarning("[ColdExposureEffect] Frost is not assigned.");
            return;
        }

        // Target frost amount: 0 right at freezeAt, ramping to 1 at minTemp.
        // Temperature above freezeAt stays clamped to 0 (no frost yet).
        float target = Mathf.InverseLerp(tempBar.freezeAt, tempBar.minTemp, tempBar.Temperature);
        target = Mathf.Clamp01(target);

        currentFrostAmount = Mathf.MoveTowards(currentFrostAmount, target, frostLerpSpeed * Time.deltaTime);
        frost.FrostAmount = currentFrostAmount;

        debugTimer += Time.deltaTime;
        if (debugTimer >= 0.5f)
        {
            debugTimer = 0f;
            Debug.Log($"[ColdExposureEffect] Temp={tempBar.Temperature:0.0}, freezeAt={tempBar.freezeAt}, minTemp={tempBar.minTemp}, IsFreezing={tempBar.IsFreezing}, target={target:0.00}, currentFrostAmount={currentFrostAmount:0.00}");
        }

        if (tempBar.IsFreezing && damagePerSecond > 0f)
        {
            damageAccumulator += damagePerSecond * Time.deltaTime;

            int wholeDamage = Mathf.FloorToInt(damageAccumulator);
            if (wholeDamage > 0)
            {
                damageAccumulator -= wholeDamage;
                onFreezeDamage?.Invoke(wholeDamage);
            }
        }
        else
        {
            // Don't let unused fractional damage carry over from a previous freeze.
            damageAccumulator = 0f;
        }
    }
}