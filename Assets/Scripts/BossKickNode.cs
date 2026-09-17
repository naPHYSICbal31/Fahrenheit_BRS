using UnityEngine;

public class BossKickNode : MonoBehaviour, IBashablePivot
{
    [Header("References")]
    public BossBrain boss; // Link the main boss script here in the Inspector

    // This is required by the IBashablePivot interface.
    // It triggers when the player right-clicks the node.
    public void SetTargeted(bool isTargeted)
    {
        // Optional: You could make the node pulse or change color when aimed at!
    }

    // This triggers the exact moment the player bashes off the node.
    public void OnBashed(PivotBash player)
    {
        if (boss != null)
        {
            // Pass the player's launch direction so the boss flies the SAME way!
            boss.TakeFinisherDamage(player.AimDirection);
        }
        
        // Hide this node immediately after it gets hit
        gameObject.SetActive(false);
    }
}