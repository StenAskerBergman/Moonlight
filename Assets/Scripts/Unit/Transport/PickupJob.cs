using System.Collections.Generic;

public sealed class PickupJob
{
    public enum JobState
    {
        Queued,
        TravelingToPickup,
        Loading,
        TravelingToWarehouse,
        Unloading,
        Completed,
        Failed
    }

    public PickupJob(
        Building producer,
        WarehouseLogisticsScheduler warehouse,
        IReadOnlyDictionary<ItemEnums.ResourceType, int> cargo,
        bool isPriority)
    {
        Producer = producer;
        Warehouse = warehouse;
        Cargo = cargo;
        IsPriority = isPriority;
    }

    public Building Producer { get; }
    public WarehouseLogisticsScheduler Warehouse { get; }
    public IReadOnlyDictionary<ItemEnums.ResourceType, int> Cargo { get; }
    public bool IsPriority { get; }
    public int RetryCount { get; private set; }

    /// <summary>
    /// Earliest Time.time at which this job may be dispatched again. Set when a
    /// retry is scheduled so a failed job cannot consume its whole retry budget
    /// inside a single frame.
    /// </summary>
    public float EarliestDispatchTime { get; private set; }
    public JobState State { get; private set; } = JobState.Queued;

    public void SetState(JobState state) => State = state;
    public void SetEarliestDispatchTime(float time) => EarliestDispatchTime = time;
    public int RegisterRetry() => ++RetryCount;
}
