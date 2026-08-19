# Moonlight Item / Inventory / Storage Architecture Reference

This document reduces each class to one core question:

> **What responsibility is this class supposed to own?**

Use that question when debugging. If a class starts answering a question that belongs to another layer, responsibilities are probably crossing.

---

# 1. Core Item Model

## `ItemData`

**Question:**  
> What kind of item is this?

**Mental model:**  
Definition/template data for an item.

Typical responsibilities seen from usage:
- Item name
- Display name
- Icon
- Item type
- Other static item properties

Think:

```text
ItemData
"What is Iron?"
```

---

## `ItemStack`

**Question:**  
> What item is in this particular stack, and how many?

**Mental model:**  
One bounded stack of one item type.

Responsibilities seen from usage:
- Holds an `ItemData`
- Holds current quantity
- Has a maximum quantity
- Can add quantity
- Can subtract quantity
- Can clear itself
- Can determine whether it is full
- Can determine whether it contains an item
- Owns/updates its item icon and quantity text

Think:

```text
ItemStack
"This is a stack of 27 Iron."
```

---

## `ItemSlot`

**Question:**  
> What stack may occupy this position, and what can the player do with it?

**Mental model:**  
A slot/container that owns or hosts an `ItemStack`.

Responsibilities:
- Own/reference its `ItemStack`
- Restrict acceptable item types
- Determine whether it is occupied
- Receive dropped stacks
- Merge/swap items
- Handle hover state
- Handle clicking
- Handle trade/transfer selection
- Maintain slot-related references

Intended hierarchy:

```text
ItemSlot GameObject
└── ItemStack GameObject
```

The slot and stack are different concepts:

```text
ItemSlot
"Where may cargo go?"

ItemStack
"What cargo is currently there?"
```

---

## `ItemStackFactory`

**Question:**  
> How do I construct an `ItemStack` when a slot needs one?

**Mental model:**  
Runtime constructor/helper.

It creates:

```text
ItemStack GameObject
├── ItemStack component
├── Image
└── Text
```

and parents it under an `ItemSlot`.

---

# 2. Inventory / Ownership

## `Inventory`

**Question:**  
> What items does this individual carrier possess?

**Mental model:**  
General inventory used directly by gameplay interactions such as:
- Trading
- Buying
- Selling
- Item manipulation

It is used by individual units/carriers such as ships.

---

## `UnitInventory`

**Question:**  
> What does this specific unit carry, including its inventory-slot structure?

**Mental model:**  
Unit-specific inventory.

Known responsibilities from its usage:
- Stores unit inventory contents
- Owns/creates runtime `ItemSlot`s
- Exposes item data to `UnitInventoryUI`
- Raises unit-inventory change events
- Participates directly in the runtime slot initialization path

---

## `Storage`

**Question:**  
> How much of each item does this storage entity possess?

**Mental model:**  
Generic aggregate storage.

Core representation:

```csharp
Dictionary<ItemData, int>
```

`Storage` deliberately does not need to model every physical stack individually.

Think:

```text
Storage
"We own 120 Coal."
```

Responsibilities:
- Add item quantity
- Remove item quantity
- Query item quantity
- Return all items
- Handle optional overall capacity

---

## `UnitStorage`

**Question:**  
> Given that this is a mobile unit, how is its cargo constrained by stacks and slots?

**Mental model:**  
`Storage` plus physical/unit cargo restrictions.

Its design notes explicitly define:
- Stacks are what slots put items into
- One stack contains one item type
- Slots determine stack capacity
- Normal items may stack
- Consumables may have stricter limits
- Different slot types exist

It tracks concepts such as:

```csharp
Dictionary<ItemStack, bool> FullStacks;
Dictionary<ItemStack, int> StackSize;
Dictionary<ItemStack, ItemSlot> slots;
List<ItemSlot> itemSlots;
```

Think:

```text
Storage
"We own 120 Coal."

UnitStorage
"This ship carries that Coal through bounded cargo slots/stacks."
```

---

# 3. Economic Storage Specializations

These are not competing inventory systems. They represent different ownership scopes.

## `BuildingStorage`

**Question:**  
> What does this individual building store?

**Mental model:**  
Building-local storage specialization.

Currently mostly an extension point around `Storage`.

