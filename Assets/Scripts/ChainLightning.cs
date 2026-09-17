using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChainLightning : MonoBehaviour
{
    [Header("Targeting")]
    public LayerMask enemyLayer;
    public float firstHitRadius = 3f;   // how far the initial bullet can find enemy #1
    public float jumpRadius = 4f;       // how far each jump can reach
    public int maxChains = 5;           // total enemies struck

    [Header("Timing")]
    public float jumpDelay = 0.08f;     // seconds between each hop

    [Header("Visuals")]
    public LightningBolt boltPrefab;    // the LineRenderer prefab (Step 3)
    public GameObject hitEffectPrefab;  // your particle prefab, optional per-hit spark (plays in addition to each enemy's own death VFX)

    // Call this from your bullet on impact, passing where it hit.
    public void Begin(Vector2 origin)
    {
        Debug.Log("ChainLightning Begin() called at " + origin);
        StartCoroutine(ChainRoutine(origin));
    }

    private IEnumerator ChainRoutine(Vector2 origin)
    {
        List<Transform> struckEnemies = new List<Transform>();
        Transform current = FindNearest(origin, firstHitRadius, struckEnemies);
        Debug.Log("First enemy found: " + (current == null ? "NONE" : current.name));

        Vector2 fromPos = origin;
        int chainsLeft = maxChains;

        while (current != null && chainsLeft > 0)
        {
            Debug.Log("Drawing bolt to " + current.name + ", boltPrefab is " + (boltPrefab == null ? "NULL" : "set"));

            SpawnBolt(fromPos, current.position);
            KillEnemy(current);
            struckEnemies.Add(current);

            chainsLeft--;
            fromPos = current.position;

            yield return new WaitForSeconds(jumpDelay);

            current = FindNearest(fromPos, jumpRadius, struckEnemies);
        }

        Destroy(gameObject, 1f);
    }

    private Transform FindNearest(Vector2 point, float radius, List<Transform> exclude)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(point, radius, enemyLayer);
        Transform best = null;
        float bestDist = Mathf.Infinity;

        foreach (Collider2D c in hits)
        {
            Transform t = c.transform;
            if (exclude.Contains(t)) continue;           // already hit this chain
            if (t.GetComponent<EnemyStruck>()) continue;  // marked by another cast

            float d = ((Vector2)t.position - point).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        }

        return best;
    }

    // Kills the struck enemy: plays the chain's own hit spark, then hands off to the
    // enemy's own death sequence (VFX + death sound + shard drop + destroy) via IKillable.
    // Falls back to a plain Destroy if the enemy type doesn't implement IKillable.
    private void KillEnemy(Transform enemy)
    {
        // Mark it so it won't be picked again by another chain this frame.
        enemy.gameObject.AddComponent<EnemyStruck>();

        // Per-hit spark (your particle prefab) — this is separate from each enemy's death VFX,
        // so you still see the electric zap even before the enemy's own explosion/death effect plays.
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, enemy.position, Quaternion.identity);
        }

        IKillable killable = enemy.GetComponent<IKillable>();
        if (killable != null)
        {
            killable.Die(); // plays this enemy's own death VFX/sound, drops shards, destroys it
        }
        else
        {
            Debug.LogWarning($"{enemy.name} was chained but has no IKillable — destroying directly with no death VFX.");
            Destroy(enemy.gameObject);
        }
    }

    private void SpawnBolt(Vector2 from, Vector2 to)
    {
        if (boltPrefab == null) return;
        LightningBolt bolt = Instantiate(boltPrefab);
        bolt.Draw(from, to);
    }

    // Visualize the radii in the editor.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, firstHitRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, jumpRadius);
    }
}