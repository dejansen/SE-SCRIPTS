// InventoryMonitor2 - Space Engineers Programmable Block Script
// Monitors item quantities across the grid using [MONITOR]-tagged inventory blocks
// (cargo containers, reactors, refineries, assemblers — any block with an inventory).
//
// Programmable Block Custom Data (optional):
//   MONITOR_TAG=[MONITOR]
//
// Cargo container Custom Data format:
//
//   [items]
//   Type/Subtype = threshold
//   Type/Subtype = threshold
//
//   [command]
//   action = light          ; OR: action = timer
//
//   ; --- for action=light ---
//   light_name = {My Alert Light}
//   light_ok   = green        ; optional — overrides default green
//   light_low  = blinkorange  ; optional — overrides default blinkorange
//
// Named light states: green, orange, red, blinkorange, blinkgreen, blinkred, off
//
//   ; --- for action=timer ---
//   timer_low = {My Timer Low}    ; triggered once when stock drops below threshold
//   timer_ok  = {My Timer OK}     ; triggered once when stock returns to OK (optional)
//
// Block names in { } braces are recommended — they handle spaces, underscores and
// special characters unambiguously. Plain names (no braces) also work.
//
// Supported short type names: Ingot, Ore, Component, AmmoMagazine, Tool, GasContainer
//
// Light states (action=light):
//   Green  (solid)    - ALL monitored items at or above threshold
//   Orange (blinking) - at least one item below threshold
//   Red    (solid)    - Custom Data missing or malformed
//
// Timer behaviour (action=timer):
//   timer_low is triggered ONCE when state changes from OK to LOW.
//   timer_ok  is triggered ONCE when state changes from LOW to OK.
//   Only one trigger fires per state change to avoid spamming.

// -------------------------------------------------------------------------
// Defaults
// -------------------------------------------------------------------------

private const string DEFAULT_MONITOR_TAG    = "[MONITOR]";

// -------------------------------------------------------------------------
// Blink / light settings
// -------------------------------------------------------------------------

private const float BLINK_INTERVAL_SECONDS = 2.0f;
private const float BLINK_LENGTH           = 50.0f;  // 0-100 percentage of cycle on
private const float LIGHT_INTENSITY        = 5f;
private const float LIGHT_RADIUS           = 5f;

// -------------------------------------------------------------------------
// Colors
// -------------------------------------------------------------------------

private readonly Color COLOR_OK    = new Color(0,   255, 0);
private readonly Color COLOR_ALERT = new Color(255, 128, 0);
private readonly Color COLOR_ERROR = new Color(255, 0,   0);

// -------------------------------------------------------------------------
// Internal types
// -------------------------------------------------------------------------

private enum ActionType { Light, Timer, Unknown }

private struct ItemRule
{
    public MyItemType ItemType;
    public float      Threshold;
}

private struct ContainerConfig
{
    public IMyTerminalBlock Container;
    public List<ItemRule>    Rules;
    public ActionType        Action;
    // light action fields
    public string            LightName;
    public string            LightOkState;   // named state, e.g. "green" (optional)
    public string            LightLowState;  // named state, e.g. "blinkorange" (optional)
    // timer action fields
    public string            TimerLowName;
    public string            TimerOkName;
}

// -------------------------------------------------------------------------
// Script-level state
// -------------------------------------------------------------------------

private string monitorTag = DEFAULT_MONITOR_TAG;

// Previous alert states per container EntityId — drives timer one-shot logic
private readonly Dictionary<long, bool> previousAlertState = new Dictionary<long, bool>();

// -------------------------------------------------------------------------
// Cached lists — allocated once, reused every run
// -------------------------------------------------------------------------

