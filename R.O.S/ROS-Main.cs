// ============================================================
// R.O.S — Rev Operating System
// Module:  Dock Control / Proximity Scanner / Fleet Operations / Dock Radar
// Author:  RevGamer (Simba "Davy" Jones)
// Version: 1.7
//
// NEW v1.7 — DOCK RADAR:
//   [RosRadar] LCD tag — add any LCD with [RosRadar] in name for radar view
//   2D top-down radar centered on base grid origin
//   4 range rings: 250m (red) / 500m (orange) / 750m (purple) / 1000m (dim)
//   Bearing ticks every 30° with N/S/E/W cardinal labels
//   Rotating sweep line driven by existing _spinnerAngle
//   Contact blips: ○ miner coplanar | △ above | ▽ below | □ large grid | ▲ small grid
//   Contact name label (first 5 chars) next to each blip
//   Fade alpha applied same as [RosScan]
//   Trig lookup tables (SinTable/CosTable float[360]) — no Math.Sin per frame
//   Cached ring+tick geometry — rebuilt only when LCD surface size changes
//   WorldPos field added to ApproachContact for accurate radar positioning
//
// NEW v1.6 (Phase 2):
//   P2-1: FC() formatter  P2-2: SaltColor shimmer  P2-3: Auto font scaling
//   P2-4: FormatElapsed   P2-5: Elevation indicators  P2-6: Contact fade-out
//   P2-7: Name scrolling  P2-8: Cargo trend arrows
//
// FIXES v1.5 (Phase 1):
//   P1-1: Block scan cached  P1-5: Camera raycast charge check
//   P1-6: Grid charge cached  BONUS: Duplicate UpdateMinerContacts removed
// ============================================================

const string CONNECTOR_TAG  = "[DOCK]";
const string LCD_TAG        = "[DockStatus]";
const string MAP_LCD_TAG    = "[RosScan]";
const string MINER_LCD_TAG  = "[RosFleet]";
const string RADAR_LCD_TAG  = "[RosRadar]";
const string CAM_TAG        = "[RosCam]";
const string ALERT_TAG      = "[RosSound]";
const string LIGHT_TAG      = "[DockLight]";
const string MINER_CHANNEL  = "DOCK_APPROACH";
const string REPLY_CHANNEL  = "DOCK_REPLY";
const double CAM_RANGE      = 1000.0;
const double APPROACH_RANGE = 1000.0;  // camera contact range + RosScan filter
const double RADAR_RANGE    = 10000.0; // [RosRadar] max display range (10km)
const float  LIGHT_SEQ_RATE = 0.25f;
const float  BOOT_TIME      = 3.0f;
const int    SCAN_INTERVAL  = 100;

const int NAME_MAX_CHARS    = 12;
const int PORT_MAX_CHARS    = 12;
const int NAME_SCROLL_SPEED = 18;

const string ROS_TITLE    = "R.O.S";
const string ROS_VERSION  = "v1.7";
const string ROS_AUTHOR   = "RevGamer (Simba \"Davy\" Jones)";
const string MOD_DOCK     = "DOCK CONTROL";
const string MOD_PROXIMITY = "PROXIMITY SCANNER";
const string MOD_FLEET    = "FLEET OPERATIONS";
const string MOD_RADAR    = "DOCK RADAR";

static readonly Color COLOR_LOCKED   = new Color(30,  180, 100);
static readonly Color COLOR_READY    = new Color(50,  180, 220);
static readonly Color COLOR_IDLE     = new Color(180, 180, 180);
static readonly Color COLOR_OFFLINE  = new Color(150, 60,  60);
static readonly Color COLOR_HEADER   = new Color(157, 225, 203);
static readonly Color COLOR_DIM      = new Color(60,  80,  70);
static readonly Color COLOR_BG       = new Color(6,   13,  8);
static readonly Color COLOR_CHARGE   = new Color(255, 200, 50);
static readonly Color COLOR_DANGER   = new Color(200, 40,  40);
static readonly Color COLOR_INCOMING = new Color(255, 140, 0);
static readonly Color COLOR_APPROACH = new Color(200, 100, 255);
static readonly Color COLOR_MINER    = new Color(80,  220, 255);
static readonly Color COLOR_DRILL    = new Color(255, 80,  80);
static readonly Color COLOR_PARKED   = new Color(180, 180, 100);
static readonly Color COLOR_ABOVE    = new Color(100, 255, 140);
static readonly Color COLOR_BELOW    = new Color(255, 100, 160);

static readonly string[] BOOT_MESSAGES = {
    "Scanning connectors...",
    "Calibrating proximity sensors...",
    "Syncing fleet telemetry...",
    "Aligning runway lights...",
    "Verifying docking protocols...",
    "Loading vessel registry...",
    "Establishing IGC channels...",
    "Charging scanner arrays...",
    "Running diagnostics...",
    "All systems nominal."
};

// Radar zoom levels in metres — cycle through with ZOOM IN / ZOOM OUT argument
static readonly double[] RADAR_ZOOM_LEVELS = { 500.0, 1000.0, 2500.0, 5000.0, 10000.0 };
int _radarZoomIndex = 4; // default = 10km (index 4)

List<IMyShipConnector>  connectors  = new List<IMyShipConnector>();
List<IMyTextPanel>      lcds        = new List<IMyTextPanel>();
List<IMyTextPanel>      mapLcds     = new List<IMyTextPanel>();
List<IMyTextPanel>      minerLcds   = new List<IMyTextPanel>();
List<IMyTextPanel>      radarLcds   = new List<IMyTextPanel>();
List<IMyBatteryBlock>   batteries   = new List<IMyBatteryBlock>();
List<IMySensorBlock>    sensors     = new List<IMySensorBlock>();
List<IMySoundBlock>     alertSounds = new List<IMySoundBlock>();
List<IMyCameraBlock>    cameras     = new List<IMyCameraBlock>();
List<IMyLightingBlock>  dockLights  = new List<IMyLightingBlock>();

Dictionary<long, float> _gridChargeCache = new Dictionary<long, float>();

float    _spinnerAngle  = 0f;
bool     _alertPlayed   = false;
bool     _lightsActive  = false;
int      _lightSeqIndex = 0;
float    _lightSeqTimer = 0f;
DateTime _lastUpdate    = DateTime.Now;

bool   _booting     = true;
float  _bootTimer   = 0f;
string _bootMessage = BOOT_MESSAGES[0];

HashSet<long> _initialisedLCDs = new HashSet<long>();
int _scanCounter = 0;
int _renderTick  = 0;
double _totalElapsedSec = 0;
Dictionary<string, int>   _nameScrollOffset = new Dictionary<string, int>();
Dictionary<string, float> _prevCargoFill    = new Dictionary<string, float>();
int _scrollTick = 0;

// Reused list — avoids allocating new List<string> every tick in MergeContactsToApproachList
List<string> _dockedNames = new List<string>();

// Trig lookup tables — built once in Program(), used in radar geometry
float[] _sinTable = new float[360];
float[] _cosTable = new float[360];

// Cached radar geometry — rebuilt only when LCD surface size changes
MySprite[] _radarRings    = null;
MySprite[] _radarTicks    = null;
bool       _radarGeomValid = false;
Vector2    _lastRadarSize  = Vector2.Zero;
Vector2    _lastRadarCenter = Vector2.Zero;
float      _lastRadarRadius = 0f;

IMyBroadcastListener _minerListener;

// ============================================================
// Data classes
// ============================================================

class MinerContact
{
    public string   Name;
    public float    Speed;
    public Vector3D Position;
    public DateTime LastSeen;
    public bool     Drilling;
    public float    CargoFill;
    public float    FuelLevel;
    public float    BattLevel;
    public string   StatusStr;

    public bool IsStale() => (DateTime.Now - LastSeen).TotalSeconds > 15;
    public double Distance(Vector3D basePos) => Vector3D.Distance(basePos, Position);

    public string AgeString()
    {
        double s = (DateTime.Now - LastSeen).TotalSeconds;
        if (s < 5)  return "LIVE";
        if (s < 60) return (int)s + "s";
        return (int)(s / 60) + "m";
    }
}

class ApproachContact
{
    public string   Name;
    public double   Distance;
    public float    Speed;
    public string   Type;
    public string   CameraName;
    public DateTime DetectedAt;
    public bool     IsMinerBroadcast;
    public int      Elevation;
    public float    FadeAlpha;
    // World position — used by [RosRadar] for accurate blip placement
    public Vector3D WorldPos;

    public bool IsExpired()
    {
        double timeout = IsMinerBroadcast ? 15.0 : 10.0;
        return (DateTime.Now - DetectedAt).TotalSeconds >= timeout;
    }

    public float ComputeAlpha()
    {
        double timeout   = IsMinerBroadcast ? 15.0 : 10.0;
        double fadeStart = timeout - 3.0;
        double age = (DateTime.Now - DetectedAt).TotalSeconds;
        if (age < fadeStart) return 1f;
        double fadeFrac = (age - fadeStart) / 3.0;
        return (float)Math.Max(0.0, 1.0 - fadeFrac);
    }

    public string DistanceString()
        => Distance >= 1000 ? ((int)(Distance / 1000.0)) + "km" : (int)Distance + "m";
}

class BayContact
{
    public string   Name;
    public double   Distance;
    public float    Speed;
    public DateTime DetectedAt;
    public bool IsStale() => (DateTime.Now - DetectedAt).TotalSeconds > 5;
}

class DockRow
{
    public string       Name;
    public string       State;
    public string       Vessel;
    public Color        StateColor;
    public float        ChargeRatio;
    public float        BatteryRatio;
    public BayContact   SensorContact;
    public DateTime?    DockedAt;
    public MinerContact MinerData;
    public float        ChargeTimeMins;

    public string ChargeTimeString()
    {
        if (ChargeTimeMins < 0f)  return "--";
        if (ChargeTimeMins == 0f) return "FULL";
        int mins = (int)ChargeTimeMins;
        if (mins < 60) return mins + "m";
        return (mins / 60) + "h" + (mins % 60).ToString("D2") + "m";
    }

    public DockRow(string name, string state, string vessel,
                   Color color, float chargeRatio, float batteryRatio,
                   BayContact sensor, DateTime? dockedAt, MinerContact minerData,
                   float chargeTimeMins = -1f)
    {
        Name = name; State = state; Vessel = vessel;
        StateColor = color; ChargeRatio = chargeRatio;
        BatteryRatio = batteryRatio; SensorContact = sensor;
        DockedAt = dockedAt; MinerData = minerData;
        ChargeTimeMins = chargeTimeMins;
    }
}

List<MinerContact>    _minerContacts = new List<MinerContact>();
List<ApproachContact> _approachList  = new List<ApproachContact>();

Dictionary<string, BayContact> _bayContacts   = new Dictionary<string, BayContact>();
Dictionary<string, DateTime>   _dockTimes     = new Dictionary<string, DateTime>();
Dictionary<string, bool>       _prevLocked    = new Dictionary<string, bool>();
Dictionary<string, string>     _prevConnState = new Dictionary<string, string>();

Dictionary<long, float> _gridChargeTimeCache = new Dictionary<long, float>();

// ============================================================
// Program / Main
// ============================================================

