using UnityEngine;

public class FrozenFrostTrap : MonoBehaviour
{
    [Header("Trap Settings")]
    public float timeUntilMelts = 5f; // How long it stays in space before disappearing

    void Start()
    {
        // Automatically clean up the trap if no one flies into it
        Destroy(gameObject, timeUntilMelts);
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        // Ignore the player so you don't freeze yourself
        if (col.CompareTag("Player")) return;

        // If an enemy flies into the frost...
        if (col.CompareTag("Enemy"))
        {
            EnemyCryoState enemy = col.GetComponent<EnemyCryoState>();
            if (enemy != null)
            {
                enemy.Freeze();
            }
            
            // The trap is used up, so destroy it
            Destroy(gameObject); 
        }
    }
}