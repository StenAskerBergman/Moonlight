You are analyzing **Moonlight**, a Unity RTS with a logistics-heavy economy built around physical goods, storage locations, transporters, units, buildings, and island-level economic accounting.

Your task is to understand the **economic data structure and domain model** before making architectural recommendations.

Do **not** assume that classes which appear to duplicate item quantities are automatically redundant. Moonlight intentionally represents goods at several different economic and physical scopes.

The key question when reading the code is:

> **What economic fact is this particular object supposed to represent?**

---

# 1. Core Economic Principle

Moonlight's economy is fundamentally about **physical goods existing within an ownership and logistics network**.

Items can exist:

- aboard ships or other units,
- inside buildings,
- inside a player's base,
- aboard transporters such as drones,
- within an island's wider economic system,
- and potentially in world-space outside storage.

The game therefore needs to represent two related but distinct concepts:

1. **Economic ownership/accounting**
   - How much of an item does an economic entity possess or account for?

2. **Physical placement/logistics**
   - Where is that quantity physically located?
   - Which building, transporter, unit, slot, or stack contains it?
   - Can it physically fit there?

Do not collapse those two questions into one without source evidence.

---

# 2. Shared Item Definition — `ItemData`

`ItemData` is the common definition used throughout the economy.

Conceptually:

```text
ItemData
"What kind of economic good is this?"
```

Examples could include resources, consumables, cargo, construction materials, trade goods, or other item types.

An `ItemData` is not itself necessarily one physical object.

It defines properties associated with the item type, such as:

- name,
- display name,
- icon,
- item type,
- stack capacity,
- value,
- stackability,
- consumable/trade properties,
- and other static characteristics.

Multiple inventories and storages can reference the same `ItemData`.

Conceptually:

```text
ItemData: Coal

Island A may have 400 Coal
Building X may contain 60 Coal
Cargo Ship Y may carry 35 Coal
Drone Z may carry 5 Coal
```

These are not four different definitions of Coal.

They are four different economic/physical holdings referencing the same `ItemData`.

---

# 3. Generic Economic Storage — `Storage`

`Storage` is the generic aggregate storage abstraction.

Its central representation is effectively:

```csharp
Dictionary<ItemData, int>
```

Conceptually:

```text
Storage
"How much of each ItemData does this storage entity currently possess?"
```

For example:

```text
Coal  -> 120
Iron  -> 80
Food  -> 35
```

This representation deliberately does **not** require every quantity to be modeled as an individual physical stack.

Its job is aggregate accounting for a particular storage entity.

`Storage` supports concepts such as:

- adding quantities,
- removing quantities,
- querying quantity,
- retrieving all held items,
- and optionally enforcing a capacity.

The intended encapsulation rule visible in the project is approximately:

```text
Gameplay/System
      ↓
StorageManager
      ↓
Storage
```

rather than arbitrary systems directly mutating `Storage`.

---

# 4. Storage Is Specialized by Economic Scope

The different `Storage` subclasses should not automatically be interpreted as competing implementations.

They represent **different holders or economic scopes**.

The important specializations currently visible include:

```text
Storage
├── IslandStorage
├── BaseStorage
├── BuildingStorage
├── DroneStorage
└── UnitStorage
```

Each answers a different economic question.

---

# 5. `IslandStorage` — Island-Level Economic Accounting

Conceptually:

```text
IslandStorage
"What goods does this island economy account for?"
```

This is an island-level abstraction.

Do **not** assume this means every item is literally sitting in one magical island warehouse.

The island economy may consist of:

```text
Island
├── Base
├── Production Buildings
├── Consumer Buildings
├── Warehouses / Storage Buildings
├── Transporters
├── Drones
├── Ships
└── Other economic actors
```

An island-level storage abstraction can coexist with individual physical storages.

A useful distinction is:

```text
IslandStorage
Economic/accounting view of island goods

BuildingStorage / DroneStorage / other local storage
Physical location of goods inside that economy
```

Whether `IslandStorage` ultimately acts as a true source of truth, cache, aggregation layer, or independent pool must be determined from actual usage.

Do not decide that merely from its class name.

---

# 6. `BuildingStorage` — Building-Local Goods

Conceptually:

```text
BuildingStorage
"What goods are physically/local-logically held by this building?"
```

This can support buildings that:

- receive inputs,
- buffer resources,
- produce outputs,
- consume resources,
- or serve as dedicated storage structures.

