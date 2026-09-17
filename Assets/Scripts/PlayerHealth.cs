using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("UI")]
    public Slider healthSlider; // drag the Healthbar/Slider object here

    private PivotBash bash;
    private bool isDead = false;

    void Start()
    {
        Debug.Log($"[PlayerHealth] Start() on {gameObject.name} (instance {GetInstanceID()}), maxHealth={maxHealth}");
        currentHealth = maxHealth;
        bash = GetComponent<PivotBash>();
        if (healthSlider != null) healthSlider.value = 1f;
    }

    public void TakeDamage(int amount)
    {
        Debug.Log($"TakeDamage called: amount={amount}, isDead={isDead}, invincible={(bash != null && bash.IsInvincible)}, healthSlider={(healthSlider != null ? "assigned" : "NULL")}");

        if (isDead) return;
        if (bash != null && bash.IsInvincible) return; // same invincibility window used by Bomb/BlasterEnemy

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (healthSlider != null) healthSlider.value = (float)currentHealth / maxHealth;

        if (currentHealth <= 0) Die();
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (healthSlider != null) healthSlider.value = (float)currentHealth / maxHealth;
    }

    void Die()
    {
        isDead = true;
        Debug.Log($"[PlayerHealth] Die() on {gameObject.name} (instance {GetInstanceID()})");
        FindFirstObjectByType<GameOverScreen>()?.Show();
    }
}