public Program()
{
    _minerListener = IGC.RegisterBroadcastListener(MINER_CHANNEL);
    _minerListener.SetMessageCallback();
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    _lastUpdate  = DateTime.Now;
    _booting     = true;
    _bootTimer   = 0f;
    _bootMessage = BOOT_MESSAGES[0];

    // Build trig lookup tables once — used for radar ring/tick geometry
    for (int i = 0; i < 360; i++)
    {
        double rad   = i * Math.PI / 180.0;
        _sinTable[i] = (float)Math.Sin(rad);
        _cosTable[i] = (float)Math.Cos(rad);
    }

    // Restore persisted state
    if (!string.IsNullOrEmpty(Storage))
    {
        var parts = Storage.Split('|');
        if (parts.Length >= 3)
        {
            int.TryParse(parts[2], out _radarZoomIndex);
            _radarZoomIndex = Math.Max(0, Math.Min(_radarZoomIndex, RADAR_ZOOM_LEVELS.Length - 1));
        }
    }

    Echo("R.O.S " + ROS_VERSION + " — Booting...");
}

public void Save()
{
    Storage = ROS_VERSION + "|" + _lightSeqIndex.ToString() + "|" + _radarZoomIndex.ToString();
}

public void Main(string argument, UpdateType updateSource)
{
    double delta = (DateTime.Now - _lastUpdate).TotalSeconds;
    _lastUpdate  = DateTime.Now;

    // Argument handling
    if (!string.IsNullOrEmpty(argument))
    {
        string arg = argument.Trim().ToUpper();
        if (arg == "ZOOM IN")
        {
            if (_radarZoomIndex > 0)
                _radarZoomIndex--;
        }
        else if (arg == "ZOOM OUT")
        {
            if (_radarZoomIndex < RADAR_ZOOM_LEVELS.Length - 1)
                _radarZoomIndex++;
        }
    }

    if (Runtime.UpdateFrequency != UpdateFrequency.Update10)
        Runtime.UpdateFrequency = UpdateFrequency.Update10;

    _spinnerAngle += 0.4f;
    if (_spinnerAngle > (float)(Math.PI * 2.0))
        _spinnerAngle -= (float)(Math.PI * 2.0);

    _renderTick++;
    _totalElapsedSec += delta;
    _scrollTick++;

    lcds.Clear(); mapLcds.Clear(); minerLcds.Clear(); radarLcds.Clear();
    GridTerminalSystem.GetBlocksOfType(lcds, b =>
        b.CustomName.Contains(LCD_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(mapLcds, b =>
        b.CustomName.Contains(MAP_LCD_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(minerLcds, b =>
        b.CustomName.Contains(MINER_LCD_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(radarLcds, b =>
        b.CustomName.Contains(RADAR_LCD_TAG) && b.IsSameConstructAs(Me));

    InitLCDs();

    if (_booting)
    {
        _bootTimer += (float)delta;
        float prog     = Math.Min(1f, _bootTimer / BOOT_TIME);
        int   msgIndex = (int)(prog * (BOOT_MESSAGES.Length - 1));
        msgIndex       = Math.Max(0, Math.Min(msgIndex, BOOT_MESSAGES.Length - 1));
        _bootMessage   = BOOT_MESSAGES[msgIndex];

        foreach (var lcd in lcds)      DrawBootScreen(lcd, MOD_DOCK);
        foreach (var lcd in mapLcds)   DrawBootScreen(lcd, MOD_PROXIMITY);
        foreach (var lcd in minerLcds) DrawBootScreen(lcd, MOD_FLEET);
        foreach (var lcd in radarLcds) DrawBootScreen(lcd, MOD_RADAR);
        DrawPBBootScreen();

        if (_bootTimer >= BOOT_TIME)
        {
            _booting     = false;
            _scanCounter = SCAN_INTERVAL;
            Echo("R.O.S " + ROS_VERSION + " — Online");
        }
        return;
    }

    _scanCounter++;
    if (_scanCounter >= SCAN_INTERVAL)
    {
        _scanCounter = 0;
        ReScanBlocks();
    }

    UpdateMinerContacts();

    bool connectorChanged = false;
    foreach (var conn in connectors)
    {
        string name      = StripTags(conn.CustomName);
        string stateNow  = conn.Status.ToString();
        string statePrev = "";
        _prevConnState.TryGetValue(name, out statePrev);
        if (stateNow != statePrev)
        {
            connectorChanged     = true;
            _prevConnState[name] = stateNow;
        }
    }

    UpdateSensorContacts();
    UpdateCameraContacts();
    MergeContactsToApproachList();
    UpdateDockTimes();
    UpdateSequentialLights((float)delta);
    UpdateAlert();
    BroadcastDistanceReplies();

    if (_scrollTick >= NAME_SCROLL_SPEED)
    {
        _scrollTick = 0;
        var keys = new List<string>(_nameScrollOffset.Keys);
        foreach (var k in keys)
            _nameScrollOffset[k] = _nameScrollOffset[k] + 1;
    }

    bool shouldDraw = connectorChanged
        || (updateSource & UpdateType.Update10)  != 0
        || (updateSource & UpdateType.Update100) != 0
        || (updateSource & UpdateType.IGC)       != 0;

    if (shouldDraw)
    {
        List<DockRow> rows = BuildRows();
        foreach (var lcd in lcds)      DrawDockingDisplay(lcd, rows);
        foreach (var lcd in mapLcds)   DrawApproachDisplay(lcd);
        foreach (var lcd in minerLcds) DrawMinerDisplay(lcd);
        foreach (var lcd in radarLcds) DrawRadarDisplay(lcd);
        DrawPBScreen(rows);
    }

    Echo("R.O.S " + ROS_VERSION + " — " + ROS_AUTHOR);
    Echo("Connectors: " + connectors.Count);
    Echo("Cameras:    " + cameras.Count);
    Echo("Miners:     " + _minerContacts.Count);
    Echo("Contacts:   " + _approachList.Count);
    Echo("Radar LCDs: " + radarLcds.Count);
    Echo("Uptime:     " + FormatElapsed(_totalElapsedSec));
    Echo("Next scan:  " + (SCAN_INTERVAL - _scanCounter) + " ticks");
    if (connectorChanged) Echo(">> CONNECTOR CHANGED");
}

// ============================================================
// Block rescan
// ============================================================

void ReScanBlocks()
{
    connectors.Clear(); batteries.Clear(); sensors.Clear();
    alertSounds.Clear(); cameras.Clear(); dockLights.Clear();

    GridTerminalSystem.GetBlocksOfType(connectors, b =>
        b.CustomName.Contains(CONNECTOR_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(batteries);
    GridTerminalSystem.GetBlocksOfType(sensors, b =>
        b.CustomName.Contains(CONNECTOR_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(alertSounds, b =>
        b.CustomName.Contains(ALERT_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(cameras, b =>
        b.CustomName.Contains(CAM_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(dockLights, b =>
        b.CustomName.Contains(LIGHT_TAG) && b.IsSameConstructAs(Me));

    dockLights.Sort((a, b) =>
        string.Compare(a.CustomName, b.CustomName,
            System.StringComparison.OrdinalIgnoreCase));

    foreach (var cam in cameras)
        cam.EnableRaycast = true;
}

// ============================================================
// LCD init
// ============================================================

void InitLCDs()
{
    foreach (var lcd in lcds)      InitLCD(lcd, COLOR_HEADER);
    foreach (var lcd in mapLcds)   InitLCD(lcd, COLOR_HEADER);
    foreach (var lcd in minerLcds) InitLCD(lcd, COLOR_MINER);
    foreach (var lcd in radarLcds) InitLCD(lcd, COLOR_HEADER);
}

void InitLCD(IMyTextPanel lcd, Color accentColor)
{
    if (_initialisedLCDs.Contains(lcd.EntityId)) return;
    lcd.ContentType           = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;
    lcd.Script                = "";
    lcd.BackgroundColor       = COLOR_BG;
    lcd.ScriptBackgroundColor = COLOR_BG;
    lcd.ScriptForegroundColor = accentColor;
    _initialisedLCDs.Add(lcd.EntityId);
}

// ============================================================
// Utility helpers
// ============================================================

string FC(double v)
{
    if (v >= 1000000.0) return (v / 1000000.0).ToString("F1") + "M";
    if (v >= 1000.0)    return (v / 1000.0).ToString("F1") + "k";
    return ((int)v).ToString();
}

Color SaltColor(Color c, int tick)
{
    int a = 218 + (tick % 25);
    if (a > 255) a = 255;
    if (a > (int)c.A) a = (int)c.A;
    return new Color(c.R, c.G, c.B, (byte)a);
}

float AutoFontSize(float surfW, float surfH, int lineCount)
{
    if (lineCount <= 0) return 0.40f;
    float targetH   = (surfH * 0.82f) / lineCount;
    float fontFromH = targetH / 34.4f;
    float fontFromW = surfW / (55f * 21.56f);
    float fs = Math.Min(fontFromH, fontFromW);
    if (fs > 0.50f) fs = 0.50f;
    if (fs < 0.28f) fs = 0.28f;
    return fs;
}

string FormatElapsed(double totalSec)
{
    int h = (int)(totalSec / 3600.0);
    int m = (int)(totalSec % 3600.0 / 60.0);
    int s = (int)(totalSec % 60.0);
    if (h > 0) return h + ":" + m.ToString("D2") + ":" + s.ToString("D2");
    return m + ":" + s.ToString("D2");
}

string ElevationLabel(int elevation)
{
    if (elevation > 0) return "^";
    if (elevation < 0) return "v";
    return "~";
}

Color ElevationColor(int elevation)
{
    if (elevation > 0) return COLOR_ABOVE;
    if (elevation < 0) return COLOR_BELOW;
    return COLOR_DIM * 2f;
}

int ComputeElevation(Vector3D worldPos)
{
    Vector3D offset = worldPos - Me.GetPosition();
    Vector3D local  = Vector3D.TransformNormal(offset,
        MatrixD.Transpose(Me.WorldMatrix));
    double horizontal = Math.Sqrt(local.X * local.X + local.Z * local.Z);
    double angle      = Math.Atan2(local.Y, horizontal);
    if (angle >  0.26) return  1;
    if (angle < -0.26) return -1;
    return 0;
}

string ScrolledName(string name, int maxChars)
{
    if (name.Length <= maxChars) return name;
    int offset = 0;
    _nameScrollOffset.TryGetValue(name, out offset);
    if (!_nameScrollOffset.ContainsKey(name)) _nameScrollOffset[name] = 0;
    string padded = name + "   ";
    int    total  = padded.Length;
    offset = offset % total;
    _nameScrollOffset[name] = offset;
    string result = "";
    for (int i = 0; i < maxChars; i++)
        result += padded[(offset + i) % total];
    return result;
}

string TrendArrow(string minerName, float currentFill)
{
    float prev = 0f;
    _prevCargoFill.TryGetValue(minerName, out prev);
    float diff = currentFill - prev;
    string arrow;
    if      (diff >  15f) arrow = "^^";
    else if (diff >   2f) arrow = "^";
    else if (diff <  -2f) arrow = "v";
    else                  arrow = "=";
    _prevCargoFill[minerName] = currentFill;
    return arrow;
}

Color TrendColor(string arrow)
{
    if (arrow == "^^") return COLOR_DANGER;
    if (arrow == "^")  return COLOR_CHARGE;
    if (arrow == "v")  return COLOR_LOCKED;
    return COLOR_DIM * 2f;
}

string StripTags(string name)
{
    var sb = new System.Text.StringBuilder();
    bool inside = false;
    foreach (char c in name)
    {
        if (c == '[') { inside = true;  continue; }
        if (c == ']') { inside = false; continue; }
        if (!inside) sb.Append(c);
    }
    string result = sb.ToString();
    while (result.Contains("  ")) result = result.Replace("  ", " ");
    return result.Trim();
}

// ============================================================
// Boot screens
// ============================================================

void DrawBootScreen(IMyTextPanel lcd, string moduleName)
{
    RectangleF viewport = new RectangleF(
        (lcd.TextureSize - lcd.SurfaceSize) / 2f, lcd.SurfaceSize);
    var   frame  = lcd.DrawFrame();
    float cx = viewport.Center.X, cy = viewport.Center.Y;
    float w = viewport.Width, margin = 20f;
    float prog = Math.Min(1f, _bootTimer / BOOT_TIME);

    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size, Color = COLOR_BG, Alignment = TextAlignment.CENTER });
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Grid",
        Position = viewport.Center, Size = viewport.Size,
        Color = new Color(20, 40, 25), Alignment = TextAlignment.CENTER });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = ROS_TITLE,
        Position = new Vector2(cx, cy - 55f), RotationOrScale = 1.6f,
        Color = COLOR_HEADER, Alignment = TextAlignment.CENTER, FontId = "Monospace" });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = moduleName,
        Position = new Vector2(cx, cy - 26f), RotationOrScale = 0.42f,
        Color = COLOR_HEADER * 0.7f, Alignment = TextAlignment.CENTER, FontId = "Monospace" });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = ROS_VERSION,
        Position = new Vector2(cx, cy - 10f), RotationOrScale = 0.38f,
        Color = COLOR_DIM * 2f, Alignment = TextAlignment.CENTER, FontId = "Monospace" });
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(cx, cy + 6f), Size = new Vector2(w * 0.6f, 1f),
        Color = COLOR_HEADER * 0.4f, Alignment = TextAlignment.CENTER });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = ROS_AUTHOR,
        Position = new Vector2(cx, cy + 12f), RotationOrScale = 0.30f,
        Color = COLOR_DIM * 1.5f, Alignment = TextAlignment.CENTER, FontId = "Monospace" });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = _bootMessage,
        Position = new Vector2(cx, cy + 36f), RotationOrScale = 0.36f,
        Color = COLOR_HEADER * 0.8f, Alignment = TextAlignment.CENTER, FontId = "Monospace" });
    float barX = viewport.X + margin * 2f, barY = cy + 54f, barW = w - margin * 4f;
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(barX + barW * 0.5f, barY), Size = new Vector2(barW, 4f),
        Color = COLOR_DIM, Alignment = TextAlignment.CENTER });
    if (prog > 0f)
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(barX + (barW * prog) * 0.5f, barY),
            Size = new Vector2(barW * prog, 4f), Color = COLOR_HEADER, Alignment = TextAlignment.CENTER });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = ((int)(prog * 100f)) + "%",
        Position = new Vector2(barX + barW, barY - 8f), RotationOrScale = 0.32f,
        Color = COLOR_DIM * 2f, Alignment = TextAlignment.RIGHT, FontId = "Monospace" });
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Screen_LoadingBar",
        Position = new Vector2(cx, cy + 72f), Size = new Vector2(18f, 18f),
        Color = COLOR_HEADER * 0.5f, Alignment = TextAlignment.CENTER, RotationOrScale = _spinnerAngle });
    frame.Dispose();
}

