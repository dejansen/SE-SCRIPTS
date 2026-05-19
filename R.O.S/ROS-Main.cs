// ============================================================
// R.O.S — Rev Operating System
// Module:  Dock Control / Proximity Scanner / Fleet Operations
// Author:  RevGamer (Simba "Davy" Jones)
// Version: 1.4
//
// FIXES v1.4:
//   - StripTags on miner names so [RGH] GroundHog matches correctly
//   - CARGO / BATTERY full labels in Fleet Operations
//   - Larger font across all three displays
//   - Full text no truncation on Dock Control
//   - ContentType/Script set once not every tick — fixes flicker
//
// BASE SETUP:
//   Connectors    → [DOCK] in name
//   LCD Docking   → [DockStatus]
//   LCD Approach  → [DockMap]
//   LCD Miners    → [MinerStatus]
//   Cameras       → [DockCam] (multiple ok)
//   Sound block   → [DockAlert]
//   Lights        → [DockLight 1] [DockLight 2] etc
// ============================================================

const string CONNECTOR_TAG  = "[DOCK]";
const string LCD_TAG        = "[DockStatus]";
const string MAP_LCD_TAG    = "[DockMap]";
const string MINER_LCD_TAG  = "[MinerStatus]";
const string CAM_TAG        = "[DockCam]";
const string ALERT_TAG      = "[DockAlert]";
const string LIGHT_TAG      = "[DockLight]";
const string MINER_CHANNEL  = "DOCK_APPROACH";
const string REPLY_CHANNEL  = "DOCK_REPLY";
const double CAM_RANGE      = 1000.0;
const double APPROACH_RANGE = 1000.0;
const float  LIGHT_SEQ_RATE = 0.25f;
const float  BOOT_TIME      = 3.0f;

const string ROS_TITLE     = "R.O.S";
const string ROS_VERSION   = "v1.4";
const string ROS_AUTHOR    = "RevGamer (Simba \"Davy\" Jones)";
const string MOD_DOCK      = "DOCK CONTROL";
const string MOD_PROXIMITY = "PROXIMITY SCANNER";
const string MOD_FLEET     = "FLEET OPERATIONS";

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

List<IMyShipConnector>  connectors  = new List<IMyShipConnector>();
List<IMyTextPanel>      lcds        = new List<IMyTextPanel>();
List<IMyTextPanel>      mapLcds     = new List<IMyTextPanel>();
List<IMyTextPanel>      minerLcds   = new List<IMyTextPanel>();
List<IMyBatteryBlock>   batteries   = new List<IMyBatteryBlock>();
List<IMySensorBlock>    sensors     = new List<IMySensorBlock>();
List<IMySoundBlock>     alertSounds = new List<IMySoundBlock>();
List<IMyCameraBlock>    cameras     = new List<IMyCameraBlock>();
List<IMyLightingBlock>  dockLights  = new List<IMyLightingBlock>();

float    _spinnerAngle  = 0f;
bool     _alertPlayed   = false;
bool     _lightsActive  = false;
int      _lightSeqIndex = 0;
float    _lightSeqTimer = 0f;
DateTime _lastUpdate    = DateTime.Now;

// Boot state
bool   _booting     = true;
float  _bootTimer   = 0f;
string _bootMessage = BOOT_MESSAGES[0];

// FIX: track which LCDs have been initialised to avoid setting Script/ContentType every tick
HashSet<long> _initialisedLCDs = new HashSet<long>();

IMyBroadcastListener _minerListener;

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

    public bool IsStale()
        => (DateTime.Now - LastSeen).TotalSeconds > 15;

    public double Distance(Vector3D basePos)
        => Vector3D.Distance(basePos, Position);

    public string DistanceString(Vector3D basePos)
    {
        double d = Distance(basePos);
        return d >= 1000 ? ((int)(d / 1000.0)) + "km" : (int)d + "m";
    }

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

    public bool IsStale()
    {
        double timeout = IsMinerBroadcast ? 15.0 : 10.0;
        return (DateTime.Now - DetectedAt).TotalSeconds > timeout;
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

    public bool IsStale()
        => (DateTime.Now - DetectedAt).TotalSeconds > 5;
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

    public string DockTimeString()
    {
        if (!DockedAt.HasValue) return "";
        double secs = (DateTime.Now - DockedAt.Value).TotalSeconds;
        if (secs < 60)   return (int)secs + "s";
        if (secs < 3600) return (int)(secs / 60) + "m";
        return (int)(secs / 3600) + "h" + (int)((secs % 3600) / 60) + "m";
    }

    public DockRow(string name, string state, string vessel,
                   Color color, float chargeRatio, float batteryRatio,
                   BayContact sensor, DateTime? dockedAt, MinerContact minerData)
    {
        Name = name; State = state; Vessel = vessel;
        StateColor = color; ChargeRatio = chargeRatio;
        BatteryRatio = batteryRatio; SensorContact = sensor;
        DockedAt = dockedAt; MinerData = minerData;
    }
}

List<MinerContact>    _minerContacts = new List<MinerContact>();
List<ApproachContact> _approachList  = new List<ApproachContact>();

Dictionary<string, BayContact> _bayContacts   = new Dictionary<string, BayContact>();
Dictionary<string, DateTime>   _dockTimes     = new Dictionary<string, DateTime>();
Dictionary<string, bool>       _prevLocked    = new Dictionary<string, bool>();
Dictionary<string, string>     _prevConnState = new Dictionary<string, string>();

public Program()
{
    _minerListener = IGC.RegisterBroadcastListener(MINER_CHANNEL);
    _minerListener.SetMessageCallback();
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    _lastUpdate  = DateTime.Now;
    _booting     = true;
    _bootTimer   = 0f;
    _bootMessage = BOOT_MESSAGES[0];
    Echo("R.O.S " + ROS_VERSION + " — Booting...");
}

public void Save()
{
    Storage = ROS_VERSION + "|" + _lightSeqIndex.ToString();
}

