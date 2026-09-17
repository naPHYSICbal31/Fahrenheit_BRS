using UnityEngine;

// Implement this on any enemy script that has its own death VFX prefab.
// EnemyOrbBullet (and anything else that destroys enemies) will look for this
// interface on whatever it destroys and play that enemy's own VFX, instead of
// a single VFX assigned on the destroyer itself.
public interface IHasDeathVfx
{
    GameObject DeathVfxPrefab { get; }
}