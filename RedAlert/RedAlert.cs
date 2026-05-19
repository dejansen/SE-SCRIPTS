// -------------------------------------------------------------------------
// Red Alert
// Commands: activate / deactivate
// Group:    "RedAlertLights"
// -------------------------------------------------------------------------

private const string GROUP_NAME     = "RedAlertLights";
private const string STORAGE_HEADER = "REDALERT_V1";
private const char   ROW_SEP        = '\n';
private const char   FIELD_SEP      = '|';

private struct LightState
{
    public long  EntityId;
    public Color Color;
    public float Intensity;
    public float Radius;
    public float BlinkInterval;
    public float BlinkLength;
    public float BlinkOffset;
    public bool  Enabled;
}

private readonly List<LightState>       savedStates  = new List<LightState>();
private readonly List<IMyLightingBlock> lightsBuffer = new List<IMyLightingBlock>();
private bool isActive = false;

public Program()
{
    LoadFromStorage();
}

public void Save()
{
    SaveToStorage();
}

public void Main(string argument, UpdateType updateSource)
{
    switch (argument.Trim().ToLower())
    {
        case "activate":   Activate();   break;
        case "deactivate": Deactivate(); break;
        default:
            Echo("Unknown command: " + argument);
            Echo("Valid commands: activate, deactivate");
            break;
    }
}

// -------------------------------------------------------------------------
// Commands
// -------------------------------------------------------------------------

private void Activate()
{
    if (isActive)
    {
        Echo("Red Alert already active.");
        return;
    }

    var group = GridTerminalSystem.GetBlockGroupWithName(GROUP_NAME);
    if (group == null)
    {
        Echo("ERROR: Group '" + GROUP_NAME + "' not found.");
        return;
    }

    lightsBuffer.Clear();
    group.GetBlocksOfType(lightsBuffer);

    if (lightsBuffer.Count == 0)
    {
        Echo("WARNING: No lights found in group '" + GROUP_NAME + "'.");
        return;
    }

    savedStates.Clear();
    foreach (var light in lightsBuffer)
    {
        savedStates.Add(new LightState
        {
            EntityId      = light.EntityId,
            Color         = light.Color,
            Intensity     = light.Intensity,
            Radius        = light.Radius,
            BlinkInterval = light.BlinkIntervalSeconds,
            BlinkLength   = light.BlinkLength,
            BlinkOffset   = light.BlinkOffset,
            Enabled       = light.Enabled
        });

        light.Color   = new Color(255, 0, 0);
        light.Enabled = true;
    }

    isActive = true;
    SaveToStorage();
    Echo("RED ALERT ACTIVE — " + lightsBuffer.Count + " light(s) set to red.");
}

private void Deactivate()
{
    if (!isActive)
    {
        Echo("Red Alert is not active.");
        return;
    }

    if (savedStates.Count == 0)
    {
        Echo("WARNING: No saved states to restore.");
        isActive = false;
        SaveToStorage();
        return;
    }

    var group = GridTerminalSystem.GetBlockGroupWithName(GROUP_NAME);
    if (group == null)
    {
        Echo("ERROR: Group '" + GROUP_NAME + "' not found.");
        return;
    }

    lightsBuffer.Clear();
    group.GetBlocksOfType(lightsBuffer);

    foreach (var state in savedStates)
    {
        var light = lightsBuffer.Find(l => l.EntityId == state.EntityId);
        if (light == null) continue;

        light.Color                = state.Color;
        light.Intensity            = state.Intensity;
        light.Radius               = state.Radius;
        light.BlinkIntervalSeconds = state.BlinkInterval;
        light.BlinkLength          = state.BlinkLength;
        light.BlinkOffset          = state.BlinkOffset;
        light.Enabled              = state.Enabled;
    }

    isActive = false;
    savedStates.Clear();
    SaveToStorage();
    Echo("Red Alert deactivated — lights restored.");
}

// -------------------------------------------------------------------------
// Persistence
// -------------------------------------------------------------------------

private void SaveToStorage()
{
    if (!isActive || savedStates.Count == 0)
    {
        Storage = "";
        return;
    }

    var sb = new StringBuilder();
    sb.Append(STORAGE_HEADER);

    foreach (var s in savedStates)
    {
        sb.Append(ROW_SEP);
        sb.Append(s.EntityId);   sb.Append(FIELD_SEP);
        sb.Append(s.Color.R);    sb.Append(',');
        sb.Append(s.Color.G);    sb.Append(',');
        sb.Append(s.Color.B);    sb.Append(FIELD_SEP);
        sb.Append(s.Intensity.ToString(System.Globalization.CultureInfo.InvariantCulture));     sb.Append(FIELD_SEP);
        sb.Append(s.Radius.ToString(System.Globalization.CultureInfo.InvariantCulture));        sb.Append(FIELD_SEP);
        sb.Append(s.BlinkInterval.ToString(System.Globalization.CultureInfo.InvariantCulture)); sb.Append(FIELD_SEP);
        sb.Append(s.BlinkLength.ToString(System.Globalization.CultureInfo.InvariantCulture));   sb.Append(FIELD_SEP);
        sb.Append(s.BlinkOffset.ToString(System.Globalization.CultureInfo.InvariantCulture));   sb.Append(FIELD_SEP);
        sb.Append(s.Enabled ? '1' : '0');
    }

    Storage = sb.ToString();
}

private void LoadFromStorage()
{
    savedStates.Clear();
    isActive = false;

    if (string.IsNullOrEmpty(Storage)) return;

    var rows = Storage.Split(ROW_SEP);
    if (rows.Length == 0 || rows[0] != STORAGE_HEADER) return;

    for (int i = 1; i < rows.Length; i++)
    {
        if (string.IsNullOrEmpty(rows[i])) continue;

        var fields = rows[i].Split(FIELD_SEP);
        if (fields.Length < 9) continue;

        long entityId;
        if (!long.TryParse(fields[0], out entityId)) continue;

        var rgb = fields[1].Split(',');
        if (rgb.Length < 3) continue;

        byte r, g, b;
        if (!byte.TryParse(rgb[0], out r)) continue;
        if (!byte.TryParse(rgb[1], out g)) continue;
        if (!byte.TryParse(rgb[2], out b)) continue;

        float intensity, radius, blinkInterval, blinkLength, blinkOffset;
        var inv   = System.Globalization.CultureInfo.InvariantCulture;
        var style = System.Globalization.NumberStyles.Float;
        if (!float.TryParse(fields[2], style, inv, out intensity))    continue;
        if (!float.TryParse(fields[3], style, inv, out radius))       continue;
        if (!float.TryParse(fields[4], style, inv, out blinkInterval)) continue;
        if (!float.TryParse(fields[5], style, inv, out blinkLength))  continue;
        if (!float.TryParse(fields[6], style, inv, out blinkOffset))  continue;

        savedStates.Add(new LightState
        {
            EntityId      = entityId,
            Color         = new Color(r, g, b),
            Intensity     = intensity,
            Radius        = radius,
            BlinkInterval = blinkInterval,
            BlinkLength   = blinkLength,
            BlinkOffset   = blinkOffset,
            Enabled       = fields[8] == "1"
        });
    }

    if (savedStates.Count > 0)
        isActive = true;
}
