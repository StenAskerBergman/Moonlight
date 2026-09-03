using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the Tycoon construction catalogue - one CostData, one BuildingData and one
/// placeholder prefab per building - from the cost table below.
///
/// The table is the source of truth. Re-running the menu item updates the existing assets
/// in place rather than making duplicates, so correcting a number here and regenerating is
/// the intended workflow. Nothing outside the generated folders is touched, and a
/// BuildingData that already exists elsewhere in the project is reused rather than
/// replaced - the Depot, City Center and Gravel Extracter keep their authored assets.
///
/// FOOTPRINTS ARE PLACEHOLDERS. The cost tables carry no size data, so sizes come from the
/// per-category defaults in <see cref="DefaultSize"/> and are meant to be tuned by hand.
/// </summary>
public static class TycoonBuildingCatalogGenerator
{
    private const string CostFolder = "Assets/Data/Construction/Costs/Tycoon";
    private const string DataFolder = "Assets/Data/Construction/Building Placeholders/Tycoon";
    private const string PrefabFolder = "Assets/Prefabs/Building Prefabs/Faction Prefabs/Tycoon Faction/Generated";
    private const string PagePath = "Assets/Data/Construction/Pages/Tycoon Production.asset";

    // Item assets the costs are paid in. Resolved by asset name.
    private const string ModulesItem = "Building Modules";
    private const string ToolsItem = "Tools";
    private const string ConcreteItem = "Concrete";
    private const string SteelItem = "Steel";
    private const string HeavyWeaponsItem = "Heavy Weapons";

    private enum Tier { Worker, Employee, Engineer, Executive }

    private enum Kind
    {
        Production,   // ordinary production/industry building
        Civic,        // residence, civic, monument-scale
        Power,        // power plants
        Monument,     // multi-phase monuments
        Field,        // a single field tile placed around a farm
        Road          // priced per tile
    }

    private struct Entry
    {
        public string Name;
        public Tier Tier;
        public Kind Kind;
        public int Credits;
        public int Modules;
        public int Tools;
        public int Concrete;
        public int Steel;
        public int HeavyWeapons;
        public string Note;

        // Where this building sits in the construction menu's production graph.
        public string Line;      // production line it belongs to
        public int Column;       // step along the chain
        public int Row;          // parallel input within that step
        public string FeedsInto; // name of the building this one supplies, for the connector

        // Monthly economy. Left unset, both are derived from the build price - see
        // DerivedExpense/DerivedRevenue. Set explicitly for anything the rule cannot know.
        public int Revenue;
        public int Expense;
        public bool HasEconomy;

        public Entry(string name, Tier tier, Kind kind, int credits, int modules, int tools,
                     int concrete, int steel, int heavyWeapons = 0, string note = null)
        {
            Name = name; Tier = tier; Kind = kind;
            Credits = credits; Modules = modules; Tools = tools;
            Concrete = concrete; Steel = steel; HeavyWeapons = heavyWeapons;
            Note = note;
            Line = null; Column = 0; Row = 0; FeedsInto = null;
            // Unset means "derive from the build price" - see DerivedExpense/DerivedRevenue.
            Revenue = 0; Expense = 0; HasEconomy = false;
        }

        /// <summary>Places this building in the construction menu graph.</summary>
        public Entry At(string line, int column, int row = 0, string feedsInto = null)
        {
            Line = line; Column = column; Row = row; FeedsInto = feedsInto;
            return this;
        }

        /// <summary>
        /// States this building's monthly income and upkeep in credits, overriding the
        /// price-derived defaults. Use it where the category rule cannot be right -
        /// residences earn tax and pay no upkeep, which no formula over a build cost knows.
        /// </summary>
        public Entry Economy(int revenue, int expense)
        {
            Revenue = revenue; Expense = expense; HasEconomy = true;
            return this;
        }
    }

