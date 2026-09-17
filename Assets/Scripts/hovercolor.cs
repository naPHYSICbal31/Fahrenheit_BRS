using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Attach this to the same GameObject as your TextMeshProUGUI (or a parent that
// covers its rect). The object needs a Graphic Raycaster in the scene (on the
// Canvas) and an EventSystem in the scene for pointer events to fire at all.
[RequireComponent(typeof(TextMeshProUGUI))]
public class TMPHoverColor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Colors (hex, e.g. #FFFFFF or FFFFFF - # optional, alpha optional as 8th/9th char)")]
    public string normalColorHex = "#FFFFFF";
    public string hoverColorHex = "#FFFF00";

    private Color normalColor;
    private Color hoverColor;

    [Header("Transition")]
    public bool smoothTransition = true;
    public float transitionSpeed = 8f; // higher = snappier

    private TextMeshProUGUI label;
    private Color targetColor;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();

        normalColor = ParseHex(normalColorHex, Color.white);
        hoverColor = ParseHex(hoverColorHex, Color.yellow);

        // If you'd rather keep whatever color is already set in the Inspector
        // on the text object as the "normal" state instead of the hex field,
        // uncomment the line below:
        // normalColor = label.color;

        label.color = normalColor;
        targetColor = normalColor;
    }

    // Accepts with or without a leading '#', and either RGB or RGBA hex (e.g. "FFFFFF" or "FFFFFF80").
    // Falls back to fallback color and logs a warning if the string can't be parsed.
    private Color ParseHex(string hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        string h = hex.StartsWith("#") ? hex : "#" + hex;

        if (ColorUtility.TryParseHtmlString(h, out Color parsed))
        {
            return parsed;
        }

        Debug.LogWarning($"TMPHoverColor: couldn't parse hex color '{hex}' on {gameObject.name}, using fallback.");
        return fallback;
    }

    void Update()
    {
        if (!smoothTransition) return;
        if (label.color != targetColor)
        {
            label.color = Color.Lerp(label.color, targetColor, transitionSpeed * Time.deltaTime);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (smoothTransition)
        {
            targetColor = hoverColor;
        }
        else
        {
            label.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (smoothTransition)
        {
            targetColor = normalColor;
        }
        else
        {
            label.color = normalColor;
        }
    }
}