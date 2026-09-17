using UnityEngine;

// A circular projectile with two phases:
//
// PHASE 1 - "enemy bullet" (from spawn until bashed):
//   Flies toward the player. If it touches the player, damages them and destroys itself.
//   Can be aimed-at and reeled-in by PivotBash (must sit on the layer(s) assigned to
//   PivotBash's "Pivot Layer" field).
//
// PHASE 2 - "player's weapon" (after being bashed):
//   Reverses to fly AWAY from the player's launch direction. No longer harms the player
//   at all. If it touches something on enemyLayer, it explodes: everything within
//   splashRadius on enemyLayer is destroyed, then the orb destroys itself.
//
// Either phase: if it goes noHitLifetime seconds without hitting a valid target,
// it self-destructs. Getting bashed resets that timer.
[RequireComponent(typeof(Collider2D))]
public class EnemyOrbBullet : MonoBehaviour, IBashablePivot
{
    [Header("Movement")]
    public float speed = 6f;            // phase 1 speed (toward the player)
    public float bashReboundSpeed = 12f; // CHANGED: phase 2 speed, after being bashed - tune independently
    private Vector2 direction;

    [Header("Damage (Phase 1 - hitting the player)")]
    public int damage = 1;

    [Header("Splash Damage (Phase 2 - hitting enemies)")]
    public float splashRadius = 2.5f;
    public LayerMask enemyLayer; // assign whatever layer your Bowman/Mage/etc. enemies are on

    [Header("Lifetime")]
    public float noHitLifetime = 2f; // seconds without hitting a valid target before self-destructing
    private float noHitTimer = 0f;

    [Header("Impact Feedback")]
    public GameObject impactEffect; // optional VFX prefab, spawned wherever this orb is finally destroyed
    public GameObject fallbackDeathVfxPrefab; // used only if the destroyed enemy doesn't implement IHasDeathVfx

    // True while PivotBash is currently aiming at / reeling the player toward this orb.
    // While true, we ignore triggers entirely - that overlap is the reel-in motion, not a genuine hit.
    private bool isTargetedForBash = false;

    // CHANGED: exposed so other orbs' Explode() can check this before destroying us,
    // so a nearby explosion can't kill the pivot the player is currently aiming at.
    public bool IsTargetedForBash => isTargetedForBash;

    // False = phase 1 (enemy bullet, hunts the player).
    // True = phase 2 (player's weapon, hunts enemies), set once OnBashed() fires.
    private bool isBashed = false;

    public void SetDirection(Vector2 dir, float overrideSpeed = -1f)
    {
        direction = dir.normalized;
        if (overrideSpeed > 0f) speed = overrideSpeed;
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        if (isTargetedForBash) return; // being aimed at / reeled in - don't tick or expire lifetime

        noHitTimer += Time.deltaTime;
        if (noHitTimer >= noHitLifetime)
        {
            Explode();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isTargetedForBash) return; // being reeled toward, not a real hit yet

        // NEW: the sun's own body stops this orb in EITHER phase - same reasoning as
        // EnemyArrow's Sun check. The collider covering the sun has to be a trigger
        // (OrbitZone needs that for player capture), so without this explicit tag check
        // the orb would just sail through it untouched, in both its enemy-bullet and
        // bashed-weapon phases.
        if (other.CompareTag("Sun"))
        {
            Explode();
            return;
        }

        if (!isBashed)
        {
            // --- Phase 1: still an enemy bullet hunting the player ---
            if (other.CompareTag("Player"))
            {
                // Extra safety net: if the player is currently invincible (mid-bash or
                // in the brief window right after), don't count this as a hit at all.
                PivotBash playerBash = other.GetComponentInParent<PivotBash>();
                if (playerBash != null && playerBash.IsInvincible) return;

                other.GetComponentInParent<PlayerHealth>()?.TakeDamage(damage);

                Explode();
            }
            return;
        }

        // --- Phase 2: now a player's weapon, hunting enemies ---
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            Explode();
        }
    }

    void Explode()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, splashRadius, enemyLayer);
        foreach (Collider2D hit in hits)
        {
            // CHANGED: skip anything currently being aimed at by the player -
            // otherwise a nearby explosion can kill the pivot mid-aim and break PivotBash's state.
            EnemyOrbBullet otherOrb = hit.GetComponent<EnemyOrbBullet>();
            if (otherOrb != null && otherOrb.IsTargetedForBash) continue;

            PlayDeathVfxFor(hit);

            Destroy(hit.transform.root.gameObject);
        }

        if (impactEffect != null)
        {
            Instantiate(impactEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    // Looks for IHasDeathVfx on the enemy (or its parents, in case the collider
    // is on a child), and plays that enemy's own death VFX. Falls back to
    // fallbackDeathVfxPrefab if the enemy doesn't implement it.
    void PlayDeathVfxFor(Collider2D hit)
    {
        IHasDeathVfx vfxSource = hit.GetComponentInParent<IHasDeathVfx>();
        GameObject vfxPrefab = vfxSource != null ? vfxSource.DeathVfxPrefab : fallbackDeathVfxPrefab;

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, hit.transform.position, Quaternion.identity);
        }
    }

    // --- IBashablePivot ---
    // Called automatically by PivotBash.LaunchBash() the instant the player bashes off this orb.
    // CHANGED: no longer destroys the orb - reverses it into phase 2 instead.
    public void OnBashed(PivotBash basher)
    {
        isTargetedForBash = false; // done being reeled toward, resume normal trigger handling
        isBashed = true;           // now behaves as the player's weapon, not an enemy bullet

        // Reverse away from the direction the player just launched in
        direction = -basher.AimDirection.normalized;
        speed = bashReboundSpeed;

        // Fresh window to find an enemy before self-destructing
        noHitTimer = 0f;
    }

    // Called by PivotBash when this orb becomes/stops being the aim target
    public void SetTargeted(bool isTargeted)
    {
        isTargetedForBash = isTargeted;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
}