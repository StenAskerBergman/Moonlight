using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Manages the persistent collection of saved stamps.
/// Each stamp is stored as an individual JSON file under
/// <c>Application.persistentDataPath/Stamps/</c>.
/// </summary>
public class StampLibrary : MonoBehaviour
{
    public static StampLibrary Instance { get; private set; }

    /// <summary>All stamps currently loaded into memory.</summary>
    public List<StampData> Stamps { get; private set; } = new List<StampData>();

    public event Action<StampData> OnStampAdded;
    public event Action<StampData> OnStampRemoved;
    public event Action<StampData> OnStampUpdated;

    private string StampsFolder => Path.Combine(Application.persistentDataPath, "Stamps");

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureFolder();
        LoadAllStamps();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────── Public API ─────────

    /// <summary>Adds a stamp to the library and persists it to disk.</summary>
    public void SaveStamp(StampData stamp)
    {
        if (stamp == null) return;

        // Ensure it has an id.
        if (string.IsNullOrEmpty(stamp.id))
        {
            stamp.id = Guid.NewGuid().ToString();
        }

        // Replace existing if same id, otherwise add.
        int existing = Stamps.FindIndex(s => s.id == stamp.id);
        if (existing >= 0)
        {
            Stamps[existing] = stamp;
            OnStampUpdated?.Invoke(stamp);
        }
        else
        {
            Stamps.Add(stamp);
            OnStampAdded?.Invoke(stamp);
        }

        WriteToDisk(stamp);
    }

    /// <summary>Permanently deletes a stamp from library and disk.</summary>
    public void DeleteStamp(string id)
    {
        int index = Stamps.FindIndex(s => s.id == id);
        if (index < 0) return;

        StampData removed = Stamps[index];
        Stamps.RemoveAt(index);

        string path = GetFilePath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        OnStampRemoved?.Invoke(removed);
    }

    /// <summary>Renames an existing stamp and persists the change.</summary>
    public void RenameStamp(string id, string newName)
    {
        StampData stamp = GetStamp(id);
        if (stamp == null) return;

        stamp.stampName = newName;
        WriteToDisk(stamp);
        OnStampUpdated?.Invoke(stamp);
    }

    /// <summary>Updates the category of an existing stamp.</summary>
    public void SetStampCategory(string id, string newCategory)
    {
        StampData stamp = GetStamp(id);
        if (stamp == null) return;

        stamp.category = newCategory;
        WriteToDisk(stamp);
        OnStampUpdated?.Invoke(stamp);
    }

    /// <summary>Returns stamps filtered by category.</summary>
    public List<StampData> GetStampsByCategory(string category)
    {
        return Stamps.FindAll(s =>
            string.Equals(s.category, category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns all distinct category names.</summary>
    public List<string> GetCategories()
    {
        HashSet<string> cats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in Stamps)
        {
            if (!string.IsNullOrEmpty(s.category))
                cats.Add(s.category);
        }
        return new List<string>(cats);
    }

    /// <summary>Finds a stamp by its id.</summary>
    public StampData GetStamp(string id)
    {
        return Stamps.Find(s => s.id == id);
    }

    // ───────── Persistence ─────────

    private void LoadAllStamps()
    {
        Stamps.Clear();
        string folder = StampsFolder;
        if (!Directory.Exists(folder)) return;

        string[] files = Directory.GetFiles(folder, "*.json");
        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                StampData stamp = JsonUtility.FromJson<StampData>(json);
                if (stamp != null && !string.IsNullOrEmpty(stamp.id))
                {
                    Stamps.Add(stamp);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StampLibrary] Failed to load stamp file {file}: {e.Message}");
            }
        }

        Debug.Log($"[StampLibrary] Loaded {Stamps.Count} stamp(s) from {folder}");
    }

    private void WriteToDisk(StampData stamp)
    {
        EnsureFolder();
        string path = GetFilePath(stamp.id);
        string json = JsonUtility.ToJson(stamp, prettyPrint: true);
        File.WriteAllText(path, json);
    }

    private string GetFilePath(string id)
    {
        return Path.Combine(StampsFolder, $"{id}.json");
    }

    private void EnsureFolder()
    {
        if (!Directory.Exists(StampsFolder))
        {
            Directory.CreateDirectory(StampsFolder);
        }
    }
}
