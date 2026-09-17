using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Body temperature readout. Orbiting a star heats the player up; drifting alone in
// open space cools them down.
// Drives a Slider whose HANDLE is the black pointer (Fill Area disabled) - Slider already
// clamps the handle inside the track, so there's no position math here.
[RequireComponent(typeof(AudioSource))] // needed to play the freeze/overheat touch SFX
public class TemperatureBar : MonoBehaviour
{
    [Header("Range (degrees)")]
    public float minTemp = -125f; // left terminal
    public float maxTemp = 150f;  // right terminal
    public float startTemp = 25f; // where the pointer sits on spawn

    [Header("Rates (degrees per second)")]
    public float heatRate = 25f; // while captured by an OrbitZone
    public float coolRate = 25f; // while adrift in open space

    [Header("Player")]
    public PlayerController player; // auto-found by the "Player" tag if left empty

    [Header("UI")]
    public Slider bar;            // Interactable OFF, Min 0 / Max 1
    public TMP_Text currentLabel; // child of the Handle, so it travels with the pointer
    public TMP_Text minLabel;     // static label at the left end
    public TMP_Text maxLabel;     // static label at the right end

    [Header("Threshold Touch SFX")]
    [Tooltip("Played once at the exact moment the freeze SOUND threshold is crossed (see Freeze Sound Offset below). Does NOT play again while you stay frozen, and does NOT play when thawing back out.")]
    public AudioClip freezeTouchSound;
    [Tooltip("Played once at the exact moment the overheat SOUND threshold is crossed (see Overheat Sound Offset below). Does NOT play again while you stay overheated, and does NOT play when cooling back down.")]
    public AudioClip overheatTouchSound;
    [Range(0f, 1f)] public float touchSoundVolume = 1f;

    [Tooltip("NEW: degrees colder than freezeAt the temperature must reach before the freeze sound plays - lets the sound lag behind the visual freeze effect (which still triggers exactly at freezeAt). 0 = sound plays at the same instant as the visual effect.")]
    public float freezeSoundOffset = 10f; // NEW
    [Tooltip("NEW: degrees hotter than overheatAt the temperature must reach before the overheat sound plays - same idea as Freeze Sound Offset, on the hot end. 0 = sound plays at the same instant as the visual effect.")]
    public float overheatSoundOffset = 10f; // NEW

    // NEW: the actual temperature the sound waits for, derived from the visual threshold plus
    // its offset. Freezing sound threshold sits further BELOW freezeAt (colder); overheat sound
    // threshold sits further ABOVE overheatAt (hotter) - so both sounds trigger strictly after
    // their corresponding visual effect has already kicked in, never before or at the same point
    // unless the offset is 0.
    public float FreezeSoundThreshold => freezeAt - freezeSoundOffset;
    public float OverheatSoundThreshold => overheatAt + overheatSoundOffset;

    private AudioSource sfxSource;
    private bool wasFreezingForSound; // NEW: tracks the sound's own threshold, separate from IsFreezing
    private bool wasOverheatingForSound; // NEW: tracks the sound's own threshold, separate from IsOverheating

    public float Temperature { get; private set; }
    public float Normalized => Mathf.InverseLerp(minTemp, maxTemp, Temperature);

    [Header("Perk Thresholds")]
    public float overheatAt = 120f; // at or above this, the plasma bomb can be pulled
    public float freezeAt = -95f;   // at or below this, the cryo snowball can be pulled

    // Read by PlasmaTetherSystem to gate which weapon you can pull. These are bands, not the
    // exact terminals - the gap to minTemp/maxTemp is your grace window after leaving orbit.
    public bool IsFreezing => Temperature <= freezeAt;
    public bool IsOverheating => Temperature >= overheatAt;

    void Start()
    {
        Temperature = startTemp;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.GetComponent<PlayerController>();
        }

        if (minLabel != null) minLabel.text = $"{minTemp:0}°";
        if (maxLabel != null) maxLabel.text = $"{maxTemp:0}°";

        sfxSource = GetComponent<AudioSource>();
        // CHANGED: seed against the sound's own threshold, not IsFreezing/IsOverheating,
        // so starting already past the (further-out) sound threshold doesn't fire a false edge.
        wasFreezingForSound = Temperature <= FreezeSoundThreshold;
        wasOverheatingForSound = Temperature >= OverheatSoundThreshold;

        PushToUI();
    }

    void Update()
    {
        // OrbitZone owns this flag: set on capture, cleared on release. Any orbit counts,
        // not just the sun's - every OrbitZone sets the same flag.
        bool inOrbit = player != null && player.isOrbiting;

        Temperature += (inOrbit ? heatRate : -coolRate) * Time.deltaTime;
        Temperature = Mathf.Clamp(Temperature, minTemp, maxTemp);

        CheckThresholdTouchSounds();

        PushToUI();
    }

    // CHANGED: now checks against FreezeSoundThreshold/OverheatSoundThreshold instead of
    // IsFreezing/IsOverheating directly, so the sound only fires once you're offset degrees
    // past the point the visual effect already turned on. Leaving the state (thawing/cooling
    // back down, checked against the same offset threshold) is still the true->false edge and
    // is intentionally ignored - nothing plays there.
    void CheckThresholdTouchSounds()
    {
        bool isFreezingForSoundNow = Temperature <= FreezeSoundThreshold;
        bool isOverheatingForSoundNow = Temperature >= OverheatSoundThreshold;

        if (isFreezingForSoundNow && !wasFreezingForSound && freezeTouchSound != null)
        {
            sfxSource.PlayOneShot(freezeTouchSound, touchSoundVolume);
        }

        if (isOverheatingForSoundNow && !wasOverheatingForSound && overheatTouchSound != null)
        {
            sfxSource.PlayOneShot(overheatTouchSound, touchSoundVolume);
        }

        wasFreezingForSound = isFreezingForSoundNow;
        wasOverheatingForSound = isOverheatingForSoundNow;
    }

    // Lets other systems (EnemyArrow, BlasterEnemy, etc.) nudge the temperature by a
    // flat amount on an event - e.g. an arrow hit cooling you down, or a Blaster explosion
    // heating you up - independent of the continuous orbit heat/cool-in-open-space rates above.
    // Positive amount heats, negative cools. Always clamped to [minTemp, maxTemp] and immediately
    // pushed to the UI so the pointer jumps right away instead of waiting for the next Update tick.
    public void AdjustTemperature(float amount)
    {
        Temperature = Mathf.Clamp(Temperature + amount, minTemp, maxTemp);
        CheckThresholdTouchSounds(); // an instant jump (e.g. a Blaster hit) can also cross a sound threshold on its own, not just the continuous drift in Update - check here too
        PushToUI();
    }

    void PushToUI()
    {
        if (bar != null) bar.value = Normalized;
        if (currentLabel != null) currentLabel.text = $"{Temperature:0}°";
    }
}