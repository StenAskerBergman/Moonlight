using System;
using UnityEngine;

public enum ShortcutType
{
    Empty,
    Building,
    Tool
}

public enum ContextMenuToolType
{
    None,
    Demolish,
    BuildMenu,
    Pipette
}

[Serializable]
public class ShortcutData
{
    public ShortcutType Type = ShortcutType.Empty;
    public ContextMenuToolType ToolType = ContextMenuToolType.None;
    public string Id;
    public string DisplayName;
    public Sprite Icon;
    public GameObject BuildingPrefab;
    public BuildingData BuildingData;

    public bool IsEmpty => Type == ShortcutType.Empty;

    public static ShortcutData CreateEmpty()
    {
        return new ShortcutData
        {
            Type = ShortcutType.Empty,
            ToolType = ContextMenuToolType.None,
            Id = string.Empty,
            DisplayName = "Empty Slot"
        };
    }

    public static ShortcutData CreateTool(ContextMenuToolType toolType, Sprite icon = null, string displayName = null)
    {
        string name = displayName;
        if (string.IsNullOrEmpty(name))
        {
            switch (toolType)
            {
                case ContextMenuToolType.Demolish: name = "Demolish Mode"; break;
                case ContextMenuToolType.BuildMenu: name = "Building Menu"; break;
                case ContextMenuToolType.Pipette: name = "Pipette (Copy)"; break;
                default: name = toolType.ToString(); break;
            }
        }

        return new ShortcutData
        {
            Type = ShortcutType.Tool,
            ToolType = toolType,
            Id = $"tool:{toolType}",
            DisplayName = name,
            Icon = icon
        };
    }

    public static ShortcutData CreateBuilding(GameObject prefab, Sprite icon = null, string displayName = null, BuildingData data = null)
    {
        string name = displayName;
        if (string.IsNullOrEmpty(name))
        {
            if (data != null && !string.IsNullOrEmpty(data.buildingName)) name = data.buildingName;
            else if (prefab != null) name = prefab.name.Replace("(Clone)", "").Trim();
            else name = "Building";
        }

        string id = data != null && !string.IsNullOrEmpty(data.Id)
            ? data.Id
            : (prefab != null ? prefab.name : "building");

        return new ShortcutData
        {
            Type = ShortcutType.Building,
            ToolType = ContextMenuToolType.None,
            Id = id,
            DisplayName = name,
            Icon = icon,
            BuildingPrefab = prefab,
            BuildingData = data
        };
    }

    public string Serialize()
    {
        if (Type == ShortcutType.Tool)
        {
            return $"tool:{(int)ToolType}";
        }
        else if (Type == ShortcutType.Building)
        {
            string prefabName = BuildingPrefab != null ? BuildingPrefab.name : "";
            string dataId = BuildingData != null ? BuildingData.Id : "";
            return $"bld:{prefabName}:{dataId}:{DisplayName}";
        }
        return "empty";
    }

    public static ShortcutData Deserialize(string serialized)
    {
        if (string.IsNullOrEmpty(serialized) || serialized == "empty")
        {
            return CreateEmpty();
        }

        if (serialized.StartsWith("tool:"))
        {
            string raw = serialized.Substring(5);
            if (int.TryParse(raw, out int toolInt))
            {
                var tool = (ContextMenuToolType)toolInt;
                return CreateTool(tool);
            }
        }
        else if (serialized.StartsWith("bld:"))
        {
            string[] parts = serialized.Substring(4).Split(':');
            string prefabName = parts.Length > 0 ? parts[0] : "";
            string dataId = parts.Length > 1 ? parts[1] : "";
            string disp = parts.Length > 2 ? parts[2] : prefabName;

            GameObject prefab = null;
            if (!string.IsNullOrEmpty(dataId) && BuildingPrefabRegistry.Instance != null)
            {
                prefab = BuildingPrefabRegistry.Instance.GetPrefab(dataId);
            }
            if (prefab == null && !string.IsNullOrEmpty(prefabName))
            {
                if (BuildingPrefabRegistry.Instance != null)
                    prefab = BuildingPrefabRegistry.Instance.GetPrefab(prefabName);
            }

            return CreateBuilding(prefab, null, disp, null);
        }

        return CreateEmpty();
    }
}