---

## `DroneStorage`

**Question:**  
> What does this logistics drone/transporter carry?

**Mental model:**  
Transport/logistics storage specialization.

Useful for physical movement of goods inside the island economy.

---

## `IslandStorage`

**Question:**  
> What goods does the island-level economy account for?

**Mental model:**  
Island/economy-level storage abstraction.

It can represent the island economy while individual buildings and transporters still physically hold and move goods.

---

## `BaseStorage`

**Question:**  
> What does the player's base possess, and how much can it hold?

**Mental model:**  
Base-level aggregate economy storage.

Adds concepts such as:
- Base capacity
- Bonus capacity from structures
- Other capacity improvements
- Full-capacity events
- Building affordability support

---

# 4. Storage Manager Layer

The intended principle in the code is:

> **Do not directly manipulate `Storage`; go through its manager.**

---

## `StorageManager`

**Question:**  
> How is outside gameplay code allowed to interact with a `Storage`?

**Mental model:**  
Public/service-facing wrapper.

Responsibilities:
- Add item
- Remove item
- Query quantity
- Query capacity
- Validate whether items can be added/removed
- Expose storage contents

Think:

```text
Storage
= owns the numbers

StorageManager
= controls access to those numbers
```

---

## `UnitStorageManager`

**Question:**  
> Can this unit actually accept or remove this cargo under unit-specific rules?

**Mental model:**  
Rules layer for `UnitStorage`.

Adds:
- Maximum stack quantity
- Normal slot count
- Consumable slot count
- Ability slot count
- Used-slot tracking
- Unit-specific capacity validation

---

## `BuildingStorageManager`

**Question:**  
> How should gameplay interact with a building's storage?

**Mental model:**  
Building-specific storage manager extension point.

Currently mostly empty.

---

## `IslandStorageManager`

**Question:**  
> How should gameplay access the island's aggregate storage?

**Mental model:**  
Manager for `IslandStorage`.

It locates or creates the island storage component and exposes it through the manager layer.

---

## `BaseStorageManager`

**Question:**  
> Can the base afford this, and how should building costs/capacity be handled?

**Mental model:**  
Base economy service.

Responsibilities include:
- Check building affordability
- Deduct building costs
- Add/remove bonus storage capacity
- Expose base-storage capacity behavior

---

# 5. Inventory UI

## `InventoryUserface`

**Question:**  
> What behavior is common to every inventory UI?

**Mental model:**  
Abstract UI base class.

Responsibilities include:
- Hold an `Inventory`
- Hold a `UnitInventory`
- Hold visible `ItemSlot`s
- Assign inventory references
- Refresh the displayed inventory
- Clear/reset slots
- Format item display text

---

## `UnitInventoryUI`

**Question:**  
> How do I display the currently inspected unit's inventory?

**Mental model:**  
View/controller for unit inventory presentation.

Its own comments describe its `ItemSlot`s as:

```text
windows which reflect the UnitInventory content
```

Responsibilities:
- Track currently displayed unit
- Track `UnitInventory`
- Track fallback/general `Inventory`
- Refresh visible slots from inventory data
- Initialize visible slots
- Clear unused slots
- Handle trade quantity selection
- Update displayed unit information

Important boundary:

```text
Inventory / UnitInventory
        ↓
       DATA
        ↓
UnitInventoryUI
        ↓
   visual translation
        ↓
ItemSlot / ItemStack
```

The UI should generally not become the authoritative owner of the actual cargo quantity.

---

## `BuildingInventoryUI`

**Question:**  
> How should a building's inventory be displayed?

**Mental model:**  
Building-specific inventory UI.

Currently mostly a placeholder/commented implementation.

---

## `InventoryUIManager`

**Question:**  
> Which inventory UI should be visible for the currently selected thing?

**Mental model:**  
Inventory UI router.

Responsibilities:
- Choose unit or building inventory template
- Activate/deactivate the correct UI
- Assign the selected unit's inventory to its UI

---

## `InventoryViewer`

**Question:**  
> Which unit/inventory are we currently inspecting?

**Mental model:**  
Intended bridge between unit selection and the visible inventory.

Current implementation is commented/redacted because the previous approach was considered flawed.

---

# 6. Drag and Drop

