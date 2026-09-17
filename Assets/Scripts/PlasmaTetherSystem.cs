using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlasmaTetherSystem : MonoBehaviour
{
    [Header("Weapon Arsenal")]
    public GameObject plasmaBombPrefab;
    public GameObject cryoSnowballPrefab;

    [Header("Tether Settings")]
    public Transform bombSpawnPoint;
    public float draggingFriction = 5f;

    [Header("Weapon Settings")]
    public float launchForce = 35f;
    public float slowMoSpeed = 0.15f;

    [Header("Cryo Spread")]
    [Tooltip("Releasing a cryo snowball always fires 3 - the held one goes straight where you aim, and two more spawn from the same tip, angled +/- this many degrees from your aim.")]
    public float cryoSpreadAngle = 20f;

    [Header("Visuals")]
    public LineRenderer aimLine;

    [Header("Temperature Gate")]
    public TemperatureBar temperature; // auto-found if left empty

    [Header("Overdrive Cost")]
    public OverdriveBar overdrive;              // auto-found if left empty
    public int plasmaBombOverdriveCost = 20;    // FIXED: int, matches OverdriveBar.TrySpend(int)
    public int cryoSnowballOverdriveCost = 20;  // FIXED: int

    private SpringJoint2D tetherJoint;
    private Rigidbody2D currentBombRb;
    private bool hasBomb = false;
    private bool isAiming = false;

    private int pendingRefund = 0;   // charge spent on the weapon currently held
    private bool weaponLaunched = false; // set true only when the weapon is actually fired

    // NEW: read-only surface for other abilities to check "is the player currently holding/
    // dragging a plasma bomb or cryo snowball" - same convention as PivotBash.IsBusy, so
    // BicycleKick (and anything else) can gate itself off this without needing to know any
    // of PlasmaTetherSystem's internals.
    public bool HasWeapon => hasBomb;

    void Start()
    {
        if (aimLine != null) aimLine.enabled = false;
        if (temperature == null) temperature = FindFirstObjectByType<TemperatureBar>();
        if (overdrive == null) overdrive = FindFirstObjectByType<OverdriveBar>();
    }

    bool CanPull(bool needsHeat)
    {
        if (temperature == null) return true;
        return needsHeat ? temperature.IsOverheating : temperature.IsFreezing;
    }

    bool TrySpendOverdrive(int cost)
    {
        if (overdrive == null) return true;
        if (!overdrive.TrySpend(cost)) return false;
        pendingRefund = cost;
        return true;
    }

    void Update()
    {
        if (hasBomb && currentBombRb == null)
        {
            ForceCleanUp();
        }

        if (!hasBomb)
        {
            if (Input.GetKeyDown(KeyCode.E) && plasmaBombPrefab != null
                && CanPull(true) && TrySpendOverdrive(plasmaBombOverdriveCost))
            {
                SpawnWeapon(plasmaBombPrefab);
            }
            else if (Input.GetKeyDown(KeyCode.Q) && cryoSnowballPrefab != null
                && CanPull(false) && TrySpendOverdrive(cryoSnowballOverdriveCost))
            {
                SpawnWeapon(cryoSnowballPrefab);
            }
        }

        if (hasBomb)
        {
            if (Input.GetMouseButtonDown(1)) EnterAimMode();
            if (Input.GetMouseButtonUp(1)) ReleaseBomb();
            if (isAiming) UpdateAimLine();

            // Anti-Snag Safety Net
            if (currentBombRb != null && bombSpawnPoint != null)
            {
                float stretchDistance = Vector2.Distance(transform.position, currentBombRb.transform.position);
                if (stretchDistance > 4.0f)
                {
                    currentBombRb.transform.position = bombSpawnPoint.position;
                    currentBombRb.linearVelocity = Vector2.zero;
                }
            }
        }
    }

    void ForceCleanUp()
    {
        SpringJoint2D[] oldJoints = GetComponents<SpringJoint2D>();
        foreach (SpringJoint2D joint in oldJoints) Destroy(joint);

        // A weapon that was never launched is destroyed instead of being orphaned,
        // and its overdrive cost is handed back.
        if (!weaponLaunched)
        {
            if (currentBombRb != null) Destroy(currentBombRb.gameObject);
            if (pendingRefund > 0 && overdrive != null) overdrive.AddCharge(pendingRefund);
        }

        pendingRefund = 0;
        weaponLaunched = false;

        hasBomb = false;
        isAiming = false;
        tetherJoint = null;
        currentBombRb = null;

        if (aimLine != null) aimLine.enabled = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    public void SpawnWeapon(GameObject weaponPrefabToSpawn)
    {
        if (weaponPrefabToSpawn == null) return;

        int carriedRefund = pendingRefund; // survive the ForceCleanUp below
        pendingRefund = 0;
        ForceCleanUp();
        pendingRefund = carriedRefund;

        hasBomb = true;
        weaponLaunched = false;

        Vector3 finalSpawnPos = bombSpawnPoint != null ? bombSpawnPoint.position : transform.position;
        Collider2D obstacleHit = Physics2D.OverlapPoint(finalSpawnPos);

        if (obstacleHit != null && obstacleHit.gameObject != this.gameObject)
        {
            finalSpawnPos = transform.position;
        }

        GameObject weapon = Instantiate(weaponPrefabToSpawn, finalSpawnPos, Quaternion.identity);
        currentBombRb = weapon.GetComponent<Rigidbody2D>();

        if (currentBombRb == null)
        {
            Debug.LogError("PlasmaTetherSystem: weapon prefab has no Rigidbody2D - " + weaponPrefabToSpawn.name);
            Destroy(weapon);
            hasBomb = false;
            if (pendingRefund > 0 && overdrive != null) overdrive.AddCharge(pendingRefund);
            pendingRefund = 0;
            return;
        }

        Collider2D playerCollider = GetComponent<Collider2D>();
        Collider2D weaponCollider = weapon.GetComponent<Collider2D>();
        if (playerCollider != null && weaponCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, weaponCollider, true);
        }

        currentBombRb.linearDamping = draggingFriction;

        tetherJoint = gameObject.AddComponent<SpringJoint2D>();
        tetherJoint.connectedBody = currentBombRb;
        tetherJoint.autoConfigureDistance = false;
        tetherJoint.distance = 1.5f;
        tetherJoint.frequency = 4f;
        tetherJoint.dampingRatio = 1f;
    }

    void EnterAimMode()
    {
        isAiming = true;
        Time.timeScale = slowMoSpeed;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        if (aimLine != null) aimLine.enabled = true;
    }

    void UpdateAimLine()
    {
        if (aimLine != null && currentBombRb != null)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            aimLine.SetPosition(0, currentBombRb.transform.position);
            aimLine.SetPosition(1, mousePos);
        }
    }

    void ReleaseBomb()
    {
        isAiming = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (aimLine != null) aimLine.enabled = false;

        if (currentBombRb != null)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            Vector2 shootDirection = (mousePos - currentBombRb.transform.position).normalized;

            currentBombRb.linearDamping = 0f;
            currentBombRb.AddForce(shootDirection * launchForce, ForceMode2D.Impulse);

            PlasmaBomb bombScript = currentBombRb.GetComponent<PlasmaBomb>();
            if (bombScript != null) bombScript.IgniteFuse();

            CryoSnowball snowScript = currentBombRb.GetComponent<CryoSnowball>();
            if (snowScript != null)
            {
                snowScript.ReleaseSnowball();

                Vector3 spawnPos = currentBombRb.transform.position;
                SpawnExtraSnowball(spawnPos, RotateVector(shootDirection, cryoSpreadAngle));
                SpawnExtraSnowball(spawnPos, RotateVector(shootDirection, -cryoSpreadAngle));
            }

            weaponLaunched = true; // no refund, no destroy - it's in flight now
        }

        ForceCleanUp();
    }

    void SpawnExtraSnowball(Vector3 position, Vector2 direction)
    {
        if (cryoSnowballPrefab == null) return;

        GameObject extra = Instantiate(cryoSnowballPrefab, position, Quaternion.identity);
        Rigidbody2D extraRb = extra.GetComponent<Rigidbody2D>();

        if (extraRb == null)
        {
            Debug.LogError("PlasmaTetherSystem: cryoSnowballPrefab has no Rigidbody2D - can't fire spread shot.");
            Destroy(extra);
            return;
        }

        Collider2D playerCollider = GetComponent<Collider2D>();
        Collider2D extraCollider = extra.GetComponent<Collider2D>();
        if (playerCollider != null && extraCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, extraCollider, true);
        }

        extraRb.linearDamping = 0f;
        extraRb.AddForce(direction.normalized * launchForce, ForceMode2D.Impulse);

        CryoSnowball extraSnow = extra.GetComponent<CryoSnowball>();
        if (extraSnow != null) extraSnow.ReleaseSnowball();
    }

    static Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}