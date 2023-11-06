using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string HoverName; // Just for the inspector to help identify the trigger

    [Tooltip("Hover Panel Target Trigger to be enabled / disabled.")]
    public GameObject InfoPanel;

    [Tooltip("Target Trigger for enabling / disabling the Panel.")]
    public Text TargetText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Check if the pointer is entering the bank info panel
        if (eventData.pointerEnter == TargetText.gameObject)
        {
            ShowInfoPanel();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Check if the pointer is exiting the bank info panel
        if (eventData.pointerEnter == TargetText.gameObject)
        {
            HideInfoPanel();
        }
    }

    #region Start / Update

    private void Start()
    {
        // Initially hide all info panels
        if (InfoPanel != null) InfoPanel.SetActive(false);
    }

    #endregion

    #region  Method: Show / Hide

    // Info Panel

    // Methods to show and hide the info panel
    public void ShowInfoPanel()
        {
            InfoPanel.SetActive(true);
        }

        public void HideInfoPanel()
        {
            InfoPanel.SetActive(false);
        }

    #endregion
}
