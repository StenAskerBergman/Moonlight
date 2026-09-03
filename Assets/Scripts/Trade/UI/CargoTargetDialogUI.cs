using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog for configuring an Anno 2070-style cargo target on a station:
/// selects the commodity and sets the desired post-station ship inventory amount.
/// </summary>
public class CargoTargetDialogUI : MonoBehaviour
{
    [Header("Dialog Elements")]
    [SerializeField] private GameObject rootModal;
    [SerializeField] private Text titleText;
    [SerializeField] private Text selectedItemNameText;
    [SerializeField] private Image selectedItemIcon;
    [SerializeField] private Slider amountSlider;
    [SerializeField] private Text amountValueText;
    [SerializeField] private Text explanationText;

    [Header("Item Grid / Content")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button deleteTargetButton;

    private ItemData currentItem;
    private int currentDesiredAmount = 40;
    private Action<ItemData, int> onConfirmCallback;
    private Action onDeleteCallback;

    private readonly List<GameObject> spawnedItemButtons = new List<GameObject>();

    private void Awake()
    {
        if (rootModal == null) rootModal = gameObject;

        if (amountSlider != null)
        {
            amountSlider.wholeNumbers = true;
            amountSlider.minValue = 0;
            amountSlider.maxValue = 60;
            amountSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        if (deleteTargetButton != null) deleteTargetButton.onClick.AddListener(OnDeleteClicked);
    }

    /// <summary>
    /// Opens the dialog to edit or create a cargo target.
    /// </summary>
    public void Open(ItemData existingItem, int existingAmount, Action<ItemData, int> onConfirm, Action onDelete = null)
    {
        onConfirmCallback = onConfirm;
        onDeleteCallback = onDelete;
        currentItem = existingItem;
        currentDesiredAmount = existingAmount >= 0 ? existingAmount : 40;

        if (rootModal != null) rootModal.SetActive(true);
        if (deleteTargetButton != null) deleteTargetButton.gameObject.SetActive(onDelete != null);

        PopulateCommodityList();
        UpdateSelectionUI();
    }

    public void Close()
    {
        if (rootModal != null) rootModal.SetActive(false);
        onConfirmCallback = null;
        onDeleteCallback = null;
    }

    private void PopulateCommodityList()
    {
        if (itemContainer == null) return;

        foreach (var btn in spawnedItemButtons)
        {
            if (btn != null) Destroy(btn);
        }
        spawnedItemButtons.Clear();

        // Retrieve commodities from Resources
        var allItems = Resources.LoadAll<ItemData>("");
        var commodityList = new List<ItemData>();

        foreach (var item in allItems)
        {
            if (item != null && !item.isSocketable)
            {
                commodityList.Add(item);
            }
        }

        // Default item if none selected
        if (currentItem == null && commodityList.Count > 0)
        {
            currentItem = commodityList[0];
        }

        // Spawn buttons for each commodity
        foreach (var item in commodityList)
        {
            GameObject btnObj = new GameObject($"Item_{item.name}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(itemContainer, false);

            var rt = btnObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(44, 44);

            var img = btnObj.GetComponent<Image>();
            img.color = new Color(0.12f, 0.24f, 0.38f, 1f);

            // Icon child
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(btnObj.transform, false);
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.sizeDelta = new Vector2(-6, -6);

            var iconImg = iconObj.GetComponent<Image>();
            if (item.Icon != null)
            {
                iconImg.sprite = item.Icon;
                iconImg.color = Color.white;
            }
            else
            {
                iconImg.color = new Color(0.3f, 0.6f, 0.8f, 0.8f);
            }

            var btn = btnObj.GetComponent<Button>();
            ItemData capturedItem = item;
            btn.onClick.AddListener(() =>
            {
                currentItem = capturedItem;
                UpdateSelectionUI();
            });

            spawnedItemButtons.Add(btnObj);
        }
    }

    private void UpdateSelectionUI()
    {
        if (currentItem != null)
        {
            if (selectedItemNameText != null)
            {
                selectedItemNameText.text = !string.IsNullOrEmpty(currentItem.displayName)
                    ? currentItem.displayName
                    : currentItem.name;
            }

            if (selectedItemIcon != null)
            {
                selectedItemIcon.enabled = currentItem.Icon != null;
                selectedItemIcon.sprite = currentItem.Icon;
            }
        }
        else
        {
            if (selectedItemNameText != null) selectedItemNameText.text = "Select Commodity";
            if (selectedItemIcon != null) selectedItemIcon.enabled = false;
        }

        if (amountSlider != null)
        {
            amountSlider.value = currentDesiredAmount;
        }

        if (amountValueText != null)
        {
            amountValueText.text = currentDesiredAmount.ToString();
        }

        if (explanationText != null)
        {
            explanationText.text = $"Desired Ship Amount: {currentDesiredAmount}\n" +
                $"• If ship has < {currentDesiredAmount}: Load towards {currentDesiredAmount}\n" +
                $"• If ship has > {currentDesiredAmount}: Unload down to {currentDesiredAmount}\n" +
                $"• If ship has = {currentDesiredAmount}: Hold unchanged";
        }
    }

    private void OnSliderChanged(float val)
    {
        currentDesiredAmount = Mathf.RoundToInt(val);
        UpdateSelectionUI();
    }

    private void OnConfirmClicked()
    {
        if (currentItem != null && onConfirmCallback != null)
        {
            onConfirmCallback.Invoke(currentItem, currentDesiredAmount);
        }
        Close();
    }

    private void OnDeleteClicked()
    {
        onDeleteCallback?.Invoke();
        Close();
    }
}
