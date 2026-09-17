using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CryoSnowball : MonoBehaviour
{
    [Header("Freeze Settings")]
    public GameObject frozenBlockPrefab; // frozen-enemy animation prefab (trigger Collider2D on it)
    public float freezeDuration = 3f;
    public string enemyTag = "Enemy";

    [Header("Lifetime")]
    public float lifetime = 6f;     // FIXED: only starts counting once thrown
    public float heldLifetime = 0f; // 0 = never expires while still tethered

    [Header("VFX")]
    public GameObject impactVfx;

    private bool isReleased = false;
    private bool hasHit = false;

    void Start()
    {
        if (heldLifetime > 0f) Destroy(gameObject, heldLifetime);
    }

    // Called by PlasmaTetherSystem.ReleaseBomb() the instant it's launched.
    public void ReleaseSnowball()
    {
        isReleased = true;
        Destroy(gameObject, lifetime); // FIXED: timer starts on release, not on spawn
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    void HandleHit(GameObject hitObject)
    {
        if (!isReleased || hasHit || hitObject == null) return;

        GameObject enemyRoot = ResolveEnemyRoot(hitObject);
        if (enemyRoot == null) return; // walls etc. just let the lifetime clean it up

        hasHit = true;
        FreezeEnemy(enemyRoot);
        SpawnImpactVfx();
        Destroy(gameObject);
    }

    // FIXED: walks up to the actual enemy root so child colliders/sprites work,
    // instead of freezing whatever collider happened to be hit.
    GameObject ResolveEnemyRoot(GameObject hitObject)
    {
        IHasDeathVfx asEnemy = hitObject.GetComponentInParent<IHasDeathVfx>();
        if (asEnemy is MonoBehaviour mb) return mb.gameObject;

        if (hitObject.CompareTag(enemyTag)) return hitObject;

        Transform t = hitObject.transform.parent;
        while (t != null)
        {
            if (t.CompareTag(enemyTag)) return t.gameObject;
            t = t.parent;
        }

        return null;
    }

    void FreezeEnemy(GameObject enemy)
    {
        if (frozenBlockPrefab == null)
        {
            Debug.LogWarning("CryoSnowball: no frozenBlockPrefab assigned - can't freeze " + enemy.name);
            return;
        }

        GameObject block = Instantiate(frozenBlockPrefab, enemy.transform.position, Quaternion.identity);
        FrozenEnemyBlock blockController = block.GetComponent<FrozenEnemyBlock>();
        if (blockController == null)
        {
            blockController = block.AddComponent<FrozenEnemyBlock>();
        }
        blockController.Init(enemy, freezeDuration);
    }

    void SpawnImpactVfx()
    {
        if (impactVfx != null)
        {
            Instantiate(impactVfx, transform.position, Quaternion.identity);
        }
    }
}