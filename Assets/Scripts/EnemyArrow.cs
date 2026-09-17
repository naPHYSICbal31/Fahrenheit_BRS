using UnityEngine;
public class EnemyArrow : MonoBehaviour
{
    public float lifetime;
    public int damage = 1;
    [Header("Bicycle Kick Reflection")]
    public bool canReflect = true; // NEW: set this false on BowEnemy's arrow prefab so its arrows pass through the mirror untouched instead of bouncing
    private Vector2 moveDirection;
    private float moveSpeed;

    [Header("VFX")]
    public GameObject hitVFX; // spawned wherever the arrow is destroyed on impact (not on lifetime timeout)
    public float hitVFXLifetime = 2f; // auto-destroyed after this long, in case the VFX prefab doesn't clean itself up

    [Header("Temperature")]
    public float temperatureDelta = -5f; // NEW: how much this arrow shifts the player's temperature when it actually lands a hit (negative = cools)

    public Vector2 MoveDirection => moveDirection; // NEW: exposed so BicycleKick can read the travel direction

    private TemperatureBar temperatureBar; // NEW: cached so we don't FindObjectOfType every hit

    public void SetDirection(Vector2 direction, float speed)
    {
        moveDirection = direction;
        moveSpeed = speed;
    }

    void SpawnHitVFX()
    {
        if (hitVFX == null) return;
        GameObject vfx = Instantiate(hitVFX, transform.position, Quaternion.identity);
        if (hitVFXLifetime > 0f) Destroy(vfx, hitVFXLifetime);
    }

    // NEW: shared by both the trigger and collision hit paths below, so the temperature
    // shift always happens alongside the health damage, never on its own.
    void ApplyTemperatureHit()
    {
        if (temperatureBar != null)
        {
            temperatureBar.AdjustTemperature(temperatureDelta);
        }
    }

    void Update()
    {
        transform.position += (Vector3)(moveDirection * moveSpeed * Time.deltaTime);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        // NEW: the sun's own body needs to actually stop arrows, not just the (trigger) orbit
        // boundary collider used for player capture. Checked by tag explicitly rather than relying
        // on !other.isTrigger below, since that collider on the Sun object is a trigger (it has to
        // be, for OrbitZone's OnTriggerEnter2D to fire) and would otherwise let arrows pass straight
        // through it.
        if (other.CompareTag("Sun"))
        {
            SpawnHitVFX();
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("MirrorEdge")) // CHANGED: bicycle kick mirror
        {
            if (CompareTag("Kickable")) return; // bashable bullets pass through untouched
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                Vector2 normal = player.BackDirection;
                Vector2 reflected = Vector2.Reflect(moveDirection, normal);
                SetDirection(reflected, moveSpeed);
            }
            return;
        }
        if (other.CompareTag("Player"))
        {
            other.GetComponentInParent<PlayerHealth>()?.TakeDamage(damage);
            ApplyTemperatureHit(); // NEW
            SpawnHitVFX();
            Destroy(gameObject);
        }
        else if (!other.isTrigger)
        {
            SpawnHitVFX();
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.gameObject.GetComponentInParent<PlayerHealth>()?.TakeDamage(damage);
            ApplyTemperatureHit(); // NEW
            SpawnHitVFX();
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Destroy(gameObject, lifetime); // single despawn timer

        temperatureBar = FindObjectOfType<TemperatureBar>(); // NEW

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }
    }
}