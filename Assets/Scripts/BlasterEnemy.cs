using UnityEngine;

public class BlasterEnemy : MonoBehaviour, IPlayerAware, IHasDeathVfx, IKillable, ISolarPullable
{
    [Header("References")]
    public Transform player;
    public Rigidbody2D rb2D;

    [Header("Chase Settings")]
    public float moveSpeed = 3.5f;
    public float explodeRange = 1.5f;

    [Header("Overshoot Orbit (elliptical path around the player, starts on 2nd entry)")]
    public float overshootArmDuration = 3f;
    public float ellipseSquishMin = 0.5f;
    public float ellipseSquishMax = 0.9f;
    public float orbitAngularSpeed = 180f;
    public float orbitEaseInDuration = 0.4f;

    [Header("Explosion")]
    public float explosionRadius = 2f;
    public int explosionDamage = 2;
    public GameObject explosionEffect;
    public LayerMask enemyLayer; // set this to your Enemy layer ONLY - never Everything

    [Header("Temperature")]
    public float temperatureDelta = 5f;

    [Header("Death VFX")]
    public GameObject deathVfxPrefab;
    public GameObject shardPrefab;

    [Header("Death Sound")]
    public AudioClip[] deathSounds;
    [Range(0f, 1f)] public float deathSoundVolume = 1f;

    [Header("Mirror Bounce (Bicycle Kick)")]
    public string mirrorEdgeTag = "MirrorEdge";
    public float reflectSpeed = 8f;
    public float reflectDespawnDelay = 4f;

    [HideInInspector] public bool isBeingPulled = false;

    private enum Phase { Chasing, PassingThrough, Orbiting }
    private Phase phase = Phase.Chasing;

    private int radiusEntryCount = 0;
    private Vector2 passThroughDirection;
    private float armTimer = 0f;
    private float armDuration;

    private float orbitSemiMajor;
    private float orbitSemiMinor;
    private float orbitRotationOffset;
    private float orbitDirectionSign;
    private float orbitAngle;
    private float orbitElapsedTime;

    private bool isReflected = false;
    public bool IsReflected => isReflected;
    public bool IsOrbiting => phase == Phase.Orbiting;
    private Vector2 reflectedDirection;
    private float reflectTimer = 0f;

    private bool isDead;

    private TemperatureBar temperatureBar;

    // --- IHasDeathVfx ---
    public GameObject DeathVfxPrefab => deathVfxPrefab;

    // --- ISolarPullable ---
    public Rigidbody2D Rb2D => rb2D;
    public bool IsBeingPulled { get => isBeingPulled; set => isBeingPulled = value; }

    public void KillBySolarPull()
    {
        if (isDead) return;
        isDead = true;

        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }
        PlayDeathSound();

