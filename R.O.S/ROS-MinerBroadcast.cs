// ============================================================
// R.O.S — Rev Operating System
// Module:  Fleet Broadcast (Miner Side)
// Author:  RevGamer (Simba "Davy" Jones)
// Version: 1.1
//
// Install on miner PB.
// Requires antenna enabled + broadcasting.
// Remote Control optional (enables auto approach speed).
//
// NEW v1.1:
//   - Boot screen on PB display
//   - Proper live status screen after boot
//   - R.O.S amber palette
// ============================================================

const string CHANNEL       = "DOCK_APPROACH";
const string REPLY_CHANNEL = "DOCK_REPLY";
const double APPROACH_DIST = 300.0;
const float  APPROACH_SPD  = 15.0f;
const float  BOOT_TIME     = 3.0f;

static readonly Color COLOR_BG      = new Color(8,   14,  4);
static readonly Color COLOR_HEADER  = new Color(157, 225, 203);
static readonly Color COLOR_AMBER   = new Color(255, 184, 0);
static readonly Color COLOR_YELLOW  = new Color(255, 224, 51);
static readonly Color COLOR_DIM     = new Color(80,  50,  0);
static readonly Color COLOR_DANGER  = new Color(255, 60,  0);
static readonly Color COLOR_OK      = new Color(80,  220, 100);
static readonly Color COLOR_CHARGE  = new Color(255, 200, 50);

static readonly string[] BOOT_QUOTES = {
    "Warming up drill systems...",
    "Calibrating ore scanners...",
    "Checking conveyor network...",
    "Syncing with base station...",
    "Loading mining protocols...",
    "Pressurising cargo bays...",
    "Engaging thruster systems...",
    "All systems go."
};

IMyShipController    _controller;
IMyRadioAntenna      _antenna;
IMyRemoteControl     _remote;
IMyBroadcastListener _replyListener;

bool   _approachModeActive = false;
bool   _booting            = true;
float  _bootTimer          = 0f;
float  _spinnerAngle       = 0f;
string _bootMessage        = BOOT_QUOTES[0];

double _distToBase = -1;
bool   _pbInitialised = false;

DateTime _lastRun = DateTime.Now;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    _replyListener = IGC.RegisterBroadcastListener(REPLY_CHANNEL);
    _replyListener.SetMessageCallback();
}

