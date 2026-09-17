using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyType
    {
        public GameObject prefab;
        [Tooltip("Relative chance this enemy type gets picked, e.g. 1 = normal, 2 = twice as likely")]
        public float spawnWeight = 1f;
    }

    [Header("References")]
    public Transform player;

    [Header("Enemy Types")]
    // CHANGED: instead of a single bowmanPrefab, spawn from a weighted list.
    // Drag in BowmanEnemy and MageEnemy prefabs here (and any future types).
    public List<EnemyType> enemyTypes = new List<EnemyType>();

    [Header("Spawn Area")]
    public float minSpawnRadius = 8f;  // enemies won't spawn any closer than this to the player
    public float maxSpawnRadius = 15f; // enemies won't spawn any further than this

    [Header("Spawn Bounds")]
    public BoxCollider2D spawnBounds; // NEW: assign the box collider marking your level's borders — any candidate spawn point outside this gets rejected and re-rolled, same as a blocked/obstacle point

    [Header("Spawn Rate")]
    public int maxEnemies = 6;         // rough cap on how many can exist at once
    public float spawnInterval = 4f;   // seconds between spawn attempts

    [Header("Spawn Validity")]
    public LayerMask obstacleLayer;    // optional: assign walls/obstacles so enemies don't spawn inside them
    public float spawnCheckRadius = 0.5f;
    public int maxSpawnAttempts = 10;  // how many random spots to try before giving up this cycle

    private List<GameObject> activeEnemies = new List<GameObject>();
    private float timer;

    void Update()
    {
        // Clean up the list (destroyed enemies, e.g. from being bashed) so count stays accurate
        activeEnemies.RemoveAll(e => e == null);

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            TrySpawnEnemy();
        }
    }

    void TrySpawnEnemy()
    {
        if (activeEnemies.Count >= maxEnemies) return;
        if (player == null) return;

        GameObject prefabToSpawn = PickEnemyPrefab();
        if (prefabToSpawn == null) return;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector2 spawnPos = GetRandomPointAroundPlayer();

            // CHANGED: instead of rejecting (and possibly burning through every attempt/skipping the
            // whole cycle) when the ring pokes outside the level, just clamp the point back inside the
            // box. It'll cluster along the border in that situation, but it always spawns something.
            if (spawnBounds != null)
            {
                Bounds b = spawnBounds.bounds;
                spawnPos.x = Mathf.Clamp(spawnPos.x, b.min.x, b.max.x);
                spawnPos.y = Mathf.Clamp(spawnPos.y, b.min.y, b.max.y);
            }

            // NEW: never spawn inside the sun's enemy no-fly zone (minRadius + padding —
            // rejecting and re-rolling instead of clamping, since clamping would just pile
            // every rejected spawn right on the boundary ring around the sun).
            if (OrbitZone.TryGetEnemyExclusionZone(out Vector2 sunCenter, out float exclusionRadius))
            {
                if (Vector2.Distance(spawnPos, sunCenter) < exclusionRadius) continue; // too close to the sun — re-roll
            }

            bool blocked = Physics2D.OverlapCircle(spawnPos, spawnCheckRadius, obstacleLayer);
            if (blocked) continue; // try another random spot

            GameObject enemy = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            activeEnemies.Add(enemy);

            // CHANGED: the prefab asset itself can never store a reference to the
            // Player (it's a scene-only object), so any enemy that needs one gets
            // it assigned here, right after spawning.
            IPlayerAware playerAware = enemy.GetComponent<IPlayerAware>();
            if (playerAware != null)
            {
                playerAware.SetPlayer(player);
            }

            return;
        }
        // If we get here, all attempts were blocked by an obstacle this cycle — just skip, we'll try again next interval
    }

    // CHANGED: weighted random pick across all configured enemy types
    GameObject PickEnemyPrefab()
    {
        if (enemyTypes == null || enemyTypes.Count == 0) return null;

        float totalWeight = 0f;
        foreach (var entry in enemyTypes)
        {
            if (entry.prefab != null) totalWeight += Mathf.Max(0f, entry.spawnWeight);
        }
        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var entry in enemyTypes)
        {
            if (entry.prefab == null) continue;
            cumulative += Mathf.Max(0f, entry.spawnWeight);
            if (roll <= cumulative) return entry.prefab;
        }

        return null; // shouldn't happen, but keeps the compiler happy
    }

    Vector2 GetRandomPointAroundPlayer()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        return (Vector2)player.position + offset;
    }

    // Visualize the spawn ring in the Scene view for easy tuning
    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = Color.yellow;
        DrawWireCircle(player.position, minSpawnRadius);
        Gizmos.color = Color.red;
        DrawWireCircle(player.position, maxSpawnRadius);

        // NEW: draw the actual border box too, so you can see spawn ring vs. level bounds together
        if (spawnBounds != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(spawnBounds.bounds.center, spawnBounds.bounds.size);
        }
    }

    void DrawWireCircle(Vector2 center, float radius)
    {
        int segments = 40;
        Vector2 prevPoint = center + new Vector2(radius, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 newPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}