using System.Collections;
using UnityEngine;

public class LightningBolt : MonoBehaviour
{
    public LineRenderer glowLine;   // thick cyan (this object)
    public LineRenderer coreLine;   // thin white (child)

    public int segments = 12;
    public float jitter = 0.08f;
    public float duration = 0.18f;

    public float glowWidth = 0.18f;
    public float coreWidth = 0.05f;

    public bool animateCrackle = true;

    private Vector2 from, to;

    void Awake()
    {
        if (glowLine == null) glowLine = GetComponent<LineRenderer>();

        glowLine.positionCount = segments + 1;
        glowLine.startWidth = glowLine.endWidth = glowWidth;

        if (coreLine != null)
        {
            coreLine.positionCount = segments + 1;
            coreLine.startWidth = coreLine.endWidth = coreWidth;
        }
    }

    public void Draw(Vector2 a, Vector2 b)
    {
        from = a; to = b;
        GeneratePoints();
        StartCoroutine(Live());
    }

    private void GeneratePoints()
    {
        Vector2 dir = to - from;
        float len = dir.magnitude;
        Vector2 normal = new Vector2(-dir.y, dir.x).normalized;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector2 p = Vector2.Lerp(from, to, t);

            if (i != 0 && i != segments)
            {
                float taper = Mathf.Sin(t * Mathf.PI);           // no wobble at ends
                p += normal * Random.Range(-jitter, jitter) * len * taper;
            }

            // SAME point on both lines — this is what makes the core sit inside the glow
            glowLine.SetPosition(i, p);
            if (coreLine != null) coreLine.SetPosition(i, p);
        }
    }

    private IEnumerator Live()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;

            // re-jitter each frame so it crackles instead of sitting still
            if (animateCrackle) GeneratePoints();

            float a = 1f - (t / duration);
            SetAlpha(glowLine, a);
            SetAlpha(coreLine, a);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void SetAlpha(LineRenderer lr, float a)
    {
        if (lr == null) return;
        Color s = lr.startColor, e = lr.endColor;
        lr.startColor = new Color(s.r, s.g, s.b, a);
        lr.endColor = new Color(e.r, e.g, e.b, a);
    }
}