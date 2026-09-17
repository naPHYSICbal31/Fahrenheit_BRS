using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    public float lifetime = 5f;
    public int damage = 1;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // TRIGGER THE HUGE SHAKE! (0.3 seconds long, 0.5 intensity)
            CameraShake.Instance.Shake(0.3f, 0.5f);
            other.GetComponentInParent<PlayerHealth>()?.TakeDamage(damage);

            Destroy(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // TRIGGER THE HUGE SHAKE!
            CameraShake.Instance.Shake(0.3f, 0.5f);
            collision.gameObject.GetComponentInParent<PlayerHealth>()?.TakeDamage(damage);

            Destroy(gameObject);
        }
    }
}