void DrawPBBootScreen()
{
    var pb = Me as IMyTextSurfaceProvider;
    if (pb == null || pb.SurfaceCount == 0) return;
    var surface = pb.GetSurface(0);
    if (surface == null) return;
    surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;
    surface.Script = ""; surface.BackgroundColor = COLOR_BG;
    surface.ScriptBackgroundColor = COLOR_BG; surface.ScriptForegroundColor = COLOR_HEADER;
    RectangleF viewport = new RectangleF((surface.TextureSize - surface.SurfaceSize) / 2f, surface.SurfaceSize);
    var frame = surface.DrawFrame();
    float cx = viewport.Center.X, cy = viewport.Center.Y, prog = Math.Min(1f, _bootTimer / BOOT_TIME);
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size, Color = COLOR_BG, Alignment = TextAlignment.CENTER });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = ROS_TITLE,
        Position = new Vector2(cx, cy - 20f), RotationOrScale = 0.8f,
        Color = COLOR_HEADER, Alignment = TextAlignment.CENTER, FontId = "Monospace" });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = ROS_VERSION + "  BOOTING",
        Position = new Vector2(cx, cy), RotationOrScale = 0.34f,
        Color = COLOR_DIM * 2f, Alignment = TextAlignment.CENTER, FontId = "Monospace" });
    float barX = viewport.X + 10f, barW = viewport.Width - 20f;
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(barX + barW * 0.5f, cy + 14f), Size = new Vector2(barW, 3f),
        Color = COLOR_DIM, Alignment = TextAlignment.CENTER });
    if (prog > 0f)
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(barX + (barW * prog) * 0.5f, cy + 14f),
            Size = new Vector2(barW * prog, 3f), Color = COLOR_HEADER, Alignment = TextAlignment.CENTER });
    frame.Dispose();
}

// ============================================================
// Sequential lights / alerts / distance replies
// ============================================================

void UpdateSequentialLights(float delta)
{
    bool active = _approachList.Count > 0;
    if (!active)
    {
        if (_lightsActive)
        {
            foreach (var l in dockLights) { l.Enabled = false; l.BlinkIntervalSeconds = 0f; }
            _lightsActive = false; _lightSeqIndex = 0; _lightSeqTimer = 0f;
        }
        return;
    }
    _lightsActive = true;
    if (dockLights.Count == 0) return;
    _lightSeqTimer += delta;
    if (_lightSeqTimer >= LIGHT_SEQ_RATE)
    {
        _lightSeqTimer -= LIGHT_SEQ_RATE;
        foreach (var l in dockLights) { l.Enabled = false; l.BlinkIntervalSeconds = 0f; }
        if (_lightSeqIndex < dockLights.Count)
        {
            var al = dockLights[_lightSeqIndex];
            al.Enabled = true; al.Color = new Color(255, 140, 0);
            al.Intensity = 10f; al.BlinkIntervalSeconds = 0f;
        }
        _lightSeqIndex++;
        if (_lightSeqIndex >= dockLights.Count) _lightSeqIndex = 0;
    }
}

void BroadcastDistanceReplies()
{
    foreach (var miner in _minerContacts)
    {
        double dist  = miner.Distance(Me.GetPosition());
        string reply = miner.Name + "|" + ((int)dist);
        IGC.SendBroadcastMessage(REPLY_CHANNEL, reply,
            TransmissionDistance.TransmissionDistanceMax);
    }
}

void UpdateDockTimes()
{
    foreach (var conn in connectors)
    {
        string name   = StripTags(conn.CustomName);
        bool   locked = conn.Status == MyShipConnectorStatus.Connected;
        bool   wasLocked = false;
        _prevLocked.TryGetValue(name, out wasLocked);
        if (locked && !wasLocked)  _dockTimes[name] = DateTime.Now;
        if (!locked && wasLocked)  _dockTimes.Remove(name);
        _prevLocked[name] = locked;
    }
}

void UpdateAlert()
{
    bool has = _approachList.Count > 0;
    if (has && !_alertPlayed) { foreach (var s in alertSounds) s.Play(); _alertPlayed = true; }
    else if (!has) _alertPlayed = false;
}

// ============================================================
// Contact update methods
// ============================================================

void UpdateMinerContacts()
{
    _minerContacts.RemoveAll(c => c.IsStale());
    while (_minerListener.HasPendingMessage)
    {
        var    msg = _minerListener.AcceptMessage();
        string raw = msg.Data.ToString();
        string[] p = raw.Split('|');
        if (p.Length < 5) continue;
        string gridName = StripTags(p[0]);
        var    ic = System.Globalization.CultureInfo.InvariantCulture;
        float  speed = 0f;
        float.TryParse(p[1], System.Globalization.NumberStyles.Float, ic, out speed);
        double px = 0, py = 0, pz = 0;
        double.TryParse(p[2], System.Globalization.NumberStyles.Float, ic, out px);
        double.TryParse(p[3], System.Globalization.NumberStyles.Float, ic, out py);
        double.TryParse(p[4], System.Globalization.NumberStyles.Float, ic, out pz);
        bool   drilling  = p.Length > 5 && p[5] == "1";
        float  cargoFill = 0f, fuelLevel = -1f, battLevel = -1f;
        string statusStr = "TRANSIT";
        if (p.Length > 6) float.TryParse(p[6], System.Globalization.NumberStyles.Float, ic, out cargoFill);
        if (p.Length > 7) float.TryParse(p[7], System.Globalization.NumberStyles.Float, ic, out fuelLevel);
        if (p.Length > 8) float.TryParse(p[8], System.Globalization.NumberStyles.Float, ic, out battLevel);
        if (p.Length > 9) statusStr = p[9];
        Vector3D pos = new Vector3D(px, py, pz);
        bool found = false;
        foreach (var c in _minerContacts)
        {
            if (c.Name == gridName)
            {
                c.Speed = speed; c.Position = pos; c.LastSeen = DateTime.Now;
                c.Drilling = drilling; c.CargoFill = cargoFill;
                c.FuelLevel = fuelLevel; c.BattLevel = battLevel;
                c.StatusStr = statusStr; found = true; break;
            }
        }
        if (!found)
            _minerContacts.Add(new MinerContact {
                Name = gridName, Speed = speed, Position = pos,
                LastSeen = DateTime.Now, Drilling = drilling,
                CargoFill = cargoFill, FuelLevel = fuelLevel,
                BattLevel = battLevel, StatusStr = statusStr
            });
    }
}

void UpdateSensorContacts()
{
    foreach (var sensor in sensors)
    {
        string tag    = StripTags(sensor.CustomName);
        var    entity = sensor.LastDetectedEntity;
        if (!entity.IsEmpty() && sensor.IsActive)
        {
            _bayContacts[tag] = new BayContact {
                Name = entity.Name,
                Distance = Vector3D.Distance(Me.GetPosition(), entity.Position),
                Speed = (float)entity.Velocity.Length(), DetectedAt = DateTime.Now
            };
        }
        else
        {
            BayContact ex;
            if (_bayContacts.TryGetValue(tag, out ex) && ex.IsStale())
                _bayContacts.Remove(tag);
        }
    }
}

