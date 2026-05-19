// InventoryMonitor - Space Engineers Programmable Block Script
// Monitors item quantities across the grid and controls [MONITOR] tagged lights.
//
// Light Custom Data format:
//   Line 1: Type/Subtype   (e.g. Ingot/Iron  or  Component/SteelPlate)
//   Line 2: threshold=N    (e.g. threshold=500)
//
// Supported short type names: Ingot, Ore, Component, AmmoMagazine, Tool, GasContainer
//
// Light states:
//   Green  (solid)    - stock is at or above threshold
//   Orange (blinking) - stock is below threshold
//   Red    (solid)    - Custom Data is missing or malformed

// -------------------------------------------------------------------------
// Configuration
// -------------------------------------------------------------------------

private const string MONITOR_TAG = "[MONITOR]";

// Blink settings for the alert state
private const float BLINK_INTERVAL_SECONDS = 1.0f;
private const float BLINK_LENGTH           = 0.5f; // 0.0-1.0, fraction of cycle light is ON

// Light intensity and radius
private const float LIGHT_INTENSITY = 5f;
private const float LIGHT_RADIUS    = 5f;

// -------------------------------------------------------------------------
// Colors
// -------------------------------------------------------------------------

private readonly Color COLOR_OK    = new Color(0,   255, 0);   // green
private readonly Color COLOR_ALERT = new Color(255, 128, 0);   // orange
private readonly Color COLOR_ERROR = new Color(255, 0,   0);   // red

// -------------------------------------------------------------------------
// Internal types
// -------------------------------------------------------------------------

private struct MonitorConfig
{
    public IMyLightingBlock Light;
    public MyItemType       ItemType;
    public float            Threshold;
}

// -------------------------------------------------------------------------
// Cached lists — allocated once, reused every run
// -------------------------------------------------------------------------

private readonly List<IMyLightingBlock> lightsBuffer   = new List<IMyLightingBlock>();
private readonly List<IMyTerminalBlock> blocksBuffer   = new List<IMyTerminalBlock>();
private readonly List<IMyInventory>     allInventories = new List<IMyInventory>();
private readonly List<MonitorConfig>    configs        = new List<MonitorConfig>();

// -------------------------------------------------------------------------
// Constructor
// -------------------------------------------------------------------------

public Program()
{
    // No automatic update — driven by a Timer Block or toolbar button.
}

// -------------------------------------------------------------------------
// Entry point
// -------------------------------------------------------------------------

public void Main(string argument, UpdateType updateSource)
{
    ParseMonitoredLights();

    if (configs.Count == 0)
    {
        Echo("No lights tagged " + MONITOR_TAG + " found.");
        return;
    }

    CollectInventories();

    Echo("Checking " + configs.Count + " monitor light(s)...");

    foreach (var cfg in configs)
    {
        double total = (double)GetItemTotal(cfg.ItemType);
        bool   ok    = total >= cfg.Threshold;

        ApplyLightState(cfg.Light, ok);

        Echo("  " + cfg.Light.CustomName + ": "
            + cfg.ItemType.SubtypeId + " = "
            + total.ToString("F0") + " / " + cfg.Threshold.ToString("F0")
            + " [" + (ok ? "OK" : "LOW") + "]");
    }
}

// -------------------------------------------------------------------------
// Parse all [MONITOR] lights and their Custom Data
// -------------------------------------------------------------------------

private void ParseMonitoredLights()
{
    configs.Clear();
    lightsBuffer.Clear();

    GridTerminalSystem.GetBlocksOfType(lightsBuffer, b =>
        b.IsSameConstructAs(Me) &&
        b.CustomName.Contains(MONITOR_TAG)
    );

    foreach (var light in lightsBuffer)
    {
        MonitorConfig cfg;
        if (TryParseConfig(light, out cfg))
        {
            configs.Add(cfg);
        }
        else
        {
            ApplyErrorState(light);
            Echo("WARNING: bad Custom Data on '" + light.CustomName + "'");
        }
    }
}

// -------------------------------------------------------------------------
// Parse a single light's Custom Data
// -------------------------------------------------------------------------

