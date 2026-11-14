# Moonlight World Generation

This context names the playable and visual landforms that make up Moonlight's generated ocean world.

## Language

**Terrain Chunk**:
A square world region that carries one generated terrain field and its gameplay cells.
_Avoid_: Tile, island object

**Island**:
An emerged landform with dry buildable ground above the surrounding water.
_Avoid_: Plateau, terrain chunk

**Deep-Sea Plateau**:
A submerged landform with a broad buildable summit and a steep perimeter descending into deep water.
_Avoid_: Underwater island, flat seabed

**Tabletop**:
The broad, low-relief summit of a deep-sea plateau, including its buildable sandy interior.
_Avoid_: Top, floor

**Rocky Rim**:
The non-buildable perimeter band at the tabletop break, containing outcrops and occasional spires.
_Avoid_: Beach, wall

**Escarpment**:
The steep rock face descending from the rocky rim.
_Avoid_: Slope, coast

**Lower Apron**:
The fractured rock and sediment transition between the escarpment and the abyssal seabed.
_Avoid_: Tabletop, beach

**Sand Opening**:
A sparse sediment chute that interrupts the rocky rim and continues down the escarpment.
_Avoid_: River, ramp

**Abyssal Seabed**:
The deep terrain surrounding a plateau after the lower apron has faded out.
_Avoid_: Empty space, ocean surface

## Inland watercourses

**River Course**:
A connected drainage landscape from an elevated source through its reaches and drops to an ocean mouth.
_Avoid_: Water ribbon, centreline stamp

**River Reach**:
A continuous portion of a river course with a consistent character, such as a gorge, lowland channel or estuary.
_Avoid_: Independent river

**River Bank**:
The shallow margin, exposed bench and rising ground beside a channel; the two banks need not have matching shapes.
_Avoid_: Clearance radius

**Waterfall**:
A river's crest and geological drop, falling water, receiving plunge pool and downstream outlet.
_Avoid_: Source label, height step

**Plunge Pool**:
The contained basin receiving a waterfall, connected to the next river reach through its outlet.
_Avoid_: Unconnected lake

**Estuary**:
The open coastal reach where a river joins ocean water and its bed continues into the coastal shelf.
_Avoid_: Any submerged river cell
