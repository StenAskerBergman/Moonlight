/// <summary>
/// A unit that can move between the surface and the deep-sea layer.
/// The mirror of ILiftable.
/// </summary>
public interface IDiveable
{
    /// <summary>
    /// Dive to the deep layer. False when the water here is too shallow, or this
    /// hull is not rated to dive.
    /// </summary>
    bool Dive();

    /// <summary>Return to the surface. False if there is no surface layer above.</summary>
    bool Surface();
}
