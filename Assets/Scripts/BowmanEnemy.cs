using UnityEngine;

public class BowmanEnemy : MonoBehaviour, IPlayerAware, IHasDeathVfx, IKillable, ISolarPullable
{
    [Header("References")]
    public Transform player;
    public Transform firePoint;
    public GameObject arrowPrefab;
    public Rigidbody2D rb2D;

    [Header("Positioning (Orbit Around Player)")]
    public float minOrbitRadius = 6f;
    public float maxOrbitRadius = 10f;
    public float moveSpeed = 3f;
    public float repositionIntervalMin = 2f;
    public float repositionIntervalMax = 5f;

    [Header("Combat Settings")]
    public int damage = 1;
    public float attackRange = 12f;
    public float attackIntervalMin = 1.5f;
    public float attackIntervalMax = 3.5f;
    public float arrowSpeed = 12f;

    [Header("Aim Inaccuracy")]
    public float aimErrorRadius = 1.5f;

    [Header("Rotation")]
    public float rotationSensitivity = 360f;

    [Header("Death VFX")]
    public GameObject deathVfxPrefab;
    public GameObject shardPrefab;

    [Header("Death Sound")]
    public AudioClip[] deathSounds;
    [Range(0f, 1f)] public float deathSoundVolume = 1f;

    [HideInInspector]
    public bool isBeingPulled = false;

    private float attackTimer;
    private float nextAttackTime;

    private float repositionTimer;
    private float nextRepositionTime;

    private Vector2 currentTargetOffset;

    private bool isDead;

    public GameObject DeathVfxPrefab => deathVfxPrefab;

    public Rigidbody2D Rb2D => rb2D;

    public bool IsBeingPulled
    {
        get => isBeingPulled;
        set => isBeingPulled = value;
    }

    public void KillBySolarPull()
    {
        Die(false);
    }

    void Awake()
    {
        if (rb2D == null)
        {
            rb2D = GetComponent<Rigidbody2D>();
        }
    }

    void Start()
    {
        nextAttackTime = Random.Range(
            attackIntervalMin,
            attackIntervalMax
        );

        attackTimer = Random.Range(
            0f,
            nextAttackTime
        );

        PickNewTargetOffset();

        nextRepositionTime = Random.Range(
            repositionIntervalMin,
            repositionIntervalMax
        );
    }

    void Update()
    {
        if (player == null) return;
        if (isBeingPulled) return;

        FaceTarget();
        HandleShooting();
    }

    void FixedUpdate()
    {
        if (player == null) return;
        if (isBeingPulled) return;

        HandleMovement();
    }

    void HandleMovement()
    {
        repositionTimer += Time.fixedDeltaTime;

        if (repositionTimer >= nextRepositionTime)
        {
            repositionTimer = 0f;

            nextRepositionTime = Random.Range(
                repositionIntervalMin,
                repositionIntervalMax
            );

            PickNewTargetOffset();
        }

        Vector2 desiredPos =
            (Vector2)player.position + currentTargetOffset;

        if (rb2D != null)
        {
            Vector2 newPos = Vector2.MoveTowards(
                rb2D.position,
                desiredPos,
                moveSpeed * Time.fixedDeltaTime
            );

            rb2D.MovePosition(newPos);
        }
        else
        {
            Vector2 newPos = Vector2.MoveTowards(
                transform.position,
                desiredPos,
                moveSpeed * Time.fixedDeltaTime
            );

            transform.position = newPos;
        }
    }

    void PickNewTargetOffset()
    {
        float angle = Random.Range(
            0f,
            Mathf.PI * 2f
        );

        float distance = Random.Range(
            minOrbitRadius,
            maxOrbitRadius
        );

        currentTargetOffset =
            new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * distance;
    }

    void FaceTarget()
    {
        Vector2 direction =
            (Vector2)player.position -
            (Vector2)transform.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        float targetAngle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) * Mathf.Rad2Deg;

        float currentAngle =
            transform.eulerAngles.z;

        float newAngle =
            Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                rotationSensitivity * Time.deltaTime
            );

        transform.rotation =
            Quaternion.Euler(
                0,
                0,
                newAngle
            );
    }

    void HandleShooting()
    {
        float distToPlayer =
            Vector2.Distance(
                transform.position,
                player.position
            );

        if (distToPlayer > attackRange)
            return;

        attackTimer += Time.deltaTime;

        if (attackTimer >= nextAttackTime)
        {
            attackTimer = 0f;

            nextAttackTime = Random.Range(
                attackIntervalMin,
                attackIntervalMax
            );

            Shoot();
        }
    }

    void Shoot()
    {
        if (arrowPrefab == null || firePoint == null)
            return;

        Vector2 aimErrorOffset =
            Random.insideUnitCircle * aimErrorRadius;

        Vector2 targetPoint =
            (Vector2)player.position +
            aimErrorOffset;

        Vector2 direction =
            (targetPoint -
            (Vector2)firePoint.position).normalized;

        GameObject arrowObj =
            Instantiate(
                arrowPrefab,
                firePoint.position,
                firePoint.rotation
            );

        EnemyArrow arrowScript =
            arrowObj.GetComponent<EnemyArrow>();

        if (arrowScript != null)
        {
            arrowScript.damage = damage;

            arrowScript.SetDirection(
                direction,
                arrowSpeed
            );
        }
    }

    void PlayDeathSound()
    {
        if (deathSounds == null ||
            deathSounds.Length == 0)
            return;

        AudioClip clip =
            deathSounds[
                Random.Range(
                    0,
                    deathSounds.Length
                )
            ];

        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(
                clip,
                transform.position,
                deathSoundVolume
            );
        }
    }

    public void Die()
    {
        Die(true);
    }

    private void Die(bool dropShard)
    {
        if (isDead)
            return;

        isDead = true;

        if (deathVfxPrefab != null)
        {
            Instantiate(
                deathVfxPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        PlayDeathSound();

        if (dropShard)
        {
            bool droppedHealthShard =
                EnemyKillTracker.Instance != null &&
                EnemyKillTracker.Instance.RegisterKill(
                    transform.position
                );

            if (!droppedHealthShard &&
                shardPrefab != null)
            {
                Instantiate(
                    shardPrefab,
                    transform.position,
                    Quaternion.identity
                );
            }
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerBullet"))
        {
            Die();
            Destroy(other.gameObject);
        }
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }
}