void UpdateCameraContacts()
{
    foreach (var cam in cameras)
    {
        if (cam.TimeUntilScan(CAM_RANGE) > 0) continue;
        MyDetectedEntityInfo hit = cam.Raycast(CAM_RANGE);
        if (hit.IsEmpty()) continue;
        if (hit.Type != MyDetectedEntityType.LargeGrid &&
            hit.Type != MyDetectedEntityType.SmallGrid) continue;

        double dist    = Vector3D.Distance(Me.GetPosition(), hit.Position);
        float  speed   = (float)hit.Velocity.Length();
        string camName = StripTags(cam.CustomName);
        int    elev    = ComputeElevation(hit.Position);
        bool   found   = false;

        foreach (var c in _approachList)
        {
            if (c.Name == hit.Name && !c.IsMinerBroadcast)
            {
                c.Distance = dist; c.Speed = speed;
                c.CameraName = camName; c.DetectedAt = DateTime.Now;
                c.Elevation  = elev; c.WorldPos = hit.Position;
                found = true; break;
            }
        }
        if (!found)
            _approachList.Add(new ApproachContact {
                Name = hit.Name, Distance = dist, Speed = speed,
                Type = hit.Type == MyDetectedEntityType.LargeGrid ? "LARGE" : "SMALL",
                CameraName = camName, DetectedAt = DateTime.Now,
                IsMinerBroadcast = false, Elevation = elev,
                FadeAlpha = 1f, WorldPos = hit.Position
            });
    }
}

void MergeContactsToApproachList()
{
    _approachList.RemoveAll(c => c.IsExpired());
    _dockedNames.Clear();
    foreach (var conn in connectors)
        if (conn.Status == MyShipConnectorStatus.Connected && conn.OtherConnector != null)
            _dockedNames.Add(StripTags(conn.OtherConnector.CubeGrid.CustomName).ToLower());

    foreach (var miner in _minerContacts)
    {
        double dist = miner.Distance(Me.GetPosition());
        int    elev = ComputeElevation(miner.Position);
        bool   found = false;
        foreach (var c in _approachList)
        {
            if (c.Name == miner.Name && c.IsMinerBroadcast)
            {
                c.Distance = dist; c.Speed = miner.Speed;
                c.DetectedAt = miner.LastSeen; c.Elevation = elev;
                c.WorldPos   = miner.Position;
                found = true; break;
            }
        }
        if (!found)
            _approachList.Add(new ApproachContact {
                Name = miner.Name, Distance = dist, Speed = miner.Speed,
                Type = "MINER", CameraName = "IGC",
                DetectedAt = miner.LastSeen, IsMinerBroadcast = true,
                Elevation = elev, FadeAlpha = 1f, WorldPos = miner.Position
            });
    }

    _approachList.RemoveAll(c => _dockedNames.Contains(c.Name.ToLower()));
    _approachList.RemoveAll(c => !c.IsMinerBroadcast && c.Distance > APPROACH_RANGE);
    _approachList.Sort((a, b) => a.Distance.CompareTo(b.Distance));
}

// ============================================================
// Data helpers
// ============================================================

void RebuildGridChargeCache()
{
    _gridChargeCache.Clear();
    _gridChargeTimeCache.Clear();
    var gridCur = new Dictionary<long, float>();
    var gridMax = new Dictionary<long, float>();
    var gridIn  = new Dictionary<long, float>();
    var gridOut = new Dictionary<long, float>();
    foreach (var bat in batteries)
    {
        long id = bat.CubeGrid.EntityId;
        float cur = 0f, max = 0f, inp = 0f, outp = 0f;
        gridCur.TryGetValue(id, out cur); gridMax.TryGetValue(id, out max);
        gridIn.TryGetValue(id, out inp);  gridOut.TryGetValue(id, out outp);
        gridCur[id] = cur + bat.CurrentStoredPower;
        gridMax[id] = max + bat.MaxStoredPower;
        gridIn[id]  = inp  + bat.CurrentInput;
        gridOut[id] = outp + bat.CurrentOutput;
    }
    foreach (var kvp in gridMax)
    {
        long  id  = kvp.Key;
        float max = kvp.Value;
        float cur = 0f; gridCur.TryGetValue(id, out cur);
        float inp = 0f; gridIn.TryGetValue(id, out inp);
        float outp = 0f; gridOut.TryGetValue(id, out outp);
        _gridChargeCache[id] = max <= 0f ? -1f : cur / max;
        float netRate = inp - outp, remaining = max - cur;
        if (cur >= max * 0.999f)       _gridChargeTimeCache[id] = 0f;
        else if (netRate <= 0f)        _gridChargeTimeCache[id] = -1f;
        else                           _gridChargeTimeCache[id] = (remaining / netRate) * 60f;
    }
}

float GetGridChargeFromCache(IMyCubeGrid g)
{ float v = -1f; _gridChargeCache.TryGetValue(g.EntityId, out v); return v; }

float GetGridChargeTimeFromCache(IMyCubeGrid g)
{ float v = -1f; _gridChargeTimeCache.TryGetValue(g.EntityId, out v); return v; }

float GetLocalCharge()
{
    float cur = 0f, max = 0f;
    foreach (var bat in batteries)
    {
        if (!bat.IsSameConstructAs(Me)) continue;
        cur += bat.CurrentStoredPower; max += bat.MaxStoredPower;
    }
    return max <= 0f ? -1f : cur / max;
}

MinerContact FindMinerData(string vesselName)
{
    if (string.IsNullOrEmpty(vesselName)) return null;
    string lower = StripTags(vesselName).ToLower();
    foreach (var m in _minerContacts)
        if (m.Name.ToLower() == lower) return m;
    return null;
}

bool IsDockedGrid(string vesselName)
{
    string lower = StripTags(vesselName).ToLower();
    foreach (var conn in connectors)
        if (conn.Status == MyShipConnectorStatus.Connected && conn.OtherConnector != null)
            if (StripTags(conn.OtherConnector.CubeGrid.CustomName).ToLower() == lower)
                return true;
    return false;
}

List<DockRow> BuildRows()
{
    RebuildGridChargeCache();
    var rows = new List<DockRow>();
    foreach (var conn in connectors)
    {
        string       name   = StripTags(conn.CustomName);
        string       state;
        string       vessel = "---";
        Color        color;
        float        chargeRatio = -1f, batteryRatio = -1f, chargeTimeMins = -1f;
        BayContact   contact  = null;
        _bayContacts.TryGetValue(name, out contact);
        DateTime?    dockedAt  = null;
        MinerContact minerData = null;

        switch (conn.Status)
        {
            case MyShipConnectorStatus.Connected:
                state = "LOCKED"; color = COLOR_LOCKED;
                if (conn.OtherConnector != null)
                {
                    vessel         = StripTags(conn.OtherConnector.CubeGrid.CustomName);
                    chargeRatio    = GetGridChargeFromCache(conn.OtherConnector.CubeGrid);
                    batteryRatio   = chargeRatio;
                    chargeTimeMins = GetGridChargeTimeFromCache(conn.OtherConnector.CubeGrid);
                    minerData      = FindMinerData(vessel);
                }
                DateTime dt;
                if (_dockTimes.TryGetValue(name, out dt)) dockedAt = dt;
                break;
            case MyShipConnectorStatus.Connectable:
                state = "READY"; color = COLOR_READY; break;
            case MyShipConnectorStatus.Unconnected:
                state = conn.Enabled ? "IDLE" : "OFFLINE";
                color = conn.Enabled ? COLOR_IDLE : COLOR_OFFLINE; break;
            default:
                state = "OFFLINE"; color = COLOR_OFFLINE; break;
        }
        rows.Add(new DockRow(name, state, vessel, color,
            chargeRatio, batteryRatio, contact, dockedAt, minerData, chargeTimeMins));
    }
    return rows;
}

// ============================================================
// Header helper
// ============================================================

float DrawROSHeader(MySpriteDrawFrame frame, float x, float startY, float w,
                    float viewportX, float margin, string moduleName, Color accentColor)
{
    float y = startY; int st = _renderTick;
    DrawTextLeft(frame, ROS_TITLE, new Vector2(x, y), SaltColor(COLOR_HEADER, st), 0.62f);
    string uptimeStr = ROS_VERSION + "  " + FormatElapsed(_totalElapsedSec);
    DrawTextRight(frame, uptimeStr,
        new Vector2(viewportX + w - margin, y), SaltColor(COLOR_DIM * 2f, st), 0.38f);
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Screen_LoadingBar",
        Position = new Vector2(viewportX + w - margin - 26f, y + 9f),
        Size = new Vector2(14f, 14f), Color = SaltColor(accentColor * 0.7f, st),
        Alignment = TextAlignment.CENTER, RotationOrScale = _spinnerAngle });
    y += 16f;
    DrawTextLeft(frame, "  " + moduleName, new Vector2(x, y), SaltColor(accentColor, st), 0.44f);
    y += 14f;
    DrawTextLeft(frame, "  " + ROS_AUTHOR, new Vector2(x, y), SaltColor(COLOR_DIM * 1.4f, st), 0.30f);
    y += 12f;
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(x + (viewportX + w - margin - x) / 2f, y),
        Size = new Vector2(viewportX + w - margin - x, 1.5f),
        Color = SaltColor(accentColor * 0.5f, st), Alignment = TextAlignment.CENTER });
    y += 5f;
    return y;
}

// ============================================================
// DISPLAY 1 — DOCK CONTROL
// ============================================================