private readonly List<IMyTerminalBlock>  containersBuffer = new List<IMyTerminalBlock>();
private readonly List<IMyTerminalBlock>  blocksBuffer     = new List<IMyTerminalBlock>();
private readonly List<IMyInventory>      allInventories   = new List<IMyInventory>();
private readonly List<ContainerConfig>   configs          = new List<ContainerConfig>();
private readonly StringBuilder           screenBuffer     = new StringBuilder();

// -------------------------------------------------------------------------
// Constructor
// -------------------------------------------------------------------------

public Program()
{
    ShowBootScreen();
}

private void ShowBootScreen()
{
    var surface = Me.GetSurface(0);
    surface.ContentType     = ContentType.TEXT_AND_IMAGE;
    surface.Font            = "Monospace";
    surface.FontSize        = 1.0f;
    surface.Alignment       = TextAlignment.CENTER;
    surface.BackgroundColor = new Color(0, 5, 15);
    surface.FontColor       = new Color(0, 200, 160);
    surface.WriteText(
        "\n" +
        "========================\n" +
        "  INVENTORY MONITOR 2  \n" +
        "========================\n" +
        "\n" +
        "   Initializing...\n"
    );
}

private void ShowStatusScreen(int total, int lowCount)
{
    bool allOk = total > 0 && lowCount == 0;

    var surface = Me.GetSurface(0);
    surface.ContentType     = ContentType.TEXT_AND_IMAGE;
    surface.Font            = "Monospace";
    surface.FontSize        = 1.0f;
    surface.Alignment       = TextAlignment.CENTER;
    surface.BackgroundColor = new Color(0, 5, 15);
    surface.FontColor       = allOk ? new Color(0, 200, 160) : new Color(255, 160, 0);

    screenBuffer.Clear();
    screenBuffer.Append("\n");
    screenBuffer.Append("========================\n");
    screenBuffer.Append("  INVENTORY MONITOR 2  \n");
    screenBuffer.Append("========================\n");
    screenBuffer.Append("\n");

    if (total == 0)
    {
        screenBuffer.Append("  No tagged containers\n");
        screenBuffer.Append("    found on grid.\n");
    }
    else if (allOk)
    {
        screenBuffer.Append("   ALL SYSTEMS OK\n");
        screenBuffer.Append("\n");
        screenBuffer.Append("  " + total + " container(s)\n");
        screenBuffer.Append("  all stocked.\n");
    }
    else
    {
        screenBuffer.Append("   !! STOCK LOW !!\n");
        screenBuffer.Append("\n");
        screenBuffer.Append("  " + lowCount + " of " + total);
        screenBuffer.Append(" container(s)\n");
        screenBuffer.Append("  need restocking.\n");
    }

    screenBuffer.Append("\n");
    screenBuffer.Append("========================\n");

    surface.WriteText(screenBuffer.ToString());
}

// -------------------------------------------------------------------------
// Entry point
// -------------------------------------------------------------------------

public void Main(string argument, UpdateType updateSource)
{
    ReadProgramCustomData();
    ParseMonitoredContainers();

    if (configs.Count == 0)
    {
        Echo("No containers tagged " + monitorTag + " found.");
        ShowStatusScreen(0, 0);
        return;
    }

    CollectInventories();

    Echo("InventoryMonitor2 — checking " + configs.Count + " container(s)...");

    int lowCount = 0;

    foreach (var cfg in configs)
    {
        bool anyLow = false;

        for (int i = 0; i < cfg.Rules.Count; i++)
        {
            var    rule  = cfg.Rules[i];
            double total = (double)GetItemTotal(rule.ItemType);
            bool   ok    = total >= rule.Threshold;
            if (!ok) anyLow = true;

            Echo("  " + cfg.Container.CustomName
                + " | " + rule.ItemType.SubtypeId
                + " = " + total.ToString("F0")
                + " / " + rule.Threshold.ToString("F0")
                + " [" + (ok ? "OK" : "LOW") + "]");
        }

        bool isAlert = anyLow;
        if (isAlert) lowCount++;

        if (cfg.Action == ActionType.Light)
            ExecuteLightAction(cfg, isAlert);
        else if (cfg.Action == ActionType.Timer)
            ExecuteTimerAction(cfg, isAlert);
    }

    ShowStatusScreen(configs.Count, lowCount);
}

