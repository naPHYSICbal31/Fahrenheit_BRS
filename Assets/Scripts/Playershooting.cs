using UnityEngine;
using Unity.Cinemachine; // Required for Unity 6 Cinemachine

public class PlayerShooting : MonoBehaviour
{
    [Header("Gun Stats")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.15f; // Time between shots. Lower = faster.
    private float nextFireTime = 0f;
    private CinemachineImpulseSource screenShake;

    [Header("Chain Lightning")]
    public GameObject chainBulletPrefab;
    public float chainFireRate = 1.5f;   // slower, it's a special
    public int chainChargeCost = 30;     // charge spent per shot - 3 shards' worth (each shard = 10 charge)
    private float nextChainFireTime = 0f;

    [Header("Audio")]
    public AudioSource gunAudioSource;     // dedicated AudioSource for gunfire � separate from any slow-mo AudioSource
    public AudioClip[] bulletSounds;       // drop all 7 bullet-hit sounds in here (any size works, not locked to 7)
    [Range(0f, 0.5f)] public float pitchVariance = 0.08f; // small random pitch wobble so repeats don't sound robotic
    [Range(0f, 1f)] public float bulletVolume = 1f; // NEW: overall volume for bullet sounds, adjustable in the Inspector

    [Header("Heat / Overheat")]
    public SpriteRenderer playerSpriteRenderer; // assign in Inspector, or auto-fetched in Awake
    public float heatPerShot = 0.15f;           // how much heat each bullet adds (1f = fully overheated)
    public float overheatCooldownDuration = 3f; // lockout duration once heat hits max
    public float idleCoolDelay = 1f;            // how long with no shots before heat starts passively draining
    public float idleCoolRate = 0.5f;           // heat drained per second once idle cooling kicks in
    public float maxRedness = 0.8f;             // caps the color shift at 80% of the way to red
    private Color originalColor;
    private float heat = 0f;                    // 0 = cool, 1 = fully overheated
    private float timeSinceLastShot = 0f;
    private bool isOverheated = false;
    private float overheatTimer = 0f;
    public float overheatShakeMultiplier = 1.5f;
    public OverdriveBar overdriveBar;// NEW � how much stronger the "denied" shake is vs a normal shot

    void Start()
    {
        screenShake = GetComponent<CinemachineImpulseSource>();
        if (gunAudioSource == null) gunAudioSource = GetComponent<AudioSource>(); // fall back to a sibling AudioSource if none was assigned

        if (playerSpriteRenderer == null) playerSpriteRenderer = GetComponent<SpriteRenderer>();
        if (playerSpriteRenderer != null) originalColor = playerSpriteRenderer.color;

        // Fallback: the inspector reference keeps not getting wired, so auto-find the
        // single OverdriveBar in the scene if it wasn't assigned manually.
        if (overdriveBar == null) overdriveBar = FindFirstObjectByType<OverdriveBar>();
        Debug.Log($"[PlayerShooting] Start: overdriveBar={(overdriveBar != null ? "resolved" : "STILL NULL - none in scene")}");
    }

    void Update()
    {
        HandleHeat();

        // Left mouse button to shoot.
        // We use Time.time to create a rhythmic, satisfying fire rate.
        if (!isOverheated && Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
        else if (isOverheated && Input.GetMouseButtonDown(0)) // NEW � only fires once per click, not every frame held
        {
            ShootDenied();
        }

        if (Input.GetKeyDown(KeyCode.None))
        {
            bool cooldownReady = Time.time >= nextChainFireTime;
            bool hasOverdriveBar = overdriveBar != null;
            Debug.Log($"[Q] pressed: cooldownReady={cooldownReady}, overdriveBar={(hasOverdriveBar ? "assigned" : "NULL")}, chainBulletPrefab={(chainBulletPrefab != null ? "assigned" : "NULL")}, cost={chainChargeCost}");

            if (cooldownReady && hasOverdriveBar && overdriveBar.TrySpend(chainChargeCost))
            {
                Debug.Log("[Q] TrySpend succeeded -> firing ShootChain()");
                ShootChain();
                nextChainFireTime = Time.time + chainFireRate;
            }
            else if (cooldownReady && hasOverdriveBar)
            {
                Debug.Log("[Q] TrySpend FAILED - not enough charge");
            }
        }

        UpdateSpriteColor();
    }

    void HandleHeat()
    {
        timeSinceLastShot += Time.deltaTime;

        if (isOverheated)
        {
            // Cooldown lockout: ease heat back down to 0 over overheatCooldownDuration
            overheatTimer += Time.deltaTime;
            float t = Mathf.Clamp01(overheatTimer / overheatCooldownDuration);
            heat = Mathf.Lerp(1f, 0f, t);

            if (overheatTimer >= overheatCooldownDuration)
            {
                isOverheated = false;
                overheatTimer = 0f;
                heat = 0f;
            }
        }
        else if (timeSinceLastShot >= idleCoolDelay && heat > 0f)
        {
            // Not overheated, just naturally cooling down from lack of firing
            heat = Mathf.Max(0f, heat - idleCoolRate * Time.deltaTime);
        }
    }

    void UpdateSpriteColor()
    {
        if (playerSpriteRenderer == null) return;
        float redness = Mathf.Clamp01(heat) * maxRedness;
        playerSpriteRenderer.color = Color.Lerp(originalColor, Color.red, redness);
    }

    void Shoot()
    {
        // 1. Spawn the bullet
        Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // 2. Trigger the camera shake! This makes it feel incredibly powerful.
        if (screenShake != null)
        {
            screenShake.GenerateImpulse();
        }

        // 3. Play a random bullet sound
        PlayRandomBulletSound();

        // 4. Build heat, and trigger overheat if we've hit the cap
        timeSinceLastShot = 0f;
        heat = Mathf.Min(1f, heat + heatPerShot);
        if (heat >= 1f && !isOverheated)
        {
            isOverheated = true;
            overheatTimer = 0f;
        }
    }

    void ShootChain()
    {
        Instantiate(chainBulletPrefab, firePoint.position, firePoint.rotation);
        if (screenShake != null)
            screenShake.GenerateImpulse();
        PlayRandomBulletSound();
    }

    // picks one of the assigned bullet clips at random and plays it as an overlapping one-shot.
    // PlayOneShot lets rapid-fire shots overlap cleanly instead of cutting each other off like Play() would.
    void PlayRandomBulletSound()
    {
        if (gunAudioSource == null || bulletSounds == null || bulletSounds.Length == 0) return;
        AudioClip clip = bulletSounds[Random.Range(0, bulletSounds.Length)];
        if (clip == null) return;
        // slight random pitch shift each shot avoids the "machine gun of the same exact sample" feel
        gunAudioSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        gunAudioSource.PlayOneShot(clip, bulletVolume); // CHANGED: volumeScale now driven by the bulletVolume slider instead of the implicit default of 1
    }
    void ShootDenied()
    {
        if (screenShake != null)
        {
            screenShake.GenerateImpulse(overheatShakeMultiplier); // 1.5x the default impulse velocity
        }
    }
}