public void Main(string argument, UpdateType updateSource)
{
    double delta = (DateTime.Now - _lastRun).TotalSeconds;
    _lastRun     = DateTime.Now;

    _spinnerAngle += 0.5f;
    if (_spinnerAngle > (float)(Math.PI * 2.0)) _spinnerAngle = 0f;

    // Cache blocks once
    if (_controller == null)
    {
        var list = new List<IMyShipController>();
        GridTerminalSystem.GetBlocksOfType(list, c => c.IsSameConstructAs(Me));
        _controller = list.FirstOrDefault(c => c.IsMainCockpit) ?? list.FirstOrDefault();
    }
    if (_antenna == null)
    {
        var list = new List<IMyRadioAntenna>();
        GridTerminalSystem.GetBlocksOfType(list, a => a.IsSameConstructAs(Me));
        _antenna = list.FirstOrDefault();
    }
    if (_remote == null)
    {
        var list = new List<IMyRemoteControl>();
        GridTerminalSystem.GetBlocksOfType(list, r => r.IsSameConstructAs(Me));
        _remote = list.FirstOrDefault();
    }

    // Boot sequence
    if (_booting)
    {
        _bootTimer += (float)delta;
        float prog     = Math.Min(1f, _bootTimer / BOOT_TIME);
        int   msgIndex = (int)(prog * (BOOT_QUOTES.Length - 1));
        msgIndex       = Math.Max(0, Math.Min(msgIndex, BOOT_QUOTES.Length - 1));
        _bootMessage   = BOOT_QUOTES[msgIndex];

        DrawBootScreen(prog);

        if (_bootTimer >= BOOT_TIME)
        {
            _booting = false;
            Echo("R.O.S Fleet Broadcast v1.1 — Online");
        }
        return;
    }

    // ---- Normal operation ----

    if (_antenna == null)
    {
        DrawErrorScreen("No antenna found",
            "Build antenna + enable broadcasting");
        Echo("R.O.S Fleet Broadcast\nERROR: No antenna");
        return;
    }
    if (!_antenna.Enabled || !_antenna.IsBroadcasting)
    {
        DrawErrorScreen("Antenna offline",
            "Enable antenna and set broadcasting ON");
        Echo("R.O.S Fleet Broadcast\nERROR: Antenna offline");
        return;
    }

    // Read distance reply from base
    while (_replyListener.HasPendingMessage)
    {
        var    replyMsg  = _replyListener.AcceptMessage();
        string replyData = replyMsg.Data.ToString();
        string[] rParts  = replyData.Split('|');
        if (rParts.Length >= 2 && rParts[0] == Me.CubeGrid.CustomName)
            double.TryParse(rParts[1], out _distToBase);
    }

    // Auto approach speed limit
    if (_remote != null && _distToBase > 0)
    {
        if (_distToBase < APPROACH_DIST && !_approachModeActive)
        {
            _remote.SpeedLimit  = APPROACH_SPD;
            _approachModeActive = true;
        }
        else if (_distToBase >= APPROACH_DIST && _approachModeActive)
        {
            _remote.SpeedLimit  = 100f;
            _approachModeActive = false;
        }
    }

    // Gather data
    var drills = new List<IMyShipDrill>();
    GridTerminalSystem.GetBlocksOfType(drills, d => d.IsSameConstructAs(Me));
    bool drilling = drills.Count > 0 && drills[0].Enabled;

    string   gridName  = Me.CubeGrid.CustomName;
    float    speed     = _controller != null
                       ? (float)_controller.GetShipSpeed() : 0f;
    Vector3D pos       = Me.GetPosition();
    float    cargoFill = GetCargoFill();
    float    fuelLevel = GetFuelLevel();
    float    battLevel = GetBatteryLevel();
    string   status    = GetMinerStatus(drilling, speed);

    // Broadcast — use InvariantCulture so decimal separators are consistent
    System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
    string broadcast = gridName
        + "|" + ((int)speed)
        + "|" + pos.X.ToString("F0", inv)
        + "|" + pos.Y.ToString("F0", inv)
        + "|" + pos.Z.ToString("F0", inv)
        + "|" + (drilling ? "1" : "0")
        + "|" + cargoFill.ToString("F0", inv)
        + "|" + fuelLevel.ToString("F0", inv)
        + "|" + battLevel.ToString("F0", inv)
        + "|" + status;

    IGC.SendBroadcastMessage(CHANNEL, broadcast,
        TransmissionDistance.TransmissionDistanceMax);

    // Draw PB status screen
    DrawStatusScreen(gridName, speed, status, drilling,
        cargoFill, fuelLevel, battLevel);

    // Echo for terminal
    Echo("R.O.S — Fleet Broadcast  v1.1");
    Echo("Channel: " + CHANNEL);
    Echo("Name:    " + gridName);
    Echo("Speed:   " + (int)speed + " m/s");
    Echo("Status:  " + status);
    Echo("Drill:   " + (drilling ? "ACTIVE" : "OFF"));
    Echo("Cargo:   " + (int)cargoFill + "%");
    Echo("Fuel:    " + (fuelLevel >= 0 ? (int)fuelLevel + "%" : "N/A"));
    Echo("Battery: " + (battLevel >= 0 ? (int)battLevel + "%" : "N/A"));
    if (_distToBase > 0)
        Echo("Base:    " + (int)_distToBase + "m");
    if (_approachModeActive)
        Echo(">> APPROACH MODE " + APPROACH_SPD + "m/s <<");
}

// ============================================================
// PB Display drawing
// ============================================================