// -------------------------------------------------------------------------
// Read tag from Programmable Block Custom Data
// -------------------------------------------------------------------------

private void ReadProgramCustomData()
{
    monitorTag = DEFAULT_MONITOR_TAG;

    string raw = Me.CustomData;
    if (string.IsNullOrEmpty(raw))
        return;

    string[] lines = raw.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    foreach (var line in lines)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith(";") || trimmed.StartsWith("#"))
            continue;

        string key, value;
        if (!TrySplitKeyValue(trimmed, '=', out key, out value))
            continue;

        if (string.Equals(key, "MONITOR_TAG", StringComparison.OrdinalIgnoreCase))
            monitorTag = value;
    }
}

// -------------------------------------------------------------------------
// Parse all tagged cargo containers
// -------------------------------------------------------------------------

private void ParseMonitoredContainers()
{
    configs.Clear();
    containersBuffer.Clear();

    GridTerminalSystem.GetBlocksOfType(containersBuffer, b =>
        b.IsSameConstructAs(Me) &&
        b.HasInventory &&
        b.CustomName.Contains(monitorTag)
    );

    foreach (var container in containersBuffer)
    {
        ContainerConfig cfg;
        if (TryParseContainerConfig(container, out cfg))
        {
            configs.Add(cfg);
        }
        else
        {
            Echo("WARNING: bad Custom Data on '" + container.CustomName + "'");
            TryApplyErrorStateByName(container);
        }
    }
}

// -------------------------------------------------------------------------
// Parse one container's Custom Data
// -------------------------------------------------------------------------

private bool TryParseContainerConfig(IMyTerminalBlock container, out ContainerConfig cfg)
{
    cfg         = default(ContainerConfig);
    cfg.Rules   = new List<ItemRule>();
    cfg.Action  = ActionType.Unknown;

    string raw = container.CustomData;
    if (string.IsNullOrEmpty(raw))
        return false;

    string[] lines   = raw.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    string   section = "";

    foreach (var line in lines)
    {
        string trimmed = line.Trim();

        if (trimmed.StartsWith(";") || trimmed.StartsWith("#"))
            continue;

        // Section header — must not be confused with the monitor tag in the container name
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]") && trimmed.Length > 2)
        {
            // Only treat as a section if the inner text has no spaces (keeps "[MONITOR]" from matching)
            string inner = trimmed.Substring(1, trimmed.Length - 2);
            if (inner.IndexOf(' ') < 0)
            {
                section = inner.ToLower();
                continue;
            }
        }

        if (section == "items")
        {
            string left, right;
            if (!TrySplitKeyValue(trimmed, '=', out left, out right))
                continue;

            string[] typeParts = left.Trim().Split('/');
            if (typeParts.Length != 2)
                continue;

            string typeId    = ExpandTypeId(typeParts[0].Trim());
            string subtypeId = typeParts[1].Trim();

            if (typeId.Length == 0 || subtypeId.Length == 0)
                continue;

            MyItemType itemType;
            try   { itemType = new MyItemType(typeId, subtypeId); }
            catch { continue; }

            float threshold;
            if (!float.TryParse(right.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out threshold))
                continue;

            cfg.Rules.Add(new ItemRule { ItemType = itemType, Threshold = threshold });
        }
        else if (section == "command")
        {
            string key, value;
            if (!TrySplitKeyValue(trimmed, '=', out key, out value))
                continue;

            switch (key.ToLower())
            {
                case "action":
                    if (string.Equals(value, "light", StringComparison.OrdinalIgnoreCase))
                        cfg.Action = ActionType.Light;
                    else if (string.Equals(value, "timer", StringComparison.OrdinalIgnoreCase))
                        cfg.Action = ActionType.Timer;
                    else
                        cfg.Action = ActionType.Unknown;
                    break;
                case "light_name":
                    cfg.LightName = value;
                    break;
                case "light_ok":
                    cfg.LightOkState = value;
                    break;
                case "light_low":
                    cfg.LightLowState = value;
                    break;
                case "timer_low":
                    cfg.TimerLowName = value;
                    break;
                case "timer_ok":
                    cfg.TimerOkName = value;
                    break;
            }
        }
    }

    if (cfg.Rules.Count == 0)      return false;
    if (cfg.Action == ActionType.Unknown) return false;

    if (cfg.Action == ActionType.Light && string.IsNullOrEmpty(cfg.LightName))
        return false;
    if (cfg.Action == ActionType.Timer && string.IsNullOrEmpty(cfg.TimerLowName))
        return false;

    cfg.Container = container;
    return true;
}