public void Main(string argument, UpdateType updateSource)
{
    double delta = (DateTime.Now - _lastUpdate).TotalSeconds;
    _lastUpdate  = DateTime.Now;

    if (Runtime.UpdateFrequency != UpdateFrequency.Update10)
        Runtime.UpdateFrequency = UpdateFrequency.Update10;

    _spinnerAngle += 0.4f;
    if (_spinnerAngle > (float)(Math.PI * 2.0))
        _spinnerAngle -= (float)(Math.PI * 2.0);

    // Fetch LCD lists every tick (needed for boot draw)
    lcds.Clear(); mapLcds.Clear(); minerLcds.Clear();
    GridTerminalSystem.GetBlocksOfType(lcds, b =>
        b.CustomName.Contains(LCD_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(mapLcds, b =>
        b.CustomName.Contains(MAP_LCD_TAG) && b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(minerLcds, b =>
        b.CustomName.Contains(MINER_LCD_TAG) && b.IsSameConstructAs(Me));

    // Initialise LCDs once (set ContentType/Script) — prevents flicker
    InitLCDs();

    // Boot sequence
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
        DrawPBBootScreen();

        if (_bootTimer >= BOOT_TIME)
        {
            _booting = false;
            Echo("R.O.S " + ROS_VERSION + " — Online");
        }
        return;
    }

    // FIX: poll every tick — IGC callback alone is unreliable in SE
    UpdateMinerContacts();

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

    UpdateMinerContacts();
    UpdateSensorContacts();
    UpdateCameraContacts();
    MergeContactsToApproachList();
    UpdateDockTimes();
    UpdateSequentialLights((float)delta);
    UpdateAlert();
    BroadcastDistanceReplies();

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
        DrawPBScreen(rows);
    }

    Echo("R.O.S " + ROS_VERSION + " — " + ROS_AUTHOR);
    Echo("Connectors: " + connectors.Count);
    Echo("Cameras:    " + cameras.Count);
    Echo("Miners:     " + _minerContacts.Count);
    Echo("Contacts:   " + _approachList.Count);
    if (connectorChanged) Echo(">> CONNECTOR CHANGED — INSTANT REDRAW");
}

// FIX: only set ContentType and Script once per LCD to prevent flicker
void InitLCDs()
{
    foreach (var lcd in lcds)      InitLCD(lcd, COLOR_HEADER);
    foreach (var lcd in mapLcds)   InitLCD(lcd, COLOR_HEADER);
    foreach (var lcd in minerLcds) InitLCD(lcd, COLOR_MINER);
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
// Boot screen
// ============================================================

void DrawBootScreen(IMyTextPanel lcd, string moduleName)
{
    RectangleF viewport = new RectangleF(
        (lcd.TextureSize - lcd.SurfaceSize) / 2f, lcd.SurfaceSize);

    var   frame  = lcd.DrawFrame();
    float cx     = viewport.Center.X;
    float cy     = viewport.Center.Y;
    float w      = viewport.Width;
    float margin = 20f;
    float prog   = Math.Min(1f, _bootTimer / BOOT_TIME);

    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size,
        Color = COLOR_BG, Alignment = TextAlignment.CENTER
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "Grid",
        Position = viewport.Center, Size = viewport.Size,
        Color = new Color(20, 40, 25), Alignment = TextAlignment.CENTER
    });

    // Title
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = ROS_TITLE,
        Position = new Vector2(cx, cy - 55f),
        RotationOrScale = 1.6f, Color = COLOR_HEADER,
        Alignment = TextAlignment.CENTER, FontId = "Monospace"
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = moduleName,
        Position = new Vector2(cx, cy - 26f),
        RotationOrScale = 0.42f, Color = COLOR_HEADER * 0.7f,
        Alignment = TextAlignment.CENTER, FontId = "Monospace"
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = ROS_VERSION,
        Position = new Vector2(cx, cy - 10f),
        RotationOrScale = 0.38f, Color = COLOR_DIM * 2f,
        Alignment = TextAlignment.CENTER, FontId = "Monospace"
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(cx, cy + 6f),
        Size = new Vector2(w * 0.6f, 1f),
        Color = COLOR_HEADER * 0.4f, Alignment = TextAlignment.CENTER
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = ROS_AUTHOR,
        Position = new Vector2(cx, cy + 12f),
        RotationOrScale = 0.30f, Color = COLOR_DIM * 1.5f,
        Alignment = TextAlignment.CENTER, FontId = "Monospace"
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = _bootMessage,
        Position = new Vector2(cx, cy + 36f),
        RotationOrScale = 0.36f, Color = COLOR_HEADER * 0.8f,
        Alignment = TextAlignment.CENTER, FontId = "Monospace"
    });

    // Progress bar
    float barX = viewport.X + margin * 2f;
    float barY = cy + 54f;
    float barW = w - margin * 4f;
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(barX + barW * 0.5f, barY),
        Size = new Vector2(barW, 4f),
        Color = COLOR_DIM, Alignment = TextAlignment.CENTER
    });
    if (prog > 0f)
    {
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(barX + (barW * prog) * 0.5f, barY),
            Size = new Vector2(barW * prog, 4f),
            Color = COLOR_HEADER, Alignment = TextAlignment.CENTER
        });
    }
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = ((int)(prog * 100f)) + "%",
        Position = new Vector2(barX + barW, barY - 8f),
        RotationOrScale = 0.32f, Color = COLOR_DIM * 2f,
        Alignment = TextAlignment.RIGHT, FontId = "Monospace"
    });
    frame.Add(new MySprite()
    {
        Type            = SpriteType.TEXTURE, Data = "Screen_LoadingBar",
        Position        = new Vector2(cx, cy + 72f),
        Size            = new Vector2(18f, 18f),
        Color           = COLOR_HEADER * 0.5f,
        Alignment       = TextAlignment.CENTER,
        RotationOrScale = _spinnerAngle
    });

    frame.Dispose();
}

