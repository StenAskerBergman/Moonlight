/// <summary>
/// How a building is currently being called out to the player.
///
/// The states are exclusive - a building is the one you clicked, or it is one of the
/// buildings that relationship reaches, never both. Selected wins if something ever
/// tries to set them at once.
/// </summary>
public enum BuildingHighlight
{
    /// <summary>No overlay renderer. The building draws as its normal self.</summary>
    None,

    /// <summary>The clicked building. Moonlight/Highlights/BuildingSelectedBlue.</summary>
    Selected,

    /// <summary>Inside the selected building's influence. Moonlight/Highlights/BuildingInfluenceGreen.</summary>
    Influence
}
