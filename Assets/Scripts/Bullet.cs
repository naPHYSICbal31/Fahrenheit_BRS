using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 25f;
    public GameObject hitEffectPrefab;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Shoot forward immediately when spawned
        rb.linearVelocity = transform.up * speed;

        // Destroy bullet after 2 seconds so they don't lag the game if they miss
        Destroy(gameObject, 2f);
    }

    void OnTriggerEnter2D(Collider2D hitInfo)
    {
        Debug.Log(
            $"Bullet collided with: {hitInfo.gameObject.name} | " +
            $"Tag: {hitInfo.gameObject.tag} | " +
            $"Layer: {LayerMask.LayerToName(hitInfo.gameObject.layer)} " +
            $"({hitInfo.gameObject.layer})"
        );

        // Don't hit the player who shot it
        if (hitInfo.CompareTag("Player")) return;

        // Spawn the sparks
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, transform.rotation);
        }

        // Here is where you would say:
        // hitInfo.GetComponent<Enemy>().TakeDamage(1);

        // Destroy the bullet
        Destroy(gameObject);
    }
}
