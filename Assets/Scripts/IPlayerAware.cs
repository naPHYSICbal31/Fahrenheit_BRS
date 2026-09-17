using UnityEngine;

// Implement this on any enemy that needs a runtime reference to the player.
// EnemySpawner calls SetPlayer() right after Instantiate() so spawned enemies
// get a working reference even though the prefab ASSET itself can never store
// one (a prefab can't hold a reference to a scene-only object like the Player).
public interface IPlayerAware
{
    void SetPlayer(Transform playerTransform);
}