void DrawPBBootScreen()
{
    var pb = Me as IMyTextSurfaceProvider;
    if (pb == null || pb.SurfaceCount == 0) return;
    var surface = pb.GetSurface(0);
    if (surface == null) return;
    surface.ContentType           = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;
    surface.Script                = "";
    surface.BackgroundColor       = COLOR_BG;
    surface.ScriptBackgroundColor = COLOR_BG;
    surface.ScriptForegroundColor = COLOR_HEADER;

    RectangleF viewport = new RectangleF(
        (surface.TextureSize - surface.SurfaceSize) / 2f, surface.SurfaceSize);

    var   frame  = surface.DrawFrame();
    float cx     = viewport.Center.X;
    float cy     = viewport.Center.Y;
    float prog   = Math.Min(1f, _bootTimer / BOOT_TIME);

    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size,
        Color = COLOR_BG, Alignment = TextAlignment.CENTER
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = ROS_TITLE,
        Position = new Vector2(cx, cy - 20f),
        RotationOrScale = 0.8f, Color = COLOR_HEADER,
        Alignment = TextAlignment.CENTER, FontId = "Monospace"
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = ROS_VERSION + "  BOOTING",
        Position = new Vector2(cx, cy),
        RotationOrScale = 0.34f, Color = COLOR_DIM * 2f,
        Alignment = TextAlignment.CENTER, FontId = "Monospace"
    });

    float barX = viewport.X + 10f;
    float barW = viewport.Width - 20f;
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(barX + barW * 0.5f, cy + 14f),
        Size = new Vector2(barW, 3f),
        Color = COLOR_DIM, Alignment = TextAlignment.CENTER
    });
    if (prog > 0f)
    {
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(barX + (barW * prog) * 0.5f, cy + 14f),
            Size = new Vector2(barW * prog, 3f),
            Color = COLOR_HEADER, Alignment = TextAlignment.CENTER
        });
    }
    frame.Dispose();
}

// ============================================================
// Sequential lights
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
        string name      = StripTags(conn.CustomName);
        bool   locked    = conn.Status == MyShipConnectorStatus.Connected;
        bool   wasLocked = false;
        _prevLocked.TryGetValue(name, out wasLocked);
        if (locked && !wasLocked)  _dockTimes[name] = DateTime.Now;
        if (!locked && wasLocked)  _dockTimes.Remove(name);
        _prevLocked[name] = locked;
    }
}

void UpdateMinerContacts()
{
    _minerContacts.RemoveAll(c => c.IsStale());
    while (_minerListener.HasPendingMessage)
    {
        var    msg   = _minerListener.AcceptMessage();
        string raw   = msg.Data.ToString();
        string[] p   = raw.Split('|');
        if (p.Length < 5) continue;

        // FIX: strip tags from miner grid name so [RGH] GroundHog → GroundHog matches
        string gridName = StripTags(p[0]);
        // FIX: use InvariantCulture for all numeric parsing — locale comma separators break TryParse
        float  speed    = 0f;
        float.TryParse(p[1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out speed);
        double px = 0, py = 0, pz = 0;
        double.TryParse(p[2], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out px);
        double.TryParse(p[3], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out py);
        double.TryParse(p[4], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out pz);

        bool   drilling  = p.Length > 5 && p[5] == "1";
        float  cargoFill = 0f;
        float  fuelLevel = -1f;
        float  battLevel = -1f;
        string statusStr = "TRANSIT";

        if (p.Length > 6) float.TryParse(p[6], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out cargoFill);
        if (p.Length > 7) float.TryParse(p[7], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out fuelLevel);
        if (p.Length > 8) float.TryParse(p[8], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out battLevel);
        if (p.Length > 9) statusStr = p[9];

        Vector3D pos   = new Vector3D(px, py, pz);
        bool     found = false;

        foreach (var c in _minerContacts)
        {
            if (c.Name == gridName)
            {
                c.Speed = speed; c.Position = pos;
                c.LastSeen = DateTime.Now; c.Drilling = drilling;
                c.CargoFill = cargoFill; c.FuelLevel = fuelLevel;
                c.BattLevel = battLevel; c.StatusStr = statusStr;
                found = true; break;
            }
        }
        if (!found)
        {
            _minerContacts.Add(new MinerContact()
            {
                Name = gridName, Speed = speed, Position = pos,
                LastSeen = DateTime.Now, Drilling = drilling,
                CargoFill = cargoFill, FuelLevel = fuelLevel,
                BattLevel = battLevel, StatusStr = statusStr
            });
        }
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
            _bayContacts[tag] = new BayContact()
            {
                Name = entity.Name,
                Distance = Vector3D.Distance(Me.GetPosition(), entity.Position),
                Speed = (float)entity.Velocity.Length(),
                DetectedAt = DateTime.Now
            };
        }
        else
        {
            BayContact ex;
            if (_bayContacts.TryGetValue(tag, out ex))
                if (ex.IsStale()) _bayContacts.Remove(tag);
        }
    }
}

void UpdateCameraContacts()
{
    foreach (var cam in cameras)
    {
        if (!cam.CanScan(CAM_RANGE)) continue;
        cam.EnableRaycast = true;
        MyDetectedEntityInfo hit = cam.Raycast(CAM_RANGE);
        if (hit.IsEmpty()) continue;
        if (hit.Type != MyDetectedEntityType.LargeGrid &&
            hit.Type != MyDetectedEntityType.SmallGrid) continue;

        double dist    = Vector3D.Distance(Me.GetPosition(), hit.Position);
        float  speed   = (float)hit.Velocity.Length();
        string camName = StripTags(cam.CustomName);
        bool   found   = false;

        foreach (var c in _approachList)
        {
            if (c.Name == hit.Name && !c.IsMinerBroadcast)
            {
                c.Distance = dist; c.Speed = speed;
                c.CameraName = camName; c.DetectedAt = DateTime.Now;
                found = true; break;
            }
        }
        if (!found)
        {
            _approachList.Add(new ApproachContact()
            {
                Name = hit.Name, Distance = dist, Speed = speed,
                Type = hit.Type == MyDetectedEntityType.LargeGrid ? "LARGE" : "SMALL",
                CameraName = camName, DetectedAt = DateTime.Now,
                IsMinerBroadcast = false
            });
        }
    }
}

