using System.Collections;
using UnityEngine;

public class Blink : MonoBehaviour
{
    public Transform[] eyes;
    public Vector2 intervalRange = new Vector2(2.5f, 6f);
    public float closeDuration = 0.07f;
    public float closedRatio = 0.08f;
    [Range(0f, 0.3f)] public float doubleBlinkChance = 0.15f;

    Vector3[] baseScales;

    void Awake()
    {
        baseScales = new Vector3[eyes.Length];
        for (int i = 0; i < eyes.Length; i++)
            baseScales[i] = eyes[i].localScale;
    }

    void Start() => StartCoroutine(Loop());

    IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(intervalRange.x, intervalRange.y));
            yield return OneBlink();

            if (Random.value < doubleBlinkChance)
            {
                yield return new WaitForSeconds(0.12f);
                yield return OneBlink();
            }
        }
    }

    IEnumerator OneBlink()
    {
        SetRatio(closedRatio);
        yield return new WaitForSeconds(closeDuration);
        SetRatio(1f);
    }

    void SetRatio(float r)
    {
        for (int i = 0; i < eyes.Length; i++)
        {
            Vector3 s = baseScales[i];
            eyes[i].localScale = new Vector3(s.x, s.y * r, s.z);
        }
    }
}