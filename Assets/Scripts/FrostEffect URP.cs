using UnityEngine;

// URP version of the original FrostEffect. Doesn't render anything itself -
// a Full Screen Pass Renderer Feature (on your 2D Renderer Data asset) owns the
// actual screen blit and reads from the Material you assign below. This script's
// only job is to push the current parameter values into that same Material each frame.
[AddComponentMenu("Image Effects/Frost (URP)")]
public class FrostEffect : MonoBehaviour
{
    [Header("Shared material (same asset assigned to the Renderer Feature)")]
    public Material material;

    [Header("Frost")]
    public float FrostAmount = 0.5f; // 0-1 (0=minimum Frost, 1=maximum frost)
    public float EdgeSharpness = 1;  // >=1
    public float minFrost = 0;       // 0-1
    public float maxFrost = 1;       // 0-1
    public float seethroughness = 0.2f; // 0=normal blend, 1="overlay" blend
    public float distortion = 0.1f;

    [Header("Textures (also set these on the Material asset itself)")]
    public Texture2D Frost;
    public Texture2D FrostNormals;

    static readonly int BlendTexID = Shader.PropertyToID("_BlendTex");
    static readonly int BumpMapID = Shader.PropertyToID("_BumpMap");
    static readonly int BlendAmountID = Shader.PropertyToID("_BlendAmount");
    static readonly int EdgeSharpnessID = Shader.PropertyToID("_EdgeSharpness");
    static readonly int SeeThroughnessID = Shader.PropertyToID("_SeeThroughness");
    static readonly int DistortionID = Shader.PropertyToID("_Distortion");

    float debugTimer;

    void Update()
    {
        if (material == null)
        {
            Debug.LogWarning("[FrostEffect] Material is not assigned - drag the same material used by the Renderer Feature into this field.");
            return;
        }

        if (Frost != null) material.SetTexture(BlendTexID, Frost);
        if (FrostNormals != null) material.SetTexture(BumpMapID, FrostNormals);

        EdgeSharpness = Mathf.Max(1, EdgeSharpness);

        float amount = Mathf.Clamp01(Mathf.Clamp01(FrostAmount) * (maxFrost - minFrost) + minFrost);
        material.SetFloat(BlendAmountID, amount);
        material.SetFloat(EdgeSharpnessID, EdgeSharpness);
        material.SetFloat(SeeThroughnessID, seethroughness);
        material.SetFloat(DistortionID, distortion);

        // Throttled debug so we can confirm values are actually reaching the material.
        debugTimer += Time.deltaTime;
        if (debugTimer >= 0.5f)
        {
            debugTimer = 0f;
            Debug.Log($"[FrostEffect] FrostAmount={FrostAmount:0.00} -> BlendAmount sent to material={amount:0.00}, material={material.name}, hasBlendTex={material.GetTexture(BlendTexID) != null}, hasBumpMap={material.GetTexture(BumpMapID) != null}");
        }
    }
}