void DrawDockingDisplay(IMyTextPanel lcd, List<DockRow> rows)
{
    RectangleF viewport = new RectangleF(
        (lcd.TextureSize - lcd.SurfaceSize) / 2f, lcd.SurfaceSize);
    var   frame       = lcd.DrawFrame();
    float margin      = 8f;
    float x           = viewport.X + margin;
    float w           = viewport.Width - margin * 2f;
    float maxY        = viewport.Y + viewport.Height - margin;
    float localCharge = GetLocalCharge();
    float footerH     = 16f + (localCharge >= 0f ? 24f : 0f);
    int   st          = _renderTick;

    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size,
        Color = COLOR_BG, Alignment = TextAlignment.CENTER });

    float y = DrawROSHeader(frame, x, viewport.Y + margin, w, viewport.X, margin, MOD_DOCK, COLOR_HEADER);

    int lockedCount = 0, idleCount = 0, offlineCount = 0, readyCount = 0;
    foreach (var r in rows)
    {
        switch (r.State)
        {
            case "LOCKED":  lockedCount++;  break;
            case "IDLE":    idleCount++;    break;
            case "OFFLINE": offlineCount++; break;
            case "READY":   readyCount++;   break;
        }
    }

    float fs = 0.42f;
    DrawDot(frame, new Vector2(x + 4f, y + 6f), SaltColor(COLOR_LOCKED, st));
    DrawTextLeft(frame, "LOCKED:" + lockedCount, new Vector2(x + 13f, y), SaltColor(COLOR_LOCKED, st), fs);
    DrawDot(frame, new Vector2(x + w * 0.30f + 4f, y + 6f), SaltColor(COLOR_IDLE, st));
    DrawTextLeft(frame, "IDLE:" + idleCount, new Vector2(x + w * 0.30f + 13f, y), SaltColor(COLOR_IDLE, st), fs);
    DrawDot(frame, new Vector2(x + w * 0.52f + 4f, y + 6f), SaltColor(COLOR_READY, st));
    DrawTextLeft(frame, "READY:" + readyCount, new Vector2(x + w * 0.52f + 13f, y), SaltColor(COLOR_READY, st), fs);
    if (offlineCount > 0)
    {
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Danger",
            Position = new Vector2(x + w * 0.76f + 4f, y + 6f),
            Size = new Vector2(10f, 10f), Color = SaltColor(COLOR_DANGER, st),
            Alignment = TextAlignment.CENTER });
        DrawTextLeft(frame, "OFF:" + offlineCount,
            new Vector2(x + w * 0.76f + 13f, y), SaltColor(COLOR_DANGER, st), fs);
    }

    y += 18f;
    DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM, st), 1f); y += 4f;

    int   totalLines = rows.Count + 8;
    float autoFs     = AutoFontSize(w, viewport.Height, totalLines);

    float colState  = w * 0.26f;
    float colVessel = w * 0.40f;
    float colBat    = w * 0.63f;
    float colTime   = w * 0.85f;
    float rightEdge = viewport.X + w - margin;

    DrawTextLeft(frame,  "PORT",    new Vector2(x + 10f, y),            SaltColor(COLOR_DIM * 2f, st), autoFs);
    DrawTextLeft(frame,  "STATE",   new Vector2(x + colState + 2f, y),  SaltColor(COLOR_DIM * 2f, st), autoFs);
    DrawTextLeft(frame,  "VESSEL",  new Vector2(x + colVessel + 2f, y), SaltColor(COLOR_DIM * 2f, st), autoFs);
    DrawTextLeft(frame,  "BATTERY", new Vector2(x + colBat, y),         SaltColor(COLOR_DIM * 2f, st), autoFs * 0.85f);
    DrawTextRight(frame, "TIME",    new Vector2(rightEdge, y),           SaltColor(COLOR_DIM * 2f, st), autoFs * 0.85f);

    y += autoFs * 34f;
    DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM, st), 1f); y += 3f;

    float rowH      = autoFs * 34f;
    float available = maxY - y - footerH;
    if (rows.Count > 0 && rows.Count * (rowH + 3f) > available)
        rowH = Math.Max(autoFs * 28f, (available / rows.Count) - 3f);

    float batColW = colTime - colBat - 6f;
    float barH    = rowH * 0.30f;
    if (barH < 3f) barH = 3f;
    if (barH > 6f) barH = 6f;

    foreach (var r in rows)
    {
        if (y + rowH > maxY - footerH) break;
        bool isOffline = r.State == "OFFLINE";
        bool isLocked  = r.State == "LOCKED";
        float rowMidY  = y + rowH * 0.5f;

        if (isOffline)
            frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Danger",
                Position = new Vector2(x + 4f, rowMidY), Size = new Vector2(10f, 10f),
                Color = SaltColor(COLOR_DANGER, st), Alignment = TextAlignment.CENTER });
        else
            DrawDot(frame, new Vector2(x + 4f, rowMidY), SaltColor(r.StateColor, st));

        string portDisplay = ScrolledName(r.Name, PORT_MAX_CHARS);
        DrawTextLeft(frame, portDisplay, new Vector2(x + 13f, y),
            SaltColor(isOffline ? COLOR_DIM * 2f : COLOR_HEADER, st), autoFs);

        float badgeW = colVessel - colState - 6f;
        DrawBadge(frame, new Vector2(x + colState, y), badgeW, rowH - 2f, r.State, SaltColor(r.StateColor, st));

        if (isLocked)
        {
            string vesselDisplay = ScrolledName(r.Vessel, NAME_MAX_CHARS);
            DrawTextLeft(frame, vesselDisplay, new Vector2(x + colVessel + 4f, y),
                SaltColor(r.MinerData != null ? COLOR_MINER : COLOR_HEADER, st), autoFs);

            if (r.BatteryRatio >= 0f)
            {
                Color batColor = r.BatteryRatio >= 0.7f ? COLOR_LOCKED
                               : r.BatteryRatio >= 0.3f ? COLOR_CHARGE : COLOR_DANGER;
                float barX     = x + colBat;
                float barY     = rowMidY - barH * 0.5f;
                float pctTextW = autoFs * 21.56f * 4f;
                float barW     = batColW - pctTextW - 4f;
                float fillW    = barW * r.BatteryRatio;
                DrawHLine(frame, new Vector2(barX, barY), barW, SaltColor(COLOR_DIM * 0.6f, st), barH);
                if (fillW > 0f)
                    DrawHLine(frame, new Vector2(barX, barY), fillW, SaltColor(batColor, st), barH);
                DrawTextLeft(frame, ((int)(r.BatteryRatio * 100f)) + "%",
                    new Vector2(barX + barW + 4f, y), SaltColor(batColor, st), autoFs * 0.85f);
            }
            else
            {
                DrawTextLeft(frame, "--", new Vector2(x + colBat, y),
                    SaltColor(COLOR_DIM * 2f, st), autoFs * 0.85f);
            }

            Color timeCol = r.ChargeTimeMins == 0f ? COLOR_LOCKED
                          : r.ChargeTimeMins < 0f  ? COLOR_DIM * 2f : COLOR_CHARGE;
            DrawTextRight(frame, r.ChargeTimeString(), new Vector2(rightEdge, y),
                SaltColor(timeCol, st), autoFs * 0.85f);
        }

        y += rowH;
        DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM * 0.4f, st), 1f); y += 3f;
    }

    float fy = maxY - footerH + 2f;
    DrawHLine(frame, new Vector2(x, fy), w, SaltColor(COLOR_HEADER * 0.5f, st), 1.5f); fy += 4f;
    if (localCharge >= 0f)
    {
        Color bc = localCharge >= 1f ? COLOR_LOCKED : localCharge >= 0.5f ? COLOR_CHARGE : COLOR_OFFLINE;
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "IconEnergy",
            Position = new Vector2(x + 5f, fy + 6f), Size = new Vector2(11f, 11f),
            Color = SaltColor(bc, st), Alignment = TextAlignment.CENTER });
        DrawTextLeft(frame, "BASE POWER", new Vector2(x + 16f, fy), SaltColor(COLOR_DIM * 2f, st), 0.40f);
        DrawTextRight(frame, ((int)(localCharge * 100f)) + "%",
            new Vector2(rightEdge, fy), SaltColor(bc, st), 0.40f);
        fy += 15f;
        DrawHLine(frame, new Vector2(x, fy), w, SaltColor(COLOR_DIM * 0.8f, st), 5f);
        DrawHLine(frame, new Vector2(x, fy), w * localCharge, SaltColor(bc, st), 5f);
    }
    frame.Dispose();
}

// ============================================================
// DISPLAY 2 — PROXIMITY SCANNER
// ============================================================

void DrawApproachDisplay(IMyTextPanel lcd)
{
    Color accentColor = _approachList.Count > 0 ? COLOR_INCOMING : COLOR_HEADER;
    RectangleF viewport = new RectangleF(
        (lcd.TextureSize - lcd.SurfaceSize) / 2f, lcd.SurfaceSize);
    var   frame  = lcd.DrawFrame();
    float margin = 8f;
    float x      = viewport.X + margin;
    float w      = viewport.Width - margin * 2f;
    float maxY   = viewport.Y + viewport.Height - margin;
    int   st     = _renderTick;

    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size, Color = COLOR_BG, Alignment = TextAlignment.CENTER });

    float y = DrawROSHeader(frame, x, viewport.Y + margin, w, viewport.X, margin, MOD_PROXIMITY, accentColor);

    float fs = 0.38f;
    DrawDot(frame, new Vector2(x + 4f, y + 5f), SaltColor(cameras.Count > 0 ? COLOR_LOCKED : COLOR_DIM, st));
    DrawTextLeft(frame,
        cameras.Count > 0 ? "CAMERA x" + cameras.Count + "  RANGE " + FC(CAM_RANGE) + "m" : "NO CAMERA — tag " + CAM_TAG,
        new Vector2(x + 13f, y), SaltColor(cameras.Count > 0 ? COLOR_DIM * 2f : COLOR_DIM, st), fs);
    y += 14f;

    DrawDot(frame, new Vector2(x + 4f, y + 5f), SaltColor(_lightsActive ? COLOR_INCOMING : COLOR_DIM, st));
    string lightStr = dockLights.Count == 0 ? "NO LIGHTS — tag " + LIGHT_TAG + " 1, 2..."
        : _lightsActive ? "RUNWAY x" + dockLights.Count + "  SEQ " + (_lightSeqIndex + 1) + "/" + dockLights.Count
        : "RUNWAY x" + dockLights.Count + "  STANDBY";
    DrawTextLeft(frame, lightStr, new Vector2(x + 13f, y),
        SaltColor(_lightsActive ? COLOR_INCOMING : COLOR_DIM, st), fs);
    y += 14f;

    DrawDot(frame, new Vector2(x + 4f, y + 5f), SaltColor(alertSounds.Count > 0 ? COLOR_LOCKED : COLOR_DIM, st));
    DrawTextLeft(frame,
        alertSounds.Count > 0 ? "ALERT x" + alertSounds.Count + (_alertPlayed ? "  ACTIVE" : "  STANDBY") : "NO SOUND — tag " + ALERT_TAG,
        new Vector2(x + 13f, y),
        SaltColor(_alertPlayed ? COLOR_INCOMING : alertSounds.Count > 0 ? COLOR_DIM * 2f : COLOR_DIM, st), fs);
    y += 14f;

    DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM, st), 1f); y += 5f;

    if (_approachList.Count == 0)
    {
        DrawTextLeft(frame, "No contacts within " + FC(APPROACH_RANGE) + "m",
            new Vector2(x, y), SaltColor(COLOR_DIM * 2f, st), 0.46f);
        if (cameras.Count > 0)
        {
            y += 22f;
            float scanW = w * ((float)Math.Abs(Math.Sin(_spinnerAngle)));
            DrawHLine(frame, new Vector2(x, y + 6f), w, SaltColor(COLOR_DIM * 0.4f, st), 2f);
            DrawHLine(frame, new Vector2(x, y + 6f), scanW, SaltColor(COLOR_DIM * 1.5f, st), 2f);
            DrawTextLeft(frame, "SCANNING...", new Vector2(x, y), SaltColor(COLOR_DIM * 1.5f, st), 0.38f);
        }
        frame.Dispose();
        return;
    }

    DrawTextLeft(frame, "VESSEL", new Vector2(x + 13f, y), SaltColor(COLOR_DIM * 2f, st), 0.38f);
    DrawTextRight(frame, "DIST    SPD    SRC",
        new Vector2(viewport.X + w - margin, y), SaltColor(COLOR_DIM * 2f, st), 0.36f);
    y += 15f;
    DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM, st), 1f); y += 3f;

    foreach (var contact in _approachList)
    {
        if (y + 22f > maxY - 14f) break;
        float alpha = contact.ComputeAlpha();
        Color cc = contact.IsMinerBroadcast ? COLOR_MINER
                 : contact.Distance < 200   ? COLOR_DANGER
                 : contact.Distance < 500   ? COLOR_INCOMING : COLOR_APPROACH;
        cc = new Color(cc.R, cc.G, cc.B, (byte)(cc.A * alpha));
        Color ccDim = new Color(cc.R, cc.G, cc.B, (byte)(cc.A * 0.7f));
        Color elevCol = ElevationColor(contact.Elevation);
        elevCol = new Color(elevCol.R, elevCol.G, elevCol.B, (byte)(elevCol.A * alpha));

        frame.Add(new MySprite { Type = SpriteType.TEXT, Data = ElevationLabel(contact.Elevation),
            Position = new Vector2(x + 5f, y), RotationOrScale = 0.40f, Color = elevCol,
            Alignment = TextAlignment.CENTER, FontId = "Monospace" });
        DrawTextLeft(frame, contact.Name, new Vector2(x + 15f, y), cc, 0.42f);
        DrawTextRight(frame,
            FC(contact.Distance) + "    " + (int)contact.Speed + "m/s    " +
            (contact.IsMinerBroadcast ? "IGC" : contact.CameraName),
            new Vector2(viewport.X + w - margin, y), ccDim, 0.38f);
        y += 18f;

        if (contact.IsMinerBroadcast && y + 12f <= maxY - 14f)
        {
            MinerContact md = FindMinerData(contact.Name);
            if (md != null)
            {
                string info = "CARGO " + (int)md.CargoFill + "%";
                if (md.BattLevel >= 0f) info += "   BAT " + (int)md.BattLevel + "%";
                if (md.FuelLevel >= 0f) info += "   FUEL " + (int)md.FuelLevel + "%";
                if (md.Drilling) info = "DRILLING   " + info;
                Color infoCol = new Color(COLOR_MINER.R, COLOR_MINER.G, COLOR_MINER.B,
                    (byte)(COLOR_MINER.A * 0.7f * alpha));
                DrawTextLeft(frame, info, new Vector2(x + 15f, y), infoCol, 0.32f);
                y += 12f;
            }
        }

        float fill = Math.Min(1f, (float)Math.Max(0, 1.0 - contact.Distance / CAM_RANGE));
        DrawHLine(frame, new Vector2(x, y), w,
            new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, (byte)(COLOR_DIM.A * 0.4f * alpha)), 3f);
        DrawHLine(frame, new Vector2(x, y), w * fill,
            new Color(cc.R, cc.G, cc.B, (byte)(cc.A * alpha)), 3f);
        y += 7f;
        DrawHLine(frame, new Vector2(x, y), w,
            new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, (byte)(COLOR_DIM.A * 0.3f)), 1f);
        y += 3f;
    }

    float footerY = maxY - 13f;
    DrawHLine(frame, new Vector2(x, footerY), w, SaltColor(accentColor * 0.4f, st), 1f);
    int minerCount = 0;
    foreach (var c in _approachList) if (c.IsMinerBroadcast) minerCount++;
    DrawTextLeft(frame, _approachList.Count + " CONTACT" + (_approachList.Count != 1 ? "S" : ""),
        new Vector2(x, footerY + 3f), SaltColor(accentColor, st), 0.40f);
    if (minerCount > 0)
        DrawTextRight(frame, minerCount + " MINER" + (minerCount != 1 ? "S" : ""),
            new Vector2(viewport.X + w - margin, footerY + 3f), SaltColor(COLOR_MINER, st), 0.40f);
    frame.Dispose();
}

