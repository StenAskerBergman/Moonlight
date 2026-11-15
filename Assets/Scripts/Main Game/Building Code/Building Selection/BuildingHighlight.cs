/// <summary>
/// How a building is currently being called out to the player.
///
/// The states are exclusive - Priority: Selected > PublisherSupplier / SubscriberConsumer > Influence.
/// </summary>
public enum BuildingHighlight
{
    /// <summary>No overlay renderer. The building draws as its normal self.</summary>
    None,

    /// <summary>The clicked building. Moonlight/Highlights/BuildingSelectedBlue.</summary>
    Selected,

    /// <summary>Inside the selected building's influence. Moonlight/Highlights/BuildingInfluenceGreen.</summary>
    Influence,

    /// <summary>Produces a resource consumed by the selected building (Warm Gold/Amber).</summary>
    PublisherSupplier,

    /// <summary>Consumes a resource produced by the selected building (Mint/Cyan).</summary>
    SubscriberConsumer
}
