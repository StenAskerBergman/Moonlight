// 
using UnityEngine;
using UnityEngine.EventSystems;

public class ItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform rectTransform;
    public CanvasGroup canvasGroup;
    public Canvas canvas;
    public ItemStack itemStack; // Reference to ItemStack instead of InventoryItem

    private bool isDraggable;
    private bool isDragging;
    public Vector2 originalPosition { private set; get; }
    public Vector2 newPosition { set; get; }

    private AudioManager AudioManager;
    private UnitDrag unitDrag;

    // General Idea:
    // Attach this script to your item slot prefab and ensure that
    // the item slot has a CanvasGroup and RectTransform component.

    private void Awake()
    {
        // Get AudioManager
        AudioManager AudioManager = FindObjectOfType<AudioManager>();

        // Get UnitDrag
        unitDrag = FindObjectOfType<UnitDrag>();

        // I assume InventoryItem component is on the same GameObject
        itemStack = GetComponent<ItemStack>();

        // if ItemStack is not present adding it to the gameObject causes a Null Ref Error + Vfx glitch

        // Get the component, or add it if it's not present
        rectTransform = GetComponent<RectTransform>() 
          ?? gameObject.AddComponent<RectTransform>();
        
        canvasGroup = GetComponent<CanvasGroup>()
        ?? gameObject.AddComponent<CanvasGroup>();

        canvas = null ?? FindObjectOfType<Canvas>();
    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        // Set: Original Position
        originalPosition = rectTransform.anchoredPosition;
        
        // Adding: Visual Effect 
        canvasGroup.alpha = .6f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Set: Visual Effect
        unitDrag.isHolding = true;

        // Adding: Mouse Movement
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;

    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Create: Visual Effect
        unitDrag.isHolding = false;

        GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
        if (dropTarget != null && dropTarget.CompareTag("ItemSlotTag")) // Replace with your actual tag for ItemSlot
        {
            ItemSlot slot = dropTarget.GetComponent<ItemSlot>();
            if (slot != null)
            {
                // Notify the slot that an item has been dropped onto it
                slot.ReceiveDroppedItem(itemStack);
            }
        }
        else
        {
            // If not dropped on a valid slot, revert to original position
            rectTransform.anchoredPosition = originalPosition;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }


    private void HandleDrop(PointerEventData eventData)
    {

        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            string tag = eventData.pointerCurrentRaycast.gameObject.tag;
            switch (tag)
            {
                case "Userface": // Userface 
                    HandlePlayerDrop(eventData);
                    break;

                case "ItemSlot": // ItemSlot
                    HandlePlayerDrop(eventData);
                    break;

                case "Ocean": // Ocean 
                    HandleOceanDrop();
                    break;

                default:
                    // Optional: Handle other cases
                    Debug.LogError("Unknown tag: " + tag);
                    break;
            }
        }
    }
    private void HandleOceanDrop()
    {
        // Logic for dropping the item into the ocean
        Debug.Log("Dropped item into the ocean.");
        // Play ocean drop sound, spawn crate, etc.
    }

    private void HandlePlayerDrop(PointerEventData eventData)
    {
        // Logic for dropping the item into another slot
        Debug.Log("Placed item into another slot.");
        // Play slot drop sound, etc.
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Logic for receiving a dropped item
        Debug.Log("OnDrop");
        // Optional: Handle the item being dropped onto this slot
    }
}

// End - ItemDragHandler.cs

