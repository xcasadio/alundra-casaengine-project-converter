namespace Alundra.Entities;

/// <summary>Convenience predicates over <see cref="EntityStatus"/>.</summary>
public static class EntityStatusExtensions
{
    /// <summary>
    /// True for <see cref="EntityStatus.Normal"/> and <see cref="EntityStatus.Deactivated"/>, the two
    /// statuses that make an entity simulated, collidable and drawn. Transliterated from the original
    /// unsigned trick <c>(uint)(status - 2) &lt; 2</c>.
    /// </summary>
    public static bool IsActive(this EntityStatus status)
        => status is EntityStatus.Normal or EntityStatus.Deactivated;
}