A building's storage is part of the island economy, but it is not necessarily identical to island-wide accounting.

Example:

```text
Island economy:
120 Coal total/accounted

Coal Mine output:
30 Coal

Power Plant input buffer:
20 Coal

Warehouse:
50 Coal

Transport network:
20 Coal currently in movement
```

The exact aggregation rules must be recovered from the code rather than assumed.

---

# 7. `BaseStorage` — Player/Base-Level Economic Storage

Conceptually:

```text
BaseStorage
"What resources does this base possess and what capacity does the base provide?"
```

`BaseStorage` contains explicit capacity concepts and is also used by building-cost logic.

It therefore belongs closer to the economic/resource-management side than to individual slot presentation.

The project also contains references to `Player` and `Owner` around this layer, indicating that broader economic ownership was intended.

However:

> Do not assume the entire ownership model is complete.

If player ownership, island ownership, faction ownership, or building ownership appears incomplete, report that rather than inventing the missing hierarchy.

---

# 8. `DroneStorage` — Logistics / Transport Storage

Conceptually:

```text
DroneStorage
"What goods is this transporter physically carrying right now?"
```

This is important to Moonlight's economy.

Transported goods are not merely numbers teleporting between economic actors.

The architecture supports the idea that goods can exist **in transit**.

Conceptually:

```text
Producer
   ↓
BuildingStorage
   ↓
Transport request
   ↓
Drone / Transporter
   ↓
DroneStorage
   ↓
Destination
   ↓
BuildingStorage
```

Therefore transporter inventory may represent a genuine physical/logistical state rather than redundant accounting.

---

# 9. Units Have a More Detailed Cargo Model

Mobile units such as ships require a more constrained representation than a generic aggregate storage.

The relevant classes include:

```text
UnitStorage
UnitStorageManager
UnitInventory
ItemSlot
ItemStack
```

These should be understood together.

---

# 10. `UnitStorage` — Physical Cargo Constraints

Conceptually:

```text
UnitStorage
"Given that this is a unit, how can its goods physically fit into cargo slots and stacks?"
```

The source comments explicitly describe the intended relationship:

- stacks are placed into slots,
- a stack contains one item type,
- stacks have current and maximum quantities,
- slots influence stack capacity,
- normal items may stack,
- consumables may obey different limits,
- and different slot categories exist.

The code contains concepts such as:

```text
FullStacks
StackSize
Stack -> ItemSlot mapping
ItemSlots
occupiedSlots
```

The implementation is currently incomplete/inconsistent in places, but these fields reveal intended domain concepts.

This means `UnitStorage` is not simply:

```text
another Dictionary<ItemData, int>
```

Its intended role is closer to:

```text
aggregate quantity
+
physical carrier constraints
+
stacking rules
+
slot availability
```

---

# 11. `UnitStorageManager` — Access and Validation Layer

Conceptually:

```text
UnitStorageManager
"Is this cargo operation legal for this unit?"
```

It currently contains rules/constants for:

- normal slots,
- consumable slots,
- ability slots,
- maximum stack quantity,
- used slots,
- and available capacity.

Some of this bookkeeping currently overlaps with `UnitStorage`.

That duplication is an implementation problem worth auditing.

However, do not confuse:

```text
duplicated bookkeeping
```

with:

```text
the conceptual distinction between manager and storage
```

The manager may legitimately be the API/rules layer while `UnitStorage` holds storage state.

---

# 12. `UnitInventory` — Unit Cargo Coordinator

Conceptually:

```text
UnitInventory
"What does this particular unit carry, and how is that cargo represented through its slots/stacks?"
```

This class sits close to the actual unit.

Its comments describe it as an interface to the unit's stacks rather than the ultimate economic database.

Its responsibilities currently include:

- creating unit inventory slots,
- finding suitable slots,
- handling item additions/removals,
- coordinating with `UnitStorageManager`,
- exposing unit inventory state,
- raising inventory-change events,
- and providing data for the UI.

Therefore think of it as a **coordinator between economic state and physical cargo structure**.

---

# 13. `ItemSlot` — Physical Cargo Position

Conceptually:

```text
ItemSlot
"Where may a stack exist?"
```

A slot represents a bounded position in a unit inventory or inventory interface.

It may enforce:

- acceptable `ItemType`,
- normal vs consumable vs ability restrictions,
- whether a position is occupied,
- dropping,
- swapping,
- merging,
- transfer selection,
- trade selection,
- and player interaction.

A slot should not normally answer:

```text
"How much Coal does the entire island own?"
```

That belongs to another economic layer.

---

# 14. `ItemStack` — Contents of One Slot

Conceptually:

```text
ItemStack
"What item occupies this slot, and how many units of it are here?"
```

A stack contains state such as:

```text
ItemData
quantity
maxQuantity
```

Example:

```text
Cargo Slot #2
└── ItemStack
    ├── ItemData = Coal
    ├── quantity = 27
    └── maxQuantity = 40
```

This is more detailed than:

```text
Storage:
Coal -> 87
```

because the storage aggregate does not necessarily tell you **how those 87 Coal are distributed physically**.

For example:

```text
Storage total:
Coal = 87

Physical stacks:
Slot 1 = 40 Coal
Slot 2 = 40 Coal
Slot 3 = 7 Coal
```

Those two representations can both be meaningful.

The problem occurs when they disagree and there is no defined synchronization rule.

---

# 15. Empty Stack vs Missing Stack

An important intended distinction appears to be:

```text
EMPTY STACK
ItemStack exists
itemData = null
quantity = 0
```

versus:

```text
MISSING STACK
ItemSlot.itemStack = null
```

The former should normally describe an available cargo slot.

The latter should normally indicate that the slot/stack structure has not been constructed correctly.

Some current methods contradict this intended model by clearing a stack and then nulling its reference.

Treat this as an implementation inconsistency to investigate, not necessarily proof that nullable stacks were the intended architecture.

---

# 16. Generic `Inventory` Is Also Part of the System

Moonlight also contains a general `Inventory` implementation separate from `UnitInventory` / `UnitStorage`.

Do **not** immediately classify it as obsolete.

Gameplay interaction systems currently use it directly for operations such as:

```text
Trade
Buy
Sell
Use Item
ItemInteraction
Transfer-related APIs
```

Conceptually this appears to answer something like:

```text
Inventory
"What goods does this gameplay actor possess?"
```

while the `UnitInventory` / `UnitStorage` path attempts to model more specific physical cargo-slot behavior.

There is therefore an unresolved relationship between:

```text
Inventory
UnitInventory
UnitStorage
Storage
```

Your job when auditing is to determine their intended contracts and actual call paths before suggesting that any one of them be deleted.

---

# 17. Economy and Inventory Are Parallel Scales

Do not model Moonlight as:

```text
Old Inventory System
        versus
New Storage System
```

unless concrete source/history proves that.

A more useful model is:

```text
                       ItemData
                          |
          +---------------+----------------+
          |                                |
          |                                |
   Individual actors                 Economic actors
          |                                |
   Ships / units                     Islands / Bases
   Transporters                      Buildings
          |                                |
          |                                |
 Inventory / UnitStorage         Storage specializations
          |                                |
   physical cargo rules          economic/local quantities
          |
      ItemSlot
          |
      ItemStack
```

And the two sides interact through logistics.

---

# 18. The Economy Is a Network, Not Just a Dictionary

When reasoning about Moonlight, think in terms of **nodes and transfers**.

Economic nodes may include:

```text
Island
Base
Building
Warehouse
Producer
Consumer
Ship
Drone
Other Unit
World Item
```

Goods can move between those nodes.

Conceptually:

```text
Resource Production
        ↓
Producer BuildingStorage
        ↓
Transporter Pickup
        ↓
DroneStorage / Unit Cargo
        ↓
Transport
        ↓
Destination BuildingStorage
        ↓
Consumption / Processing
```

or:

```text
Island A
   ↓
Trade Ship UnitInventory
   ↓
Ocean / Route
   ↓
Island B
```

The exact transaction implementation must be derived from source code.

This prompt describes the domain structure, not a claim that every transfer pipeline is currently implemented correctly.

---

# 19. Separate Economic Truth From Presentation

Moonlight also has `UnitInventoryUI` and UI-side `ItemSlot`s.

The project's own comments describe UI slots essentially as **windows reflecting unit inventory content**.

Therefore distinguish:

```text
Unit's cargo slot
physical/logical inventory state
```

from:

```text
Canvas ItemSlot
visual representation presented to the player
```

It can be perfectly legitimate to have separate UI slot objects.

The problem is only if the presentation objects independently become authoritative economic state.

Use this rule:

```text
MODEL / ECONOMY
determines what exists

UI
shows what exists
```

The UI may send player intentions such as:

```text
move this stack
trade this amount
transfer this item
```

but the underlying inventory/economic layer should validate and execute the actual state change.

