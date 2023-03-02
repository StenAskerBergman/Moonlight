using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class State : MonoBehaviour {
    public string stateName;
    public int population;
    public Nation nation;

    public Dictionary<string, int> resources; // State Resources
    public List<Province> provinces; // Provinces within a State
    public Nation owner; // Owner

    public void TransferOwnership(Nation newOwner)
    {
        owner = newOwner;
        foreach (Province province in provinces)
        {
            province.owner = newOwner;
        }
    }
}

public class Province : MonoBehaviour
{
    
    public string Name;
    public TerrainType Terrain;
    public Nation owner;
    public List<Province> neighbors;

    public bool IsConnectedTo(Province destination) {
        return neighbors.Contains(destination);
    }

    public enum TerrainType
    {
        Plains,
        Hills,
        Mountains,
        Forest,
        Desert
    }
}

public class Nation {
    public string name;
    public List<State> states;
    public int resources;
    
    public Nation(string name, List<State> states, int resources) {
        this.name = name;
        this.states = states;
        this.resources = resources;
    }


    public class StateData
{
    public string Name;
    public int Population;
    public string Nation;
    public Dictionary<string, int> Resources;
}


    private Dictionary<string, StateData> stateData;

    private void Start()
    {   
        stateData = new Dictionary<string, StateData>();
        TextAsset csvFile = Resources.Load<TextAsset>("state_data");
        string[] lines = csvFile.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split(',');
            StateData state = new StateData();
            state.Name = values[0];
            state.Population = int.Parse(values[1]);
            state.Nation = values[2];
            state.Resources = new Dictionary<string, int>();
            state.Resources["Steel"] = int.Parse(values[3]);
            state.Resources["Oil"] = int.Parse(values[4]);
            state.Resources["Rubber"] = int.Parse(values[5]);
            state.Resources["Chromium"] = int.Parse(values[6]);
            state.Resources["Tungsten"] = int.Parse(values[7]);
            state.Resources["Aluminium"] = int.Parse(values[8]);
            stateData[state.Name] = state;
        }
    }
}