// -------------------------------------------------------------------------
// Execute light action
// -------------------------------------------------------------------------

private void ExecuteLightAction(ContainerConfig cfg, bool isAlert)
{
    IMyLightingBlock light = GridTerminalSystem.GetBlockWithName(cfg.LightName) as IMyLightingBlock;
    if (light == null)
    {
        Echo("WARNING: light '" + cfg.LightName + "' not found for '"
            + cfg.Container.CustomName + "'");
        return;
    }

    string state = isAlert ? cfg.LightLowState : cfg.LightOkState;
    if (!string.IsNullOrEmpty(state))
        ApplyNamedLightState(light, state);
    else
        ApplyLightState(light, !isAlert);
}

// -------------------------------------------------------------------------
// Execute timer action (fires once per state change)
// -------------------------------------------------------------------------

private void ExecuteTimerAction(ContainerConfig cfg, bool isAlert)
{
    long id = cfg.Container.EntityId;

    bool wasPreviouslyAlert;
    previousAlertState.TryGetValue(id, out wasPreviouslyAlert);

    bool stateChanged = isAlert != wasPreviouslyAlert;
    previousAlertState[id] = isAlert;

    if (!stateChanged)
        return;

    if (isAlert)
    {
        TriggerTimerByName(cfg.TimerLowName, cfg.Container.CustomName);
    }
    else
    {
        if (!string.IsNullOrEmpty(cfg.TimerOkName))
            TriggerTimerByName(cfg.TimerOkName, cfg.Container.CustomName);
    }
}

