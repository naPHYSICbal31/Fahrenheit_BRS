using UnityEngine;

// URP screen-space fire effect for Custom/FireScreen.shader. Doesn't render anything
// itself - a Full Screen Pass Renderer Feature (on your 2D Renderer Data asset) owns the
// actual screen blit and reads from the Material you assign below. This script's only
// job is to push the current FireAmount value into that same Material each frame.
//
// CHANGED: this shader only exposes one runtime-relevant property, _FireAmount - all the
// texture/bumpmap/edge-sharpness/see-throughness/distortion properties from the earlier
// version were copied from a different shader (Frost's) and don't exist on this one, which
// is why they were doing nothing while the material's own _FireAmount slider (last set by
// hand) silently controlled everything. Removed the properties that don't exist here.
[AddComponentMenu("Image Effects/Fire (URP)")]
public class FireEffect : MonoBehaviour
{
    [Header("Shared material (same asset assigned to the Renderer Feature)")]
    public Material material;

    [Header("Fire")]
    public float FireAmount = 0f; // 0-1, maps directly onto the shader's _FireAmount property
    public float minFire = 0f;    // 0-1, remaps FireAmount's floor
    public float maxFire = 1f;    // 0-1, remaps FireAmount's ceiling

    static readonly int FireAmountID = Shader.PropertyToID("_FireAmount");

    float debugTimer;

    void OnDisable()
    {
        // Force the effect fully off whenever this component is disabled, so a leftover
        // value never lingers on screen between states.
        if (material != null)
        {
            material.SetFloat(FireAmountID, 0f);
        }
    }

    void Update()
    {
        if (material == null)
        {
            Debug.LogWarning("[FireEffect] Material is not assigned - drag the Fire Screen Material into this field.");
            return;
        }

        float amount = Mathf.Clamp01(Mathf.Clamp01(FireAmount) * (maxFire - minFire) + minFire);
        material.SetFloat(FireAmountID, amount);

        // Throttled debug so we can confirm values are actually reaching the material.
        debugTimer += Time.deltaTime;
        if (debugTimer >= 0.5f)
        {
            debugTimer = 0f;
            Debug.Log($"[FireEffect] FireAmount={FireAmount:0.00} -> _FireAmount sent to material={amount:0.00}, material={material.name}");
        }
    }
}