void InitPBDisplay(IMyTextSurface surface)
{
    if (_pbInitialised) return;
    surface.ContentType           = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;
    surface.Script                = "";
    surface.BackgroundColor       = COLOR_BG;
    surface.ScriptBackgroundColor = COLOR_BG;
    surface.ScriptForegroundColor = COLOR_AMBER;
    _pbInitialised = true;
}

void DrawBootScreen(float progress)
{
    var pb = Me as IMyTextSurfaceProvider;
    if (pb == null || pb.SurfaceCount == 0) return;
    var surface = pb.GetSurface(0);
    if (surface == null) return;
    InitPBDisplay(surface);

    RectangleF vp = new RectangleF(
        (surface.TextureSize - surface.SurfaceSize) / 2f,
        surface.SurfaceSize);

    using (var frame = surface.DrawFrame())
    {
        float cx = vp.Center.X, cy = vp.Center.Y;
        float w  = vp.Width,    h  = vp.Height;

        // BG
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = vp.Center, Size = vp.Size,
            Color = COLOR_BG, Alignment = TextAlignment.CENTER
        });

        // R.O.S title
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT, Data = "R.O.S",
            Position = new Vector2(cx, cy - 60f),
            RotationOrScale = 1.4f, Color = COLOR_HEADER,
            Alignment = TextAlignment.CENTER, FontId = "Monospace"
        });

        // Module subtitle
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT, Data = "FLEET BROADCAST",
            Position = new Vector2(cx, cy - 32f),
            RotationOrScale = 0.42f, Color = COLOR_AMBER * 0.8f,
            Alignment = TextAlignment.CENTER, FontId = "Monospace"
        });

        // Version
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT, Data = "v1.1",
            Position = new Vector2(cx, cy - 16f),
            RotationOrScale = 0.36f, Color = COLOR_DIM * 2f,
            Alignment = TextAlignment.CENTER, FontId = "Monospace"
        });

        // Divider
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(cx, cy),
            Size = new Vector2(w * 0.6f, 1f),
            Color = COLOR_AMBER * 0.4f, Alignment = TextAlignment.CENTER
        });

        // Author
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT,
            Data = "RevGamer (Simba \"Davy\" Jones)",
            Position = new Vector2(cx, cy + 8f),
            RotationOrScale = 0.28f, Color = COLOR_DIM * 1.5f,
            Alignment = TextAlignment.CENTER, FontId = "Monospace"
        });

        // Boot message
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT, Data = _bootMessage,
            Position = new Vector2(cx, cy + 34f),
            RotationOrScale = 0.34f, Color = COLOR_AMBER * 0.8f,
            Alignment = TextAlignment.CENTER, FontId = "Monospace"
        });

        // Progress bar track
        float barX = vp.X + 20f;
        float barY = cy + 52f;
        float barW = w - 40f;
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(barX + barW * 0.5f, barY),
            Size = new Vector2(barW, 4f),
            Color = COLOR_DIM, Alignment = TextAlignment.CENTER
        });
        if (progress > 0f)
        {
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE, Data = "SquareSimple",
                Position = new Vector2(barX + (barW * progress) * 0.5f, barY),
                Size = new Vector2(barW * progress, 4f),
                Color = COLOR_AMBER, Alignment = TextAlignment.CENTER
            });
        }

        // Percent
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT,
            Data = ((int)(progress * 100f)) + "%",
            Position = new Vector2(barX + barW, barY - 8f),
            RotationOrScale = 0.30f, Color = COLOR_DIM * 2f,
            Alignment = TextAlignment.RIGHT, FontId = "Monospace"
        });

        // Spinner
        frame.Add(new MySprite()
        {
            Type            = SpriteType.TEXTURE, Data = "Screen_LoadingBar",
            Position        = new Vector2(cx, cy + 70f),
            Size            = new Vector2(18f, 18f),
            Color           = COLOR_AMBER * 0.5f,
            Alignment       = TextAlignment.CENTER,
            RotationOrScale = _spinnerAngle
        });
    }
}

