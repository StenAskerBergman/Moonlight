using System;
using UnityEngine;

/// <summary>
/// Optional upgrade / agricultural module attached to or associated with a Farm Core.
/// Provides bonuses such as increased productivity, reduced cycle time, bonus output, or extra storage.
/// </summary>
public class AgriculturalModule : MonoBehaviour
{
    [Header("Module Info")]
    [SerializeField] private string moduleName = "Agricultural Upgrade";
    [SerializeField, TextArea(1, 3)] private string moduleDescription = "Enhances crop farm efficiency.";

    [Header("Productivity Modifiers")]
    [Tooltip("Multiplier applied to effective productivity (1.0 = normal, 1.2 = +20%).")]
    [SerializeField] private float productivityMultiplier = 1.0f;

    [Tooltip("Flat additive productivity bonus (e.g. 0.1 = +10%).")]
    [SerializeField] private float productivityFlatBonus = 0.0f;

    [Header("Cycle Modifiers")]
    [Tooltip("Multiplier for base cycle time (0.8 = 20% faster).")]
    [SerializeField] private float cycleTimeMultiplier = 1.0f;

    [Header("Output & Storage Modifiers")]
    [Tooltip("Additional items produced per completed harvest cycle.")]
    [SerializeField] private int extraOutputAmount = 0;

    [Tooltip("Additional storage capacity granted to the Farm Core.")]
    [SerializeField] private int extraStorageCapacity = 0;

    [Header("Workforce & Field Modifiers")]
    [Tooltip("Reduction in workforce requirement (e.g. automation/tools).")]
    [SerializeField] private int workforceReduction = 0;

    [Tooltip("Bonus virtual fields added towards the required field count.")]
    [SerializeField] private int virtualFieldBonus = 0;

    [Header("Associated Farm")]
    [SerializeField] private CropFarmCore associatedFarmCore;

    public string ModuleName => moduleName;
    public string ModuleDescription => moduleDescription;
    public float ProductivityMultiplier => Mathf.Max(0.1f, productivityMultiplier);
    public float ProductivityFlatBonus => productivityFlatBonus;
    public float CycleTimeMultiplier => Mathf.Max(0.1f, cycleTimeMultiplier);
    public int ExtraOutputAmount => Mathf.Max(0, extraOutputAmount);
    public int ExtraStorageCapacity => Mathf.Max(0, extraStorageCapacity);
    public int WorkforceReduction => Mathf.Max(0, workforceReduction);
    public int VirtualFieldBonus => Mathf.Max(0, virtualFieldBonus);
    public CropFarmCore AssociatedFarmCore => associatedFarmCore;

    private void Start()
    {
        if (associatedFarmCore == null)
        {
            associatedFarmCore = GetComponentInParent<CropFarmCore>();
        }

        if (associatedFarmCore != null)
        {
            associatedFarmCore.AddAgriculturalModule(this);
        }
    }

    private void OnDestroy()
    {
        if (associatedFarmCore != null)
        {
            associatedFarmCore.RemoveAgriculturalModule(this);
        }
    }

    /// <summary>
    /// Manually binds this module to a Farm Core.
    /// </summary>
    public void BindToFarm(CropFarmCore farmCore)
    {
        if (associatedFarmCore == farmCore) return;

        if (associatedFarmCore != null)
        {
            associatedFarmCore.RemoveAgriculturalModule(this);
        }

        associatedFarmCore = farmCore;
        if (associatedFarmCore != null)
        {
            associatedFarmCore.AddAgriculturalModule(this);
        }
    }
}
