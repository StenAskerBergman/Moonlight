# Moonlight RTS — Economy & Inventory Architecture Audit

---

## 1. CONFIRMED DOMAIN MODEL

### Developer-Confirmed Intent

```text
┌─────────────────────────────────────────────────────────────┐
│                    ISLAND ECONOMY                           │
│                                                             │
│  IslandStorage = one shared island-wide stockpile           │
│  Warehouses = capacity expansions + loading ramps           │
│  Buildings consume from / produce into IslandStorage        │
│  Construction costs deducted from IslandStorage             │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                  PHYSICAL CARRIERS                          │
│                                                             │
│  Ship = fixed N permanent cargo slots                       │
│  Each slot = persistent ItemStack                           │
│  Each stack = one ItemData type + quantity ≤ per-slot cap   │
│  Empty slot = stack with itemData=null, quantity=0          │
│  Cargo is real: takes time to load/unload, drops as         │
│  flotsam on destruction                                     │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                    CAPACITY MODEL                           │
│                                                             │
│  Two dimensions:                                            │
│    1. Slot count (e.g., 4 cargo slots)                      │
│    2. Per-slot quantity cap (e.g., 50 tons per slot)         │
│  "Can cargo fit?" = compatible slot? + same item or         │
│  empty? + remaining quantity?                               │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                    UI LAYER                                  │
│                                                             │
│  UI slots = presentation only, never authoritative          │
│  UI requests mutation → backend validates → backend         │
│  performs → UI reflects result                              │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│              GENERIC INVENTORY                              │
│                                                             │
│  Transitional: should migrate to specialized APIs           │
│  Island operations → IslandStorage/IslandStorageManager     │
│  Ship operations → UnitInventory/UnitStorage                │
│  Building operations → building-specific interface          │
└─────────────────────────────────────────────────────────────┘
```

### Current Implementation Reality

| Aspect | Intent | Current State |
|---|---|---|
| IslandStorage as shared stockpile | One island-wide dictionary | **Stub**: [IslandStorage.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/IslandStorage.cs) is 17 lines — inherits `Storage`, overrides `AddItem` with only `base.AddItem()`, no capacity, no warehouse integration |
| Warehouses expand capacity | Warehouse buildings increase island capacity | **No warehouse code exists** — zero files matching `*Warehouse*` in Scripts |
| Ship fixed slots with persistent stacks | N permanent ItemSlots each owning one ItemStack | **Partially built**: `UnitInventory.CreateNewItemSlot()` creates slots+stacks, but lifecycle bugs corrupt them (see bugs section) |
| Two-dimensional capacity | Slot count × per-slot quantity | **Broken**: `UnitStorage.SetCapacityLimit(6)` feeds slot count into a quantity-checking API |
| UI is presentation only | UI reads from backend, requests mutations | **Violated**: drag/drop mutates stacks directly, `InitializeSlot` writes back into logical arrays |
| Generic Inventory is transitional | Migrate callers to specialized APIs | **Still universal**: 7 interaction classes all use `Inventory` exclusively |
| Cargo loading/unloading takes time | Time-based transfer animation | **Not implemented** |
| Flotsam on ship destruction | Cargo → world items | **Not implemented** — no flotsam code exists |
| Goods in transit | Items removed from source during transport | **Not implemented** — no transfer transaction exists |

### My Inference (not developer-stated)

- The `BaseStorage` / `BaseStorageManager` layer appears intended for the **player's base/HQ economy** — distinct from island storage. `BaseStorageManager.CanAffordBuilding()` and `DeductBuildingCosts()` confirm this is the construction-cost authority.
- `CityInventory.cs` uses `Dictionary<string, int>` (string keys, not `ItemData`) — appears to be an early/legacy prototype, completely disconnected from the item system.
- `IslandInventory.cs` (`List<Item>`) and `IslandItemManager.cs` (fully commented out) are abandoned attempts at island-level item management that never reached completion.
- `BuildingInventory.cs` uses `ItemEnums.ResourceType` enum lists — a different type system from the `ItemData`/`Storage` architecture. This is a parallel, unfinished system.

---

## 2. ECONOMIC ENTITY MAP

