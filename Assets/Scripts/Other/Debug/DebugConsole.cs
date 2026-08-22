using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEngine;

public class DebugConsole : MonoBehaviour
{
    bool showConsole;
    string input;


    public List<object> commandList;

    public static DebugCommand AddItem;
    public static DebugCommand SpawnUnit;

    private void Awake()
    {
        AddItem = new DebugCommand("additem", "adds a item to selection.", "additem", () =>
        {
            Debug.Log("Added Item!");// UnitSelections.GetSelectedUnitInventory(); // Something similar to this Psduo code
        });

        SpawnUnit = new DebugCommand("spawnunit", "Spawns a unit by domain.", "spawnunit <ship|sub|humanoid|aircraft>", () =>
        {
            if (UnitService.Instance != null && Camera.main != null)
            {
                UnitService.Instance.RequestUnit(MoveType.Watercraft, Camera.main.transform.position + Camera.main.transform.forward * 20f);
                Debug.Log("Requested Watercraft unit spawn via UnitService.");
            }
        });

        commandList = new List<object>()
        {
            AddItem,
            SpawnUnit,
        };
    }

    public void OnToggleDebug()
    {
        showConsole = !showConsole;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.BackQuote)) // Assuming you meant the ` key
        {
            OnToggleDebug();
        }
    }

    private void OnGUI()
    {
        if (showConsole){ return; }

        float y = 0f;

        GUI.Box(new Rect(0, y, Screen.width, 30), "");
        GUI.backgroundColor = new Color(0,0, 0, 0);
        input = GUI.TextField(new Rect(10f, y + 5f, Screen.width - 20f, 20f), input);
    }
}

public class DebugCommandBase
{
    private string _commandId;
    private string _commandDescription;
    private string _commandFormat;

    public string commandId { get { return _commandId; } }
    public string commandDescription { get { return _commandDescription; } }
    public string commandFormat { get { return _commandFormat; } }

    public DebugCommandBase(string id, string description, string format)
    {
        _commandId = id;
        _commandDescription = description;
        _commandFormat = format;
    }
}

public class DebugCommand : DebugCommandBase
{
    private Action command;

    public DebugCommand(string id, string description, string format, Action command)
        : base(id, description, format)
    {
        this.command = command;
    }

    public void Invoke()
    {
        command.Invoke();
    }
}