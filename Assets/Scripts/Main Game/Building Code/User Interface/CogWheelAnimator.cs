using UnityEngine;

/// <summary>
/// Controls the animated rotation of the interlocking mechanical cog wheels
/// displayed in the background of the Production / Oil Station facility panel.
/// Spins or stops based on the production percentage rate.
/// </summary>
public class CogWheelAnimator : MonoBehaviour
{
    [Header("Cog Wheel Transforms")]
    [SerializeField] private RectTransform cogLeft;
    [SerializeField] private RectTransform cogRight;

    [Header("Animation Settings")]
    [Tooltip("Base rotation speed in degrees per second at 100% production rate.")]
    [SerializeField] private float baseSpeed = 40f;

    [Tooltip("Gear teeth ratio between left and right gears for realistic meshing.")]
    [SerializeField] private float gearRatio = 1.0f;

    [Tooltip("Current target production rate (0 - 100%).")]
    [SerializeField] private float productionRatePercent = 100f;

    [Tooltip("Smoothing speed for start and stop acceleration/deceleration.")]
    [SerializeField] private float responseSpeed = 5f;

    private float currentSmoothedRate = 100f;

    public float ProductionRatePercent
    {
        get => productionRatePercent;
        set => productionRatePercent = Mathf.Clamp(value, 0f, 100f);
    }

    public void SetProductionRate(float ratePercent)
    {
        productionRatePercent = Mathf.Clamp(ratePercent, 0f, 100f);
    }

    private void Update()
    {
        // Smoothly interpolate towards target production rate
        currentSmoothedRate = Mathf.MoveTowards(
            currentSmoothedRate,
            productionRatePercent,
            responseSpeed * 100f * Time.unscaledDeltaTime
        );

        if (currentSmoothedRate <= 0.01f)
        {
            // Production is halted, paused, or stopped: cog wheels do not spin
            return;
        }

        float normalizedRate = currentSmoothedRate / 100f;
        float deltaAngle = baseSpeed * normalizedRate * Time.unscaledDeltaTime;

        if (cogLeft != null)
        {
            // Left gear rotates counter-clockwise
            cogLeft.Rotate(Vector3.forward, deltaAngle);
        }

        if (cogRight != null)
        {
            // Right gear meshes and rotates clockwise
            cogRight.Rotate(Vector3.forward, -deltaAngle * gearRatio);
        }
    }
}