## `ItemDragHandler`

**Question:**  
> What happens while the player physically drags a stack around the UI?

**Mental model:**  
Pointer/drag behavior.

Responsibilities:
- Begin drag
- Track original position
- Move with mouse
- Change drag visuals
- Disable raycast blocking while dragging
- Detect drop target
- Return to original location if invalid

Conceptually:

```text
ItemDragHandler
"I am being dragged."
```

---

## `ItemSlot` during drag/drop

**Question:**  
> A stack was dropped here. Can I accept it, merge it, or swap it?

Flow:

```text
ItemDragHandler
        ↓
  player drops stack
        ↓
ItemSlot
        ↓
validate item type
        ↓
merge / swap / reject
        ↓
ItemStack changes
```

---

# 7. Gameplay Item Interactions

These classes represent gameplay verbs rather than storage itself.

---

## `UnitInteractions`

**Question:**  
> What actions is this unit capable of performing?

**Mental model:**  
Capability dispatcher/facade.

It discovers interfaces such as:
- `ITradable`
- `IBuildable`
- `IDiveable`
- `IBuyable`
- `ISellable`
- `IItemManagement`

Then exposes methods such as:
- Perform trade
- Perform build
- Perform dive
- Perform buy
- Perform sell
- Add/remove items

---

## `ItemInteraction`

**Question:**  
> How does this unit add, remove, use, or discard items?

**Mental model:**  
General item-action implementation.

Uses the unit's `Inventory`.

Responsibilities:
- Add item
- Remove item
- Use item
- Throw item into ocean

---

## `TradeInteraction`

**Question:**  
> How does one unit exchange items with another?

**Mental model:**  
Unit-to-unit trade logic.

Current logic:
- Remove item from sender
- Add item to receiver
- Validate inventories where applicable
- Notify interaction events
- Manage trade proximity/session concepts

---

## `TransferInteraction`

**Question:**  
> How should items move between inventories when this is not necessarily a trade?

**Mental model:**  
Transfer API/skeleton.

Planned concepts:
- Basic transfer
- Transfer request
- Transfer offer
- Transfer from
- Transfer to
- Transfer closest
- Transfer all
- Future partial/failed transfers

Much of it is currently unimplemented.

---

## `BuyInteraction`

**Question:**  
> How does this unit acquire an item through purchase?

**Mental model:**  
Purchase gameplay operation.

Current behavior:
- Check placeholder credit/license conditions
- Add item to unit inventory
- Notify success/failure

Currency logic is not yet complete.

---

## `SellInteraction`

**Question:**  
> How does this unit sell items it carries?

**Mental model:**  
Sale gameplay operation.

Current behavior:
- Remove item from the unit inventory
- Notify success/failure
- Currency reward remains TODO

---

## `BuildInteraction`

**Question:**  
> How does a unit use its capabilities/resources to build?

**Mental model:**  
Unit construction action.

Currently largely a design stub.

---

## `DiveInteraction`

**Question:**  
> Can this unit dive?

**Mental model:**  
Submarine-specific capability.

Current file is mostly design notes for conditions such as:
- Unit must be capable of diving
- Water must be sufficiently deep
- Dive ability must be available

---

## `TradeMenu`

**Question:**  
> How does the player select and confirm items involved in a trade?

**Mental model:**  
Trade UI/session presentation.

Responsibilities/plans include:
- Player item slots
- NPC item slots
- Trade quantity
- Populate visible slots
- Select trade contents
- Confirm/finalize trade
- Open/close trade session

Much of the transaction flow is still unfinished.

---

# 8. Interfaces

Interfaces answer:

> **What capability must a class provide if it claims it can do this action?**

## `IItemManagement`

```text
"Can you add and remove items?"
```

Methods:
- `AddItem`
- `RemoveItem`

---

## `ITradable`

```text
"Can you trade items?"
```

Method:
- `TradeItem`

---

## `ITransferable`

```text
"Can you transfer items between inventories?"
```

Defines:
- Transfer
- Request
- Offer
- From
- To
- Closest
- All

---

## `IBuyable`

```text
"Can you buy items?"
```

Method:
- `BuyItem`

---

## `ISellable`

```text
"Can you sell items?"
```

Method:
- `SellItem`

---

## `IBuildable`