void DrawStatusScreen(string gridName, float speed, string status,
                       bool drilling, float cargo, float fuel, float batt)
{
    var pb = Me as IMyTextSurfaceProvider;
    if (pb == null || pb.SurfaceCount == 0) return;
    var surface = pb.GetSurface(0);
    if (surface == null) return;
    InitPBDisplay(surface);

    RectangleF vp = new RectangleF(
        (surface.TextureSize - surface.SurfaceSize) / 2f,
        surface.SurfaceSize);

    using (var frame = surface.DrawFrame())
    {
        float w      = vp.Width;
        float margin = 8f;
        float gap    = 5f;
        float x      = vp.X + margin;
        float right  = vp.X + w - margin;
        float fs     = 0.44f;
        float fsS    = 0.36f;

        // BG
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = vp.Center, Size = vp.Size,
            Color = COLOR_BG, Alignment = TextAlignment.CENTER
        });

        float y = vp.Y + margin;

        // Header
        AddText(frame, "R.O.S", new Vector2(x, y), COLOR_HEADER, 0.52f, TextAlignment.LEFT);
        AddText(frame, "v1.1", new Vector2(right, y), COLOR_DIM * 2f, 0.34f, TextAlignment.RIGHT);

        // Spinner
        frame.Add(new MySprite()
        {
            Type            = SpriteType.TEXTURE, Data = "Screen_LoadingBar",
            Position        = new Vector2(right - 20f, y + 7f),
            Size            = new Vector2(12f, 12f),
            Color           = COLOR_AMBER * 0.7f,
            Alignment       = TextAlignment.CENTER,
            RotationOrScale = _spinnerAngle
        });

        y += 13f;
        AddText(frame, "  FLEET BROADCAST",
            new Vector2(x, y), COLOR_AMBER, 0.36f, TextAlignment.LEFT);
        y += 11f;
        AddText(frame, "  RevGamer (Simba \"Davy\" Jones)",
            new Vector2(x, y), COLOR_DIM * 1.4f, 0.26f, TextAlignment.LEFT);
        y += 9f;

        // Divider
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(x + (w - margin * 2f) / 2f, y),
            Size = new Vector2(w - margin * 2f, 1.5f),
            Color = COLOR_AMBER * 0.4f, Alignment = TextAlignment.CENTER
        });
        y += 5f;

        // Grid name
        AddText(frame, gridName, new Vector2(x, y), COLOR_YELLOW, fs, TextAlignment.LEFT);
        y += fs * 16f + gap;

        // Status row
        Color statCol;
        switch (status)
        {
            case "MINING":  statCol = COLOR_DANGER;  break;
            case "TRANSIT": statCol = COLOR_AMBER;   break;
            case "PARKED":  statCol = COLOR_CHARGE;  break;
            default:        statCol = COLOR_OK;       break;
        }
        AddText(frame, "STATUS", new Vector2(x, y), COLOR_DIM * 2f, fsS, TextAlignment.LEFT);
        AddText(frame, status,   new Vector2(right, y), statCol, fsS, TextAlignment.RIGHT);
        y += fsS * 15f + gap;

        // Speed
        AddText(frame, "SPEED",  new Vector2(x, y), COLOR_DIM * 2f, fsS, TextAlignment.LEFT);
        AddText(frame, (int)speed + " m/s", new Vector2(right, y),
            speed > 1f ? COLOR_AMBER : COLOR_DIM * 2f, fsS, TextAlignment.RIGHT);
        y += fsS * 15f + gap;

        // Drill
        AddText(frame, "DRILL",  new Vector2(x, y), COLOR_DIM * 2f, fsS, TextAlignment.LEFT);
        AddText(frame, drilling ? "ACTIVE" : "OFF", new Vector2(right, y),
            drilling ? COLOR_DANGER : COLOR_DIM * 2f, fsS, TextAlignment.RIGHT);
        y += fsS * 15f + gap;

        // Divider
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = new Vector2(x + (w - margin * 2f) / 2f, y),
            Size = new Vector2(w - margin * 2f, 1f),
            Color = COLOR_DIM, Alignment = TextAlignment.CENTER
        });
        y += gap;

        float barW = w - margin * 2f;
        float barH = 10f;

        // Cargo bar
        Color cargoCol = cargo >= 90f ? COLOR_DANGER
                       : cargo >= 60f ? COLOR_CHARGE : COLOR_OK;
        AddText(frame, "CARGO", new Vector2(x, y), COLOR_DIM * 2f, fsS, TextAlignment.LEFT);
        AddText(frame, (int)cargo + "%", new Vector2(right, y), cargoCol, fsS, TextAlignment.RIGHT);
        y += fsS * 14f + 2f;
        AddHLine(frame, new Vector2(x, y), barW, COLOR_DIM * 0.6f, barH);
        AddHLine(frame, new Vector2(x, y), barW * (cargo / 100f), cargoCol, barH);
        y += barH + gap;

        // Battery bar
        if (batt >= 0f)
        {
            Color batCol = batt < 20f ? COLOR_DANGER : batt < 50f ? COLOR_CHARGE : COLOR_OK;
            AddText(frame, "BATTERY", new Vector2(x, y), COLOR_DIM * 2f, fsS, TextAlignment.LEFT);
            AddText(frame, (int)batt + "%", new Vector2(right, y), batCol, fsS, TextAlignment.RIGHT);
            y += fsS * 14f + 2f;
            AddHLine(frame, new Vector2(x, y), barW, COLOR_DIM * 0.6f, barH);
            AddHLine(frame, new Vector2(x, y), barW * (batt / 100f), batCol, barH);
            y += barH + gap;
        }

        // Fuel bar
        if (fuel >= 0f)
        {
            Color fuelCol = fuel < 20f ? COLOR_DANGER : fuel < 50f ? COLOR_CHARGE : COLOR_OK;
            AddText(frame, "FUEL", new Vector2(x, y), COLOR_DIM * 2f, fsS, TextAlignment.LEFT);
            AddText(frame, (int)fuel + "%", new Vector2(right, y), fuelCol, fsS, TextAlignment.RIGHT);
            y += fsS * 14f + 2f;
            AddHLine(frame, new Vector2(x, y), barW, COLOR_DIM * 0.6f, barH);
            AddHLine(frame, new Vector2(x, y), barW * (fuel / 100f), fuelCol, barH);
            y += barH + gap;
        }

        // Distance to base
        if (_distToBase > 0)
        {
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE, Data = "SquareSimple",
                Position = new Vector2(x + (w - margin * 2f) / 2f, y),
                Size = new Vector2(w - margin * 2f, 1f),
                Color = COLOR_DIM, Alignment = TextAlignment.CENTER
            });
            y += gap;

            string distStr = _distToBase >= 1000
                ? string.Format("{0:0.0}km", _distToBase / 1000.0)
                : (int)_distToBase + "m";

            AddText(frame, "BASE", new Vector2(x, y), COLOR_DIM * 2f, fsS, TextAlignment.LEFT);
            AddText(frame, distStr, new Vector2(right, y),
                _approachModeActive ? COLOR_DANGER : COLOR_AMBER, fsS, TextAlignment.RIGHT);
            y += fsS * 15f + gap;

            if (_approachModeActive)
            {
                AddText(frame, ">> APPROACH MODE " + APPROACH_SPD + "m/s <<",
                    new Vector2(vp.X + w / 2f, y),
                    COLOR_DANGER, fsS * 0.9f, TextAlignment.CENTER);
            }
        }
    }
}

