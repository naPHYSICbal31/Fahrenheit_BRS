using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class EnemyCryoState : MonoBehaviour
{
    public float freezeDuration = 4f;
    public Color frozenColor = new Color(0.5f, 0.9f, 1f); 

    public bool isFrozen { get; private set; } 

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private float freezeTimer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }

    void Update()
    {
        if (isFrozen)
        {
            freezeTimer -= Time.deltaTime;
            if (freezeTimer <= 0f) ThawOut();
        }
    }

    public void Freeze()
    {
        isFrozen = true;
        freezeTimer = freezeDuration;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        spriteRenderer.color = frozenColor;
        
        // Disable enemy AI here if you have a script for it!
        // GetComponent<EnemyMovement>().enabled = false;
    }

    void ThawOut()
    {
        isFrozen = false;
        spriteRenderer.color = originalColor;
        
        // Re-enable enemy AI here!
        // GetComponent<EnemyMovement>().enabled = true;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isFrozen && collision.gameObject.CompareTag("Player"))
        {
            Shatter();
        }
    }

    void Shatter()
    {
        // Add particle effects or camera shake here later
        Destroy(gameObject);
    }
}