---

# 20. Do Not Misinterpret Multiple Quantities

The current code contains several quantity/count representations:

```text
Storage.items[ItemData]
ItemStack.quantity
UnitStorage.occupiedSlots
UnitStorage.StackSize
UnitStorageManager.usedSlots
UnitStorageManager.totalStacks
```

Do not simply say:

> "There must only be one number."

Instead determine what each number is **supposed to mean**.

Potential legitimate distinctions include:

```text
total item quantity
quantity inside one stack
number of occupied slots
number of stacks
capacity consumed
capacity available
quantity in transit
```

The actual bug is when two fields represent the **same fact independently** and can diverge.

For each field, classify it as one of:

```text
AUTHORITATIVE STATE
DERIVED STATE
CACHE
INDEX
VALIDATION COUNTER
PRESENTATION STATE
BUG / REDUNDANT STATE
```

Do not collapse representations until this classification is complete.

---

# 21. Capacity Has Multiple Meanings

Be careful with the word `capacity`.

Moonlight potentially contains several different capacity dimensions:

```text
Storage quantity capacity
Number of slots
Stack capacity
Item-type-specific slot limits
Building storage capacity
Unit cargo capacity
Transport capacity
```

For example:

```text
4 Normal Slots
```

does not automatically mean:

```text
4 total items
```

And:

```text
40 maximum items per normal stack
```

does not automatically mean:

```text
240 total cargo
```

because different slot categories may obey different rules.

If the code passes a slot count into a generic quantity-capacity API, identify that as a **semantic mismatch**.

Do not invent the intended final numeric capacity without further evidence.

---

# 22. Economic Ownership Is Not Fully Recovered Yet

Do not assume ownership architecture that is not implemented.

There are signs of intended concepts such as:

```text
Player
Owner
Unit ownership
Island ownership
Building ownership
Trading partners
```

but some interaction files explicitly note that ownership is incomplete.

Therefore distinguish:

```text
confirmed existing architecture
```

from:

```text
obvious intended future architecture
```

from:

```text
your own proposed design
```

Never mix the three.

---

# 23. Gameplay Interaction Layer

The following concepts operate on top of inventories/economic storage:

```text
TradeInteraction
BuyInteraction
SellInteraction
ItemInteraction
TransferInteraction
TradeMenu
UnitInteractions
```

These answer gameplay questions such as:

```text
Can this actor trade?
Can this actor buy?
Can this actor sell?
Can this actor transfer cargo?
What quantity is being transferred?
Who is the sender?
Who is the receiver?
```

They are consumers of the inventory/economy architecture.

They should not silently become the primary storage authority.

When tracing an economic transaction, follow the complete path:

```text
Player/gameplay request
       ↓
Interaction
       ↓
Inventory/storage API
       ↓
Validation
       ↓
Authoritative mutation
       ↓
Physical stack/slot update if applicable
       ↓
Event
       ↓
UI refresh
```

Determine where the current implementation deviates from this flow.

---

# 24. Production and Consumption

The source currently available establishes the storage/inventory/logistics structure more strongly than it establishes the complete production economy.

Therefore, when analyzing production:

Do not assume formulas, production ticks, worker systems, demand simulation, market clearing, prices, or consumption mechanics unless those files are actually inspected.

Instead search for and classify systems such as:

```text
production
resource generation
consumption
building inputs
building outputs
production rates
transport requests
warehouse logistics
trade
prices
baseValue
market logic
construction costs
maintenance
population needs
```

Then connect those systems to the storage graph described above.

---

# 25. Recommended Mental Model

Use this high-level structure:

```text
                       MOONLIGHT ECONOMY

                           ItemData
                    "What good is this?"
                              |
          +-------------------+-------------------+
          |                                       |
          v                                       v
   PHYSICAL CARRIERS                       ECONOMIC LOCATIONS
          |                                       |
    Unit / Ship                              Island
    Transporter                              Base
    Drone                                    Building
          |                                       |
          v                                       v
  Inventory / UnitStorage              Storage specializations
          |                                       |
          v                                       |
      UnitInventory                              |
          |                                       |
      ItemSlot                                    |
          |                                       |
      ItemStack                                   |
          |                                       |
          +----------------+----------------------+
                           |
                     LOGISTICS FLOW
                           |
               pickup / transfer / trade
                           |
                     destination
```

This is not necessarily a strict inheritance graph.

It is a **domain relationship graph**.

---

