using UnityEngine;

public class SpritePingPong : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 14f;

    SpriteRenderer sr;
    float timer;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (frames == null || frames.Length < 2) return;

        timer += Time.deltaTime * fps;

        int span = frames.Length - 1;
        int i = Mathf.RoundToInt(Mathf.PingPong(timer, span));

        sr.sprite = frames[i];
    }
}