void DrawErrorScreen(string title, string detail)
{
    var pb = Me as IMyTextSurfaceProvider;
    if (pb == null || pb.SurfaceCount == 0) return;
    var surface = pb.GetSurface(0);
    if (surface == null) return;
    InitPBDisplay(surface);

    RectangleF vp = new RectangleF(
        (surface.TextureSize - surface.SurfaceSize) / 2f, surface.SurfaceSize);

    using (var frame = surface.DrawFrame())
    {
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "SquareSimple",
            Position = vp.Center, Size = vp.Size,
            Color = COLOR_BG, Alignment = TextAlignment.CENTER
        });

        float cx = vp.Center.X, cy = vp.Center.Y;

        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE, Data = "Danger",
            Position = new Vector2(cx, cy - 30f),
            Size = new Vector2(40f, 40f), Color = COLOR_DANGER,
            Alignment = TextAlignment.CENTER
        });
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT, Data = title,
            Position = new Vector2(cx, cy + 20f),
            RotationOrScale = 0.44f, Color = COLOR_DANGER,
            Alignment = TextAlignment.CENTER, FontId = "Monospace"
        });
        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT, Data = detail,
            Position = new Vector2(cx, cy + 38f),
            RotationOrScale = 0.32f, Color = COLOR_DIM * 2f,
            Alignment = TextAlignment.CENTER, FontId = "Monospace"
        });
    }
}