void MergeContactsToApproachList()
{
    _approachList.RemoveAll(c => c.IsStale());
    var dockedNames = new List<string>();
    foreach (var conn in connectors)
    {
        if (conn.Status == MyShipConnectorStatus.Connected
            && conn.OtherConnector != null)
            dockedNames.Add(
                StripTags(conn.OtherConnector.CubeGrid.CustomName).ToLower());
    }

    foreach (var miner in _minerContacts)
    {
        double dist  = miner.Distance(Me.GetPosition());
        bool   found = false;
        foreach (var c in _approachList)
        {
            if (c.Name == miner.Name && c.IsMinerBroadcast)
            {
                c.Distance = dist; c.Speed = miner.Speed;
                c.DetectedAt = miner.LastSeen;
                found = true; break;
            }
        }
        if (!found)
        {
            _approachList.Add(new ApproachContact()
            {
                Name = miner.Name, Distance = dist, Speed = miner.Speed,
                Type = "MINER", CameraName = "IGC",
                DetectedAt = miner.LastSeen, IsMinerBroadcast = true
            });
        }
    }
    _approachList.RemoveAll(c => dockedNames.Contains(c.Name.ToLower()));
    _approachList.RemoveAll(c => c.Distance > APPROACH_RANGE);
    _approachList.Sort((a, b) => a.Distance.CompareTo(b.Distance));
}

void UpdateAlert()
{
    bool has = _approachList.Count > 0;
    if (has && !_alertPlayed)
    {
        foreach (var s in alertSounds) s.Play();
        _alertPlayed = true;
    }
    else if (!has) _alertPlayed = false;
}

// ============================================================
// Data helpers
// ============================================================

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

float GetGridCharge(IMyCubeGrid targetGrid)
{
    float cur = 0f, max = 0f;
    foreach (var bat in batteries)
    {
        if (bat.CubeGrid != targetGrid) continue;
        cur += bat.CurrentStoredPower; max += bat.MaxStoredPower;
    }
    return max <= 0f ? -1f : cur / max;
}

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
    // FIX: compare stripped names
    string lower = StripTags(vesselName).ToLower();
    foreach (var m in _minerContacts)
        if (m.Name.ToLower() == lower) return m;
    return null;
}

bool IsDockedGrid(string vesselName)
{
    string lower = StripTags(vesselName).ToLower();
    foreach (var conn in connectors)
    {
        if (conn.Status == MyShipConnectorStatus.Connected
            && conn.OtherConnector != null)
            if (StripTags(conn.OtherConnector.CubeGrid.CustomName).ToLower() == lower)
                return true;
    }
    return false;
}

List<DockRow> BuildRows()
{
    var rows = new List<DockRow>();
    foreach (var conn in connectors)
    {
        string       name         = StripTags(conn.CustomName);
        string       state;
        string       vessel       = "---";
        Color        color;
        float        chargeRatio  = -1f;
        float        batteryRatio = -1f;
        BayContact   contact      = null;
        _bayContacts.TryGetValue(name, out contact);
        DateTime?    dockedAt     = null;
        MinerContact minerData    = null;

        switch (conn.Status)
        {
            case MyShipConnectorStatus.Connected:
                state = "LOCKED"; color = COLOR_LOCKED;
                if (conn.OtherConnector != null)
                {
                    vessel       = StripTags(conn.OtherConnector.CubeGrid.CustomName);
                    chargeRatio  = GetGridCharge(conn.OtherConnector.CubeGrid);
                    batteryRatio = chargeRatio;
                    minerData    = FindMinerData(vessel);
                }
                DateTime dt;
                if (_dockTimes.TryGetValue(name, out dt)) dockedAt = dt;
                break;
            case MyShipConnectorStatus.Connectable:
                state = "READY"; color = COLOR_READY; break;
            case MyShipConnectorStatus.Unconnected:
                state = conn.Enabled ? "IDLE" : "OFFLINE";
                color = conn.Enabled ? COLOR_IDLE : COLOR_OFFLINE;
                break;
            default:
                state = "OFFLINE"; color = COLOR_OFFLINE; break;
        }

        rows.Add(new DockRow(name, state, vessel, color,
            chargeRatio, batteryRatio, contact, dockedAt, minerData));
    }
    return rows;
}

// ============================================================
// Header helper
// ============================================================

