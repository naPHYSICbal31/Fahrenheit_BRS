using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverScreen : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text timerText;
    public float timeoutSeconds = 10f;
    public string startMenuSceneName = "Startmenu";
    public string gameSceneName = "ui adding";

    private float remaining;
    private bool active = false;

    public void Show()
    {
        Debug.Log($"[GameOverScreen] Show() called on {gameObject.name} (instance {GetInstanceID()}), timeScale was {Time.timeScale}");
        active = true;
        remaining = timeoutSeconds;
        Time.timeScale = 0f;
        panel.SetActive(true);
    }

    void Update()
    {
        if (!active) return;

        remaining -= Time.unscaledDeltaTime;
        if (timerText != null) timerText.text = Mathf.CeilToInt(remaining).ToString();

        if (remaining <= 0f) GoToStartMenu();
    }

    public void PlayAgain()
    {
        // Reload whatever level the player actually died in, not a hardcoded scene name -
        // so dying in Level 2 restarts Level 2, not Level 1.
        string current = SceneManager.GetActiveScene().name;
        Debug.Log($"[GameOverScreen] PlayAgain() called on {gameObject.name}, reloading current scene '{current}'");
        active = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(current);
    }

    public void GoToStartMenu()
    {
        Debug.Log($"[GameOverScreen] GoToStartMenu() called, loading scene '{startMenuSceneName}'");
        active = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(startMenuSceneName);
    }

    public void ExitGame()
    {
        Debug.Log($"[GameOverScreen] ExitGame() called on {gameObject.name}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