private void TriggerTimerByName(string timerName, string containerName)
{
    IMyTimerBlock timer = GridTerminalSystem.GetBlockWithName(timerName) as IMyTimerBlock;
    if (timer == null)
    {
        Echo("WARNING: timer '" + timerName + "' (len=" + timerName.Length + ") not found for '" + containerName + "'");
        return;
    }

    timer.ApplyAction("TriggerNow");
    Echo("  Timer '" + timerName + "' triggered for '" + containerName + "'");
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
// Apply a named light state (light_ok / light_low values)
// Supported: green, orange, red, blinkorange, blinkgreen, blinkred, off
// -------------------------------------------------------------------------

private void ApplyNamedLightState(IMyLightingBlock light, string state)
{
    switch (state.ToLower())
    {
        case "green":
            light.Enabled              = true;
            light.Color                = COLOR_OK;
            light.Intensity            = LIGHT_INTENSITY;
            light.Radius               = LIGHT_RADIUS;
            light.BlinkIntervalSeconds = 0f;
            break;
        case "orange":
            light.Enabled              = true;
            light.Color                = COLOR_ALERT;
            light.Intensity            = LIGHT_INTENSITY;
            light.Radius               = LIGHT_RADIUS;
            light.BlinkIntervalSeconds = 0f;
            break;
        case "red":
            light.Enabled              = true;
            light.Color                = COLOR_ERROR;
            light.Intensity            = LIGHT_INTENSITY;
            light.Radius               = LIGHT_RADIUS;
            light.BlinkIntervalSeconds = 0f;
            break;
        case "blinkorange":
            light.Enabled              = true;
            light.Color                = COLOR_ALERT;
            light.Intensity            = LIGHT_INTENSITY;
            light.Radius               = LIGHT_RADIUS;
            light.BlinkIntervalSeconds = BLINK_INTERVAL_SECONDS;
            light.BlinkLength          = BLINK_LENGTH;
            light.BlinkOffset          = 0f;
            break;
        case "blinkgreen":
            light.Enabled              = true;
            light.Color                = COLOR_OK;
            light.Intensity            = LIGHT_INTENSITY;
            light.Radius               = LIGHT_RADIUS;
            light.BlinkIntervalSeconds = BLINK_INTERVAL_SECONDS;
            light.BlinkLength          = BLINK_LENGTH;
            light.BlinkOffset          = 0f;
            break;
        case "blinkred":
            light.Enabled              = true;
            light.Color                = COLOR_ERROR;
            light.Intensity            = LIGHT_INTENSITY;
            light.Radius               = LIGHT_RADIUS;
            light.BlinkIntervalSeconds = BLINK_INTERVAL_SECONDS;
            light.BlinkLength          = BLINK_LENGTH;
            light.BlinkOffset          = 0f;
            break;
        case "off":
            light.Enabled = false;
            break;
        default:
            Echo("WARNING: unknown light state '" + state + "'");
            break;
    }
}

// -------------------------------------------------------------------------
// Apply ERROR state — called when container config is malformed
// -------------------------------------------------------------------------

private void TryApplyErrorStateByName(IMyTerminalBlock container)
{
    string raw = container.CustomData;
    if (string.IsNullOrEmpty(raw))
        return;

    string[] lines = raw.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    foreach (var line in lines)
    {
        string key, value;
        if (!TrySplitKeyValue(line.Trim(), '=', out key, out value))
            continue;
        if (!string.Equals(key, "light_name", StringComparison.OrdinalIgnoreCase))
            continue;

        IMyLightingBlock light = GridTerminalSystem.GetBlockWithName(value) as IMyLightingBlock;
        if (light != null)
            ApplyErrorState(light);
        break;
    }
}

private void ApplyErrorState(IMyLightingBlock light)
{
    light.Enabled              = true;
    light.Color                = COLOR_ERROR;
    light.Intensity            = LIGHT_INTENSITY;
    light.Radius               = LIGHT_RADIUS;
    light.BlinkIntervalSeconds = 0f;
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
    return shortType;
}

// -------------------------------------------------------------------------
// Split "key = value" and strip inline comments starting with ';'
// Values may optionally be wrapped in { } braces to protect spaces and
// special characters: timer_low = {My Timer Block}
// -------------------------------------------------------------------------

private bool TrySplitKeyValue(string line, char separator, out string key, out string value)
{
    key   = "";
    value = "";

    // Strip inline comment — only when ';' is NOT inside a { } block name
    int braceOpen = line.IndexOf('{');
    int braceClose = line.IndexOf('}');
    int commentIdx = line.IndexOf(';');
    if (commentIdx >= 0)
    {
        bool insideBraces = braceOpen >= 0 && braceClose > braceOpen && commentIdx > braceOpen && commentIdx < braceClose;
        if (!insideBraces)
            line = line.Substring(0, commentIdx).Trim();
    }

    int idx = line.IndexOf(separator);
    if (idx < 1)
        return false;

    key   = line.Substring(0, idx).Trim();
    value = line.Substring(idx + 1).Trim();

    // Unwrap { } braces from block name values
    if (value.Length >= 2 && value[0] == '{' && value[value.Length - 1] == '}')
        value = value.Substring(1, value.Length - 2).Trim();

    return key.Length > 0;
}
