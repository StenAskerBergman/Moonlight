/// <summary>
/// A unit that can leave the ground for an altitude band, and come back down.
/// The mirror of IDiveable.
/// </summary>
public interface ILiftable
{
    /// <summary>Climb to the next altitude band. False if there is no sky to climb into.</summary>
    bool LiftOff();

    /// <summary>Descend to the apron. False if there is nowhere below to land.</summary>
    bool Land();
}