        Explode(); // no shard on a Solar Pull kill
    }

    // --- IKillable ---
    // NEW: clean, non-detonating death used by the frozen-block shatter (and anything else
    // that just wants this enemy gone). Drops its shard like a normal player kill.
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }
        PlayDeathSound();
        DropShard();

        Destroy(gameObject);
    }

    void Awake()
    {
        if (rb2D == null)
        {
            rb2D = GetComponent<Rigidbody2D>();
        }

        temperatureBar = FindFirstObjectByType<TemperatureBar>();
    }

    void Update()
    {
        if (player == null) return;
        if (isBeingPulled) return;

        if (isReflected)
        {
            Vector2 nextPos = (Vector2)transform.position + reflectedDirection * reflectSpeed * Time.deltaTime;
            transform.position = OrbitZone.ClampOutsideEnemyZone(nextPos);

            reflectTimer += Time.deltaTime;
            if (reflectTimer >= reflectDespawnDelay)
            {
                Explode();
            }
            return;
        }

        switch (phase)
        {
            case Phase.Chasing:
                UpdateChasing();
                break;
            case Phase.PassingThrough:
                UpdatePassingThrough();
                break;
            case Phase.Orbiting:
                UpdateOrbiting();
                break;
        }
    }

    void UpdateChasing()
    {
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > explodeRange)
        {
            MoveTowardPlayer();
            return;
        }

        radiusEntryCount++;

        if (radiusEntryCount == 1)
        {
            Vector2 dir = (Vector2)player.position - (Vector2)transform.position;
            passThroughDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : (Vector2)transform.right;
            phase = Phase.PassingThrough;
        }
        else
        {
            Arm();
            phase = Phase.Orbiting;
        }
    }

    void UpdatePassingThrough()
    {
        Vector2 nextPos = (Vector2)transform.position + passThroughDirection * moveSpeed * Time.deltaTime;
        transform.position = OrbitZone.ClampOutsideEnemyZone(nextPos);

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > explodeRange)
        {
            phase = Phase.Chasing;
        }
    }

    void MoveTowardPlayer()
    {
        Vector2 nextPos = Vector2.MoveTowards(transform.position, player.position, moveSpeed * Time.deltaTime);
        transform.position = OrbitZone.ClampOutsideEnemyZone(nextPos);
    }

    void UpdateOrbiting()
    {
        OrbitAroundPlayer();

        armTimer += Time.deltaTime;
        if (armTimer >= armDuration)
        {
            Explode();
        }
    }

    void Arm()
    {
        armTimer = 0f;
        armDuration = overshootArmDuration;
        orbitElapsedTime = 0f;

        Vector2 offset = (Vector2)transform.position - (Vector2)player.position;
        float entryDistance = offset.magnitude;
        float entryAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

        orbitRotationOffset = entryAngle;
        orbitSemiMajor = entryDistance;
        float squish = Random.Range(ellipseSquishMin, ellipseSquishMax);
        orbitSemiMinor = orbitSemiMajor * squish;

        orbitDirectionSign = Random.value < 0.5f ? 1f : -1f;
        orbitAngle = 0f;
    }

    void OrbitAroundPlayer()
    {
        orbitElapsedTime += Time.deltaTime;
        float easeFactor = orbitEaseInDuration > 0f ? Mathf.Clamp01(orbitElapsedTime / orbitEaseInDuration) : 1f;

        orbitAngle += orbitAngularSpeed * orbitDirectionSign * easeFactor * Time.deltaTime;
        float rad = orbitAngle * Mathf.Deg2Rad;

        Vector2 ellipsePoint = new Vector2(Mathf.Cos(rad) * orbitSemiMajor, Mathf.Sin(rad) * orbitSemiMinor);

        float rotRad = orbitRotationOffset * Mathf.Deg2Rad;
        float cosR = Mathf.Cos(rotRad);
        float sinR = Mathf.Sin(rotRad);
        Vector2 rotatedPoint = new Vector2(
            ellipsePoint.x * cosR - ellipsePoint.y * sinR,
            ellipsePoint.x * sinR + ellipsePoint.y * cosR
        );

        Vector2 nextPos = (Vector2)player.position + rotatedPoint;
        transform.position = OrbitZone.ClampOutsideEnemyZone(nextPos);
    }

    void Explode()
    {
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }

        if (player != null)
        {
            float distToPlayer = Vector2.Distance(transform.position, player.position);
            if (distToPlayer <= explosionRadius)
            {
                PivotBash playerBash = player.GetComponentInParent<PivotBash>();
                bool playerIsInvincible = playerBash != null && playerBash.IsInvincible;

                if (!playerIsInvincible)
                {
                    player.GetComponentInParent<PlayerHealth>()?.TakeDamage(explosionDamage);

                    if (temperatureBar != null)
                    {
                        temperatureBar.AdjustTemperature(temperatureDelta);
                    }
                }
            }
        }

        // FIXED: the splash used to Destroy() whatever collider the layer mask caught, with no
        // check that it was actually an enemy. If the Player (or any child of it) sat on a layer
        // inside enemyLayer, the player object was deleted outright - which is why the player
        // vanished "after a certain time" (Blasters self-detonate on the orbit timer and on
        // reflect despawn). Now it only ever destroys resolved enemy roots.
        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, explosionRadius, enemyLayer);
        foreach (Collider2D enemyHit in nearbyEnemies)
        {
            if (enemyHit == null) continue;
            if (enemyHit.gameObject == gameObject) continue;

            GameObject enemyRoot = ResolveEnemyRoot(enemyHit);
            if (enemyRoot == null) continue;                  // not an enemy - never touch it
            if (enemyRoot == gameObject) continue;
            if (enemyRoot.GetComponent<PlayerHealth>() != null) continue;      // hard player guard
            if (enemyRoot.GetComponent<PlayerController>() != null) continue;  // hard player guard
            if (enemyRoot.GetComponent<BlasterEnemy>() != null) continue;      // Blasters immune to Blasters

            IHasDeathVfx vfxSource = enemyRoot.GetComponent<IHasDeathVfx>();
            if (vfxSource != null && vfxSource.DeathVfxPrefab != null)
            {
                Instantiate(vfxSource.DeathVfxPrefab, enemyRoot.transform.position, Quaternion.identity);
            }

            Destroy(enemyRoot);
        }

        Destroy(gameObject);
    }

    // Resolves a hit collider up to the GameObject that actually carries the enemy script,
    // so child colliders/sprites don't get destroyed on their own.
    GameObject ResolveEnemyRoot(Collider2D col)
    {
        IHasDeathVfx asEnemy = col.GetComponentInParent<IHasDeathVfx>();
        if (asEnemy is MonoBehaviour mb) return mb.gameObject;

        ISolarPullable pullable = col.GetComponentInParent<ISolarPullable>();
        if (pullable is MonoBehaviour pmb) return pmb.gameObject;

        return null;
    }

    void PlayDeathSound()
    {
        if (deathSounds == null || deathSounds.Length == 0) return;

        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, deathSoundVolume);
        }
    }

    void DropShard()
    {
        bool droppedHealthShard = EnemyKillTracker.Instance != null
            && EnemyKillTracker.Instance.RegisterKill(transform.position);

        if (!droppedHealthShard && shardPrefab != null)
        {
            Instantiate(shardPrefab, transform.position, Quaternion.identity);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerBullet"))
        {
            if (isDead) return;
            isDead = true;

            if (deathVfxPrefab != null)
            {
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            }

            PlayDeathSound();
            DropShard();

            Destroy(other.gameObject); // NEW: consume the bullet, same as Bowman/Mage
            Explode();
            return;
        }

        if (other.CompareTag(mirrorEdgeTag))
        {
            if (isBeingPulled) return;
            if (isReflected) return;

            PlayerController playerController = other.GetComponentInParent<PlayerController>();
            if (playerController != null && player != null)
            {
                Vector2 normal = playerController.BackDirection;

                Vector2 moveDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
                Vector2 reflected = Vector2.Reflect(moveDir, normal);

                BicycleKick kick = other.GetComponentInParent<BicycleKick>();
                reflectSpeed = kick != null ? moveSpeed * kick.reflectSpeedMultiplier : reflectSpeed;

                reflectedDirection = reflected;
                isReflected = true;
                reflectTimer = 0f;
            }
        }
    }

    // --- IPlayerAware ---
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }
}