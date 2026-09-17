using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(LineRenderer))]
public class GravityWell : MonoBehaviour
{
    [Header("Physics: The Pull")]
    public float captureFriction = 15f;         
    public float escapeVelocityThreshold = 35f; 
    public float maxPullStrength = 25f;         
    public float effectRadius = 15f;            
    public float eventHorizonRadius = 1f;       
    public LayerMask playerLayer;               

    [Header("Physics: The Trap")]
    public float radialDamping = 12f;           
    public float tangentialDamping = 6f;        
    public float minSinkSpeed = 2f;             
    
    [Header("Boss Cage Settings")]
    public bool isCageMode = false;

    [Header("Level Portal Settings")]
    public bool isPortalMode = false; // when true, bashing past escapeVelocityThreshold loads the next level instead of just bypassing the pull
    public bool disableGravityPull = false; // when true (with isPortalMode), skips all pull physics - just walk close enough to trigger the level transition
    public float portalTriggerRadius = 1f; // how close the player needs to get when disableGravityPull is on
    public string nextLevelSceneName;
    private bool hasTriggeredTransition = false;

    private MonoBehaviour trappedPlayerMovement;
    private bool isPlayerTrapped = false;
    private bool isPlayerBeingPulled = false;  
    private bool wasPlayerBeingPulled = false; 

    [Header("Visuals: The Anomaly")]
    public int linePoints = 24;         
    public float visualRadius = 3f;     
    public float rotationSpeed = 60f;   
    public float pulseSpeed = 8f;       
    public float pulseMagnitude = 0.4f; 

    [Header("Visuals: The Alarm")]
    public Color normalColor = Color.white;
    public Color alarmColor = Color.red;
    public float alarmSpinMultiplier = 5f;    
    public float alarmPulseMultiplier = 3f;   
    public float alarmNoiseStrength = 0.8f;   

    [Header("Audio: The Alarm")]
    public AudioSource alarmAudioSource; 
    public AudioClip alarmSirenClip;     

    private LineRenderer lr;
    private float currentRotation;