float DrawROSHeader(MySpriteDrawFrame frame,
                    float x, float startY, float w,
                    float viewportX, float margin,
                    string moduleName, Color accentColor)
{
    float y = startY;

    // FIX: increased font sizes for readability
    DrawTextLeft(frame, ROS_TITLE, new Vector2(x, y), COLOR_HEADER, 0.62f);
    DrawTextRight(frame, ROS_VERSION,
        new Vector2(viewportX + w - margin, y), COLOR_DIM * 2f, 0.40f);

    frame.Add(new MySprite()
    {
        Type            = SpriteType.TEXTURE, Data = "Screen_LoadingBar",
        Position        = new Vector2(viewportX + w - margin - 26f, y + 9f),
        Size            = new Vector2(14f, 14f), Color = accentColor * 0.7f,
        Alignment       = TextAlignment.CENTER,
        RotationOrScale = _spinnerAngle
    });

    y += 16f;
    DrawTextLeft(frame, "  " + moduleName,
        new Vector2(x, y), accentColor, 0.44f);
    y += 14f;
    DrawTextLeft(frame, "  " + ROS_AUTHOR,
        new Vector2(x, y), COLOR_DIM * 1.4f, 0.30f);
    y += 12f;

    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = new Vector2(x + (viewportX + w - margin - x) / 2f, y),
        Size = new Vector2(viewportX + w - margin - x, 1.5f),
        Color = accentColor * 0.5f, Alignment = TextAlignment.CENTER
    });
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

    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size,
        Color = COLOR_BG, Alignment = TextAlignment.CENTER
    });

    float y = DrawROSHeader(frame, x, viewport.Y + margin, w,
        viewport.X, margin, MOD_DOCK, COLOR_HEADER);

    // Summary row
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
    DrawDot(frame, new Vector2(x + 4f, y + 6f), COLOR_LOCKED);
    DrawTextLeft(frame, "LOCKED:" + lockedCount, new Vector2(x + 13f, y), COLOR_LOCKED, fs);
    DrawDot(frame, new Vector2(x + w * 0.30f + 4f, y + 6f), COLOR_IDLE);
    DrawTextLeft(frame, "IDLE:" + idleCount, new Vector2(x + w * 0.30f + 13f, y), COLOR_IDLE, fs);
    DrawDot(frame, new Vector2(x + w * 0.52f + 4f, y + 6f), COLOR_READY);
    DrawTextLeft(frame, "READY:" + readyCount, new Vector2(x + w * 0.52f + 13f, y), COLOR_READY, fs);
    if (offlineCount > 0)
    {
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "Danger",
            Position = new Vector2(x + w * 0.76f + 4f, y + 6f),
            Size = new Vector2(10f, 10f), Color = COLOR_DANGER,
            Alignment = TextAlignment.CENTER
        });
        DrawTextLeft(frame, "OFF:" + offlineCount,
            new Vector2(x + w * 0.76f + 13f, y), COLOR_DANGER, fs);
    }

    y += 18f;
    DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM, 1f); y += 4f;

    // Column headers
    float colPort   = w * 0.28f;
    float colVessel = w * 0.36f;
    float colCharge = w * 0.16f;

    DrawTextLeft(frame, "PORT",   new Vector2(x + 10f, y),                COLOR_DIM * 2f, 0.38f);
    DrawTextLeft(frame, "STATE",  new Vector2(x + colPort, y),             COLOR_DIM * 2f, 0.38f);
    DrawTextLeft(frame, "VESSEL", new Vector2(x + colPort + colVessel * 0.46f, y), COLOR_DIM * 2f, 0.38f);
    DrawTextRight(frame, "BATTERY  TIME",
        new Vector2(viewport.X + w - margin, y), COLOR_DIM * 2f, 0.36f);

    y += 14f;
    DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM, 1f); y += 3f;

    float rowH      = 20f;
    float available = maxY - y - footerH;
    if (rows.Count * (rowH + 3f) > available && rows.Count > 0)
        rowH = Math.Max(14f, (available / rows.Count) - 3f);

    foreach (var r in rows)
    {
        if (y + rowH > maxY - footerH) break;

        bool isOffline = r.State == "OFFLINE";
        bool isLocked  = r.State == "LOCKED";

        if (isOffline)
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE, Data = "Danger",
                Position = new Vector2(x + 4f, y + rowH * 0.5f),
                Size = new Vector2(10f, 10f), Color = COLOR_DANGER,
                Alignment = TextAlignment.CENTER
            });
        else
            DrawDot(frame, new Vector2(x + 4f, y + rowH * 0.5f), r.StateColor);

        // FIX: show full port name — no truncation, just smaller font if needed
        DrawTextLeft(frame, r.Name,
            new Vector2(x + 13f, y),
            isOffline ? COLOR_DIM * 2f : COLOR_HEADER, 0.38f);

        DrawBadge(frame, new Vector2(x + colPort, y),
            colVessel * 0.42f, rowH - 2f, r.State, r.StateColor);

        if (isLocked)
        {
            // FIX: show full vessel name
            DrawTextLeft(frame, r.Vessel,
                new Vector2(x + colPort + colVessel * 0.44f, y),
                r.MinerData != null ? COLOR_MINER : COLOR_HEADER, 0.38f);

            if (r.BatteryRatio >= 0f)
            {
                Color batColor = r.BatteryRatio >= 0.5f ? COLOR_LOCKED
                               : r.BatteryRatio >= 0.2f ? COLOR_CHARGE
                               : COLOR_DANGER;
                float batBarW = colCharge - 4f;
                DrawHLine(frame,
                    new Vector2(x + colPort + colVessel, y + rowH * 0.5f),
                    batBarW, COLOR_DIM * 0.6f, 3f);
                DrawHLine(frame,
                    new Vector2(x + colPort + colVessel, y + rowH * 0.5f),
                    batBarW * r.BatteryRatio, batColor, 3f);
            }

            string chargeStr = r.ChargeRatio >= 0f
                ? ((int)(r.ChargeRatio * 100f)) + "%" : "--";
            string timeStr = r.DockTimeString();
            DrawTextRight(frame,
                chargeStr + (timeStr.Length > 0 ? "  " + timeStr : ""),
                new Vector2(viewport.X + w - margin, y),
                COLOR_DIM * 2f, 0.36f);
        }

        y += rowH;
        DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM * 0.4f, 1f); y += 3f;
    }

    // Footer
    float fy = maxY - footerH + 2f;
    DrawHLine(frame, new Vector2(x, fy), w, COLOR_HEADER * 0.5f, 1.5f); fy += 4f;

    if (localCharge >= 0f)
    {
        Color bc = localCharge >= 1f   ? COLOR_LOCKED
                 : localCharge >= 0.5f ? COLOR_CHARGE : COLOR_OFFLINE;
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "IconEnergy",
            Position = new Vector2(x + 5f, fy + 6f),
            Size = new Vector2(11f, 11f), Color = bc,
            Alignment = TextAlignment.CENTER
        });
        DrawTextLeft(frame, "BASE POWER",
            new Vector2(x + 16f, fy), COLOR_DIM * 2f, 0.40f);
        DrawTextRight(frame, ((int)(localCharge * 100f)) + "%",
            new Vector2(viewport.X + w - margin, fy), bc, 0.40f);
        fy += 15f;
        DrawHLine(frame, new Vector2(x, fy), w, COLOR_DIM * 0.8f, 5f);
        DrawHLine(frame, new Vector2(x, fy), w * localCharge, bc, 5f);
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

    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size,
        Color = COLOR_BG, Alignment = TextAlignment.CENTER
    });

    float y = DrawROSHeader(frame, x, viewport.Y + margin, w,
        viewport.X, margin, MOD_PROXIMITY, accentColor);

    // Status rows — FIX: increased font
    float fs = 0.38f;
    DrawDot(frame, new Vector2(x + 4f, y + 5f),
        cameras.Count > 0 ? COLOR_LOCKED : COLOR_DIM);
    DrawTextLeft(frame,
        cameras.Count > 0
            ? "CAMERA x" + cameras.Count + "  RANGE " + (int)CAM_RANGE + "m"
            : "NO CAMERA — tag " + CAM_TAG,
        new Vector2(x + 13f, y),
        cameras.Count > 0 ? COLOR_DIM * 2f : COLOR_DIM, fs);
    y += 14f;

    DrawDot(frame, new Vector2(x + 4f, y + 5f),
        _lightsActive ? COLOR_INCOMING : COLOR_DIM);
    string lightStr = dockLights.Count == 0
        ? "NO LIGHTS — tag " + LIGHT_TAG + " 1, 2..."
        : _lightsActive
            ? "RUNWAY x" + dockLights.Count + "  SEQ " + (_lightSeqIndex + 1) + "/" + dockLights.Count
            : "RUNWAY x" + dockLights.Count + "  STANDBY";
    DrawTextLeft(frame, lightStr, new Vector2(x + 13f, y),
        _lightsActive ? COLOR_INCOMING : COLOR_DIM, fs);
    y += 14f;

    DrawDot(frame, new Vector2(x + 4f, y + 5f),
        alertSounds.Count > 0 ? COLOR_LOCKED : COLOR_DIM);
    DrawTextLeft(frame,
        alertSounds.Count > 0
            ? "ALERT x" + alertSounds.Count + (_alertPlayed ? "  ACTIVE" : "  STANDBY")
            : "NO SOUND — tag " + ALERT_TAG,
        new Vector2(x + 13f, y),
        _alertPlayed ? COLOR_INCOMING
        : alertSounds.Count > 0 ? COLOR_DIM * 2f : COLOR_DIM, fs);
    y += 14f;

    DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM, 1f); y += 5f;

    if (_approachList.Count == 0)
    {
        DrawTextLeft(frame, "No contacts within 1km",
            new Vector2(x, y), COLOR_DIM * 2f, 0.46f);
        if (cameras.Count > 0)
        {
            y += 22f;
            float scanW = w * ((float)Math.Abs(Math.Sin(_spinnerAngle)));
            DrawHLine(frame, new Vector2(x, y + 6f), w, COLOR_DIM * 0.4f, 2f);
            DrawHLine(frame, new Vector2(x, y + 6f), scanW, COLOR_DIM * 1.5f, 2f);
            DrawTextLeft(frame, "SCANNING...", new Vector2(x, y), COLOR_DIM * 1.5f, 0.38f);
        }
        frame.Dispose();
        return;
    }

    DrawTextLeft(frame, "VESSEL", new Vector2(x + 13f, y), COLOR_DIM * 2f, 0.38f);
    DrawTextRight(frame, "DISTANCE    SPEED    SOURCE",
        new Vector2(viewport.X + w - margin, y), COLOR_DIM * 2f, 0.36f);
    y += 15f;
    DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM, 1f); y += 3f;

    foreach (var contact in _approachList)
    {
        if (y + 22f > maxY - 14f) break;

        Color cc = contact.IsMinerBroadcast ? COLOR_MINER
                 : contact.Distance < 200   ? COLOR_DANGER
                 : contact.Distance < 500   ? COLOR_INCOMING
                 : COLOR_APPROACH;

        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE,
            Data = contact.IsMinerBroadcast ? "Circle" : "Triangle",
            Position = new Vector2(x + 5f, y + 9f),
            Size = new Vector2(10f, 10f), Color = cc,
            Alignment = TextAlignment.CENTER
        });

        // FIX: show full contact name
        DrawTextLeft(frame, contact.Name, new Vector2(x + 15f, y), cc, 0.42f);
        DrawTextRight(frame,
            contact.DistanceString() + "    " +
            (int)contact.Speed + "m/s    " +
            (contact.IsMinerBroadcast ? "IGC" : contact.CameraName),
            new Vector2(viewport.X + w - margin, y), cc * 0.9f, 0.38f);

        y += 18f;

        if (contact.IsMinerBroadcast && y + 12f <= maxY - 14f)
        {
            MinerContact md = FindMinerData(contact.Name);
            if (md != null)
            {
                string info = "CARGO " + (int)md.CargoFill + "%";
                if (md.BattLevel >= 0f) info += "   BATTERY " + (int)md.BattLevel + "%";
                if (md.FuelLevel >= 0f) info += "   FUEL " + (int)md.FuelLevel + "%";
                if (md.Drilling) info = "DRILLING   " + info;
                DrawTextLeft(frame, info, new Vector2(x + 15f, y),
                    COLOR_MINER * 0.7f, 0.32f);
                y += 12f;
            }
        }

        float fill = Math.Min(1f, (float)Math.Max(0, 1.0 - contact.Distance / CAM_RANGE));
        DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM * 0.4f, 3f);
        DrawHLine(frame, new Vector2(x, y), w * fill, cc, 3f);
        y += 7f;
        DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM * 0.3f, 1f); y += 3f;
    }

    float footerY = maxY - 13f;
    DrawHLine(frame, new Vector2(x, footerY), w, accentColor * 0.4f, 1f);
    int minerCount = 0;
    foreach (var c in _approachList) if (c.IsMinerBroadcast) minerCount++;
    DrawTextLeft(frame,
        _approachList.Count + " CONTACT" + (_approachList.Count != 1 ? "S" : ""),
        new Vector2(x, footerY + 3f), accentColor, 0.40f);
    if (minerCount > 0)
        DrawTextRight(frame, minerCount + " MINER" + (minerCount != 1 ? "S" : ""),
            new Vector2(viewport.X + w - margin, footerY + 3f), COLOR_MINER, 0.40f);

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

    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size,
        Color = COLOR_BG, Alignment = TextAlignment.CENTER
    });

    float y = DrawROSHeader(frame, x, viewport.Y + margin, w,
        viewport.X, margin, MOD_FLEET, COLOR_MINER);

    if (_minerContacts.Count == 0)
    {
        DrawTextLeft(frame, "No miners broadcasting",
            new Vector2(x, y), COLOR_DIM * 2f, 0.44f);
        DrawTextLeft(frame, "Install R.O.S MinerBroadcast.cs on miner PB",
            new Vector2(x, y + 16f), COLOR_DIM, 0.32f);
        frame.Dispose();
        return;
    }

    foreach (var miner in _minerContacts)
    {
        if (y + 50f > maxY - 14f) break;

        string distStr  = miner.DistanceString(Me.GetPosition());
        bool   isDocked = IsDockedGrid(miner.Name);
        string statusStr = isDocked ? "DOCKED" : miner.StatusStr;

        Color statusColor;
        switch (statusStr)
        {
            case "DOCKED":  statusColor = COLOR_LOCKED;  break;
            case "MINING":  statusColor = COLOR_DRILL;   break;
            case "PARKED":  statusColor = COLOR_PARKED;  break;
            case "TRANSIT": statusColor = COLOR_MINER;   break;
            default:        statusColor = COLOR_IDLE;    break;
        }

        // FIX: show full miner name, larger font
        DrawDot(frame, new Vector2(x + 4f, y + 8f), statusColor);
        DrawTextLeft(frame, miner.Name,
            new Vector2(x + 13f, y), statusColor, 0.46f);
        DrawTextRight(frame, distStr + "   " + statusStr,
            new Vector2(viewport.X + w - margin, y), statusColor, 0.40f);

        y += 16f;

        // Bars — FIX: CARGO / BATTERY labels
        bool hasH2  = miner.FuelLevel >= 0f;
        bool hasBat = miner.BattLevel >= 0f;
        int   barCount = 1 + (hasBat ? 1 : 0) + (hasH2 ? 1 : 0);
        float barSlotW = (w - (barCount - 1) * 5f) / barCount;
        float barH     = 5f;
        float bx       = x;

        // CARGO
        Color cargoColor = miner.CargoFill >= 90f ? COLOR_DANGER
                         : miner.CargoFill >= 60f ? COLOR_CHARGE
                         : COLOR_LOCKED;
        DrawTextLeft(frame, "CARGO", new Vector2(bx, y), COLOR_DIM * 2f, 0.30f);
        DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW, COLOR_DIM * 0.6f, barH);
        DrawHLine(frame, new Vector2(bx, y + 10f),
            barSlotW * (miner.CargoFill / 100f), cargoColor, barH);
        DrawTextRight(frame, (int)miner.CargoFill + "%",
            new Vector2(bx + barSlotW, y), cargoColor, 0.30f);
        bx += barSlotW + 5f;

        // BATTERY
        if (hasBat)
        {
            Color batColor = miner.BattLevel < 20f ? COLOR_DANGER
                           : miner.BattLevel < 50f ? COLOR_CHARGE : COLOR_LOCKED;
            DrawTextLeft(frame, "BATTERY", new Vector2(bx, y), COLOR_DIM * 2f, 0.30f);
            DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW, COLOR_DIM * 0.6f, barH);
            DrawHLine(frame, new Vector2(bx, y + 10f),
                barSlotW * (miner.BattLevel / 100f), batColor, barH);
            DrawTextRight(frame, (int)miner.BattLevel + "%",
                new Vector2(bx + barSlotW, y), batColor, 0.30f);
            bx += barSlotW + 5f;
        }

        // FUEL
        if (hasH2)
        {
            Color h2Color = miner.FuelLevel < 20f ? COLOR_DANGER
                          : miner.FuelLevel < 50f ? COLOR_CHARGE : COLOR_LOCKED;
            DrawTextLeft(frame, "FUEL", new Vector2(bx, y), COLOR_DIM * 2f, 0.30f);
            DrawHLine(frame, new Vector2(bx, y + 10f), barSlotW, COLOR_DIM * 0.6f, barH);
            DrawHLine(frame, new Vector2(bx, y + 10f),
                barSlotW * (miner.FuelLevel / 100f), h2Color, barH);
            DrawTextRight(frame, (int)miner.FuelLevel + "%",
                new Vector2(bx + barSlotW, y), h2Color, 0.30f);
        }

        y += 18f;

        DrawTextLeft(frame,
            (miner.Drilling ? "DRILLING" : statusStr) +
            "   " + (int)miner.Speed + " m/s" +
            "   UPDATED: " + miner.AgeString(),
            new Vector2(x + 13f, y),
            miner.Drilling ? COLOR_DRILL * 0.9f : COLOR_DIM * 2f, 0.32f);

        y += 11f;
        DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM * 0.4f, 1f); y += 5f;
    }

    float footerY = maxY - 13f;
    DrawHLine(frame, new Vector2(x, footerY), w, COLOR_MINER * 0.4f, 1f);
    DrawTextLeft(frame,
        _minerContacts.Count + " UNIT" + (_minerContacts.Count != 1 ? "S" : "") + " ONLINE",
        new Vector2(x, footerY + 3f), COLOR_DIM * 2f, 0.40f);
    int miningCount = 0;
    foreach (var m in _minerContacts) if (m.Drilling) miningCount++;
    if (miningCount > 0)
        DrawTextRight(frame, miningCount + " MINING",
            new Vector2(viewport.X + w - margin, footerY + 3f), COLOR_DRILL, 0.40f);

    frame.Dispose();
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
        surface.ContentType           = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;
        surface.Script                = "";
        surface.BackgroundColor       = COLOR_BG;
        surface.ScriptBackgroundColor = COLOR_BG;
        surface.ScriptForegroundColor = COLOR_HEADER;
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

    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = viewport.Center, Size = viewport.Size,
        Color = COLOR_BG, Alignment = TextAlignment.CENTER
    });

    DrawTextLeft(frame, ROS_TITLE, new Vector2(x, y), COLOR_HEADER, 0.56f);
    DrawTextRight(frame, ROS_VERSION,
        new Vector2(viewport.X + w - margin, y), COLOR_DIM * 2f, 0.38f);
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "Screen_LoadingBar",
        Position = new Vector2(viewport.X + w - margin - 22f, y + 8f),
        Size = new Vector2(12f, 12f), Color = COLOR_HEADER * 0.7f,
        Alignment = TextAlignment.CENTER, RotationOrScale = _spinnerAngle
    });

    y += 16f;
    DrawHLine(frame, new Vector2(x, y), w, COLOR_HEADER * 0.5f, 1f); y += 3f;

    if (_approachList.Count > 0 && y + 14f <= maxY)
    {
        int mc = 0;
        foreach (var c in _approachList) if (c.IsMinerBroadcast) mc++;
        Color ac = mc > 0 ? COLOR_MINER : COLOR_INCOMING;
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "Danger",
            Position = new Vector2(x + 4f, y + 5f),
            Size = new Vector2(9f, 9f), Color = ac, Alignment = TextAlignment.CENTER
        });
        DrawTextLeft(frame,
            mc > 0 ? "MINERS  " + mc : "CONTACT " + _approachList.Count,
            new Vector2(x + 12f, y), ac, 0.38f);
        y += 14f;
        DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM, 1f); y += 3f;
    }

    int locked = 0, idle = 0, offline = 0, ready = 0;
    foreach (var r in rows)
    {
        switch (r.State)
        {
            case "LOCKED":  locked++;  break;
            case "READY":   ready++;   break;
            case "IDLE":    idle++;    break;
            default:        offline++; break;
        }
    }

    string[] labels = { "LOCKED", "READY", "IDLE", "OFFLINE" };
    int[]    counts = { locked, ready, idle, offline };
    Color[]  colors = { COLOR_LOCKED, COLOR_READY, COLOR_IDLE, COLOR_OFFLINE };

    for (int i = 0; i < 4; i++)
    {
        if (y + 14f > maxY) break;
        bool isDanger = labels[i] == "OFFLINE" && counts[i] > 0;
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = isDanger ? "Danger" : "Circle",
            Position = new Vector2(x + 4f, y + 5f),
            Size = new Vector2(isDanger ? 9f : 6f, isDanger ? 9f : 6f),
            Color = isDanger ? COLOR_DANGER : colors[i], Alignment = TextAlignment.CENTER
        });
        DrawTextLeft(frame, labels[i].PadRight(8) + counts[i],
            new Vector2(x + 12f, y), isDanger ? COLOR_DANGER : colors[i], 0.38f);
        y += 14f;
    }

    if (_minerContacts.Count > 0 && y + 14f <= maxY)
    {
        DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM, 1f); y += 3f;
        DrawDot(frame, new Vector2(x + 4f, y + 5f), COLOR_MINER);
        DrawTextLeft(frame, "FLEET  ".PadRight(8) + _minerContacts.Count,
            new Vector2(x + 12f, y), COLOR_MINER, 0.38f);
        y += 14f;
    }

    float lc = GetLocalCharge();
    if (lc >= 0f && y + 18f <= maxY)
    {
        DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM, 1f); y += 3f;
        Color pc = lc >= 1f ? COLOR_LOCKED : lc >= 0.5f ? COLOR_CHARGE : COLOR_OFFLINE;
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "IconEnergy",
            Position = new Vector2(x + 4f, y + 5f),
            Size = new Vector2(9f, 9f), Color = pc, Alignment = TextAlignment.CENTER
        });
        DrawTextLeft(frame, "BASE PWR", new Vector2(x + 12f, y), COLOR_DIM * 2f, 0.36f);
        DrawTextRight(frame, ((int)(lc * 100f)) + "%",
            new Vector2(viewport.X + w - margin, y), pc, 0.36f);
        y += 14f;
        if (y + 5f <= maxY)
        {
            DrawHLine(frame, new Vector2(x, y), w, COLOR_DIM * 0.8f, 5f);
            DrawHLine(frame, new Vector2(x, y), w * lc, pc, 5f);
        }
    }

    frame.Dispose();
}

