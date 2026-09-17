using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossArrow : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("How much health the player loses when hit")]
    public int damage = 35; // Increased to severely punish the player
    public GameObject hitEffectPrefab; 

    [Header("Audio")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitVolume = 1f;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Auto-rotates the arrow to face the direction it is flying
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Did we hit the Player?
        if (other.CompareTag("Player"))
        {
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health == null) health = other.GetComponentInParent<PlayerHealth>();

            if (health != null)
                health.TakeDamage(damage);

            Shatter();
        }
        
        // 2. Did the Player shoot US?
        if (other.CompareTag("PlayerBullet") || 
            other.GetComponent<PlasmaBomb>() != null || 
            other.GetComponent<CryoSnowball>() != null)
        {
            Debug.Log("Player shot the Boss Arrow out of the air!");
            Shatter();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Just in case player weapons use rigid colliders instead of triggers
        if (collision.gameObject.CompareTag("PlayerBullet") || 
            collision.gameObject.GetComponent<PlasmaBomb>() != null || 
            collision.gameObject.GetComponent<CryoSnowball>() != null)
        {
            Debug.Log("Player smashed the Boss Arrow out of the air!");
            Shatter();
        }
    }

    void OnBecameInvisible()
    {
        // Cleans up the arrow if it flies off the screen
        Destroy(gameObject);
    }

    void Shatter()
    {
        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position, hitVolume);

        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

        // Uses your existing CameraShake script if it exists in the scene
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.15f, 0.3f);

        Destroy(gameObject);
    }
}