    void Start()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = linePoints;
        lr.loop = true;
        lr.useWorldSpace = true;

        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        
        lr.startColor = normalColor;
        lr.endColor = normalColor;
    }
    
    public void SetCageMode()
    {
        isCageMode = true;
        eventHorizonRadius = 0f; 
        
        // --- THE FIX ---
        // Shrink the physics detection to match the visual graphics.
        // We add a tiny 1f buffer so the dash script has exactly enough room to register the escape!
        effectRadius = visualRadius + 1f; 
    }

    void Update()
    {
        Color targetColor = isPlayerTrapped ? alarmColor : normalColor;
        lr.startColor = Color.Lerp(lr.startColor, targetColor, Time.deltaTime * 10f);
        lr.endColor = lr.startColor;

        DrawJaggedCircle();
    }

    void FixedUpdate()
    {
        ApplyGravity();
    }

    void ApplyGravity()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, effectRadius, playerLayer);
        bool foundPlayer = false;
        
        isPlayerTrapped = false; 
        isPlayerBeingPulled = false; 

        foreach (Collider2D col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                foundPlayer = true;
                Rigidbody2D playerRb = col.GetComponent<Rigidbody2D>();
                if (playerRb == null) continue;

                // Simplified portal: no pull physics at all, just a proximity trigger.
                if (isPortalMode && disableGravityPull)
                {
                    float distToPlayer = Vector2.Distance(transform.position, col.transform.position);
                    if (!hasTriggeredTransition && distToPlayer <= portalTriggerRadius && !string.IsNullOrEmpty(nextLevelSceneName))
                    {
                        hasTriggeredTransition = true;
                        SceneManager.LoadScene(nextLevelSceneName);
                    }
                    continue; // skip all pull/trap physics below entirely
                }

                if (!isCageMode)
                {
                    if (trappedPlayerMovement == null)
                        trappedPlayerMovement = col.GetComponent<MonoBehaviour>(); 

                    if (trappedPlayerMovement != null)
                        trappedPlayerMovement.enabled = false;
                }

                // THE DASH CHECK: High speed bypasses the wall entirely.
                if (playerRb.linearVelocity.magnitude > escapeVelocityThreshold)
                {
                    if (isPortalMode && !hasTriggeredTransition && !string.IsNullOrEmpty(nextLevelSceneName))
                    {
                        hasTriggeredTransition = true;
                        SceneManager.LoadScene(nextLevelSceneName);
                    }
                    continue;
                }

                isPlayerBeingPulled = true;

                Vector2 dirToCenter = (Vector2)transform.position - (Vector2)col.transform.position;
                float distance = dirToCenter.magnitude;
                Vector2 inward = (distance > 0.0001f) ? (dirToCenter / distance) : Vector2.zero;

                if (isCageMode)
                {
                    // --- CAGE WALL BEHAVIOR ---
                    // The boundary is now exactly the edge of the visible circle
                    float cageEdge = visualRadius; 
                    
                    if (distance > cageEdge)
                    {
                        // 1. Physically snap them back to the visible line
                        col.transform.position = (Vector2)transform.position - (inward * cageEdge);
                        
                        // 2. Kill outward velocity so they don't slide outward weirdly, 
                        // but let them keep running sideways along the wall
                        Vector2 vel = playerRb.linearVelocity;
                        float radialSpeed = Vector2.Dot(vel, inward);
                        if (radialSpeed < 0f) // Moving away from center
                        {
                            playerRb.linearVelocity = vel - (inward * radialSpeed);
                        }
                    }
                }
                else
                {
                    // --- NORMAL BLACK HOLE BEHAVIOR ---
                    // --- NORMAL BLACK HOLE BEHAVIOR ---
                    if (distance <= eventHorizonRadius)
                    {
                        playerRb.linearVelocity = Vector2.zero;
                        col.transform.position = transform.position;
                        isPlayerTrapped = true; 
                        continue;
                    }

                    Vector2 tangent = new Vector2(-inward.y, inward.x);
                    Vector2 vel = playerRb.linearVelocity;
                    float radialSpeed = Vector2.Dot(vel, inward);    
                    float tangentialSpeed = Vector2.Dot(vel, tangent);

                    float bleed = Mathf.Clamp01(Time.fixedDeltaTime * captureFriction);
                    radialSpeed = Mathf.Lerp(radialSpeed, 0f, bleed);
                    tangentialSpeed = Mathf.Lerp(tangentialSpeed, 0f, Mathf.Clamp01(Time.fixedDeltaTime * tangentialDamping));

                    if (radialSpeed < 0f)
                        radialSpeed = Mathf.Lerp(radialSpeed, 0f, Mathf.Clamp01(Time.fixedDeltaTime * radialDamping));

                    float pullMultiplier = 1f - (distance / effectRadius);
                    
                    // --- THE FIX ---
                    // 1. Force the multiplier to stay at least at 10% so the edge isn't a dead-zone
                    pullMultiplier = Mathf.Max(pullMultiplier, 0.1f); 
                    
                    radialSpeed += maxPullStrength * pullMultiplier * Time.fixedDeltaTime;
                    
                    // 2. Remove the multiplier from minSinkSpeed so they are forced inward
                    radialSpeed = Mathf.Max(radialSpeed, minSinkSpeed);

                    float maxStepSpeed = (distance - eventHorizonRadius * 0.5f) / Time.fixedDeltaTime;
                    radialSpeed = Mathf.Min(radialSpeed, Mathf.Max(maxStepSpeed, 0f));

                    playerRb.linearVelocity = inward * radialSpeed + tangent * tangentialSpeed;
                }
            }
        }

        if (!foundPlayer && trappedPlayerMovement != null)
        {
            trappedPlayerMovement.enabled = true;
            trappedPlayerMovement = null;
        }

        HandleAlarmAudio();
    }

    void HandleAlarmAudio()
    {
        if (alarmAudioSource == null) return; 

        if (isPlayerBeingPulled && !wasPlayerBeingPulled)
        {
            if (alarmSirenClip != null)
            {
                alarmAudioSource.clip = alarmSirenClip;
                alarmAudioSource.loop = true; 
                alarmAudioSource.Play();
            }
        }
        else if (!isPlayerBeingPulled && wasPlayerBeingPulled)
        {
            alarmAudioSource.Stop();
        }

        wasPlayerBeingPulled = isPlayerBeingPulled;
    }

    void DrawJaggedCircle()
    {
        float currentRotSpeed = isPlayerTrapped ? rotationSpeed * alarmSpinMultiplier : rotationSpeed;
        float currentPulseMag = isPlayerTrapped ? pulseMagnitude * alarmPulseMultiplier : pulseMagnitude;
        float baseNoise = isPlayerTrapped ? alarmNoiseStrength : 0.3f;

        currentRotation += currentRotSpeed * Time.deltaTime;
        float currentPulse = visualRadius + Mathf.Sin(Time.time * pulseSpeed) * currentPulseMag;

        for (int i = 0; i < linePoints; i++)
        {
            float angle = (i * (360f / linePoints) + currentRotation) * Mathf.Deg2Rad;
            float noise = (i % 2 == 0) ? baseNoise : -baseNoise;

            float x = Mathf.Cos(angle) * (currentPulse + noise);
            float y = Mathf.Sin(angle) * (currentPulse + noise);

            lr.SetPosition(i, new Vector3(x, y, 0) + transform.position);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, effectRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, eventHorizonRadius);
    }
}