// ============================================================
// Draw helpers
// ============================================================

void DrawTextLeft(MySpriteDrawFrame frame, string text,
                  Vector2 pos, Color color, float scale)
{
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = text, Position = pos,
        RotationOrScale = scale, Color = color,
        Alignment = TextAlignment.LEFT, FontId = "Monospace"
    });
}

void DrawTextRight(MySpriteDrawFrame frame, string text,
                   Vector2 pos, Color color, float scale)
{
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = text, Position = pos,
        RotationOrScale = scale, Color = color,
        Alignment = TextAlignment.RIGHT, FontId = "Monospace"
    });
}

void DrawHLine(MySpriteDrawFrame frame, Vector2 pos,
               float width, Color color, float height = 1.5f)
{
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = pos, Size = new Vector2(width, height),
        Color = color, Alignment = TextAlignment.LEFT
    });
}

void DrawDot(MySpriteDrawFrame frame, Vector2 pos, Color color)
{
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "Circle",
        Position = pos, Size = new Vector2(7f, 7f),
        Color = color, Alignment = TextAlignment.CENTER
    });
}

void DrawBadge(MySpriteDrawFrame frame, Vector2 pos,
               float width, float height, string label, Color color)
{
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = pos, Size = new Vector2(width, height),
        Color = color * 0.2f, Alignment = TextAlignment.LEFT
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = pos, Size = new Vector2(2f, height),
        Color = color, Alignment = TextAlignment.LEFT
    });
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = label,
        Position = pos + new Vector2(4f, 1f),
        RotationOrScale = 0.36f, Color = color,
        Alignment = TextAlignment.LEFT, FontId = "Monospace"
    });
}
