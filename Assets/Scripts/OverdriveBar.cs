using UnityEngine;
using UnityEngine.UI;

public class OverdriveBar : MonoBehaviour
{
    public int maxCharge = 100;
    private int currentCharge = 0;

    public Slider overdriveSlider;
    public bool IsFull => currentCharge >= maxCharge;


    [Header("Level Portal")]
    public GameObject portal; // the GravityWell instance guarding the level exit - starts hidden, revealed at full charge

    void Start()
    {
        if (overdriveSlider != null) overdriveSlider.value = 0f;
        if (portal != null) portal.SetActive(false);
    }

    public void AddCharge(int amount)
    {
        bool wasFull = currentCharge >= maxCharge;
        currentCharge = Mathf.Min(maxCharge, currentCharge + amount);
        if (overdriveSlider != null) overdriveSlider.value = (float)currentCharge / maxCharge;

        if (!wasFull && currentCharge >= maxCharge && portal != null)
        {
            portal.SetActive(true);
        }
    }

    // Spends a fixed amount of charge (e.g. the chain lightning cost) if affordable.
    // Does NOT touch the portal - once opened at full charge, it stays open for the level.
    public bool TrySpend(int cost)
    {
        if (currentCharge < cost) return false;
        currentCharge -= cost;
        if (overdriveSlider != null) overdriveSlider.value = (float)currentCharge / maxCharge;
        return true;
    }
}
