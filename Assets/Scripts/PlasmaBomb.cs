using UnityEngine;
using System.Collections;

public class PlasmaBomb : MonoBehaviour
{
    [Header("Explosion Logic")]
    public float fuseTime = 2f; // Vanishes in the void after 2 seconds
    public float explosionRadius = 3f; 
    
    [Header("Teammate VFX Slot")]
    public GameObject explosionVisualPrefab; 

    private bool isArmed = false;
    private bool hasExploded = false;

    public void IgniteFuse()
    {
        if (!isArmed)
        {
            isArmed = true;
            StartCoroutine(VoidTimer());
        }
    }

    IEnumerator VoidTimer()
    {
        yield return new WaitForSeconds(fuseTime);
        
        // RULE 3: Vanish/Explode in the void after 2 seconds if it hits nothing
        if (!hasExploded) Explode();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isArmed || collision.gameObject.CompareTag("Player")) return;

        if (!hasExploded) 
        {
            // RULE 1: Ensure the specific enemy it collided with is instantly killed
            if (collision.gameObject.CompareTag("Enemy"))
            {
                Destroy(collision.gameObject);
            }
            
            // RULE 2: Normal collisions (like walls) instantly destroy the bomb and trigger the blast
            Explode();
        }
    }

    void Explode()
    {
        hasExploded = true;

        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.4f, 0.8f); 
        }

        if (explosionVisualPrefab != null)
        {
            Instantiate(explosionVisualPrefab, transform.position, Quaternion.identity);
        }

        // Catch any *other* enemies standing too close to the blast
        Collider2D[] objectsInBlast = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (Collider2D hit in objectsInBlast)
        {
            // 1. Did the blast hit the Boss?
            OuroborosEngine boss = hit.GetComponentInParent<OuroborosEngine>();
            if (boss != null)
            {
                boss.TakePlasmaHit();
            }
            // 2. Did the blast hit a normal enemy?
            else if (hit.CompareTag("Enemy"))
            {
                Destroy(hit.gameObject); 
            }
        } // <--- THIS WAS THE MISSING BRACE!

        // The bomb finally vanishes/destroys itself
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
    