    // ---------------------------------------------------------------------------------
    // Residence rows are the INCREMENTAL upgrade cost, not the cumulative build value.
    // The raw extracted data accumulates the earlier residence's construction cost, which
    // would make every upgrade overcharge the player.
    // ---------------------------------------------------------------------------------
    private static readonly Entry[] Catalog =
    {
        // ---- Worker -------------------------------------------------------------
        new Entry("Worker Barracks",         Tier.Worker, Kind.Civic,       0,  2,  0,  0,  0).At("Civic", 0, 0, "City Center").Economy(8, 0),
        new Entry("City Center",             Tier.Worker, Kind.Civic,     300,  5,  3,  0,  0).At("Civic", 1, 0, "Casino"),
        new Entry("Casino",                  Tier.Worker, Kind.Civic,     300,  4,  4,  0,  0).At("Civic", 2, 0, null).Economy(40, 24),
        new Entry("Basalt Crusher",          Tier.Worker, Kind.Production, 50,  0,  1,  0,  0).At("Building Modules", 0, 0, "Smelter"),
        new Entry("Smelter",                 Tier.Worker, Kind.Production, 50,  0,  2,  0,  0).At("Building Modules", 1, 0, null),
        new Entry("Distillery",              Tier.Worker, Kind.Production, 50,  2,  1,  0,  0).At("Liquor", 0, 0, null),
        new Entry("Fishery",                 Tier.Worker, Kind.Production,125,  1,  2,  0,  0).At("Fish", 0, 0, null),
        new Entry("Rotary Excavator",        Tier.Worker, Kind.Production, 50,  1,  3,  0,  0).At("Gravel", 0, 0, null),
        new Entry("Coal Power Station",      Tier.Worker, Kind.Power,     350,  1,  4,  0,  0).At("Energy", 0, 0, null),

        // ---- Employee -----------------------------------------------------------
        new Entry("Employee House Upgrade",  Tier.Employee, Kind.Civic,      0,  0,  1,  0,  0, 0,
                  "Incremental upgrade cost from Worker Barracks. The Employee House already contains the original Barracks investment; do not charge it again.").At("Civic", 0, 0, null).Economy(16, 0),
        new Entry("Iron Ore Mine",           Tier.Employee, Kind.Production, 600, 2,  4,  0,  0).At("Tools", 0, 0, "Iron Smelter"),
        new Entry("Coal Mine",               Tier.Employee, Kind.Production, 600, 2,  2,  0,  0).At("Tools", 0, 1, "Iron Smelter"),
        new Entry("Iron Smelter",            Tier.Employee, Kind.Production, 400, 2,  4,  0,  0).At("Tools", 1, 0, "Tools Workshop"),
        new Entry("Tools Workshop",          Tier.Employee, Kind.Production, 500, 2,  3,  0,  0).At("Tools", 2, 0, null),
        new Entry("Sand Extractor",          Tier.Employee, Kind.Production, 200, 2,  4,  0,  0).At("Concrete", 0, 1, "Concrete Factory"),
        new Entry("Limestone Quarry",        Tier.Employee, Kind.Production, 300, 4,  4,  0,  0).At("Concrete", 0, 0, "Concrete Factory"),
        new Entry("Concrete Factory",        Tier.Employee, Kind.Production, 250, 3,  4,  0,  0).At("Concrete", 1, 0, null),
        new Entry("Meat Factory",            Tier.Employee, Kind.Production, 100, 1,  2,  3,  0).At("Food", 0, 0, "Flavor Lab"),
        new Entry("Flavor Lab",              Tier.Employee, Kind.Production, 150, 2,  3,  5,  0).At("Food", 1, 0, "Food Supply Factory"),
        new Entry("Food Supply Factory",     Tier.Employee, Kind.Production, 200, 2,  4,  7,  0).At("Food", 2, 0, null),
        new Entry("Oil Driller",             Tier.Employee, Kind.Production, 150, 1,  1,  3,  0).At("Plastics", 0, 0, "Oil Refinery"),
        new Entry("Oil Refinery",            Tier.Employee, Kind.Production, 400, 2,  6,  0,  0).At("Plastics", 1, 0, "Plastics Factory"),
        new Entry("Plastics Factory",        Tier.Employee, Kind.Production, 300, 2,  3,  6,  0).At("Plastics", 2, 0, null),
        new Entry("Munitions Factory",       Tier.Employee, Kind.Production,1000, 4,  6,  0,  0).At("Military", 0, 0, null),
        new Entry("Ministry of Truth",       Tier.Employee, Kind.Civic,      800,10, 12, 20,  0).At("Civic", 1, 0, null),
        new Entry("Tycoon Shipyard",         Tier.Employee, Kind.Production, 400, 3,  5,  9,  0).At("Civic", 2, 0, null),
        new Entry("Waste Compactor",         Tier.Employee, Kind.Production, 800, 4,  5, 12,  0).At("Civic", 3, 0, null),

        // ---- Engineer -----------------------------------------------------------
        new Entry("Engineer Apartment Upgrade", Tier.Engineer, Kind.Civic,     0,  2,  2,  3,  0, 0,
                  "Incremental upgrade cost from Employee House. Not the cumulative building value.").At("Civic", 0, 0, null).Economy(32, 0),
        new Entry("Steelworks",              Tier.Engineer, Kind.Production, 400, 6,  6,  8,  0).At("Steel", 0, 0, null),
        new Entry("Lobster Farm",            Tier.Engineer, Kind.Production, 300, 5,  3,  6,  4).At("Gourmet", 0, 0, "Gourmet Factory"),
        new Entry("Truffle Farm",            Tier.Engineer, Kind.Production, 200, 0,  3,  8,  3).At("Gourmet", 0, 1, "Gourmet Factory"),
        new Entry("Gourmet Factory",         Tier.Engineer, Kind.Production, 400, 8,  4, 10,  6).At("Gourmet", 1, 0, null),
        new Entry("Uranium Mine",            Tier.Engineer, Kind.Production,1000, 7, 12,  8,  2).At("Nuclear", 0, 0, "Fuel Element Factory"),
        new Entry("Fuel Element Factory",    Tier.Engineer, Kind.Production, 600, 5, 14,  4,  8).At("Nuclear", 1, 0, "Nuclear Power Plant"),
        new Entry("Nuclear Power Plant",     Tier.Engineer, Kind.Power,     3000,10, 24, 30, 36).At("Nuclear", 2, 0, null),
        new Entry("Vineyard",                Tier.Engineer, Kind.Production, 200, 0,  4,  7,  8).At("Champagne", 0, 0, "Champagne Cellar"),
        new Entry("Sugar Beet Plantation",   Tier.Engineer, Kind.Production, 500, 7,  8,  0,  0).At("Champagne", 0, 1, "Champagne Cellar"),
        new Entry("Champagne Cellar",        Tier.Engineer, Kind.Production, 400, 7,  8, 12,  5).At("Champagne", 1, 0, null),
        new Entry("Explosives Factory",      Tier.Engineer, Kind.Production, 600, 0,  6,  8,  5).At("Military", 0, 0, "Arsenal"),
        new Entry("Arsenal",                 Tier.Engineer, Kind.Production,1000, 4,  8,  0, 12).At("Military", 1, 0, null),
        new Entry("Financial Center",        Tier.Engineer, Kind.Civic,     1200,20,  8, 30, 15).At("Civic", 1, 0, null).Economy(150, 96),
        new Entry("Deacidification Station", Tier.Engineer, Kind.Production,2000,15, 20, 15, 10).At("Civic", 2, 0, null),
        new Entry("Banes Avenue",            Tier.Engineer, Kind.Road,        30, 0,  0,  0,  0, 0,
                  "Priced PER TILE. Multiply by the number of tiles laid.").At("Civic", 3, 0, null),

        // ---- Executive ----------------------------------------------------------
        new Entry("Executive Mansion Upgrade", Tier.Executive, Kind.Civic,     0,  1,  3,  3,  4, 0,
                  "Incremental upgrade cost from Engineer Apartment. Not the cumulative building value.").At("Civic", 0, 0, null).Economy(64, 0),
        new Entry("Gold Refinery",           Tier.Executive, Kind.Production, 400, 7, 12,  0,  8).At("Jewellery", 0, 0, "Gold Smeltery"),
        new Entry("Gold Smeltery",           Tier.Executive, Kind.Production, 500, 8,  6, 15,  6).At("Jewellery", 1, 0, "Jewelery Manufactory"),
        new Entry("Diamond Harvesting Station", Tier.Executive, Kind.Production,2000,20,12, 0, 0).At("Jewellery", 1, 1, "Jewelery Manufactory"),
        new Entry("Jewelery Manufactory",    Tier.Executive, Kind.Production, 600, 9,  8, 18,  8).At("Jewellery", 2, 0, null),
        new Entry("Fat Factory",             Tier.Executive, Kind.Production, 100, 0,  6, 12, 14).At("Food", 1, 0, null),
        new Entry("Aquafarm",                Tier.Executive, Kind.Production, 500, 6,  2,  0,  0).At("Food", 0, 0, "Fat Factory"),
        new Entry("Chemical Plant",          Tier.Executive, Kind.Production, 400,12, 20, 20, 15).At("Chemicals", 1, 0, null),
        new Entry("Manganese Excavation Robot", Tier.Executive, Kind.Production,1000,12,6, 0, 0).At("Chemicals", 0, 0, "Chemical Plant"),
        new Entry("Rare-Earth Borer",        Tier.Executive, Kind.Production,1500,15, 12,  0,  0).At("Chemicals", 0, 1, "Chemical Plant"),
        new Entry("Healthcare Office",       Tier.Executive, Kind.Civic,     1000,15, 20, 25, 20).At("Civic", 1, 0, null),
        new Entry("CO2 Reservoir",           Tier.Executive, Kind.Production,5000,30, 20, 25, 25).At("Civic", 2, 0, null),
        new Entry("Missile Launch Pad",      Tier.Executive, Kind.Monument, 20000,50, 35, 36, 36, 20).At("Monuments", 0, 0, null),
        new Entry("Corporate HQ Foundation", Tier.Executive, Kind.Monument, 50000,80, 50, 25, 25, 0,
                  "FOUNDATION ONLY. The monument then consumes further construction materials across three building phases, which are not part of this cost.").At("Monuments", 1, 0, null),

        // ---- Fields -------------------------------------------------------------
        // Placement cost per field tile; the count each farm needs is in the note.
        new Entry("Rice Paddy",              Tier.Worker, Kind.Field,   5, 0, 0, 0, 0, 0, "2 fields required.").At("Fields", 0, 0, null),
        new Entry("Pigsty",                  Tier.Worker, Kind.Field,  15, 0, 0, 0, 0, 0, "3 fields required.").At("Fields", 0, 1, null),
        new Entry("Vegetable Cultivation",   Tier.Worker, Kind.Field,  20, 0, 0, 0, 0, 0, "3 fields required.").At("Fields", 0, 2, null),
        new Entry("Truffle Farm Field",      Tier.Engineer, Kind.Field,  20, 0, 0, 0, 0, 0, "5 fields required.").At("Fields", 0, 0, null),
        new Entry("Vineyard Field",          Tier.Engineer, Kind.Field,  15, 0, 0, 0, 0, 0, "7 fields required.").At("Fields", 0, 1, null),
        new Entry("Slaughterhouse",          Tier.Employee, Kind.Field,  10, 0, 0, 0, 0, 0, "6 fields required.").At("Fields", 0, 0, null),
        new Entry("Sugar Beet Field",        Tier.Engineer, Kind.Field,  50, 0, 0, 0, 0, 0, "7 fields required.").At("Fields", 0, 2, null),
        new Entry("Diamond Harvesting Field",Tier.Executive, Kind.Field, 200, 0, 0, 0, 0, 0, "8 fields required.").At("Fields", 0, 0, null),
        new Entry("Algae Farm Field",        Tier.Executive, Kind.Field, 150, 0, 0, 0, 0, 0, "6 fields required.").At("Fields", 0, 1, null),
    };

