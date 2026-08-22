using UnityEngine;

/// <summary>
/// The gameplay-facing façade for unit creation. Follows the project's Singleton 
/// pattern. Handles publishing lifecycle events to the GameEventBus.
/// </summary>
public class UnitService : MonoBehaviour
{
    public static UnitService Instance { get; private set; }

    [Tooltip("The catalogue mapping domains to concrete unit definitions.")]
    [SerializeField] private UnitCatalogue catalogue;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Requests the creation of a unit by its movement domain category.
    /// Publishes OnUnitRequested and OnUnitCreated events.
    /// </summary>
    public Unit RequestUnit(MoveType domain, Vector3 position)
    {
        if (catalogue == null)
        {
            Debug.LogError("UnitService: Catalogue is not assigned.");
            return null;
        }

        UnitDefinition def = catalogue.Resolve(domain);
        if (def == null) return null;

        GameEventBus.Publish(new OnUnitRequested(def, position));

        Unit unit = UnitFactory.Create(def, position, Quaternion.identity);
        if (unit != null)
        {
            GameEventBus.Publish(new OnUnitCreated(unit, def));
        }

        return unit;
    }
}
