using UnityEngine;

public class HealthShard : MonoBehaviour
{
    public int healAmount = 10;
    public float lifetime = 4f;

    void Start()
    {
        Destroy(gameObject, lifetime); // single despawn timer, same convention as Shard/EnemyArrow/EnemyBullet
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[HealthShard] HEALTH shard picked up -> Heal({healAmount})");
            other.GetComponentInParent<PlayerHealth>()?.Heal(healAmount);
            Destroy(gameObject);
        }
    }
}