    [MenuItem("Moonlight/Buildings/Generate Tycoon Catalogue")]
    public static void Generate()
    {
        Dictionary<string, ItemData> items = ResolveCostItems();
        if (items == null) return;

        EnsureFolder(CostFolder);
        EnsureFolder(DataFolder);
        EnsureFolder(PrefabFolder);

        int created = 0, updated = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < Catalog.Length; i++)
            {
                Entry entry = Catalog[i];
                EditorUtility.DisplayProgressBar("Tycoon catalogue", entry.Name, (float)i / Catalog.Length);

                CostData cost = UpsertCostData(entry, items, ref created, ref updated);
                BuildingData data = UpsertBuildingData(entry, ref created, ref updated);
                UpsertPrefab(entry, data, cost, ref created, ref updated);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        int lines = BuildProductionPage();
        int appended = AppendMissingToProductionPage();
        int published = PublishToPrefabRegistry();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Tycoon catalogue: {Catalog.Length} buildings processed, {created} assets created, " +
                  $"{updated} updated, {lines} production lines on the construction page " +
                  $"({appended} appended for buildings that had none), " +
                  $"{published} prefabs published to BuildingPrefabRegistry.");
    }

    // ---------------------------------------------------------------------------------
    // Construction menu
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Rewrites the Tycoon production page from the catalogue.
    ///
    /// One page asset serves all four tier pages - ConstructionMenuProductionHost installs
    /// it four times and ProductionLineDefinition.Tier decides which lines each tier shows.
    /// Authored icons are carried across by BuildingData, so regenerating does not wipe art
    /// somebody has already assigned.
    /// </summary>
    private static int BuildProductionPage()
    {
        var page = AssetDatabase.LoadAssetAtPath<ConstructionPageDefinition>(PagePath);
        if (page != null && page.ProductionLines != null && page.ProductionLines.Length > 0)
        {
            // The production page has been fully authored with Anno 2070 DAG layouts and icons.
            // Do not overwrite it with unconfigured catalog dummies.
            return page.ProductionLines.Length;
        }

        if (page == null)
        {
            EnsureFolder(Path.GetDirectoryName(PagePath).Replace('\\', '/'));
            page = ScriptableObject.CreateInstance<ConstructionPageDefinition>();
            AssetDatabase.CreateAsset(page, PagePath);
        }

        Dictionary<BuildingData, Sprite> existingIcons = CollectAuthoredIcons(page);
        Dictionary<string, Sprite> existingLineIcons = CollectAuthoredLineIcons(page);

        var lines = new List<ProductionLineDefinition>();

        foreach (Tier tier in new[] { Tier.Worker, Tier.Employee, Tier.Engineer, Tier.Executive })
        {
            // Preserve the order the lines appear in the catalogue rather than sorting them.
            var lineOrder = new List<string>();
            var byLine = new Dictionary<string, List<Entry>>();

            foreach (Entry entry in Catalog)
            {
                if (entry.Tier != tier || string.IsNullOrEmpty(entry.Line)) continue;
                if (!byLine.TryGetValue(entry.Line, out List<Entry> bucket))
                {
                    bucket = new List<Entry>();
                    byLine[entry.Line] = bucket;
                    lineOrder.Add(entry.Line);
                }
                bucket.Add(entry);
            }

            foreach (string lineName in lineOrder)
            {
                List<Entry> entries = byLine[lineName];
                string lineId = $"tycoon.{tier.ToString().ToLowerInvariant()}.{Slug(lineName)}";

                var nodes = new List<ProductionNodeDefinition>();
                var connections = new List<ProductionConnectionDefinition>();
                var nodeIds = new HashSet<string>();

                foreach (Entry entry in entries)
                {
                    BuildingData data = FindBuildingDataByName(entry.Name);
                    if (data == null) continue;

                    string nodeId = Slug(entry.Name);
                    if (!nodeIds.Add(nodeId)) continue;

                    Sprite icon;
                    existingIcons.TryGetValue(data, out icon);

                    nodes.Add(new ProductionNodeDefinition
                    {
                        Id = nodeId,
                        BuildingData = data,
                        DisplayName = entry.Name,
                        Icon = icon,
                        Column = entry.Column,
                        Row = entry.Row,
                        UnlockCondition = new PopulationUnlock(),
                    });
                }

                // Connections come last so both endpoints are known to exist - a connection
                // naming a missing node is exactly what ValidateDefinition reports.
                foreach (Entry entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.FeedsInto)) continue;

                    string from = Slug(entry.Name);
                    string to = Slug(entry.FeedsInto);
                    if (!nodeIds.Contains(from) || !nodeIds.Contains(to)) continue;

                    connections.Add(new ProductionConnectionDefinition
                    {
                        FromNodeId = from,
                        ToNodeId = to,
                        Type = ProductionConnectionType.Horizontal,
                        JunctionPosition = 0.5f,
                    });
                }

                if (nodes.Count == 0) continue;

                Sprite outputIcon;
                existingLineIcons.TryGetValue(lineId, out outputIcon);

                lines.Add(new ProductionLineDefinition
                {
                    Id = lineId,
                    DisplayName = lineName,
                    OutputIcon = outputIcon,
                    Tier = PopulationClassFor(tier),
                    UnlockCondition = new PopulationUnlock(),
                    Nodes = nodes.ToArray(),
                    Connections = connections.ToArray(),
                });
            }
        }

        page.Faction = Enums.Faction.Tyc;
        page.Section = ConstructionSection.Production;
        page.ProductionLines = lines.ToArray();
        EditorUtility.SetDirty(page);

        foreach (string problem in page.ValidateDefinition())
        {
            Debug.LogWarning("Construction page: " + problem, page);
        }

        return lines.Count;
    }

    [MenuItem("Moonlight/Buildings/Append Missing Buildings To Page")]
    public static void AppendMissingBuildings()
    {
        int appended = AppendMissingToProductionPage();
        AssetDatabase.SaveAssets();
        Debug.Log($"Construction page: appended {appended} line(s) for catalogue buildings that had no node.");
    }

    /// <summary>
    /// Adds a node for every catalogue building the page does not already show, without
    /// touching a single existing line.
    ///
    /// The page's production chains are hand-authored - DAG layout, connector routing and
    /// icons that no generator can infer - so <see cref="BuildProductionPage"/> refuses to
    /// rewrite it. That left the buildings outside those chains (residence upgrades, civic
    /// and monument builds, farm fields) with no way into the menu at all. This appends
    /// them as their own lines and leaves the authored ones exactly as they are.
    ///
    /// Idempotent: a building already anywhere on the page is skipped, so re-running adds
    /// nothing and re-ordering an appended line by hand survives the next run.
    /// </summary>
    private static int AppendMissingToProductionPage()
    {
        var page = AssetDatabase.LoadAssetAtPath<ConstructionPageDefinition>(PagePath);
        if (page == null) return 0;

        // Lines this generator appended previously are dropped and rebuilt. Authored
        // lines use ids like "tycoon.tools"; appended ones carry a tier segment
        // ("tycoon.worker.fields"), so the two are told apart without a marker field.
        var existingLines = new List<ProductionLineDefinition>();
        foreach (ProductionLineDefinition line in page.ProductionLines ?? new ProductionLineDefinition[0])
        {
            if (line != null && IsGeneratedLineId(line.Id)) continue;
            existingLines.Add(line);
        }
        int removed = (page.ProductionLines != null ? page.ProductionLines.Length : 0) - existingLines.Count;

        // Buildings the civic lanes already show must not also appear as production nodes.
        HashSet<string> civicLanePaths = TycoonConstructionMenuBuilder.GetCivicLanePrefabPaths();

        var onPage = new HashSet<BuildingData>();
        var usedLineIds = new HashSet<string>();
        foreach (ProductionLineDefinition line in existingLines)
        {
            if (line == null) continue;
            if (!string.IsNullOrEmpty(line.Id)) usedLineIds.Add(line.Id);
            if (line.Nodes == null) continue;
            foreach (ProductionNodeDefinition node in line.Nodes)
            {
                if (node != null && node.BuildingData != null) onPage.Add(node.BuildingData);
            }
        }

        int appended = 0;

        foreach (Tier tier in new[] { Tier.Worker, Tier.Employee, Tier.Engineer, Tier.Executive })
        {
            var lineOrder = new List<string>();
            var byLine = new Dictionary<string, List<Entry>>();

            foreach (Entry entry in Catalog)
            {
                if (entry.Tier != tier) continue;

                BuildingData data = FindBuildingDataByName(entry.Name);
                if (data == null || onPage.Contains(data)) continue;

                // Already reachable from a Public or Special lane on this page.
                if (civicLanePaths.Contains($"{PrefabFolder}/{entry.Tier}/{entry.Name}.prefab")) continue;

                string lineName = string.IsNullOrEmpty(entry.Line) ? "Other" : entry.Line;
                if (!byLine.TryGetValue(lineName, out List<Entry> bucket))
                {
                    bucket = new List<Entry>();
                    byLine[lineName] = bucket;
                    lineOrder.Add(lineName);
                }
                bucket.Add(entry);
            }

            foreach (string lineName in lineOrder)
            {
                List<Entry> entries = byLine[lineName];

                string lineId = $"tycoon.{tier.ToString().ToLowerInvariant()}.{Slug(lineName)}";
                string uniqueId = lineId;
                int suffix = 2;
                while (usedLineIds.Contains(uniqueId)) uniqueId = lineId + "_" + suffix++;
                usedLineIds.Add(uniqueId);

                var nodes = new List<ProductionNodeDefinition>();
                var nodeIds = new HashSet<string>();

                // Laid out in a single row in catalogue order. These are standalone builds
                // rather than a chain, so there is nothing to route connectors between;
                // Column/Row from the catalogue would leave gaps in the grid.
                int column = 0;
                foreach (Entry entry in entries)
                {
                    BuildingData data = FindBuildingDataByName(entry.Name);
                    if (data == null) continue;

                    string nodeId = Slug(entry.Name);
                    if (!nodeIds.Add(nodeId)) continue;

                    nodes.Add(new ProductionNodeDefinition
                    {
                        Id = nodeId,
                        BuildingData = data,
                        DisplayName = entry.Name,
                        Icon = null,
                        Column = column++,
                        Row = 0,
                        UnlockCondition = new PopulationUnlock(),
                    });
                    onPage.Add(data);
                }

                if (nodes.Count == 0) continue;

                existingLines.Add(new ProductionLineDefinition
                {
                    Id = uniqueId,
                    DisplayName = lineName,
                    OutputIcon = null,
                    Tier = PopulationClassFor(tier),
                    UnlockCondition = new PopulationUnlock(),
                    Nodes = nodes.ToArray(),
                    Connections = new ProductionConnectionDefinition[0],
                });
                appended++;
            }
        }

        if (appended == 0 && removed == 0) return 0;

        page.ProductionLines = existingLines.ToArray();
        EditorUtility.SetDirty(page);

        foreach (string problem in page.ValidateDefinition())
        {
            Debug.LogWarning("Construction page: " + problem, page);
        }

        return appended;
    }

    /// <summary>
    /// Whether a production line id was written by this generator rather than authored by
    /// hand. Generated ids carry the tier as their second segment.
    /// </summary>
    private static bool IsGeneratedLineId(string lineId)
    {
        if (string.IsNullOrEmpty(lineId)) return false;

        foreach (Tier tier in new[] { Tier.Worker, Tier.Employee, Tier.Engineer, Tier.Executive })
        {
            if (lineId.StartsWith($"tycoon.{tier.ToString().ToLowerInvariant()}.")) return true;
        }
        return false;
    }

    private static Dictionary<BuildingData, Sprite> CollectAuthoredIcons(ConstructionPageDefinition page)
    {
        var icons = new Dictionary<BuildingData, Sprite>();
        if (page.ProductionLines == null) return icons;

        foreach (ProductionLineDefinition line in page.ProductionLines)
        {
            if (line?.Nodes == null) continue;
            foreach (ProductionNodeDefinition node in line.Nodes)
            {
                if (node?.BuildingData != null && node.Icon != null) icons[node.BuildingData] = node.Icon;
            }
        }
        return icons;
    }

    private static Dictionary<string, Sprite> CollectAuthoredLineIcons(ConstructionPageDefinition page)
    {
        var icons = new Dictionary<string, Sprite>();
        if (page.ProductionLines == null) return icons;

        foreach (ProductionLineDefinition line in page.ProductionLines)
        {
            if (line != null && !string.IsNullOrEmpty(line.Id) && line.OutputIcon != null)
            {
                icons[line.Id] = line.OutputIcon;
            }
        }
        return icons;
    }

    private static PopulationClass PopulationClassFor(Tier tier)
    {
        switch (tier)
        {
            case Tier.Employee: return PopulationClass.Employees;
            case Tier.Engineer: return PopulationClass.Engineers;
            case Tier.Executive: return PopulationClass.Executives;
            default: return PopulationClass.Workers;
        }
    }

    private static BuildingData FindBuildingDataByName(string buildingName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && data.buildingName == buildingName) return data;
        }
        return null;
    }

    /// <summary>
    /// Adds every generated prefab to the scene registry's manual list.
    ///
    /// The registry otherwise learns prefabs by scanning BuildingButton components, and
    /// the generated catalogue deliberately has no buttons - the production page resolves
    /// prefabs through this registry instead.
    /// </summary>
    private static int PublishToPrefabRegistry()
    {
        BuildingPrefabRegistry registry = null;
        foreach (var candidate in Resources.FindObjectsOfTypeAll<BuildingPrefabRegistry>())
        {
            if (EditorUtility.IsPersistent(candidate) || candidate.gameObject.scene.name == null) continue;
            registry = candidate;
            break;
        }

        if (registry == null)
        {
            Debug.LogWarning("No BuildingPrefabRegistry in the open scene - generated prefabs were not published. " +
                             "The production menu resolves prefabs through it, so its buildings will not be placeable.");
            return 0;
        }

        var so = new SerializedObject(registry);
        SerializedProperty list = so.FindProperty("additionalPrefabs");

        var known = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object value = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (value != null) known.Add(value);
        }

        int added = 0;
        foreach (Entry entry in Catalog)
        {
            string path = $"{PrefabFolder}/{entry.Tier}/{entry.Name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || known.Contains(prefab)) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = prefab;
            known.Add(prefab);
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
        return added;
    }

    // ---------------------------------------------------------------------------------

    private static Dictionary<string, ItemData> ResolveCostItems()
    {
        var wanted = new[] { ModulesItem, ToolsItem, ConcreteItem, SteelItem, HeavyWeaponsItem };
        var found = new Dictionary<string, ItemData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null) continue;
            foreach (string name in wanted)
            {
                if (item.name == name && !found.ContainsKey(name)) found[name] = item;
            }
        }

        var missing = new List<string>();
        foreach (string name in wanted) if (!found.ContainsKey(name)) missing.Add(name);

        // Heavy Weapons is the one cost item the project does not have yet - it appears
        // only in the Missile Launch Pad. Everything else must already exist; inventing a
        // second "Tools" would split the currency the player is actually holding.
        if (missing.Count == 1 && missing[0] == HeavyWeaponsItem)
        {
            found[HeavyWeaponsItem] = CreateHeavyWeaponsItem();
            missing.Clear();
        }

        if (missing.Count > 0)
        {
            Debug.LogError("Tycoon catalogue aborted - missing cost ItemData assets: " + string.Join(", ", missing));
            return null;
        }
        return found;
    }

    private static ItemData CreateHeavyWeaponsItem()
    {
        const string path = "Assets/Prefabs/Item Prefabs/Materials/Refined/Heavy Weapons.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (existing != null) return existing;

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.name = "Heavy Weapons";
        item.displayName = "Heavy Weapons";
        item.itemName = "heavy_weapons";
        item.description = "Military hardware. Consumed by the Missile Launch Pad.";
        item.baseValue = 120f;
        item.isStackable = true;
        item.isTradeable = true;

        var so = new SerializedObject(item);
        so.FindProperty("identifier").stringValue = "core:heavy_weapons";
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(item, path);
        Debug.LogWarning("Tycoon catalogue created a new ItemData 'Heavy Weapons' - it had no asset. " +
                         "Give it an icon and a production chain before shipping.", item);
        return item;
    }

    private static CostData UpsertCostData(Entry entry, Dictionary<string, ItemData> items, ref int created, ref int updated)
    {
        string path = $"{CostFolder}/{entry.Name} Cost.asset";
        var cost = AssetDatabase.LoadAssetAtPath<CostData>(path);
        bool isNew = cost == null;
        if (isNew)
        {
            cost = ScriptableObject.CreateInstance<CostData>();
            AssetDatabase.CreateAsset(cost, path);
            created++;
        }
        else updated++;

        var costItems = new List<ItemData>();
        var costAmounts = new List<int>();
        Add(costItems, costAmounts, items[ModulesItem], entry.Modules);
        Add(costItems, costAmounts, items[ToolsItem], entry.Tools);
        Add(costItems, costAmounts, items[ConcreteItem], entry.Concrete);
        Add(costItems, costAmounts, items[SteelItem], entry.Steel);
        Add(costItems, costAmounts, items[HeavyWeaponsItem], entry.HeavyWeapons);

        cost.price = entry.Credits;
        cost.revenue = entry.HasEconomy ? entry.Revenue : DerivedRevenue(entry);
        cost.expense = entry.HasEconomy ? entry.Expense : DerivedExpense(entry);
        cost.costItems = costItems.ToArray();
        cost.costAmounts = costAmounts.ToArray();
        EditorUtility.SetDirty(cost);
        return cost;
    }

    // ---------------------------------------------------------------------------------
    // MONTHLY ECONOMY
    //
    // These are DERIVED, not extracted. The cost tables state build costs only, so upkeep
    // is taken as a fraction of the build price per category and income is stated
    // explicitly for the few buildings that earn credits directly.
    //
    // The bank applies (revenue - expense) across every standing building once a month,
    // so these are the numbers that decide whether an economy is survivable. Treat them as
    // a coherent starting point to balance against, not as Anno's real figures.
    // ---------------------------------------------------------------------------------

    /// <summary>Monthly upkeep in credits, as a fraction of what the building cost to put up.</summary>
    private static int DerivedExpense(Entry entry)
    {
        if (entry.Credits <= 0) return 0;

        float rate;
        switch (entry.Kind)
        {
            case Kind.Power:    rate = 0.20f; break;  // fuel and staff dominate
            case Kind.Civic:    rate = 0.08f; break;  // cheap to keep open
            case Kind.Monument: rate = 0.02f; break;  // enormous to build, modest to run
            case Kind.Field:
            case Kind.Road:     rate = 0f;    break;  // upkeep belongs to the parent building
            default:            rate = 0.10f; break;  // Production
        }

        return Mathf.RoundToInt(entry.Credits * rate);
    }

    /// <summary>
    /// Monthly income in credits. Zero for production buildings by design: they earn by
    /// selling what they make, which the production and warehouse systems handle - booking
    /// that as bank revenue as well would pay the player twice for the same goods.
    /// </summary>
    private static int DerivedRevenue(Entry entry)
    {
        return 0;
    }

    private static void Add(List<ItemData> items, List<int> amounts, ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;
        items.Add(item);
        amounts.Add(amount);
    }

    private static BuildingData UpsertBuildingData(Entry entry, ref int created, ref int updated)
    {
        // Reuse an authored BuildingData wherever one already exists under this name,
        // rather than shadowing it with a generated one.
        BuildingData data = FindExistingBuildingData(entry.Name);
        string path = $"{DataFolder}/{entry.Name}.asset";

        if (data == null)
        {
            data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        }

        bool isNew = data == null;
        if (isNew)
        {
            data = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(data, path);
            created++;
        }
        else updated++;

        // Assets inside the placeholder folder are ours to size; the earlier UI pass left
        // several of them at a token 1x1 that means nothing. An asset authored elsewhere
        // (City Center's 6x8) keeps whatever it was given.
        bool generatorOwnsSize = AssetDatabase.GetAssetPath(data).StartsWith(DataFolder);

        var so = new SerializedObject(data);
        so.FindProperty("identifier").stringValue = "core:tycoon_" + Slug(entry.Name);
        so.ApplyModifiedPropertiesWithoutUndo();

        data.buildingName = entry.Name;
        data.buildingType = BuildingTypeFor(entry.Kind);
        if (generatorOwnsSize || data.buildingSize == Vector3.zero) data.buildingSize = DefaultSize(entry.Kind);

        string note = string.IsNullOrEmpty(entry.Note) ? string.Empty : " " + entry.Note;
        data.buildingDescription = $"{entry.Tier} tier Tycoon {entry.Kind.ToString().ToLowerInvariant()}. " +
                                   $"Placeholder art and footprint.{note}";

        EditorUtility.SetDirty(data);
        return data;
    }

    private static BuildingData FindExistingBuildingData(string buildingName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(DataFolder)) continue; // generated ones are handled by name

            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (data != null && data.buildingName == buildingName) return data;
        }
        return null;
    }

    private static void UpsertPrefab(Entry entry, BuildingData data, CostData cost, ref int created, ref int updated)
    {
        string folder = $"{PrefabFolder}/{entry.Tier}";
        EnsureFolder(folder);
        string path = $"{folder}/{entry.Name}.prefab";

        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        bool isNew = root == null;

        GameObject working = isNew
            ? new GameObject(entry.Name)
            : (GameObject)PrefabUtility.InstantiatePrefab(root);

        // The minimum a building needs to survive placement: Building carries the grid
        // occupancy and the quay foundation ownership, BuildingProperties is dereferenced
        // unguarded by BuildingPlacer, and BuildingCost is what pays for it.
        Building building = Require<Building>(working);
        BuildingProperties properties = Require<BuildingProperties>(working);
        BuildingCost buildingCost = Require<BuildingCost>(working);
        Require<BuildingPlaceholderModel>(working);

        building.buildingData = data;

        properties.buildingData = data;
        properties.costData = cost;
        properties.buildingSize = data.buildingSize;
        properties.buildingName = entry.Name;
        properties.buildingDescription = data.buildingDescription;

        // BuildingCost.costData is the field the affordability path actually reads.
        buildingCost.costData = cost;

        PrefabUtility.SaveAsPrefabAsset(working, path);
        Object.DestroyImmediate(working);

        if (isNew) created++; else updated++;
    }

    private static T Require<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static string BuildingTypeFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Field: return "Field";
            case Kind.Road: return "Road";
            case Kind.Power: return "Power";
            case Kind.Civic: return "Civic";
            case Kind.Monument: return "Monument";
            default: return "Production";
        }
    }

    /// <summary>
    /// Placeholder footprints. The cost tables carry no size data, so these are a
    /// readable starting point by category rather than anything authoritative.
    /// </summary>
    private static Vector3 DefaultSize(Kind kind)
    {
        switch (kind)
        {
            case Kind.Field: return new Vector3(1, 1, 1);
            case Kind.Road: return new Vector3(1, 1, 1);
            case Kind.Civic: return new Vector3(4, 1, 4);
            case Kind.Power: return new Vector3(4, 1, 4);
            case Kind.Monument: return new Vector3(6, 1, 6);
            default: return new Vector3(3, 1, 3);
        }
    }

    private static string Slug(string value)
    {
        return value.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        Directory.CreateDirectory(folder);
    }
}
