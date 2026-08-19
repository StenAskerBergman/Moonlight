public interface IItemManagement
{
    void AddItem(ItemData item, int quantity);
    void RemoveItem(ItemData item, int quantity);
}