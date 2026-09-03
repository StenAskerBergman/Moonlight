using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attaches to production nodes and selectors to dispatch hover events for contextual tooltips.
/// </summary>
public sealed class ProductionTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string Title;
    public string Description;

    public static event Action<string, string, RectTransform> OnTooltipShow;
    public static event Action OnTooltipHide;

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnTooltipShow?.Invoke(Title, Description, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnTooltipHide?.Invoke();
    }

    private void OnDisable()
    {
        OnTooltipHide?.Invoke();
    }
}
