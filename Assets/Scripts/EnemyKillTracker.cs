using UnityEngine;

// Tracks kills across every enemy type and drops one HealthShard every
// minKillsForDrop-maxKillsForDrop kills (randomized each time), instead of
// each enemy independently rolling its own chance.
public class EnemyKillTracker : MonoBehaviour
{
    public static EnemyKillTracker Instance;

    public GameObject healthShardPrefab;
    public int minKillsForDrop = 5;
    public int maxKillsForDrop = 7; // inclusive

    private int killCount = 0;
    private int nextDropAt;

    void Awake()
    {
        Instance = this;
        RollNextDropThreshold();
    }

    // Returns true if this kill dropped a HealthShard - callers use this to skip
    // spawning their own overdrive shard on the same kill, keeping the two drops mutually exclusive.
    public bool RegisterKill(Vector3 position)
    {
        killCount++;
        if (killCount >= nextDropAt)
        {
            killCount = 0;
            RollNextDropThreshold();

            if (healthShardPrefab != null)
            {
           
                Instantiate(healthShardPrefab, position, Quaternion.identity);
            }
            return true;
        }
        return false;
    }

    void RollNextDropThreshold()
    {
        nextDropAt = Random.Range(minKillsForDrop, maxKillsForDrop + 1); // Range max is exclusive for ints, so +1
    }
}
