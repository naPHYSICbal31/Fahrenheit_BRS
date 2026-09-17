using UnityEngine;
using System.Collections;

// Spawned on top of a frozen enemy by CryoSnowball. Hides and pauses the real enemy for
// freezeDuration seconds: break the block inside that window (player contact or player
// bullet) and the enemy dies through its own death path; let the timer run out and the
// enemy wakes back up exactly where it left off.
public class FrozenEnemyBlock : MonoBehaviour
{
    [Header("Break Conditions")]
    public bool breakOnPlayerContact = true;
    public bool breakOnPlayerBullet = true;
    public string playerTag = "Player";
    public string playerBulletTag = "PlayerBullet";

    [Header("Timing")]
    public bool useUnscaledTime = false; // true = freeze timer ignores aim slow-mo

    private GameObject frozenEnemy;
    private float freezeDuration;
    private bool resolved = false;

    // Called by CryoSnowball right after Instantiate().
    public void Init(GameObject enemy, float duration)
    {
        frozenEnemy = enemy;
        freezeDuration = duration;

        if (frozenEnemy != null)
        {
            // Follow the enemy's position at the instant of freezing so a thaw puts it back
            // exactly where it stood.
            transform.position = frozenEnemy.transform.position;
            frozenEnemy.SetActive(false);
        }

        StartCoroutine(FreezeTimer());
    }

    IEnumerator FreezeTimer()
    {
        float elapsed = 0f;
        while (elapsed < freezeDuration && !resolved)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        if (!resolved) Thaw();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (resolved) return;

        // The enemy was destroyed by something else while frozen - just clean up.
        if (frozenEnemy == null)
        {
            resolved = true;
            Destroy(gameObject);
            return;
        }

        bool isPlayerHit = breakOnPlayerContact && IsTagged(other, playerTag);
        bool isBulletHit = breakOnPlayerBullet && IsTagged(other, playerBulletTag);
        if (!isPlayerHit && !isBulletHit) return;

        resolved = true;

        if (isBulletHit) Destroy(other.gameObject); // FIXED: consume the bullet

        Shatter();
    }

    bool IsTagged(Collider2D col, string tag)
    {
        if (col.CompareTag(tag)) return true;
        Transform t = col.transform.parent;
        while (t != null)
        {
            if (t.CompareTag(tag)) return true;
            t = t.parent;
        }
        return false;
    }

    // Player broke the ice in time - the enemy dies for good, through its own death
    // routine so it gets its VFX, death sound, kill-tracking and shard drop.
    void Shatter()
    {
        GameObject enemy = frozenEnemy;
        frozenEnemy = null; // clear first so OnDestroy's safety thaw can't re-enable it

        if (enemy != null)
        {
            IKillable killable = enemy.GetComponent<IKillable>();
            if (killable != null)
            {
                enemy.SetActive(true); // re-enable so Die()'s own logic runs cleanly
                killable.Die();
            }
            else
            {
                IHasDeathVfx vfxSource = enemy.GetComponent<IHasDeathVfx>();
                if (vfxSource != null && vfxSource.DeathVfxPrefab != null)
                {
                    Instantiate(vfxSource.DeathVfxPrefab, enemy.transform.position, Quaternion.identity);
                }

                if (EnemyKillTracker.Instance != null)
                {
                    EnemyKillTracker.Instance.RegisterKill(enemy.transform.position);
                }

                Destroy(enemy);
            }
        }

        Destroy(gameObject);
    }

    // Timer ran out - the enemy wakes back up exactly where it left off.
    void Thaw()
    {
        resolved = true;

        if (frozenEnemy != null)
        {
            frozenEnemy.transform.position = transform.position;
            frozenEnemy.SetActive(true);
            frozenEnemy = null;
        }

        Destroy(gameObject);
    }

    // Safety net: if this block is ever destroyed some other way (scene cleanup, splash
    // damage, etc.) the enemy must never be left disabled and invisible forever.
    void OnDestroy()
    {
        if (!resolved && frozenEnemy != null)
        {
            frozenEnemy.transform.position = transform.position;
            frozenEnemy.SetActive(true);
            frozenEnemy = null;
        }
    }
}