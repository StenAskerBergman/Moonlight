using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class IslandSwitcher : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Hover Panel Target Trigger to be enabled / disabled.")]
    public GameObject InfoPanel;

    [Tooltip("Target Trigger for enabling / disabling the Panel.")]
    public Text TargetText;

    private Island currentIsland;
    private IslandPower islandPower;
    private IslandEcology islandEcology;
    private Inventory islandInventory;

    public EcologyUIManager ecologyUIManager;
    public PowerUIManager powerUIManager;

    private void Start()
    {
        // Subscribe to events related to island switching
        IslandManager.instance.OnPlayerHoverIsland += OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland += OnCurrentIslandChanged;

        // Initialize other variables and UI elements

        // Initially hide all info panels
        if (InfoPanel != null) InfoPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // Unsubscribe from events on script destruction
        IslandManager.instance.OnPlayerHoverIsland -= OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland -= OnCurrentIslandChanged;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TargetText == null) return;

        // Check if the pointer is entering the target panel
        if (eventData.pointerEnter == TargetText.gameObject)
        {
            ShowInfoPanel();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TargetText == null) return;

        // Check if the pointer is exiting the target panel
        if (eventData.pointerEnter == TargetText.gameObject)
        {
            HideInfoPanel();
        }
    }

    private void OnCurrentIslandChanged(Island island)
    {
        if (island == null)
        {
            Debug.Log("Island = Null");
            return;
        }

        // Update variables and UI elements based on the new island
        currentIsland = island;
        islandPower = island.GetComponent<IslandPower>();
        islandEcology = island.GetComponent<IslandEcology>();
        islandInventory = island.GetComponent<Inventory>();

        ecologyUIManager.OnCurrentIslandChanged(island); // Is a Null Ref
        powerUIManager.OnCurrentIslandChanged(island); // Is probably a Null Ref
    }

    // Implement methods to update UI elements based on the current island

    private void ShowInfoPanel()
    {
        InfoPanel.SetActive(true);
    }

    private void HideInfoPanel()
    {
        InfoPanel.SetActive(false);
    }
}
