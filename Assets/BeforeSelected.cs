using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Unit))]
public class BeforeSelected : MonoBehaviour
{
    public bool isSelected, isHovered, isSelectable, isHoverable;


    // Event Player is Hovering on this Unit

    /*
    Unit Interaction Manager oversees all player interactions with units. When the player hovers over a unit, the OnHover method takes the hovered unit as an argument. Firstly, it checks whether the game is paused or if the unit is out of the player's view, if true it immediately exits the method, ensuring no unnecessary calculations are made during these states.

    However, if the player has vision of the unit, it sets the unit’s wasViewed and isViewed properties to true, acknowledging player awareness and potential interest in the unit. This sets the stage for possible future interactions or UI updates based on player awareness of units.

    The crux happens when the unit is both selectable and hoverable - vital states ensuring the player can interact. In such cases, we navigate to UnitHighlighter to visually accentuate the unit and to UIManager to display pertinent UI, crafting a seamless and interactive player experience.

    public void OnHover(Unit unit)
    {

        if (isSelected || !isSelectable || !isHoverable) return;

        if () //player can see unit)
        {
            unit.wasViewed = true;
            unit.isViewed = true;

            if (unit is selectable and hoverable)
            {
                UnitHighlighter(unit);
                UIManager(unit);
            }
        }
    }
    */
}

// Other
//  Colorblind Equivalent ( Depends on what Colorblind Mode is picked )
//  Every Color has a - Player Set Option - if Colorblind Mode isn't good enough or if Player wants to change the Color