using UnityEngine;

// Tints every SpriteRenderer, Renderer (mesh/material-based), and ParticleSystem found in
// this GameObject and all its children, so a whole prefab (sprite + any child VFX/particles)
// takes on one shade uniformly - useful for damage flashes, freeze/poison status tints,
// team-color recoloring, etc.
public class GameObjectTint : MonoBehaviour
{
    [Header("Tint")]
    public Color tintColor = Color.white; // white = no tint (multiplies against each renderer's own base color)

    private SpriteRenderer[] spriteRenderers;
    private Renderer[] meshRenderers;
    private ParticleSystem[] particleSystems;
    private MaterialPropertyBlock propBlock;

    // Cached so setting the tint doesn't allocate a new array of components every call.
    void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        meshRenderers = GetComponentsInChildren<Renderer>(true);
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        propBlock = new MaterialPropertyBlock();
    }

    // Call this whenever you want to change the tint (e.g. from a status-effect script).
    public void SetTint(Color color)
    {
        tintColor = color;

        // 1. SpriteRenderers - .color is a straight multiply against the sprite's own pixels,
        // no MaterialPropertyBlock needed, this is the simplest case.
        foreach (var sr in spriteRenderers)
        {
            if (sr != null) sr.color = color;
        }

        // 2. Mesh Renderers - use a MaterialPropertyBlock instead of renderer.material.color
        // so we DON'T create a runtime material instance per-object (that both costs memory
        // and breaks batching). Tries both _Color (Built-in shaders) and _BaseColor (URP Lit/
        // Unlit) since different shaders name their base color property differently.
        foreach (var rend in meshRenderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(propBlock);
            if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_Color"))
            {
                propBlock.SetColor("_Color", color);
            }
            if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_BaseColor"))
            {
                propBlock.SetColor("_BaseColor", color);
            }
            rend.SetPropertyBlock(propBlock);
        }

        // 3. Particle Systems - tint via the main module's startColor. This multiplies against
        // whatever color the particles were already emitting with.
        foreach (var ps in particleSystems)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.startColor = color;
        }
    }

    void OnValidate()
    {
        // Lets you drag the Tint Color slider in the Inspector at edit time and see it apply
        // immediately, without needing Play mode.
        if (spriteRenderers == null) Awake();
        SetTint(tintColor);
    }
}