// ============================================================
// DISPLAY 3 — FLEET OPERATIONS
// ============================================================

void DrawMinerDisplay(IMyTextPanel lcd)
{
    RectangleF viewport = new RectangleF(
        (lcd.TextureSize - lcd.SurfaceSize) / 2f, lcd.SurfaceSize);
    var   frame  = lcd.DrawFrame();
    float margin = 8f;
    float x      = viewport.X + margin;
    float w      = viewport.Width - margin * 2f;
    float maxY   = viewport.Y + viewport.Height - margin;
    int   st     = _renderTick;

    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size, Color = COLOR_BG, Alignment = TextAlignment.CENTER });

    float y = DrawROSHeader(frame, x, viewport.Y + margin, w, viewport.X, margin, MOD_FLEET, COLOR_MINER);

    if (_minerContacts.Count == 0)
    {
        DrawTextLeft(frame, "No miners broadcasting", new Vector2(x, y), SaltColor(COLOR_DIM * 2f, st), 0.44f);
        DrawTextLeft(frame, "Install R.O.S Fleet Broadcast on miner PB",
            new Vector2(x, y + 16f), SaltColor(COLOR_DIM, st), 0.32f);
        frame.Dispose(); return;
    }

    foreach (var miner in _minerContacts)
    {
        if (y + 50f > maxY - 14f) break;
        double dist      = miner.Distance(Me.GetPosition());
        string distStr   = FC(dist) + (dist >= 1000 ? "" : "m");
        bool   isDocked  = IsDockedGrid(miner.Name);
        string statusStr = isDocked ? "DOCKED" : miner.StatusStr;

        Color statusColor;
        switch (statusStr)
        {
            case "DOCKED":  statusColor = COLOR_LOCKED; break;
            case "MINING":  statusColor = COLOR_DRILL;  break;
            case "PARKED":  statusColor = COLOR_PARKED; break;
            case "TRANSIT": statusColor = COLOR_MINER;  break;
            default:        statusColor = COLOR_IDLE;   break;
        }

        DrawDot(frame, new Vector2(x + 4f, y + 8f), SaltColor(statusColor, st));
        DrawTextLeft(frame, miner.Name, new Vector2(x + 13f, y), SaltColor(statusColor, st), 0.46f);
        DrawTextRight(frame, distStr + "   " + statusStr,
            new Vector2(viewport.X + w - margin, y), SaltColor(statusColor, st), 0.40f);
        y += 16f;

        bool  hasH2  = miner.FuelLevel >= 0f;
        bool  hasBat = miner.BattLevel >= 0f;
        int   barCount = 1 + (hasBat ? 1 : 0) + (hasH2 ? 1 : 0);
        float barSlotW = (w - (barCount - 1) * 5f) / barCount;
        float barH = 5f, bx = x;

        string trendArrow = TrendArrow(miner.Name, miner.CargoFill);
        Color  trendCol   = TrendColor(trendArrow);
        Color cargoColor  = miner.CargoFill >= 90f ? COLOR_DANGER
                          : miner.CargoFill >= 60f ? COLOR_CHARGE : COLOR_LOCKED;
        DrawTextLeft(frame, "CARGO", new Vector2(bx, y), SaltColor(COLOR_DIM * 2f, st), 0.30f);
        DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW, SaltColor(COLOR_DIM * 0.6f, st), barH);
        DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW * (miner.CargoFill / 100f), SaltColor(cargoColor, st), barH);
        DrawTextRight(frame, (int)miner.CargoFill + "% " + trendArrow,
            new Vector2(bx + barSlotW, y), SaltColor(trendCol, st), 0.30f);
        bx += barSlotW + 5f;

        if (hasBat)
        {
            Color batColor = miner.BattLevel < 20f ? COLOR_DANGER : miner.BattLevel < 50f ? COLOR_CHARGE : COLOR_LOCKED;
            DrawTextLeft(frame, "BATTERY", new Vector2(bx, y), SaltColor(COLOR_DIM * 2f, st), 0.30f);
            DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW, SaltColor(COLOR_DIM * 0.6f, st), barH);
            DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW * (miner.BattLevel / 100f), SaltColor(batColor, st), barH);
            DrawTextRight(frame, (int)miner.BattLevel + "%",
                new Vector2(bx + barSlotW, y), SaltColor(batColor, st), 0.30f);
            bx += barSlotW + 5f;
        }

        if (hasH2)
        {
            Color h2Color = miner.FuelLevel < 20f ? COLOR_DANGER : miner.FuelLevel < 50f ? COLOR_CHARGE : COLOR_LOCKED;
            DrawTextLeft(frame, "FUEL", new Vector2(bx, y), SaltColor(COLOR_DIM * 2f, st), 0.30f);
            DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW, SaltColor(COLOR_DIM * 0.6f, st), barH);
            DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW * (miner.FuelLevel / 100f), SaltColor(h2Color, st), barH);
            DrawTextRight(frame, (int)miner.FuelLevel + "%",
                new Vector2(bx + barSlotW, y), SaltColor(h2Color, st), 0.30f);
        }

        y += 18f;
        DrawTextLeft(frame,
            (miner.Drilling ? "DRILLING" : statusStr) + "   " + (int)miner.Speed + "m/s" +
            "   UPD: " + miner.AgeString(),
            new Vector2(x + 13f, y),
            SaltColor(miner.Drilling ? COLOR_DRILL * 0.9f : COLOR_DIM * 2f, st), 0.32f);
        y += 11f;
        DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM * 0.4f, st), 1f); y += 5f;
    }

    float footerY = maxY - 13f;
    DrawHLine(frame, new Vector2(x, footerY), w, SaltColor(COLOR_MINER * 0.4f, st), 1f);
    DrawTextLeft(frame, _minerContacts.Count + " UNIT" + (_minerContacts.Count != 1 ? "S" : "") + " ONLINE",
        new Vector2(x, footerY + 3f), SaltColor(COLOR_DIM * 2f, st), 0.40f);
    int miningCount = 0;
    foreach (var m in _minerContacts) if (m.Drilling) miningCount++;
    if (miningCount > 0)
        DrawTextRight(frame, miningCount + " MINING",
            new Vector2(viewport.X + w - margin, footerY + 3f), SaltColor(COLOR_DRILL, st), 0.40f);
    frame.Dispose();
}

// ============================================================
// DISPLAY 4 — DOCK RADAR
// New in v1.7. Tag: [RosRadar]
// ============================================================

// Build cached ring and tick sprite arrays using center-relative positions.
// Sprites are stored with Position as OFFSET from center (not absolute).
// DrawRadarDisplay applies the actual center at draw time.
void BuildRadarGeometry(float radius)
{
    var rings = new List<MySprite>();
    var ticks = new List<MySprite>();

    float[] ringRatios = { 0.25f, 0.50f, 0.75f, 1.00f };
    Color[] ringColors = {
        new Color(COLOR_DANGER.R,   COLOR_DANGER.G,   COLOR_DANGER.B,   50),
        new Color(COLOR_INCOMING.R, COLOR_INCOMING.G, COLOR_INCOMING.B, 45),
        new Color(COLOR_APPROACH.R, COLOR_APPROACH.G, COLOR_APPROACH.B, 40),
        new Color(COLOR_DIM.R,      COLOR_DIM.G,      COLOR_DIM.B,      60)
    };

    for (int ri = 0; ri < ringRatios.Length; ri++)
    {
        float r = radius * ringRatios[ri];
        for (int i = 0; i < 36; i++)
        {
            int deg1 = (i * 10) % 360;
            int deg2 = ((i + 1) * 10) % 360;
            // Positions are OFFSETS from center (center = 0,0 here)
            float x1 = _sinTable[deg1] * r;
            float y1 = -_cosTable[deg1] * r;
            float x2 = _sinTable[deg2] * r;
            float y2 = -_cosTable[deg2] * r;
            float midX = (x1 + x2) * 0.5f;
            float midY = (y1 + y2) * 0.5f;
            float len  = (float)Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            float ang  = (float)Math.Atan2(y2 - y1, x2 - x1);
            rings.Add(new MySprite {
                Type = SpriteType.TEXTURE, Data = "SquareSimple",
                Position = new Vector2(midX, midY),
                Size = new Vector2(len + 1f, 1.5f),
                Color = ringColors[ri],
                RotationOrScale = ang,
                Alignment = TextAlignment.CENTER
            });
        }
    }

    string[] allLabels = { "N","030","060","E","120","150","S","210","240","W","300","330" };
    for (int i = 0; i < 12; i++)
    {
        int deg   = i * 30;
        bool isCrd = (deg % 90 == 0);
        float inner = radius * 1.03f;
        float outer = radius * (isCrd ? 1.14f : 1.10f);
        float tx1 = _sinTable[deg] * inner;
        float ty1 = -_cosTable[deg] * inner;
        float tx2 = _sinTable[deg] * outer;
        float ty2 = -_cosTable[deg] * outer;
        float midX = (tx1 + tx2) * 0.5f;
        float midY = (ty1 + ty2) * 0.5f;
        float len  = (float)Math.Sqrt((tx2 - tx1) * (tx2 - tx1) + (ty2 - ty1) * (ty2 - ty1));
        float ang  = (float)Math.Atan2(ty2 - ty1, tx2 - tx1);
        Color tickCol = isCrd
            ? new Color(COLOR_HEADER.R, COLOR_HEADER.G, COLOR_HEADER.B, 180)
            : new Color(COLOR_DIM.R,   COLOR_DIM.G,    COLOR_DIM.B,    120);
        ticks.Add(new MySprite {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(midX, midY),
            Size = new Vector2(len, isCrd ? 2f : 1f),
            Color = tickCol, RotationOrScale = ang, Alignment = TextAlignment.CENTER
        });
        float labelR = radius * (isCrd ? 1.22f : 1.18f);
        float lx = _sinTable[deg] * labelR;
        float ly = -_cosTable[deg] * labelR - 5f;
        ticks.Add(new MySprite {
            Type = SpriteType.TEXT, Data = allLabels[i],
            Position = new Vector2(lx, ly),
            RotationOrScale = isCrd ? 0.40f : 0.30f,
            Color = tickCol, Alignment = TextAlignment.CENTER, FontId = "Monospace"
        });
    }

    _radarRings    = rings.ToArray();
    _radarTicks    = ticks.ToArray();
    _radarGeomValid = true;
}

