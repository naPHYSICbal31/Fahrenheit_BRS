public interface IKillable
{
    // Triggers this enemy's full death sequence (VFX, sound, shard drop, destroy).
    // Anything that wants to kill an enemy — bullets, chain lightning, AoE, etc. —
    // should call this instead of destroying the GameObject directly.
    void Die();
}