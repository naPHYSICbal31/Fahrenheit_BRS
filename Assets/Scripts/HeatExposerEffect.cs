using UnityEngine;
using UnityEngine.Events;

// Bridges TemperatureBar -> FireEffect (visual) and applies damage while overheating.
// Mirrors ColdExposureEffect exactly, just on the hot end of the range: ramps from
// overheatAt up to maxTemp instead of from freezeAt down to minTemp.
// Attach this to the same camera that has FireEffect, or anywhere in the scene -
// just wire up the references in the Inspector.
public class HeatExposureEffect : MonoBehaviour
{
    [Header("Sources")]
    public TemperatureBar tempBar;
    public FireEffect fire;

    [Header("Fire Visual")]
    [Tooltip("How fast FireAmount chases its target value, in units/sec. Higher = snappier.")]
    public float fireLerpSpeed = 1.5f;

    [Header("Damage")]
    [Tooltip("Health lost per second while IsOverheating is true.")]
    public float damagePerSecond = 5f;

    [Tooltip("Fired with a whole-number damage amount once enough fractional damage has accumulated. Wire this to PlayerHealth.TakeDamage in the Inspector.")]
    public UnityEvent<int> onOverheatDamage;

    float currentFireAmount;
    float damageAccumulator; // holds fractional damage between frames until it reaches a whole point

    void Reset()
    {
        // Convenience auto-wire when the component is first added.
        if (tempBar == null) tempBar = FindObjectOfType<TemperatureBar>();
        if (fire == null) fire = GetComponent<FireEffect>();
    }

    float debugTimer;

    void Update()
    {
        if (tempBar == null)
        {
            Debug.LogWarning("[HeatExposureEffect] Temp Bar is not assigned.");
            return;
        }
        if (fire == null)
        {
            Debug.LogWarning("[HeatExposureEffect] Fire is not assigned.");
            return;
        }

        // Target fire amount: 0 right at overheatAt, ramping to 1 at maxTemp.
        // Temperature below overheatAt stays clamped to 0 (no fire yet).
        float target = Mathf.InverseLerp(tempBar.overheatAt, tempBar.maxTemp, tempBar.Temperature);
        target = Mathf.Clamp01(target);

        currentFireAmount = Mathf.MoveTowards(currentFireAmount, target, fireLerpSpeed * Time.deltaTime);
        fire.FireAmount = currentFireAmount;

        debugTimer += Time.deltaTime;
        if (debugTimer >= 0.5f)
        {
            debugTimer = 0f;
            Debug.Log($"[HeatExposureEffect] Temp={tempBar.Temperature:0.0}, overheatAt={tempBar.overheatAt}, maxTemp={tempBar.maxTemp}, IsOverheating={tempBar.IsOverheating}, target={target:0.00}, currentFireAmount={currentFireAmount:0.00}");
        }

        if (tempBar.IsOverheating && damagePerSecond > 0f)
        {
            damageAccumulator += damagePerSecond * Time.deltaTime;

            int wholeDamage = Mathf.FloorToInt(damageAccumulator);
            if (wholeDamage > 0)
            {
                damageAccumulator -= wholeDamage;
                onOverheatDamage?.Invoke(wholeDamage);
            }
        }
        else
        {
            // Don't let unused fractional damage carry over from a previous overheat spell.
            damageAccumulator = 0f;
        }
    }
}