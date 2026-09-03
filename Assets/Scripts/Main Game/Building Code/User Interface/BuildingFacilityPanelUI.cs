using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anno 2070-style building selection GUI panel that displays when selecting
/// Ecobalance buildings (Mode A) or Production facilities (Mode B).
/// Subscribes to BuildingSelections.Instance.selectionChanged.
/// </summary>
public class BuildingFacilityPanelUI : MonoBehaviour
{
    public static BuildingFacilityPanelUI Instance { get; private set; }

    [Header("Root Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Header Elements")]
    [SerializeField] private Image headerIcon;
    [SerializeField] private TMP_Text categoryLabel;
    [SerializeField] private TMP_Text buildingNameLabel;

    [Header("Ecobalance Panel (Mode A)")]
    [SerializeField] private GameObject ecobalanceRoot;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image ecoProgressRing;
    [SerializeField] private TMP_Text ecoPercentageText;
    [SerializeField] private TMP_Text ecoEffectText;
    [SerializeField] private Image ecoEffectIcon;

    [Header("Production Panel (Mode B)")]
    [SerializeField] private GameObject productionRoot;
    [SerializeField] private CogWheelAnimator cogAnimator;
    [SerializeField] private Image prodProgressRing;
    [SerializeField] private TMP_Text prodPercentageText;
    [SerializeField] private Image inputIcon;
    [SerializeField] private TMP_Text inputAmountText;
    [SerializeField] private Image outputIcon;
    [SerializeField] private TMP_Text outputAmountText;
    [SerializeField] private Image outputFillBar;

    [Header("Bottom Status Bar")]
    [SerializeField] private Image creditsIcon;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private Image energyIcon;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private Image ecoIcon;
    [SerializeField] private TMP_Text ecoText;
    [SerializeField] private Image healthIcon;
    [SerializeField] private TMP_Text healthText;

