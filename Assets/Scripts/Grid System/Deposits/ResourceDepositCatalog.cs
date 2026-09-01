using UnityEngine;

/// <summary>
/// Build-safe references to generated resource-deposit visuals. Keeping this catalog
/// in Resources lets procedurally created MapGrid instances resolve the same authored
/// prefabs without requiring references on every generated chunk.
/// </summary>
[CreateAssetMenu(fileName = "ResourceDepositCatalog", menuName = "Data/Terrain/Resource Deposit Catalog")]
public sealed class ResourceDepositCatalog : ScriptableObject
{
    private const string ResourceName = "ResourceDepositCatalog";
    private static ResourceDepositCatalog cached;

    [SerializeField] private GameObject crudeOilPrefab;

    public GameObject CrudeOilPrefab => crudeOilPrefab;

    public static ResourceDepositCatalog Load()
    {
        if (cached == null) cached = Resources.Load<ResourceDepositCatalog>(ResourceName);
        return cached;
    }
}
