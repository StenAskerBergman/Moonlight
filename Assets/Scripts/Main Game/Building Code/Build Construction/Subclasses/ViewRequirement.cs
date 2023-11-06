using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "View Requirement", menuName = "Building/Requirements/View")]
public class ViewRequirement : BuildingRequirement
{
    public override bool IsSatisfied()
    {
        // Logic to check if the player's current view matches the required view.
        return true;
    }

    public float requiredZoomLevel;
    public enum ViewType {Space, Land, Water}; // Idea: TopDown, Isometric, FirstPerson, ThirdPerson 
    public enum TransitionType 
    { 
        FlyUpToSpace,   // Camera Transition Fly up to Space - Flying Into Space 
        DownToEarth,    // Camera Transition Down to Earth - Burn Up in Atmosphere
        RiseAboveWater, // Camera Transition Rising above water - Bubbles Breaking Down
        DiveIntoWater, // Camera Transition Diving into water  - Bubbles Breaking Up
    };
    private enum ViewMode { Beyond, Above, Over, Neutral, Under, Beneth };
    private ViewType requiredViewType;

}
