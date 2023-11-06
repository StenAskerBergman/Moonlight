using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HoverManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject infoPanel;
    public Text targetText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetText == null) return;
        if (eventData.pointerEnter == targetText.gameObject)
        {
            ShowInfoPanel();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetText == null) return;
        if (eventData.pointerEnter == targetText.gameObject)
        {
            HideInfoPanel();
        }
    }

    public void ShowInfoPanel()
    {
        infoPanel.SetActive(true);
    }

    public void HideInfoPanel()
    {
        infoPanel.SetActive(false);
    }
}
