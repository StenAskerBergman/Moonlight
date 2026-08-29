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
