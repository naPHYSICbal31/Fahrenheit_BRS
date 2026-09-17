using UnityEngine;
using UnityEngine.SceneManagement;

public class BossCore : MonoBehaviour
{
    [Header("Phase 2 Settings")]
    public GameObject portalPrefab; // Drag your Portal prefab here
    public Transform portalSpawnPoint; // Where the portal spawns relative to the core
    
    [Header("The Final Void Portal")]
    public string finalVoidSceneName = "FinalVoidScene"; // Name of your final level/scene
    
    private bool portalOpened = false;

    // Detect when the CryoSnowball hits the core
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (portalOpened) return;

        // Check if the incoming object is the CryoSnowball
        if (collision.gameObject.GetComponent<CryoSnowball>() != null)
        {
            Debug.Log("CryoSnowball hit the Boss Core! Opening Final Void portal...");
            OpenPortal();
            
            // Destroy the snowball on impact
            Destroy(collision.gameObject);
        }
    }

    // Alternatively, if your snowball uses triggers, use this instead:
    void OnTriggerEnter2D(Collider2D other)
    {
        if (portalOpened) return;

        if (other.GetComponent<CryoSnowball>() != null)
        {
            Debug.Log("CryoSnowball hit the Boss Core! Opening Final Void portal...");
            OpenPortal();
            
            Destroy(other.gameObject);
        }
    }

    void OpenPortal()
    {
        portalOpened = true;

        // 1. Spawn the portal
        if (portalPrefab != null)
        {
            Vector3 spawnPos = portalSpawnPoint != null ? portalSpawnPoint.position : transform.position;
            Instantiate(portalPrefab, spawnPos, Quaternion.identity);
        }

        // 2. You can also make the core disappear or trigger a final effect here
        // Destroy(gameObject); // Uncomment if the core should vanish when the portal opens
    }
}