void DrawRadarDisplay(IMyTextPanel lcd)
{
    RectangleF viewport = new RectangleF(
        (lcd.TextureSize - lcd.SurfaceSize) / 2f, lcd.SurfaceSize);
    var   frame  = lcd.DrawFrame();
    float margin = 8f;
    float x      = viewport.X + margin;
    float w      = viewport.Width - margin * 2f;
    float maxY   = viewport.Y + viewport.Height - margin;
    int   st     = _renderTick;

    // Background
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size,
        Color = COLOR_BG, Alignment = TextAlignment.CENTER });

    // Header
    float y = DrawROSHeader(frame, x, viewport.Y + margin, w, viewport.X, margin, MOD_RADAR, COLOR_HEADER);

    // Radar area: square centered in remaining space, with room for legend at bottom
    float legendH  = 18f;
    float radarAreaH = maxY - y - legendH - 4f;
    float radarAreaW = w;
    float diameter = Math.Min(radarAreaW, radarAreaH);
    float radius   = diameter * 0.42f;
    float cx       = viewport.X + viewport.Width * 0.5f;
    float cy       = y + radarAreaH * 0.5f;
    var   center   = new Vector2(cx, cy);

    // Rebuild cached geometry if LCD size or radius changed
    if (!_radarGeomValid
        || Vector2.DistanceSquared(_lastRadarSize, viewport.Size) > 1f
        || Math.Abs(_lastRadarRadius - radius) > 1f)
    {
        _lastRadarSize   = viewport.Size;
        _lastRadarCenter = center;
        _lastRadarRadius = radius;
        BuildRadarGeometry(radius);
    }

    // Draw outer circle border
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Circle",
        Position = center, Size = new Vector2(radius * 2f + 3f, radius * 2f + 3f),
        Color = new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, 80),
        Alignment = TextAlignment.CENTER });
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Circle",
        Position = center, Size = new Vector2(radius * 2f, radius * 2f),
        Color = new Color(4, 10, 6, 255),
        Alignment = TextAlignment.CENTER });

    // Draw cached rings — construct fresh sprite with center-offset position
    if (_radarRings != null)
        foreach (var s in _radarRings)
        {
            Vector2 sPos = s.Position.HasValue ? s.Position.Value : Vector2.Zero;
            frame.Add(new MySprite {
                Type = s.Type, Data = s.Data,
                Position = new Vector2(center.X + sPos.X, center.Y + sPos.Y),
                Size = s.Size, Color = s.Color,
                RotationOrScale = s.RotationOrScale,
                Alignment = s.Alignment
            });
        }

    // Crosshairs — faint N/S and E/W lines through center
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = center, Size = new Vector2(radius * 2f, 0.8f),
        Color = new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, 40),
        Alignment = TextAlignment.CENTER });
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = center, Size = new Vector2(0.8f, radius * 2f),
        Color = new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, 40),
        Alignment = TextAlignment.CENTER });

    // Sweep line — driven by _spinnerAngle (0→2π)
    // Convert radians to integer degree index for trig table lookup
    int sweepDeg = (int)(_spinnerAngle * 180.0 / Math.PI) % 360;
    if (sweepDeg < 0) sweepDeg += 360;
    float sweepX = cx + _sinTable[sweepDeg] * radius;
    float sweepY = cy - _cosTable[sweepDeg] * radius;

    // Trailing sweep lines with decreasing alpha
    int[] trailOffsets = { 8, 16, 24 };
    byte[] trailAlphas = { 50, 30, 15 };
    for (int ti = 0; ti < 3; ti++)
    {
        int trailDeg = ((sweepDeg - trailOffsets[ti]) + 360) % 360;
        float trailX = cx + _sinTable[trailDeg] * radius;
        float trailY = cy - _cosTable[trailDeg] * radius;
        float tmx = (cx + trailX) * 0.5f, tmy = (cy + trailY) * 0.5f;
        float tlen = (float)Math.Sqrt((trailX - cx) * (trailX - cx) + (trailY - cy) * (trailY - cy));
        float tang = (float)Math.Atan2(trailY - cy, trailX - cx);
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(tmx, tmy), Size = new Vector2(tlen, 1f),
            Color = new Color(COLOR_LOCKED.R, COLOR_LOCKED.G, COLOR_LOCKED.B, trailAlphas[ti]),
            RotationOrScale = tang, Alignment = TextAlignment.CENTER });
    }

    // Main sweep line
    {
        float smx = (cx + sweepX) * 0.5f, smy = (cy + sweepY) * 0.5f;
        float slen = (float)Math.Sqrt((sweepX - cx) * (sweepX - cx) + (sweepY - cy) * (sweepY - cy));
        float sang = (float)Math.Atan2(sweepY - cy, sweepX - cx);
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(smx, smy), Size = new Vector2(slen, 1.5f),
            Color = new Color(COLOR_LOCKED.R, COLOR_LOCKED.G, COLOR_LOCKED.B, 130),
            RotationOrScale = sang, Alignment = TextAlignment.CENTER });
    }

    // Draw cached ticks — construct fresh sprite with center-offset position
    if (_radarTicks != null)
        foreach (var s in _radarTicks)
        {
            Vector2 sPos = s.Position.HasValue ? s.Position.Value : Vector2.Zero;
            frame.Add(new MySprite {
                Type = s.Type, Data = s.Data,
                Position = new Vector2(center.X + sPos.X, center.Y + sPos.Y),
                Size = s.Size, Color = s.Color,
                RotationOrScale = s.RotationOrScale,
                Alignment = s.Alignment, FontId = s.FontId
            });
        }

    // Base position marker — center dot
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Circle",
        Position = center, Size = new Vector2(7f, 7f),
        Color = SaltColor(COLOR_HEADER, st), Alignment = TextAlignment.CENTER });

    // Contact blips
    Vector3D basePos = Me.GetPosition();

    foreach (var contact in _approachList)
    {
        float alpha = contact.ComputeAlpha();
        if (alpha <= 0f) continue;

        // Use raw world offset — no PB orientation transform
        // Radar N = world -Z, E = world +X (SE world axes, consistent regardless of base orientation)
        Vector3D offset = contact.WorldPos - basePos;
        double realDist = offset.Length();
        if (realDist < 0.01) continue;

        // World X = east/west, World Z = north/south (negated so -Z = up on radar = north)
        double horizX =  offset.X; // east = right
        double horizZ = -offset.Z; // north = up (negate because SE -Z is forward/north)
        double horizDist = Math.Sqrt(horizX * horizX + horizZ * horizZ);

        float normX, normZ;
        if (horizDist < 0.01)
        {
            // Contact is directly above/below — place at center
            normX = 0f; normZ = 0f;
        }
        else
        {
            // Scale by real 3D distance so radar distance matches FC() distance
            double activeRange = RADAR_ZOOM_LEVELS[_radarZoomIndex];
            float scale = (float)(realDist / activeRange);
            float dirX  = (float)(horizX / horizDist);
            float dirZ  = (float)(horizZ / horizDist);
            normX = dirX * scale;
            normZ = dirZ * scale;
        }

        // Clamp to radar edge if beyond range
        float magnitude = (float)Math.Sqrt(normX * normX + normZ * normZ);
        if (magnitude > 1.0f)
        {
            normX /= magnitude;
            normZ /= magnitude;
        }

        float blipX = cx + normX * radius;
        float blipY = cy - normZ * radius;

        // Choose color and shape based on contact type + elevation
        Color blipColor;
        string blipShape;
        float  blipSize = 9f;
        float  blipRot  = 0f;

        if (contact.IsMinerBroadcast)
        {
            blipColor = COLOR_MINER;
            blipShape = contact.Elevation > 0 ? "Triangle"
                      : contact.Elevation < 0 ? "Triangle" : "Circle";
            if (contact.Elevation < 0) blipRot = (float)Math.PI;
        }
        else
        {
            blipColor = COLOR_APPROACH;
            blipShape = contact.Type == "LARGE" ? "SquareSimple"
                      : contact.Elevation > 0   ? "Triangle"
                      : contact.Elevation < 0   ? "Triangle" : "Circle";
            if (contact.Elevation < 0) blipRot = (float)Math.PI;
        }

        // Override color for elevation
        if (contact.Elevation > 0)      blipColor = COLOR_ABOVE;
        else if (contact.Elevation < 0) blipColor = COLOR_BELOW;

        // Apply fade alpha
        blipColor = new Color(blipColor.R, blipColor.G, blipColor.B,
            (byte)(blipColor.A * alpha));

        // Shadow dot
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = blipShape,
            Position = new Vector2(blipX + 1f, blipY + 1f),
            Size = new Vector2(blipSize + 2f, blipSize + 2f),
            Color = new Color(0, 0, 0, (byte)(80 * alpha)),
            RotationOrScale = blipRot, Alignment = TextAlignment.CENTER });

        // Blip
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = blipShape,
            Position = new Vector2(blipX, blipY),
            Size = new Vector2(blipSize, blipSize),
            Color = blipColor, RotationOrScale = blipRot,
            Alignment = TextAlignment.CENTER });

        // Short name label — first 5 chars, offset right of blip
        string shortName = contact.Name.Length > 5 ? contact.Name.Substring(0, 5) : contact.Name;
        Color  labelCol  = new Color(blipColor.R, blipColor.G, blipColor.B,
            (byte)(Math.Min(blipColor.A + 30, 255) * alpha));
        frame.Add(new MySprite { Type = SpriteType.TEXT, Data = shortName,
            Position = new Vector2(blipX + blipSize * 0.7f, blipY - 4f),
            RotationOrScale = 0.28f, Color = labelCol,
            Alignment = TextAlignment.LEFT, FontId = "Monospace" });
    }

    // Range labels on ring edges — dynamic based on current zoom level
    double zoomRange = RADAR_ZOOM_LEVELS[_radarZoomIndex];
    string[] rangeLbls = {
        FC(zoomRange * 0.25) + (zoomRange * 0.25 >= 1000 ? "" : "m"),
        FC(zoomRange * 0.50) + (zoomRange * 0.50 >= 1000 ? "" : "m"),
        FC(zoomRange * 0.75) + (zoomRange * 0.75 >= 1000 ? "" : "m"),
        FC(zoomRange)        + (zoomRange        >= 1000 ? "" : "m")
    };
    for (int ri = 0; ri < 4; ri++)
    {
        float r = radius * ringRatios_local(ri);
        frame.Add(new MySprite { Type = SpriteType.TEXT, Data = rangeLbls[ri],
            Position = new Vector2(cx + r + 2f, cy - 6f),
            RotationOrScale = 0.26f,
            Color = new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, 100),
            Alignment = TextAlignment.LEFT, FontId = "Monospace" });
    }

    // Legend strip at bottom
    float ly = maxY - legendH + 3f;
    DrawHLine(frame, new Vector2(x, ly - 2f), w, SaltColor(COLOR_DIM * 0.5f, st), 1f);

    // ○ miner
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Circle",
        Position = new Vector2(x + 6f, ly + 6f), Size = new Vector2(8f, 8f),
        Color = new Color(COLOR_MINER.R, COLOR_MINER.G, COLOR_MINER.B, 160),
        Alignment = TextAlignment.CENTER });
    DrawTextLeft(frame, "miner", new Vector2(x + 13f, ly),
        new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, 160), 0.28f);

    // △ above
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Triangle",
        Position = new Vector2(x + 55f, ly + 6f), Size = new Vector2(8f, 8f),
        Color = new Color(COLOR_ABOVE.R, COLOR_ABOVE.G, COLOR_ABOVE.B, 160),
        Alignment = TextAlignment.CENTER });
    DrawTextLeft(frame, "above", new Vector2(x + 62f, ly),
        new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, 160), 0.28f);

    // ▽ below
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Triangle",
        Position = new Vector2(x + 108f, ly + 6f), Size = new Vector2(8f, 8f),
        Color = new Color(COLOR_BELOW.R, COLOR_BELOW.G, COLOR_BELOW.B, 160),
        RotationOrScale = (float)Math.PI, Alignment = TextAlignment.CENTER });
    DrawTextLeft(frame, "below", new Vector2(x + 115f, ly),
        new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, 160), 0.28f);

    // □ large grid
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(x + 161f, ly + 6f), Size = new Vector2(7f, 7f),
        Color = new Color(COLOR_APPROACH.R, COLOR_APPROACH.G, COLOR_APPROACH.B, 160),
        Alignment = TextAlignment.CENTER });
    DrawTextLeft(frame, "large", new Vector2(x + 168f, ly),
        new Color(COLOR_DIM.R, COLOR_DIM.G, COLOR_DIM.B, 160), 0.28f);

    // Contact count + zoom level right side
    string zoomStr = "RNG " + FC(RADAR_ZOOM_LEVELS[_radarZoomIndex]) + (RADAR_ZOOM_LEVELS[_radarZoomIndex] >= 1000 ? "" : "m");
    string countStr = _approachList.Count + " contact" + (_approachList.Count != 1 ? "s" : "");
    DrawTextRight(frame, zoomStr + "  " + countStr,
        new Vector2(viewport.X + w - margin, ly),
        SaltColor(COLOR_DIM * 2f, st), 0.30f);

    frame.Dispose();
}

