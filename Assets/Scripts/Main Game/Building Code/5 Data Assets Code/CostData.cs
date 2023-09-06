using UnityEngine;

[CreateAssetMenu(fileName = "New Cost Data", menuName = "Cost Data")]
public class CostData : ScriptableObject
{
    // Resource Material Good Array
    public ItemEnums.ResourceType[] resourceArray;
    public ItemEnums.MaterialType[] materialArray;
    public ItemEnums.GoodType[] goodArray;

    // Resource Material Good Type
    public ItemEnums.ResourceType resourceType;
    public ItemEnums.MaterialType materialType;
    public ItemEnums.GoodType goodType;

    // Currancy Cost
    public int price;
    public int cost;
    public int expense;

    // The cost of various building material
    public int[] costResource;

    [SerializeField] public int costResource1;
    [SerializeField] public int costResource2;
    [SerializeField] public int costResource3;
    [SerializeField] public int costResource4;

    [SerializeField] public int costResource5;
    [SerializeField] public int costResource6;
    [SerializeField] public int costResource7;
    [SerializeField] public int costResource8;
    [SerializeField] public int costResource9;
    [SerializeField] public int costResource10;
}
