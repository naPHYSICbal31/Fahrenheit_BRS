using UnityEngine;

// Implement this on any pivot-layer object that should react/get destroyed
// when the player bashes off of it — as opposed to a permanent world pivot,
// which should NOT implement this and will just keep sitting there.
public interface IBashablePivot
{
    // Called by PivotBash the instant the bash launch fires (see LaunchBash()).
    // 'basher' is the PivotBash component, in case you want to read its
    // velocity/direction/position for VFX, knockback, etc.
    void OnBashed(PivotBash basher);

    // Called by PivotBash when this becomes (true) or stops being (false) the
    // thing currently being aimed at / reeled toward. Bullets use this to avoid
    // self-destructing when the player's reel-in motion overlaps their collider
    // — that overlap isn't a "hit", it's the player grabbing the pivot.
    void SetTargeted(bool isTargeted);
}