# Moonlight playable loop

## Slice 1: warehouse output pickup

Slice 1 proves one outbound logistics path with one existing producer, one
resource, and no production inputs.

```text
Producer creates output locally
    → output reaches one drone-load or local storage becomes full
    → assigned warehouse reserves one load
    → warehouse road drone travels to producer
    → drone loads reserved output
    → drone returns to its warehouse/depot
    → cargo enters island shared storage
```

### Ownership

- A producer is assigned to one warehouse whose influence covers it.
- If several warehouses cover it, choose the nearest by world-space distance;
  break exact ties by warehouse ID.
- Assignment remains sticky until coverage is lost.
- An active Pickup Job finishes before reassignment.
- Each warehouse owns its assigned buildings, Pickup Job queue, logistics
  scheduler, and drone pool.
- Warehouse levels 1, 2, and 3 provide 1, 2, and 3 road drones respectively.
- Shared storage belongs to the island, not to an individual warehouse.

### Job vocabulary

| Term | Meaning |
| --- | --- |
| Pickup Job | Warehouse/depot drone collects producer output. |
| Collector Job | Producer-owned drone fetches an input. Deferred. |
| Delivery Job | Owner sends goods to another destination. Deferred. |
| Priority Pickup | Manual island-wide output request. |

Only one Pickup Job may be active for a producer. Cargo is reserved when the
job is created. If travel fails, the job retries a bounded number of times while
keeping its reservation. After the retry limit, the reservation is released and
the producer becomes eligible for automatic pickup again.

### Logistics drone model

`Drone` describes an execution unit, not a vehicle shape. The owner assigns the
job, pickup, destination, cargo, and priority. A physical truck, boat, or aircraft
only executes the common lifecycle:

```text
Idle → ReceiveJob → TravelToPickup → Load
     → TravelToDropoff → Unload → Return/Idle
```

- `ProductionDrone`: producer-owned input collector; deferred.
- `TransportDrone`: warehouse/depot-owned output collector; Slice 1.
- `Truck`: road-going physical implementation of a TransportDrone.

### Priority pickup

Manual priority pickup searches island-wide for the next eligible free road
drone without changing normal warehouse assignment. If no road path exists, it
may use an island-owned airborne drone. The airborne adapter is deliberately not
part of automatic logistics and remains a follow-up implementation.

## Slice 1 acceptance

- A no-input producer accumulates output locally.
- A road-connected producer inside warehouse influence receives one assignment.
- Exactly one Pickup Job reserves its cargo.
- A warehouse-owned truck collects and returns the cargo.
- `IslandResourceStorage` increases by the delivered amount.
- Removing coverage does not migrate an active job; reassignment happens after it
  finishes.
- A failed pre-load job retries, then releases and requeues its reservation.
- Warehouses never dispatch more drones than their level permits.

## Slice 2: warehouse input delivery and recipe consumption

Consumers declare their operating inputs through `BuildingSupply`. The assigned
warehouse keeps up to three production cycles on hand, reserves available island
stock, and sends a road drone from storage to the consumer. Reservations prevent
two consumers from claiming the same stock. A production cycle only consumes its
inputs when output capacity is available, so blocked output never destroys goods.

```text
Island shared storage reserves requested input
    → warehouse drone loads reserved stock
    → drone travels over the road network to the consumer
    → consumer receives local input stock
    → production cycle atomically checks and consumes its recipe
    → output enters local producer storage for Slice 1 pickup
```
