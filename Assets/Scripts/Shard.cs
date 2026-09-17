using UnityEngine;

public class Shard : MonoBehaviour
{
    public int chargeAmount = 10;
    public float lifetime = 4f;

    void Start()
    {
        Destroy(gameObject, lifetime); // single despawn timer, same convention as EnemyArrow/EnemyBullet
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("[Shard] OVERDRIVE shard picked up -> AddCharge");
            other.GetComponentInParent<OverdriveBar>()?.AddCharge(chargeAmount);
            Destroy(gameObject);
        }
    }
}