private bool TryParseConfig(IMyLightingBlock light, out MonitorConfig cfg)
{
    cfg = default(MonitorConfig);

    string[] lines = light.CustomData.Split(
        new char[] { '\r', '\n' },
        StringSplitOptions.RemoveEmptyEntries
    );

    if (lines.Length < 2)
        return false;

    // Line 1: Type/Subtype  (e.g. "Ingot/Iron")
    string[] parts = lines[0].Trim().Split('/');
    if (parts.Length != 2)
        return false;

    string typeId    = ExpandTypeId(parts[0].Trim());
    string subtypeId = parts[1].Trim();

    if (typeId.Length == 0 || subtypeId.Length == 0)
        return false;

    MyItemType itemType;
    try
    {
        itemType = new MyItemType(typeId, subtypeId);
    }
    catch
    {
        return false;
    }

    // Line 2: threshold=N
    string threshLine = lines[1].Trim();
    string prefix     = "threshold=";

    if (threshLine.Length <= prefix.Length)
        return false;

    if (string.Compare(threshLine.Substring(0, prefix.Length), prefix,
            StringComparison.OrdinalIgnoreCase) != 0)
        return false;

    float threshold;
    if (!float.TryParse(threshLine.Substring(prefix.Length).Trim(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out threshold))
        return false;

    cfg = new MonitorConfig
    {
        Light     = light,
        ItemType  = itemType,
        Threshold = threshold
    };
    return true;
}

// -------------------------------------------------------------------------
// Expand short type names to full MyObjectBuilder type strings
// -------------------------------------------------------------------------

private string ExpandTypeId(string shortType)
{
    if (string.Equals(shortType, "Ingot",        StringComparison.OrdinalIgnoreCase)) return "MyObjectBuilder_Ingot";
    if (string.Equals(shortType, "Ore",          StringComparison.OrdinalIgnoreCase)) return "MyObjectBuilder_Ore";
    if (string.Equals(shortType, "Component",    StringComparison.OrdinalIgnoreCase)) return "MyObjectBuilder_Component";
    if (string.Equals(shortType, "AmmoMagazine", StringComparison.OrdinalIgnoreCase)) return "MyObjectBuilder_AmmoMagazine";
    if (string.Equals(shortType, "Tool",         StringComparison.OrdinalIgnoreCase)) return "MyObjectBuilder_PhysicalGunObject";
    if (string.Equals(shortType, "GasContainer", StringComparison.OrdinalIgnoreCase)) return "MyObjectBuilder_GasContainerObject";
    return shortType; // already a full name or unknown — pass through
}

// -------------------------------------------------------------------------
// Collect all inventories on the main grid
// -------------------------------------------------------------------------

private void CollectInventories()
{
    allInventories.Clear();
    blocksBuffer.Clear();

    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocksBuffer, b =>
        b.IsSameConstructAs(Me) && b.HasInventory
    );

    foreach (var block in blocksBuffer)
    {
        for (int j = 0; j < block.InventoryCount; j++)
            allInventories.Add(block.GetInventory(j));
    }
}

// -------------------------------------------------------------------------
// Sum a specific item type across all collected inventories
// -------------------------------------------------------------------------

private VRage.MyFixedPoint GetItemTotal(MyItemType itemType)
{
    VRage.MyFixedPoint total = 0;
    foreach (var inv in allInventories)
        total += inv.GetItemAmount(itemType);
    return total;
}

// -------------------------------------------------------------------------
// Apply OK or ALERT light state
// -------------------------------------------------------------------------

private void ApplyLightState(IMyLightingBlock light, bool isOk)
{
    light.Enabled   = true;
    light.Intensity = LIGHT_INTENSITY;
    light.Radius    = LIGHT_RADIUS;

    if (isOk)
    {
        light.Color                = COLOR_OK;
        light.BlinkIntervalSeconds = 0f;
    }
    else
    {
        light.Color                = COLOR_ALERT;
        light.BlinkIntervalSeconds = BLINK_INTERVAL_SECONDS;
        light.BlinkLength          = BLINK_LENGTH;
        light.BlinkOffset          = 0f;
    }
}

// -------------------------------------------------------------------------
// Apply ERROR state (bad Custom Data)
// -------------------------------------------------------------------------

private void ApplyErrorState(IMyLightingBlock light)
{
    light.Enabled              = true;
    light.Color                = COLOR_ERROR;
    light.Intensity            = LIGHT_INTENSITY;
    light.Radius               = LIGHT_RADIUS;
    light.BlinkIntervalSeconds = 0f;
}
