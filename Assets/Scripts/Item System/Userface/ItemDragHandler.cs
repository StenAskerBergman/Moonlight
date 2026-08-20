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
        AudioManager = AudioManager.Instance ?? FindObjectOfType<AudioManager>();

        // Get UnitDrag
        unitDrag = FindObjectOfType<UnitDrag>();

        // ItemStack component is on the same GameObject
        itemStack = GetComponent<ItemStack>();

        // Get the component, or add it if it's not present
        rectTransform = GetComponent<RectTransform>() 
          ?? gameObject.AddComponent<RectTransform>();
        
        canvasGroup = GetComponent<CanvasGroup>()
        ?? gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        // Set: Original Position
        originalPosition = rectTransform.anchoredPosition;
        
        // Adding: Visual Effect 
        if (canvasGroup != null)
        {
            canvasGroup.alpha = .6f;
            canvasGroup.blocksRaycasts = false;
        }

        if (unitDrag != null)
        {
            unitDrag.isHolding = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Set: Visual Effect
        if (unitDrag != null)
        {
            unitDrag.isHolding = true;
        }

        // Adding: Mouse Movement
        float scale = (canvas != null && canvas.scaleFactor > 0) ? canvas.scaleFactor : 1f;
        rectTransform.anchoredPosition += eventData.delta / scale;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Release: Visual Effect
        if (unitDrag != null)
        {
            unitDrag.isHolding = false;
        }

        // Always restore the dragged element's anchored position to its slot origin
        rectTransform.anchoredPosition = originalPosition;

        // Restore canvas group properties
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        // Handle world/ocean drops if not dropped on a UI slot
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            string tag = eventData.pointerCurrentRaycast.gameObject.tag;
            if (tag == "Ocean")
            {
                HandleOceanDrop();
            }
        }
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
                    Debug.LogError($"<color=red>ItemDragHandler: Unknown tag: {tag}</color>");
                    break;
            }
        }
    }

    private void HandleOceanDrop()
    {
        // Logic for dropping the item into the ocean
        Debug.Log("<color=lightblue>ItemDragHandler: </color><color=yellow>Dropped item into the ocean.</color>");
        if (AudioManager != null && AudioManager.DropIntoSea != null)
        {
            AudioManager.PlaySound(AudioManager.DropIntoSea);
        }
    }

    private void HandlePlayerDrop(PointerEventData eventData)
    {
        // Logic for dropping the item into another slot
        Debug.Log("<color=lightblue>ItemDragHandler: </color><color=green>Placed item into another slot.</color>");
    }
}

// End - ItemDragHandler.cs

