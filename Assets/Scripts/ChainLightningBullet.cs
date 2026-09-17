using UnityEngine;

public class ChainLightningBullet : MonoBehaviour
{
    public float speed = 12f;
    public LayerMask enemyLayer;
    public GameObject chainLightningPrefab; // your Prefab A
    public float lifetime = 3f;             // despawn if it hits nothing

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Fly in the direction the bullet is facing (its local right/up).
        transform.position += transform.up * speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Only react to enemies.
        if (enemyLayer != (enemyLayer | (1 << other.gameObject.layer))) return;

        Vector2 hitPoint = transform.position;

        // Spawn the chain lightning brain and kick off the chain.
        GameObject go = Instantiate(chainLightningPrefab, hitPoint, Quaternion.identity);
        ChainLightning cl = go.GetComponent<ChainLightning>();
        if (cl != null) cl.Begin(hitPoint);

        Destroy(gameObject); // bullet is consumed
    }
}