```text
"Can you build?"
```

Method:
- `Build`

---

## `IDiveable`

```text
"Can you dive?"
```

Method:
- `Dive`

---

# 9. Setup / Supporting Classes

## `StarterUnit`

**Question:**  
> What should this unit start the game carrying?

**Mental model:**  
Startup/bootstrap helper.

Current implementation directly initializes visible inventory slots with starting item data.

That is worth treating carefully because startup inventory should ideally update the actual inventory authority first and let the UI reflect it.

---

## `UnitManager`

**Question:**  
> How do I create a new unit?

**Mental model:**  
Unit spawning/creation service.

Responsibilities:
- Instantiate unit prefab
- Assign unit display name

---

## `NameGenerator`

**Question:**  
> What name should a newly created thing receive?

**Mental model:**  
Random name provider grouped by `NameType`.

Includes pools for:
- Ships
- Submarines
- Aircraft
- Islands
- Cities
- People
- Other categories

---

## `Interactable`

**Question:**  
> How close must something be to interact with this object?

**Mental model:**  
Interaction-radius component.

Currently mainly:
- Stores interaction radius
- Draws a Gizmo sphere

---

# 10. The Whole Item System in One Diagram

## Unit / Carrier Side

```text
ItemData
"What item is this?"
        ↓
Inventory / Storage
"Who owns how much?"
        ↓
UnitStorage / UnitInventory
"How does this particular carrier carry it?"
        ↓
ItemSlot
"Where may a stack go?"
        ↓
ItemStack
"What occupies this slot, and how many?"
        ↓
UnitInventoryUI
"How do I show it?"
        ↓
ItemDragHandler
"How does the player manipulate it?"
        ↓
Trade / Buy / Sell / Transfer / ItemInteraction
"What gameplay action is happening?"
```

---

## Island / Economy Side

```text
                    Island Economy
                          |
                    IslandStorage
                          |
            +-------------+-------------+
            |                           |
      BaseStorage                 BuildingStorage
                                        |
                                production / usage
                                        |
                                  transporters
                                        |
                                   DroneStorage
```

The island-level storage can describe the economy as a whole while individual buildings and transporters still hold and move physical goods.

---

# 11. Fundamental Debugging Rule

For any bug, ask:

> **Which question is this class supposed to answer?**

Examples:

If `Storage` starts creating `Image` components:

```text
WRONG LAYER
Storage should answer:
"How much do we own?"

not:
"How should it look on screen?"
```

If `UnitInventoryUI` becomes the only place that knows how many items a ship owns:

```text
WRONG LAYER
UI should answer:
"How do I display inventory?"

not:
"What inventory actually exists?"
```

If `ItemSlot` becomes the global economic authority:

```text
WRONG LAYER
ItemSlot should answer:
"Where may this stack go?"

not:
"How much coal does the entire island own?"
```

---

# 12. The Six Questions for Debugging Any Moonlight Inventory Bug

When something breaks:

1. **What object/class failed?**
2. **What reference or state was missing/wrong?**
3. **Which class was supposed to create or own it?**
4. **When was that supposed to happen?**
5. **Who accessed it first?**
6. **Was it guaranteed to be valid at that exact moment?**

Example — current `ItemStack` error:

```text
1. ItemSlot failed.
2. ItemStack was missing.
3. ItemSlot / ItemStackFactory was supposed to provide it.
4. During slot initialization.
5. ItemSlot.Awake accessed it.
6. No — the runtime slot had only just been created.
```

That immediately points toward initialization order.

---

# 13. Core Invariants Worth Memorizing

An invariant is something that should always be true.

Useful candidate invariants for Moonlight:

```text
Every normal ItemSlot has exactly one usable ItemStack child.
```

```text
An empty slot means its stack contains no ItemData/quantity,
not necessarily that the ItemStack object itself ceases to exist.
```

```text
Inventory/Storage owns gameplay item state.
UI reflects that state.
```

```text
ItemData describes an item type.
ItemStack represents a bounded quantity of that item.
ItemSlot represents where that stack may exist.
```

```text
StorageManager classes control access to their Storage classes.
```

These rules make bugs substantially easier to trace because each failure becomes:

> **Which invariant stopped being true, and who was responsible for maintaining it?**
