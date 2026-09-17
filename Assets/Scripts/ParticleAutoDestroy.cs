using UnityEngine;

public class ParticleAutoDestroy : MonoBehaviour
{
    void Start()
    {
        float longest = 0f;
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            var m = ps.main;
            longest = Mathf.Max(longest, m.duration + m.startLifetime.constantMax);
        }
        Destroy(gameObject, longest + 0.1f);
    }
}