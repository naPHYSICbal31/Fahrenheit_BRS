using UnityEngine;

// Implemented by any enemy that can be grabbed by the player's Solar Pull
// overdrive ability and dragged into the sun.
public interface ISolarPullable
{
    Rigidbody2D Rb2D { get; }
    bool IsBeingPulled { get; set; }
    void KillBySolarPull();
}