// ============================================================
// Draw helpers
// ============================================================

void AddText(MySpriteDrawFrame frame, string text, Vector2 pos,
             Color color, float scale, TextAlignment align)
{
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXT, Data = text, Position = pos,
        RotationOrScale = scale, Color = color,
        Alignment = align, FontId = "Monospace"
    });
}

void AddHLine(MySpriteDrawFrame frame, Vector2 pos,
              float width, Color color, float height = 1.5f)
{
    frame.Add(new MySprite()
    {
        Type = SpriteType.TEXTURE, Data = "SquareSimple",
        Position = pos, Size = new Vector2(width, height),
        Color = color, Alignment = TextAlignment.LEFT
    });
}

// ============================================================
// Status detection
// ============================================================

string GetMinerStatus(bool drilling, float speed)
{
    if (drilling) return "MINING";
    if (speed < 1f)
    {
        if (_controller != null)
        {
            double elevation = 0;
            bool onPlanet = _controller.TryGetPlanetElevation(
                MyPlanetElevation.Surface, out elevation);
            if (onPlanet && elevation < 5.0) return "PARKED";
            if (!onPlanet) return "PARKED";
        }
        else return "PARKED";
        return "IDLE";
    }
    return "TRANSIT";
}

// ============================================================
// Cargo
// ============================================================

float GetCargoFill()
{
    var blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType(blocks,
        b => b.IsSameConstructAs(Me) && b.HasInventory);

    float current = 0f, max = 0f;
    foreach (var b in blocks)
    {
        if (b is IMyShipController) continue;
        if (b is IMyShipToolBase)   continue;
        for (int i = 0; i < b.InventoryCount; i++)
        {
            var inv = b.GetInventory(i);
            current += (float)inv.CurrentVolume;
            max     += (float)inv.MaxVolume;
        }
    }
    return max <= 0f ? 0f : (current / max) * 100f;
}

// ============================================================
// Fuel
// ============================================================

float GetFuelLevel()
{
    var tanks = new List<IMyGasTank>();
    GridTerminalSystem.GetBlocksOfType(tanks, t => t.IsSameConstructAs(Me));
    float total = 0f; int count = 0;
    foreach (var tank in tanks)
    {
        if (!tank.BlockDefinition.SubtypeId.ToLower().Contains("hydro")) continue;
        total += (float)tank.FilledRatio; count++;
    }
    return count == 0 ? -1f : (total / count) * 100f;
}

// ============================================================
// Battery
// ============================================================

float GetBatteryLevel()
{
    var bats = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType(bats, b => b.IsSameConstructAs(Me));
    float current = 0f, max = 0f;
    foreach (var bat in bats) { current += bat.CurrentStoredPower; max += bat.MaxStoredPower; }
    return max <= 0f ? -1f : (current / max) * 100f;
}