// Helper — ring ratio lookup (avoids array in method that would allocate)
float ringRatios_local(int i)
{
    switch (i) { case 0: return 0.25f; case 1: return 0.50f; case 2: return 0.75f; default: return 1.00f; }
}

// ============================================================
// PB Screen
// ============================================================

void DrawPBScreen(List<DockRow> rows)
{
    var pb = Me as IMyTextSurfaceProvider;
    if (pb == null || pb.SurfaceCount == 0) return;
    var surface = pb.GetSurface(0);
    if (surface == null) return;

    if (!_initialisedLCDs.Contains(Me.EntityId))
    {
        surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;
        surface.Script = ""; surface.BackgroundColor = COLOR_BG;
        surface.ScriptBackgroundColor = COLOR_BG; surface.ScriptForegroundColor = COLOR_HEADER;
        _initialisedLCDs.Add(Me.EntityId);
    }

    RectangleF viewport = new RectangleF(
        (surface.TextureSize - surface.SurfaceSize) / 2f, surface.SurfaceSize);
    var   frame  = surface.DrawFrame();
    float margin = 8f;
    float x      = viewport.X + margin;
    float y      = viewport.Y + margin;
    float w      = viewport.Width - margin * 2f;
    float maxY   = viewport.Y + viewport.Height - margin;
    int   st     = _renderTick;

    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size, Color = COLOR_BG, Alignment = TextAlignment.CENTER });

    DrawTextLeft(frame, ROS_TITLE, new Vector2(x, y), SaltColor(COLOR_HEADER, st), 0.56f);
    DrawTextRight(frame, ROS_VERSION,
        new Vector2(viewport.X + w - margin, y), SaltColor(COLOR_DIM * 2f, st), 0.38f);
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Screen_LoadingBar",
        Position = new Vector2(viewport.X + w - margin - 22f, y + 8f),
        Size = new Vector2(12f, 12f), Color = SaltColor(COLOR_HEADER * 0.7f, st),
        Alignment = TextAlignment.CENTER, RotationOrScale = _spinnerAngle });

    y += 16f;
    DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_HEADER * 0.5f, st), 1f); y += 3f;

    if (y + 12f <= maxY)
    {
        DrawTextLeft(frame, "UP " + FormatElapsed(_totalElapsedSec),
            new Vector2(x, y), SaltColor(COLOR_DIM * 2f, st), 0.34f);
        y += 13f;
        DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM, st), 1f); y += 3f;
    }

    if (_approachList.Count > 0 && y + 14f <= maxY)
    {
        int mc = 0;
        foreach (var c in _approachList) if (c.IsMinerBroadcast) mc++;
        Color ac = mc > 0 ? COLOR_MINER : COLOR_INCOMING;
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Danger",
            Position = new Vector2(x + 4f, y + 5f), Size = new Vector2(9f, 9f),
            Color = SaltColor(ac, st), Alignment = TextAlignment.CENTER });
        DrawTextLeft(frame, mc > 0 ? "MINERS  " + mc : "CONTACT " + _approachList.Count,
            new Vector2(x + 12f, y), SaltColor(ac, st), 0.38f);
        y += 14f;
        DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM, st), 1f); y += 3f;
    }

    int locked = 0, idle = 0, offline = 0, ready = 0;
    foreach (var r in rows)
    {
        switch (r.State)
        {
            case "LOCKED": locked++;  break; case "READY": ready++;  break;
            case "IDLE":   idle++;    break; default:       offline++; break;
        }
    }

    string[] labels = { "LOCKED", "READY", "IDLE", "OFFLINE" };
    int[]    counts = { locked, ready, idle, offline };
    Color[]  colors = { COLOR_LOCKED, COLOR_READY, COLOR_IDLE, COLOR_OFFLINE };

    for (int i = 0; i < 4; i++)
    {
        if (y + 14f > maxY) break;
        bool isDanger = labels[i] == "OFFLINE" && counts[i] > 0;
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = isDanger ? "Danger" : "Circle",
            Position = new Vector2(x + 4f, y + 5f),
            Size = new Vector2(isDanger ? 9f : 6f, isDanger ? 9f : 6f),
            Color = SaltColor(isDanger ? COLOR_DANGER : colors[i], st),
            Alignment = TextAlignment.CENTER });
        DrawTextLeft(frame, labels[i].PadRight(8) + counts[i], new Vector2(x + 12f, y),
            SaltColor(isDanger ? COLOR_DANGER : colors[i], st), 0.38f);
        y += 14f;
    }

    if (_minerContacts.Count > 0 && y + 14f <= maxY)
    {
        DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM, st), 1f); y += 3f;
        DrawDot(frame, new Vector2(x + 4f, y + 5f), SaltColor(COLOR_MINER, st));
        DrawTextLeft(frame, "FLEET  ".PadRight(8) + _minerContacts.Count,
            new Vector2(x + 12f, y), SaltColor(COLOR_MINER, st), 0.38f);
        y += 14f;
    }

    float lc = GetLocalCharge();
    if (lc >= 0f && y + 18f <= maxY)
    {
        DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM, st), 1f); y += 3f;
        Color pc = lc >= 1f ? COLOR_LOCKED : lc >= 0.5f ? COLOR_CHARGE : COLOR_OFFLINE;
        frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "IconEnergy",
            Position = new Vector2(x + 4f, y + 5f), Size = new Vector2(9f, 9f),
            Color = SaltColor(pc, st), Alignment = TextAlignment.CENTER });
        DrawTextLeft(frame, "BASE PWR", new Vector2(x + 12f, y), SaltColor(COLOR_DIM * 2f, st), 0.36f);
        DrawTextRight(frame, ((int)(lc * 100f)) + "%",
            new Vector2(viewport.X + w - margin, y), SaltColor(pc, st), 0.36f);
        y += 14f;
        if (y + 5f <= maxY)
        {
            DrawHLine(frame, new Vector2(x, y), w, SaltColor(COLOR_DIM * 0.8f, st), 5f);
            DrawHLine(frame, new Vector2(x, y), w * lc, SaltColor(pc, st), 5f);
        }
    }
    frame.Dispose();
}

// ============================================================
// Draw helpers
// ============================================================

void DrawTextLeft(MySpriteDrawFrame frame, string text, Vector2 pos, Color color, float scale)
{
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = text, Position = pos,
        RotationOrScale = scale, Color = color, Alignment = TextAlignment.LEFT, FontId = "Monospace" });
}

void DrawTextRight(MySpriteDrawFrame frame, string text, Vector2 pos, Color color, float scale)
{
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = text, Position = pos,
        RotationOrScale = scale, Color = color, Alignment = TextAlignment.RIGHT, FontId = "Monospace" });
}

void DrawHLine(MySpriteDrawFrame frame, Vector2 pos, float width, Color color, float height = 1.5f)
{
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = pos, Size = new Vector2(width, height),
        Color = color, Alignment = TextAlignment.LEFT });
}

void DrawDot(MySpriteDrawFrame frame, Vector2 pos, Color color)
{
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "Circle",
        Position = pos, Size = new Vector2(7f, 7f),
        Color = color, Alignment = TextAlignment.CENTER });
}

void DrawBadge(MySpriteDrawFrame frame, Vector2 pos, float width, float height, string label, Color color)
{
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = pos, Size = new Vector2(width, height),
        Color = new Color(color.R, color.G, color.B, (byte)(color.A / 5)),
        Alignment = TextAlignment.LEFT });
    frame.Add(new MySprite { Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = pos, Size = new Vector2(2f, height),
        Color = color, Alignment = TextAlignment.LEFT });
    frame.Add(new MySprite { Type = SpriteType.TEXT, Data = label,
        Position = pos + new Vector2(4f, 1f), RotationOrScale = 0.36f,
        Color = color, Alignment = TextAlignment.LEFT, FontId = "Monospace" });
}