# 26. Questions to Ask When Auditing Any Economy Class

For every relevant class, answer:

1. **What economic entity does this class represent?**
2. **What economic fact does it own?**
3. **Is that fact aggregate or physical?**
4. **Is its state authoritative, derived, cached, or visual?**
5. **Who is allowed to mutate it?**
6. **Who reads it?**
7. **What manager/controller sits between it and callers?**
8. **What other representation must remain synchronized with it?**
9. **What event communicates that it changed?**
10. **What happens when goods move to another economic actor?**
11. **Does movement represent an atomic transfer or two unrelated mutations?**
12. **Can goods temporarily exist in transit?**
13. **How are stack limits different from total capacity?**
14. **How are building capacities different from unit cargo slots?**
15. **Does UI merely project the data or accidentally own it?**
16. **Does the code distinguish empty state from nonexistent structure?**
17. **Which behaviors are implemented versus merely described in comments/TODOs?**

---

# 27. Critical Rule for Recommendations

Before proposing a refactor, explicitly distinguish:

### A. Intentional domain layers

Examples:

```text
IslandStorage vs BuildingStorage
Storage vs physical ItemStack
UnitInventory vs UnitInventoryUI
Building storage vs transporter cargo
aggregate quantity vs per-stack quantity
```

These may legitimately coexist.

### B. Accidental duplicate authority

Examples potentially present in the current implementation:

```text
multiple counters independently representing used slots
multiple quantities independently representing the same inventory
UI slot state being written back into logical slot state
several unrelated objects creating ItemStacks
```

These should be investigated and potentially consolidated.

### C. Incomplete systems

Examples:

```text
ownership
trade finalization
transfer implementation
some storage specialization behavior
production/logistics integration
```

Do not solve these merely by deleting existing abstractions.

---

# 28. Desired Audit Output

After studying the codebase using this mental model, produce:

## 1. ECONOMIC ENTITY MAP

Identify every major economic holder:

```text
Player
Island
Base
Building
Unit
Ship
Drone
Transporter
etc.
```

State which storage/inventory class each uses.

## 2. ITEM STATE MAP

For each representation of an item quantity, identify:

- file/class,
- field,
- exact semantic meaning,
- authoritative vs derived,
- who mutates it,
- who consumes it.

## 3. STORAGE HIERARCHY

Explain:

```text
Storage
IslandStorage
BaseStorage
BuildingStorage
DroneStorage
UnitStorage
```

and what economic scope each represents.

## 4. UNIT CARGO MODEL

Explain:

```text
UnitStorageManager
UnitStorage
UnitInventory
ItemSlot
ItemStack
```

and how these are intended to cooperate.

## 5. ECONOMIC FLOW

Trace at least:

```text
Add item to unit
Remove item from unit
Building receives item
Building produces item, if implemented
Transporter pickup/dropoff, if implemented
Trade between actors
Construction resource consumption
```

If a flow is incomplete, say exactly where it ends.

## 6. SOURCES OF TRUTH

Do not simply demand one universal source of truth.

Instead propose **one source of truth per economic fact**.

For example:

```text
Fact: Total goods owned by storage entity
Authority: ?

Fact: Contents of physical cargo slot
Authority: ?

Fact: Number of occupied cargo slots
Authority: authoritative or derived?

Fact: What UI displays
Authority: derived only
```

## 7. DUPLICATED STATE

Identify only cases where two structures truly represent the same fact.

Do not classify different economic scopes as duplication.

## 8. INCOMPLETE DOMAIN MODEL

Identify missing or unfinished concepts such as:

```text
ownership
transport transactions
trade settlement
production integration
resource reservation
etc.
```

only where supported by the code.

## 9. ARCHITECTURAL VERDICT

Answer:

- Which current abstractions are fundamentally sound?
- Which classes have blurred responsibilities?
- Which duplicated authorities should be removed?
- Which relationships should be explicit?
- Which state should be derived rather than stored?
- Can the current economy architecture be repaired incrementally?
- What must be understood before writing fixes?

---

# Final Constraint

Treat Moonlight as a **physical logistics RTS with multiple legitimate economic scopes**, not as a simple RPG inventory system.

A building storing 20 Coal, a ship carrying 20 Coal, and an island accounting for 20 Coal may represent:

- different goods,
- the same goods viewed at different aggregation levels,
- or inconsistent duplicated state.

Your job is to determine which one from the actual code.

Do not assume.

Recover the intended economic model first, then diagnose the implementation.