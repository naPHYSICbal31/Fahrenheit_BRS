using UnityEngine;

public class StarLifeCycle : MonoBehaviour
{
    [Header("Death of a Star")]
    [Tooltip("Drag your GravityWell prefab here")]
    public GameObject blackHolePrefab; 

    void OnTriggerEnter2D(Collider2D other)
    {
        CheckForArrowHit(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        CheckForArrowHit(collision.gameObject);
    }

    void CheckForArrowHit(GameObject hitObject)
    {
        BossArrow arrow = hitObject.GetComponent<BossArrow>();
        
        if (arrow != null)
        {
            CollapseIntoBlackHole();
            
            Destroy(hitObject); 
        }
    }

    void CollapseIntoBlackHole()
    {
        // 1. Spawn exactly at this object's position (which is now the accurate Glow object)
        Vector3 spawnPosition = new Vector3(transform.position.x, transform.position.y, 0f);

        if (blackHolePrefab != null)
        {
            Instantiate(blackHolePrefab, spawnPosition, Quaternion.identity);
        }

        // 2. Destroy the entire Sun (the parent), not just the Glow graphic!
        if (transform.parent != null)
        {
            Destroy(transform.parent.gameObject);
        }
        else 
        {
            Destroy(gameObject);
        }
    }
}