    [Header("Action Bar Buttons")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button pickaxeButton;
    [SerializeField] private Button diplomacyButton;
    [SerializeField] private Button cycleButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button infoButton;

    private Building currentBuilding;
    private BuildingFacilityInfo currentInfo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        WireActionButtons();
        Close();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;

        GameObject prefab = Resources.Load<GameObject>("UI/Building Facility Panel");
        if (prefab == null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform hudBot = canvas.transform.Find("Graphical User Interface/HUD Bot") ?? canvas.transform;
        GameObject instance = Instantiate(prefab, hudBot);
        instance.name = "Building Facility Panel";
    }

    private void OnEnable()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.AddListener(OnSelectionChanged);
        }
    }

    private void OnDisable()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.RemoveListener(OnSelectionChanged);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnSelectionChanged(Building building)
    {
        // Null or warehouses are not handled by this panel
        if (building == null || building.GetComponent<Depot>() != null || building.GetComponent<WarehouseSockets>() != null)
        {
            Close();
            return;
        }

        Open(building);
    }

    public void Open(Building building)
    {
        currentBuilding = building;
        currentInfo = BuildingFacilityInfo.ResolveOrCreate(building);

        if (panelRoot != null) panelRoot.SetActive(true);

        RefreshStaticDisplay();
        RefreshDynamicDisplay();
    }

    public void Close()
    {
        currentBuilding = null;
        currentInfo = null;

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        if (currentBuilding == null || !currentBuilding.gameObject)
        {
            Close();
            return;
        }

        RefreshDynamicDisplay();
    }

    private void RefreshStaticDisplay()
    {
        if (currentBuilding == null) return;

        FacilityPanelMode mode = ResolvePanelMode();
        string category = ResolveCategoryTitle(mode);
        string nameText = ResolveBuildingName();

        if (categoryLabel != null) categoryLabel.text = category;
        if (buildingNameLabel != null) buildingNameLabel.text = nameText;

        Sprite hIcon = currentInfo != null ? currentInfo.headerIcon : null;
        if (headerIcon != null)
        {
            if (hIcon != null)
            {
                headerIcon.sprite = hIcon;
                headerIcon.gameObject.SetActive(true);
            }
            else
            {
                // Fallback icon based on mode
                headerIcon.gameObject.SetActive(false);
            }
        }

        if (ecobalanceRoot != null) ecobalanceRoot.SetActive(mode == FacilityPanelMode.Ecobalance);
        if (productionRoot != null) productionRoot.SetActive(mode == FacilityPanelMode.Production);

        if (mode == FacilityPanelMode.Ecobalance)
        {
            if (portraitImage != null && currentInfo != null && currentInfo.portraitImage != null)
            {
                portraitImage.sprite = currentInfo.portraitImage;
                portraitImage.gameObject.SetActive(true);
            }

            if (ecoEffectText != null)
            {
                ecoEffectText.text = currentInfo != null ? currentInfo.effectText : "+100";
            }

            if (ecoEffectIcon != null && currentInfo != null && currentInfo.effectIcon != null)
            {
                ecoEffectIcon.sprite = currentInfo.effectIcon;
            }
        }
        else
        {
            if (inputIcon != null && currentInfo != null && currentInfo.inputIcon != null)
            {
                inputIcon.sprite = currentInfo.inputIcon;
            }
            if (outputIcon != null && currentInfo != null && currentInfo.outputIcon != null)
            {
                outputIcon.sprite = currentInfo.outputIcon;
            }
        }
    }

    private void RefreshDynamicDisplay()
    {
        if (currentBuilding == null) return;

        FacilityPanelMode mode = ResolvePanelMode();

        float rate = currentInfo != null ? currentInfo.GetCurrentProductionRate() : 100f;
        string rateStr = $"{Mathf.RoundToInt(rate)}%";
        float cycleProgress = currentInfo != null ? currentInfo.GetCycleProgress() : (rate / 100f);

        if (mode == FacilityPanelMode.Ecobalance)
        {
            if (ecoPercentageText != null) ecoPercentageText.text = rateStr;
            if (ecoProgressRing != null) ecoProgressRing.fillAmount = rate > 0 ? cycleProgress : 0f;
        }
        else
        {
            if (prodPercentageText != null) prodPercentageText.text = rateStr;
            if (prodProgressRing != null) prodProgressRing.fillAmount = rate > 0 ? cycleProgress : 0f;

            if (cogAnimator != null)
            {
                cogAnimator.SetProductionRate(rate);
            }

            if (inputAmountText != null)
            {
                int depositAmount = currentInfo != null ? currentInfo.GetInputDepositAmount() : 594484;
                inputAmountText.text = depositAmount.ToString("N0");
            }

            int outAmount = currentInfo != null ? currentInfo.GetCurrentOutputAmount() : 1;
            int outCap = currentInfo != null ? currentInfo.GetOutputCapacity() : 30;

            if (outputAmountText != null)
            {
                outputAmountText.text = outAmount.ToString("N0");
            }

            if (outputFillBar != null)
            {
                outputFillBar.fillAmount = outCap > 0 ? (float)outAmount / outCap : 0f;
            }
        }

        // Refresh bottom status bar
        int upkeep = currentInfo != null ? currentInfo.GetUpkeepCredits() : -50;
        int energy = currentInfo != null ? currentInfo.GetEnergyValue() : -20;
        string eco = currentInfo != null ? currentInfo.GetEcobalanceValue() : "-";
        int curHp = currentInfo != null ? currentInfo.GetCurrentHealth() : 1000;
        int maxHp = currentInfo != null ? currentInfo.GetMaxHealth() : 1000;

        if (creditsText != null) creditsText.text = upkeep > 0 ? $"+{upkeep}" : $"{upkeep}";
        if (energyText != null) energyText.text = energy > 0 ? $"+{energy}" : $"{energy}";
        if (ecoText != null) ecoText.text = eco;
        if (healthText != null) healthText.text = $"{curHp:N0}/{maxHp:N0}";
    }

    private FacilityPanelMode ResolvePanelMode()
    {
        if (currentInfo != null) return currentInfo.panelMode;

        string bName = currentBuilding.buildingData != null ? currentBuilding.buildingData.buildingName : currentBuilding.name;
        if (bName.Contains("Ozone") || bName.Contains("Deacidification") || bName.Contains("CO2"))
        {
            return FacilityPanelMode.Ecobalance;
        }

        return FacilityPanelMode.Production;
    }

    private string ResolveCategoryTitle(FacilityPanelMode mode)
    {
        if (currentInfo != null && !string.IsNullOrEmpty(currentInfo.categoryTitle))
        {
            return currentInfo.categoryTitle;
        }

        return mode == FacilityPanelMode.Ecobalance ? "ECOBALANCE BUILDINGS" : "PRODUCTION BUILDINGS";
    }

    private string ResolveBuildingName()
    {
        if (currentInfo != null && !string.IsNullOrEmpty(currentInfo.buildingDisplayName))
        {
            return currentInfo.buildingDisplayName;
        }

        if (currentBuilding.buildingData != null && !string.IsNullOrEmpty(currentBuilding.buildingData.buildingName))
        {
            return currentBuilding.buildingData.buildingName;
        }

        return currentBuilding.name.Replace("(Clone)", "").Trim();
    }

    #region Action Buttons Wiring

    private void WireActionButtons()
    {
        if (homeButton != null) homeButton.onClick.AddListener(OnHomeClicked);
        if (pickaxeButton != null) pickaxeButton.onClick.AddListener(OnPickaxeClicked);
        if (diplomacyButton != null) diplomacyButton.onClick.AddListener(OnDiplomacyClicked);
        if (cycleButton != null) cycleButton.onClick.AddListener(OnCycleClicked);
        if (plusButton != null) plusButton.onClick.AddListener(OnPlusClicked);
        if (infoButton != null) infoButton.onClick.AddListener(OnInfoClicked);
    }

    private void OnHomeClicked()
    {
        if (currentBuilding == null) return;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 target = currentBuilding.transform.position;
            cam.transform.position = new Vector3(target.x, cam.transform.position.y, target.z - 15f);
        }
    }

    private void OnPickaxeClicked()
    {
        if (currentBuilding == null) return;
        BuildingDemolition demolition = currentBuilding.GetComponent<BuildingDemolition>()
            ?? currentBuilding.gameObject.AddComponent<BuildingDemolition>();
        demolition.Demolish();
        Close();
    }

    private void OnDiplomacyClicked()
    {
        Debug.Log("Diplomacy / Trade action clicked for " + (currentBuilding != null ? currentBuilding.name : "null"));
    }

    private void OnCycleClicked()
    {
        if (currentBuilding == null) return;
        // Toggle building pause state
        if (currentBuilding.CurrentState == BuildingEnums.BuildingState.Active)
        {
            currentBuilding.SetState(BuildingEnums.BuildingState.Paused);
        }
        else if (currentBuilding.CurrentState == BuildingEnums.BuildingState.Paused)
        {
            currentBuilding.SetState(BuildingEnums.BuildingState.Active);
        }
        RefreshDynamicDisplay();
    }

    private void OnPlusClicked()
    {
        if (currentBuilding == null) return;
        Damageable damageable = currentBuilding.GetComponent<Damageable>();
        if (damageable != null)
        {
            damageable.currentHealth = damageable.totalHealth;
            RefreshDynamicDisplay();
        }
    }

    private void OnInfoClicked()
    {
        Debug.Log("Facility Info details clicked for " + (currentBuilding != null ? currentBuilding.name : "null"));
    }

    #endregion
}
