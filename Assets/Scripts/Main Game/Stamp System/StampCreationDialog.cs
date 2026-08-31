using TMPro;
using UnityEngine;

/// <summary>
/// Finalises a captured stamp before it is persisted. All UI references are
/// optional so the capture tool retains its existing default-name fallback while
/// the dialog prefab is being wired.
/// </summary>
public sealed class StampCreationDialog : MonoBehaviour
{
    [Header("Dialog")]
    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField categoryInput;
    [SerializeField] private TMP_Dropdown iconDropdown;

    private StampData pendingStamp;

    private void Awake()
    {
        SetVisible(false);
    }

    public void Open(StampData stamp)
    {
        if (stamp == null) return;

        pendingStamp = stamp;
        if (nameInput != null) nameInput.text = stamp.stampName;
        if (categoryInput != null) categoryInput.text = stamp.category;
        if (iconDropdown != null)
        {
            iconDropdown.value = Mathf.Clamp(
                stamp.iconIndex,
                0,
                Mathf.Max(0, iconDropdown.options.Count - 1));
        }

        SetVisible(true);
        if (nameInput != null)
        {
            nameInput.Select();
            nameInput.ActivateInputField();
        }
    }

    public void Confirm()
    {
        if (pendingStamp == null)
        {
            SetVisible(false);
            return;
        }

        if (nameInput != null && !string.IsNullOrWhiteSpace(nameInput.text))
        {
            pendingStamp.stampName = nameInput.text.Trim();
        }

        if (categoryInput != null && !string.IsNullOrWhiteSpace(categoryInput.text))
        {
            pendingStamp.category = categoryInput.text.Trim();
        }

        if (iconDropdown != null) pendingStamp.iconIndex = iconDropdown.value;

        if (StampLibrary.Instance != null)
        {
            StampLibrary.Instance.SaveStamp(pendingStamp);
        }
        else
        {
            Debug.LogWarning("[StampCreationDialog] No StampLibrary is active; the captured stamp was not saved.", this);
        }

        pendingStamp = null;
        SetVisible(false);
    }

    public void Cancel()
    {
        pendingStamp = null;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        GameObject target = dialogRoot != null ? dialogRoot : gameObject;
        if (target != gameObject || target.activeSelf != visible)
        {
            target.SetActive(visible);
        }
    }
}