| Entity | Current Storage | Current Inventory | Intended Authority | Status |
|---|---|---|---|---|
| **Player** | — | — | Per-island `BaseStorage` via `BaseStorageManager` | Base economy works for construction costs |
| **Island** | `IslandStorage` (stub) | `IslandInventory` (stub), `Island.cs` uses generic `Inventory` | `IslandStorage` via `IslandStorageManager` | ⚠️ Skeleton only — `Island.cs` [L30](file:///e:/GitHub/Moonlight/Assets/Scripts/Code/Island%20Code/Island%20Gen/Island.cs) uses generic `Inventory` instead |
| **Warehouse** | None | None | Capacity expansion on `IslandStorage` | ❌ Not implemented |
| **Base (Player HQ)** | `BaseStorage` | Generic `Inventory` on same GO | `BaseStorage` via `BaseStorageManager` | ✅ Partially working — construction costs functional |
| **Building** | `BuildingStorage` (empty stub) | `BuildingInventory` (enum-based, different type system) | Building-specific storage or island-delegated | ⚠️ Two incompatible stubs |
| **Ship / Unit** | `UnitStorage` on unit GO | `UnitInventory` on unit GO + generic `Inventory` on same GO | `UnitStorage` via `UnitStorageManager`, managed by `UnitInventory` | ⚠️ Has structure but critical bugs prevent function |
| **Drone / Transporter** | `DroneStorage` (empty stub) | — | Transporter-specific cargo | ❌ Stub only |
| **World Item / Flotsam** | — | — | World item entity with ItemData + quantity | ❌ Not implemented |
| **UI** | — | `UnitInventoryUI.itemSlots[]`, `InventoryUserface.inventorySlots` | Presentation only — derived from inventory state | ⚠️ Currently violates boundary |

### Evidence: Island.cs uses generic Inventory

[`Island.cs` L30, L37](file:///e:/GitHub/Moonlight/Assets/Scripts/Code/Island%20Code/Island%20Gen/Island.cs):
```csharp
Inventory islandInventory;
// ...
islandInventory = gameObject.GetComponent<Inventory>();
```

This means the island currently uses the **generic** `Inventory` class (which `RequireComponent(typeof(StorageManager), typeof(Storage))`) rather than `IslandStorage`/`IslandStorageManager`. The specialized island storage classes exist but aren't wired to the actual Island entity.

### Evidence: Unit.cs holds both Inventory and UnitInventory

[`Unit.cs`](file:///e:/GitHub/Moonlight/Assets/Scripts/Unit/Unit%20Scripts/Unit.cs) (from subagent research):
```csharp
public Inventory inventory;
public UnitInventory unitInventory;
```

Both are present on the Unit. All interaction classes (`BuyInteraction`, `SellInteraction`, `TradeInteraction`, `ItemInteraction`) use `GetComponent<Inventory>()` — they exclusively call the generic `Inventory`, never `UnitInventory`.

---

## 3. STATE AUTHORITY TABLE

| Field | Location | Classification | Rationale |
|---|---|---|---|
| `Storage.items` (`Dictionary<ItemData,int>`) | [Storage.cs L17](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/Storage.cs#L17) | **Authoritative** (for island/base economy) / **Broken duplicate** (for unit cargo) | For islands/bases: this is the single source. For units: this duplicates `ItemStack.quantity` and they never sync |
| `Storage.capacityLimit` | [Storage.cs L18](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/Storage.cs#L18) | **Authoritative** (for quantity-based stores) / **Semantic mismatch** (for units) | Intended for total-quantity capacity. UnitStorage feeds a slot count into it |
| `UnitStorage.occupiedSlots` | [UnitStorage.cs L80](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/UnitStorage.cs#L80-L84) | **Broken — should be derived** | Intended to track occupied slot count, but increments per `AddItem` call not per actual slot occupation. Should be derived from slot contents |
| `UnitStorage.StackSize` | [UnitStorage.cs L59](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/UnitStorage.cs#L59) | **Dead index** | `Dictionary<ItemStack, int>` — populated only by `GetInventoryItemList()` read path, never written to by add/remove operations |
| `UnitStorage.FullStacks` | [UnitStorage.cs L56](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/UnitStorage.cs#L56) | **Dead index** | `Dictionary<ItemStack, bool>` — declared, never populated |
| `UnitStorage.slots` | [UnitStorage.cs L62](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/UnitStorage.cs#L62) | **Dead index** | `Dictionary<ItemStack, ItemSlot>` — only read in `GetInventoryItemList()`, never populated by any code path |
| `UnitStorageManager.usedSlots` | [UnitStorageManager.cs L80](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/UnitStorageManager.cs#L80-L85) | **Broken duplicate authority** | Duplicates `UnitStorage.occupiedSlots` with independent increment/decrement. Same semantic fact, two competing stores |
| `UnitStorageManager.totalStacks` | [UnitStorageManager.cs L79](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/UnitStorageManager.cs#L79) | **Broken — misnamed quantity accumulator** | Named "totalStacks" but `+= quantity` at [L112](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/UnitStorageManager.cs#L112). Represents total items, not stack count. Duplicates `Storage.items` totals |
| `UnitStorageManager.maxQuantity` | [UnitStorageManager.cs L45](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/UnitStorageManager.cs#L45) | **Validation state** | Per-slot max quantity (40 + bonus). Legitimate — represents the per-slot cap design intent |
| `UnitInventory.itemSlots[]` | [UnitInventory.cs L44](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L44) | **Authoritative** | The logical slot array for the unit's physical cargo. This is the correct single owner |
| `UnitInventory.uiSlots[]` | [UnitInventory.cs L43](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L43) | **Presentation cache** | Inspector-wired reference to Canvas UI slots. Legitimate as a presentation binding |
| `UnitInventoryUI.itemSlots[]` | [UnitInventoryUI.cs L26](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/UnitInventoryUI.cs#L26) | **Presentation** | UI-side slot references. Legitimate if kept as view-only, currently violated |
| `InventoryUserface.inventorySlots` | [InventoryUserface.cs L14](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/InventoryUserface.cs#L14) | **Presentation — redundant with above** | `List<ItemSlot>` base-class collection. Both this AND `UnitInventoryUI.itemSlots[]` exist, serving the same display purpose |
| `ItemStack.itemData` | [ItemStack.cs L28](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L28) | **Authoritative** (for ship cargo) | The physical per-slot item identity. This IS the cargo |
| `ItemStack.quantity` | [ItemStack.cs L116](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L116) | **Authoritative** (for ship cargo) / **Broken — never assigned by SetItemData(data, qty)** | Should be authoritative per-slot quantity. Currently corrupted by the setter bug |
| `ItemStack.maxQuantity` | [ItemStack.cs L115](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L115) | **Configuration** | Per-slot capacity ceiling. Set from `ItemData.maxStackSize` or `UnitStorageManager.MAX_STACK_SIZE` |

---

## 4. TRANSACTION FLOW AUDIT

### 4.1 Add item to ship

**Current flow** ([`UnitInventory.AddItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L360-L437)):

```text
1. Find slot via GetSlot(itemData, amount)           ← no same-ItemData check (Bug D)
2. If no slot, CreateNewItemSlot()                   ← lifecycle bugs (Bug G)
3. InitializeOrUpdateItemStack(slot, itemData)       ← may overwrite existing stack data
4. ValidateAndAddItem:
   4a. unitStorageManager.CanAddItem() check         ← validation only, capacity mismatch (Bug C)
   4b. slot.itemStack.AddQuantity(amount)            ← mutates stack quantity
5. MISSING: unitStorageManager.AddItem() never called ← BUG B: backend not updated
6. Events fired, UI updated
```

**Pipeline breaks at step 5**: `Storage.items` dictionary never learns about the addition.

### 4.2 Remove item from ship

**Current flow** ([`UnitInventory.RemoveItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L546-L568)):

```text
1. unitStorageManager.CanRemoveItem() check
2. Events + UI fired BEFORE mutation ← wrong ordering
3. unitStorageManager.RemoveItem() → Storage.items updated
4. MISSING: No ItemStack.quantity subtraction
```

**Pipeline breaks at step 4**: Stack quantities never decrease. Events precede the mutation.

### 4.3 Load ship from island

**NOT IMPLEMENTED**

No code exists to transfer between `IslandStorage` and `UnitInventory`/`UnitStorage`. The `TransferInteraction.cs` file contains empty stubs:

```csharp
public void Transfer(Inventory senderInventory, Inventory receiverInventory, ItemData item, int quantity) { }
public void TransferFrom(Inventory fromInventory, Inventory toInventory, ItemData item, int quantity) { }
```

All methods are empty bodies.

### 4.4 Unload ship to island

**NOT IMPLEMENTED** — same as above.

### 4.5 Move stack between cargo slots (drag/drop)

**Current flow** ([`ItemSlot.OnDrop`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L771-L806) → [`HandleItemDrop`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L490-L575)):

```text
1. Get droppedItemStack from eventData.pointerDrag
2. If slot occupied AND items match:
   a. SwapItems()                    ← executes FIRST regardless of match
   b. THEN checks if items match and tries AddQuantity  ← executes SECOND
   → Both swap AND merge on same drop = data corruption
3. If slot empty:
   a. itemStack.SetItemData(data, qty) ← quantity not stored (Bug A)
4. MISSING: No backend storage calls at any point
5. MISSING: No source slot cleanup
```

**Pipeline breaks**: No backend sync, double mutation (swap+merge), source slot retains items.

### 4.6 Trade

**Current flow** ([`TradeInteraction.ExecuteTrade`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/TradeInteraction.cs)):

```text
1. CanRemove check on sender Inventory
2. CanAdd check on receiver Inventory
3. sender.Inventory.RemoveItem()
4. receiver.Inventory.AddItem()
```

This operates on the **generic `Inventory`** class, which correctly delegates to `StorageManager` → `Storage`. But for ships, this updates `Storage.items` without touching `ItemStack` quantities. For islands, it would use the generic `Inventory` on the Island GO rather than `IslandStorage`.

**Pipeline is internally consistent** for the generic path but **bypasses** all slot/stack logic.

### 4.7 Buy

[`BuyInteraction.BuyItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/BuyInteraction.cs): Calls `unitInventory.AddItem(item, quantity)` — uses generic `Inventory`, not `UnitInventory`. Currency check is hardcoded `true`.

### 4.8 Sell

[`SellInteraction.SellItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/SellInteraction.cs): Calls `unitInventory.RemoveItem(item, quantity)` — uses generic `Inventory`. Credit addition is TODO.

### 4.9 Construction resource consumption

[`BaseStorageManager.DeductBuildingCosts`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/BaseStorageManager.cs#L46-L56):

```text
1. CanAffordBuilding() → checks BaseStorage.GetAllItems()
2. DeductCosts() → baseStorage.RemoveItem() for each cost entry
```

**This pipeline works correctly** within `BaseStorage`. It uses `baseStorage.RemoveItem()` which directly mutates `Storage.items`. However, it calls `BaseStorage.RemoveItem()` which uses `new` (hiding `virtual`), so it does NOT trigger the virtual override chain — but the `BaseStorage.RemoveItem` implementation is identical to `Storage.RemoveItem`, so the effect is the same.

### 4.10 Ship destruction / cargo loss

**NOT IMPLEMENTED** — no flotsam conversion exists. No code references cargo-on-death behavior.

---

## 5. VERIFIED BUGS

### CRITICAL

#### Bug A — `SetItemData(ItemData, int)` never assigns `this.quantity`

- **Class**: `ItemStack`
- **Method**: [`SetItemData(ItemData data, int quantity)`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L157-L162)
- **Evidence**: Parameter `quantity` shadows field `this.quantity`. Only `itemData = data` and `UpdateStackUI(quantity)` execute. No `this.quantity = quantity`.
- **Runtime consequence**: Every initialization and swap that uses this overload displays the correct number but stores 0 internally. All subsequent `AddQuantity`, `SubtractQuantity`, `IsFull` checks operate on stale data.
- **Violated invariant**: Stack compatibility invariant (quantity state must reflect actual cargo).
- **Affected call sites**: `CreateNewItemSlot` L337, `CreateItemStackInSlot` L468, `PopulateItemSlots` L101, `ItemSlot.SetItemData` L132, `SwapItems` L582-583, `HandleItemDrop` L570, `ReceiveDroppedItem` L750.

#### Bug B — `UnitInventory.AddItem` never calls backend `AddItem`

- **Class**: `UnitInventory`
- **Method**: [`AddItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L360-L437) → [`ValidateAndAddItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L532-L544)
- **Evidence**: L536 calls `unitStorageManager.CanAddItem()` (read-only). L542 calls `slot.itemStack.AddQuantity(amount)`. No call to `unitStorageManager.AddItem()`.
- **Runtime consequence**: `Storage.items` dictionary never updated. `UnitStorage.occupiedSlots` and `UnitStorageManager.usedSlots` never updated. Only `ItemStack.quantity` changes.
- **Violated invariant**: Transfer invariant (goods must exist in exactly one authority).

#### Bug C — Capacity semantic mismatch: slot count fed into quantity check

- **Class**: `UnitStorage` → `Storage`
- **Method**: [`UnitStorage.Awake`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/UnitStorage.cs#L73-L77) feeding into [`Storage.HasReachedCapacity`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/Storage.cs#L27-L41)
- **Evidence**: `SetCapacityLimit(NORMAL_SLOTS + ABILITY_SLOT + CONSUME_SLOT)` = 6. `HasReachedCapacity` sums all `items[key].Value` and compares to 6.
- **Runtime consequence**: Even if Bug B were fixed and `Storage.items` got updated, the unit would reject any addition once total item count exceeds 6 — not 6 slots × 40 per slot.
- **Violated invariant**: Capacity has two dimensions (slot count and per-slot quantity), not one.

### HIGH

#### Bug D — `GetSlot` accepts any compatible non-full slot without ItemData match

- **Class**: `UnitInventory`
- **Method**: [`GetSlot(ItemData, int)`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L603-L624)
- **Evidence**: Condition at L618: `slot.CanHoldItemType(itemData.type) && (slot.itemStack == null || !slot.IsSlotFull())`. No check for `slot.itemStack.GetItemData() == itemData`.
- **Runtime consequence**: Wood slot selected for Stone if both are ItemType.Normal.
- **Violated invariant**: Stack compatibility invariant.

#### Bug E — `UnitStorage.occupiedSlots` increments per AddItem call, not per slot occupation

- **Class**: `UnitStorage`
- **Method**: [`AddItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Storages/UnitStorage.cs#L104-L124)
- **Evidence**: L113: `occupiedSlots[ItemType.Normal]++` runs every `AddItem` call. Adding 3 quantities of Wood in 3 calls = `occupiedSlots[Normal] = 3`, not 1.
- **Runtime consequence**: Slot availability check at L137 (`occupiedSlots[Normal] + 1 > NORMAL_SLOTS`) rejects items prematurely.
- **Violated invariant**: Slot occupancy invariant.

#### Bug F — Duplicate manager bookkeeping

- **Class**: `UnitStorageManager`
- **Method**: [`AddItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/UnitStorageManager.cs#L96-L118), [`RemoveItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/UnitStorageManager.cs#L193-L214)
- **Evidence**: Manager increments `usedSlots[type]++` at L105 AND calls `unitStorage.AddItem()` at L100 which increments `occupiedSlots[type]++` at UnitStorage L113. Two independent counters for same fact.
- **Runtime consequence**: Validation in `CanAddItem` uses `CalculateAvailableSlots` which reads `usedSlots`, while `UnitStorage.CanAddSpecificItem` uses `occupiedSlots`. If either drifts, validation becomes inconsistent.
- **Violated invariant**: One authority per economic fact.

#### Bug G — `ItemSlot.Awake` creates premature ItemStack, then creator creates another

- **Class**: `ItemSlot`
- **Method**: [`Awake()`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L84-L121) → vs [`UnitInventory.CreateNewItemSlot`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L303-L353)
- **Evidence**: Awake L94-98: `GetComponentInChildren<ItemStack>()` returns null → falls through to `ItemStackFactory.CreateItemStack(transform)` → creates Stack A with UI components. Then CreateNewItemSlot L331: `stackGO.AddComponent<ItemStack>()` creates Stack B without UI. L344: `slot.itemStack = stack` points to Stack B. Stack A is orphaned.
- **Runtime consequence**: Orphaned GameObjects, wasted memory, potential UI component conflicts.
- **Violated invariant**: Empty-slot invariant (slot should own exactly one persistent stack).

#### Bug H — `ItemStack.Start` dereferences null `itemSlot`

- **Class**: `ItemStack`
- **Method**: [`Start()`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L35-L79)
- **Evidence**: L37: `itemSlot.storageManager?.GetComponent<Item>()` — dereferences `itemSlot` before null check at L38. Property `itemSlot` is `{ get; private set; }` and `SetItemSlot()` is never called during `CreateNewItemSlot`.
- **Runtime consequence**: `NullReferenceException` on every programmatically created ItemStack when `Start()` runs.
- **Violated invariant**: Stack reverse-ownership initialization.

### MEDIUM

#### Bug I — `ClearSlot` and `CheckAndClearSlotIfEmpty` null out itemStack

- **Class**: `ItemSlot`
- **Methods**: [`ClearSlot()`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L756-L767), [`CheckAndClearSlotIfEmpty()`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L588-L595)
- **Evidence**: `ClearSlot` L760: `itemStack.ClearStack()` then L762: `itemStack = null`. Comment at L593: `// keep itemStack for reuse.` but code does `itemStack = null`.
- **Runtime consequence**: Violates persistent-stack design. `ClearSlot` then calls `UpdateSlotUI(0)` at L764 which accesses `itemStack.itemData` → NRE. `UseItem()` L604 accesses `itemStack.GetQuantity()` after potential nullification → NRE.
- **Violated invariant**: Empty-slot invariant.
- **Developer comment evidence**: [ItemSlot.cs L616-L619](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L616-L619): "Just clear it, removing a Stack or Slot is too tedious… when we eventually will fill it anyway."

#### Bug J — UI initialization timing: UpdateStackUI called before Start

- **Class**: `ItemStack`
- **Method**: [`SetItemData`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L157-L162) called from `CreateNewItemSlot` (during Awake) → [`UpdateStackUI`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L332-L380)
- **Evidence**: `itemIcon` and `itemQuantityText` assigned in `Start()` L61-L74. `SetItemData` called during `Awake` phase. UpdateStackUI has null guards (L340, L349) that log errors but don't crash.
- **Stacks from `ItemStackFactory`**: DO get UI components via `InitializeUIComponents(icon, text)` at ItemSlot L44 — but those stacks are then orphaned (Bug G).
- **Runtime consequence**: Silent UI initialization failure for all programmatically created stacks.

#### Bug K — `RemoveItem` fires events before mutation

- **Class**: `UnitInventory`
- **Method**: [`RemoveItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Inventory/UnitInventory.cs#L546-L568)
- **Evidence**: L552: `OnUnitInventoryChanged?.Invoke()` fires before L559: `unitStorageManager.RemoveItem()`. UI refreshes see pre-mutation state.
- **Runtime consequence**: UI shows stale data; code after `return` at L559 is unreachable.

#### Bug L — `UnitStorageManager` hides parent's `storage` field

- **Class**: `UnitStorageManager`
- **Field**: [`protected new Storage storage`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/UnitStorageManager.cs#L9)
- **Evidence**: Child declares `new Storage storage` hiding `StorageManager.storage`. Child never assigns its own `storage`. Parent's `Awake()` assigns the parent's `storage` via `GetComponent<Storage>()`.
- **Runtime consequence**: Accidentally non-fatal because inherited methods access the parent's field (correctly set) while `UnitStorageManager` uses `unitStorage` directly. But creates ongoing maintenance confusion.

#### Bug M — `new UnitStorage()` on MonoBehaviour

- **Class**: `UnitStorageManager`
- **Method**: [`Awake()` L27](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Manager/UnitStorageManager.cs#L27)
- **Evidence**: `unitStorage = new UnitStorage()` — `UnitStorage` extends `MonoBehaviour`.
- **Runtime consequence**: Unity warning; object not attached to any GO; `UnitStorage.Awake()` never fires; `SetCapacityLimit` never called. Only triggers when `GetComponent<UnitStorage>()` returns null.

### LOW

#### Bug N — `HandleItemDrop` executes both swap AND merge

- **Class**: `ItemSlot`  
- **Method**: [`HandleItemDrop`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L525-L555)
- **Evidence**: L532: `SwapItems(droppedItem)` always called when occupied. Then L540: checks if items match and calls `AddQuantity`. Both execute on same drop.

#### Bug O — `GetSpaceLeft`/`GetStackSpaceLeft` inverted calculation

- **Class**: `ItemStack`
- **Methods**: [`GetSpaceLeft`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L234-L244), [`GetStackSpaceLeft`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Item%20Code/ItemStack.cs#L250-L272)
- **Evidence**: `quantity - maxQuantity` instead of `maxQuantity - quantity`, clamped to 0. Returns 0 when there IS space.

---

## 6. INTENTIONAL LAYERS VS ACCIDENTAL DUPLICATION

### KEEP AS DISTINCT CONCEPTS

These represent **genuinely different economic holders** per the confirmed domain rules:

| Layer | Represents | Why distinct |
|---|---|---|
| `IslandStorage` | Shared island stockpile | Fundamentally different from ship cargo — limitless slots, shared pool |
| `BaseStorage` | Player's construction economy | Separate from island's general stockpile — player-scoped, building-cost focused |
| `UnitStorage` | Ship cargo slot constraints | Fixed slots, per-slot caps, physical cargo semantics |
| `BuildingStorage` | Per-building storage behavior | Buildings may have input/output buffers distinct from island stockpile |
| `DroneStorage` | Transporter carrying capacity | Carries goods between points — different constraints from ships |
| `Storage` (abstract) | Shared Dictionary-based API | Legitimate base class for quantity-based stores |
| `StorageManager` → `*StorageManager` | Validation/gating layer | Each entity type needs different validation rules |
| `UnitInventory.itemSlots[]` | Physical cargo slots | The actual ship cargo representation |
| `UnitInventory.uiSlots[]` / `UnitInventoryUI.itemSlots[]` | UI presentation binding | Intentional view projection of physical state |
| `ItemStack.itemData` + `ItemStack.quantity` | Per-slot cargo contents | The authoritative physical cargo fact |
| `ItemStack.maxQuantity` | Per-slot capacity ceiling | Legitimate configuration |
| Generic `Inventory` | Transitional gameplay API | Exists as interim; callers will migrate |

### COLLAPSE / DERIVE / REMOVE DUPLICATE AUTHORITY

| Duplicate | Should become | Reason |
|---|---|---|
| `UnitStorageManager.usedSlots` | **Remove** — delegate to `UnitStorage` or derive from `ItemSlot[]` | Duplicates `UnitStorage.occupiedSlots`. Only one slot-count authority needed |
| `UnitStorageManager.totalStacks` | **Remove** — derive from `ItemStack.quantity` sums if needed | Misnamed; duplicates total from `Storage.items`. Not an independent fact |
| `UnitStorage.occupiedSlots` | **Derive** from `UnitInventory.itemSlots[]` contents | Per the domain rules, slot occupancy is best derived from actual slot state. An occupied slot is one where `itemStack.itemData != null` |
| `UnitStorage.FullStacks` | **Remove** — dead code, never written | |
| `UnitStorage.StackSize` | **Remove** — dead code, never written by add/remove | |
| `UnitStorage.slots` | **Remove** — dead code, never populated | |
| `InventoryUserface.inventorySlots` + `UnitInventoryUI.itemSlots[]` | **Collapse** to one UI slot collection | Two collections for the same purpose on the presentation side |
| `Storage.items` on `UnitStorage` | **Re-scope** — for units, `ItemStack` is authoritative; `Storage.items` can become derived/cache or be removed for UnitStorage | Two competing quantity authorities for ship cargo |
| `IslandInventory` (`List<Item>`) | **Remove** — superseded by `IslandStorage` | Abandoned stub with wrong data type |
| `CityInventory` (`Dictionary<string,int>`) | **Remove** or **rewrite** | Uses string keys, completely disconnected from ItemData system |

---

## 7. REPAIR ORDER

| # | Repair | Priority | Dependencies | Findings |
|---|---|---|---|---|
| **1** | **Fix `SetItemData(ItemData, int)`: add `this.quantity = quantity`** | Correctness | None | Bug A |
| **2** | **Fix `ItemStack.Start` L37: null-check `itemSlot` before dereference** | Correctness | None | Bug H |
| **3** | **Fix `ClearSlot`/`CheckAndClearSlotIfEmpty`: clear data, don't null `itemStack`** | Lifecycle | None | Bug I |
| **4** | **Fix `ClearStack`: add null guards for `itemIcon`/`itemQuantityText`** | Lifecycle | #3 | Bug I (NRE chain) |
| **5** | **Remove premature ItemStack creation from `ItemSlot.Awake`** — let the slot creator be the sole stack creator | Lifecycle | #3 | Bug G |
| **6** | **In `CreateNewItemSlot`: call `stack.SetItemSlot(slot)` after creation** | Lifecycle | #2 | Bug H (reverse ownership) |
| **7** | **Wire `UnitInventory.AddItem` to call `unitStorageManager.AddItem()` after validation** | Backend sync | #1 | Bug B |
| **8** | **Wire `UnitInventory.RemoveItem` to subtract from `ItemStack.quantity`; move events after mutation** | Backend sync | #1, #7 | Bug K |
| **9** | **Fix `GetSlot(ItemData, int)`: require same `ItemData` for non-empty stacks** | Slot matching | #7 | Bug D |
| **10** | **Fix `UnitStorage.occupiedSlots`: increment only on new slot occupation, not per `AddItem` call** | Occupancy | #7 | Bug E |
| **11** | **Remove `UnitStorageManager.usedSlots` and `totalStacks` — derive from `UnitStorage` or slot state** | Authority collapse | #10 | Bug F |
| **12** | **Fix capacity semantics: either give `UnitStorage` a proper quantity-based capacity (slots × maxStackSize), or make `UnitStorage` not use `Storage.capacityLimit` at all and implement its own two-dimensional check** | Capacity | #7, #10 | Bug C |
| **13** | **Remove `protected new Storage storage` from `UnitStorageManager`** | Cleanup | #11 | Bug L |
| **14** | **Replace `new UnitStorage()` with `gameObject.AddComponent<UnitStorage>()`** | Lifecycle | #13 | Bug M |
| **15** | **Fix drag/drop to route through `UnitInventory.AddItem`/`RemoveItem` instead of direct stack mutation; fix swap+merge double-execution** | UI boundary | #7, #8, #9 | Bug N |
| **16** | **Fix `GetSpaceLeft`/`GetStackSpaceLeft`: `maxQuantity - quantity`** | Correctness | None | Bug O |
| **17** | **Collapse `InventoryUserface.inventorySlots` and `UnitInventoryUI.itemSlots[]` to one list** | UI cleanup | #15 | Duplication |
| **18** | **Remove dead `UnitStorage` fields: `FullStacks`, `StackSize`, `slots`** | Cleanup | #11 | Dead code |
| **19** | **Wire `IslandStorage`/`IslandStorageManager` to `Island.cs` instead of generic `Inventory`** | Economy integration | #7 domain stability | Island intent |
| **20** | **Begin `Inventory` caller migration: redirect interaction classes to specialized APIs** | Architecture | #19, all above stable | Generic Inventory |

---

## FINAL QUESTIONS — Direct Answers

### 1. Is Moonlight's economy architecture fundamentally sound?

**Yes.** The conceptual layering — `Storage` → `StorageManager` → specialized inventory → slot/stack → UI — is appropriate for an Anno-inspired economy. The specialization classes (`IslandStorage`, `UnitStorage`, `BaseStorage`, `BuildingStorage`, `DroneStorage`) represent genuinely different economic actors. The problem is implementation execution, not architectural conception.

### 2. Can it be repaired incrementally without rewriting?

**Yes.** Repairs #1-#6 (one-liners to small method fixes) unblock the entire stack. Repairs #7-#12 (sync wiring + authority collapse) fix the core data flow. None require restructuring the class hierarchy. The architecture can be stabilized in-place.

### 3. What should be authoritative for island stockpiles?

**`IslandStorage.items`** (inherited from `Storage`) via `IslandStorageManager`. One shared dictionary per island. Warehouses should modify `IslandStorage.capacityLimit`, not maintain separate dictionaries. Currently `IslandStorage` is a stub and `Island.cs` uses generic `Inventory` — this needs to be wired up (#19).

### 4. What should be authoritative for ship cargo?

**`ItemStack.itemData` and `ItemStack.quantity`** on each slot's persistent ItemStack. These are the physical cargo facts. `UnitStorage.items` (the `Storage` dictionary) should either be removed for `UnitStorage` or become a derived cache updated transactionally from slot state. Slot count should be derived from `UnitInventory.itemSlots.Length`. Occupied count should be derived from slots where `itemStack.itemData != null`.

### 5. What should be derived rather than independently stored?

| Fact | Should be derived from |
|---|---|
| Occupied slot count | Count of `itemSlots` where `itemStack.itemData != null` |
| Total cargo quantity | Sum of all `itemStack.quantity` across slots |
| Slot fullness | `itemStack.quantity >= itemStack.maxQuantity` |
| UI display | Read from `itemSlots[i].itemStack.itemData/quantity` |
| `UnitStorageManager.usedSlots` | Remove entirely |
| `UnitStorageManager.totalStacks` | Remove entirely |
| `UnitStorage.occupiedSlots` | Derive or remove |

### 6. Which UI/logical boundaries are currently violated?

| Violation | Location | Description |
|---|---|---|
| UI writes into logical arrays | [`ItemSlot.InitializeSlot` L310-L332](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L310-L332) | UI slot's `InitializeSlot` writes itself into `unitInventoryUI.unitInventory.itemSlots[]` |
| Drag/drop bypasses backend | [`ItemSlot.HandleItemDrop`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L490-L575), [`ReceiveDroppedItem`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L728-L753) | Directly mutates `ItemStack` data without going through `UnitInventory` |
| `FindObjectOfType<UnitInventory>()` in OnDrop | [`ItemSlot.OnDrop` L781](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/ItemSlot.cs#L781) | Returns arbitrary unit's inventory, not the correct one |
| UI clear affects logical state | [`InventoryUserface.ClearSlots`](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/InventoryUserface.cs#L123-L133) | Calls `CheckAndClearSlotIfEmpty` which nulls out `itemStack` on logical slots |
| Same `ItemSlot` class for both layers | All files | No type distinction between logical cargo slot and UI display slot |

### 7. Which generic `Inventory` callers should eventually migrate?

| Caller | File | Currently Uses | Should Migrate To |
|---|---|---|---|
| `BuildInteraction` | [BuildInteraction.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/BuildInteraction.cs) | `Inventory` via `GetComponent` | `BaseStorageManager.DeductBuildingCosts()` or island construction API |
| `BuyInteraction` | [BuyInteraction.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/BuyInteraction.cs) | `Inventory.AddItem` | `UnitInventory.AddItem` (for ship cargo) or `IslandStorageManager.AddItem` (for island trade) |
| `SellInteraction` | [SellInteraction.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/SellInteraction.cs) | `Inventory.RemoveItem` | `UnitInventory.RemoveItem` or `IslandStorageManager.RemoveItem` |
| `TradeInteraction` | [TradeInteraction.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/TradeInteraction.cs) | `Inventory.RemoveItem/AddItem` | Transfer API between appropriate storage types |
| `TransferInteraction` | [TransferInteraction.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/TransferInteraction.cs) | `Inventory` parameters (all stubs) | Specialized transfer: IslandStorage↔UnitInventory, UnitInventory↔UnitInventory |
| `ItemInteraction` | [ItemInteraction.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/ItemInteraction.cs) | `Inventory.AddItem/RemoveItem` | Context-dependent: ship cargo or island stockpile |
| `Island.cs` | [Island.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Code/Island%20Code/Island%20Gen/Island.cs) | `Inventory islandInventory` | `IslandStorageManager` |
| `UnitInteractions` | [UnitInteractions.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/UnitInteractions.cs) | Indirect via `IItemManagement` | Route to `UnitInventory` for ship operations |
| `StarterUnit` | [StarterUnit.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Interactions/StarterUnit.cs) | `UnitInventoryUI.inventorySlots` | Should use `UnitInventory.AddItem` to populate starting cargo |
| `TradeMenu` | [TradeMenu.cs](file:///e:/GitHub/Moonlight/Assets/Scripts/Item%20System/Userface/TradeMenu.cs) | `Inventory.GetAllItems` | Display from appropriate storage; execute via `TradeInteraction` |

### 8. Which bugs are implementation errors versus architectural misunderstandings?

**Implementation errors** (correct architecture, wrong code):
- Bug A (missing `this.quantity = quantity`) — one line
- Bug B (missing backend `AddItem` call) — wiring omission
- Bug D (missing ItemData match check) — logic omission
- Bug H (null dereference before check) — ordering error
- Bug I (nullifying persistent stack) — code contradicts own comment
- Bug K (event before mutation) — ordering error
- Bug N (swap + merge double execution) — logic error
- Bug O (inverted subtraction) — arithmetic error

**Architectural misunderstandings** (design concept needs correction):
- Bug C (slot count as quantity capacity) — misunderstanding of two-dimensional capacity
- Bug E/F (per-call increment instead of per-slot) — wrong semantic model for slot occupancy
- Bug G (multiple stack creators) — no clear ownership of stack lifecycle

### 9. What are the smallest changes required before the inventory can be trusted?

The **minimum viable fix set** (repairs #1, #2, #3, #7, #8, #9):

1. Add `this.quantity = quantity;` to `SetItemData(ItemData, int)`
2. Guard `itemSlot` null check in `ItemStack.Start` before L37
3. Remove `itemStack = null` from `ClearSlot` and `CheckAndClearSlotIfEmpty`
4. Call `unitStorageManager.AddItem()` in `ValidateAndAddItem`
5. Subtract from `ItemStack.quantity` in `RemoveItem`; move events after mutation
6. Add `slot.itemStack.GetItemData() == itemData` check in `GetSlot`

These six changes make the core add/remove/clear paths produce consistent state. Everything else is important but secondary.

### 10. What major economy systems remain incomplete after those repairs?

| System | Status | What's missing |
|---|---|---|
| **Island stockpile** | Stub | Wire `IslandStorage`/`IslandStorageManager` to `Island.cs`; implement warehouse capacity expansion |
| **Cargo loading/unloading** | Not started | Time-based transfer between `IslandStorage` ↔ `UnitInventory`; transactional remove-from-source-add-to-destination |
| **Ship destruction / flotsam** | Not started | On-death handler that converts `ItemStack` contents to world cargo entities |
| **Building production/consumption** | Stub only | `BuildingInventory` uses different type system (`ResourceType` enums). Needs integration with `IslandStorage` |
| **Trade execution** | Stubs | `TradeInteraction` methods partially written; `TradeMenu` fully commented out |
| **Transfer system** | Empty stubs | All `TransferInteraction` methods are empty |
| **Currency system** | Hardcoded `true` | `BuyInteraction` currency check is placeholder |
| **Drone/transporter cargo** | Stub | `DroneStorage` is empty shell |
| **Item use/effects** | Stub | `ItemInteraction.UseItem` only removes, no actual effect |
| **Drag/drop backend sync** | Broken | Currently mutates UI only; needs routing through inventory APIs |
