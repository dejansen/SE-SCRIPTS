// -------------------------------------------------------------------------
// PTA — Planetary Travel Assistant
// Hotbar commands:
//   PTA_ON                     — initialise system, start fresh
//   PTA_OFF                    — stop everything, full manual control
//   HORIZON_ON / HORIZON_OFF   — keep ship level with horizon
//   ALTITUDE_ON / ALTITUDE_OFF — hold terrain-relative cruise altitude
//   SET_ALTITUDE               — lock current terrain altitude as target
//   CRUISE_ON / CRUISE_OFF     — disable brake thrusters, enable both features
// Display: tag any LCD or cockpit with [PTA] in the name
// -------------------------------------------------------------------------

// -------------------------------------------------------------------------
// Config constants
// -------------------------------------------------------------------------

private const string SEC_HORIZON  = "horizon";
private const string SEC_ALTITUDE = "altitude";
private const string SEC_CRUISE   = "cruise";
private const string SEC_DISPLAY  = "display";

private const float  DEFAULT_HORIZON_CORRECTION  = 0.5f;
private const float  DEFAULT_HORIZON_DAMPING     = 0.2f;
private const float  DEFAULT_HORIZON_THRESHOLD   = 0.02f;

private const float  DEFAULT_ALTITUDE_TARGET     = 1000f;
private const float  DEFAULT_ALTITUDE_CORRECTION = 0.005f;
private const float  DEFAULT_ALTITUDE_DAMPING    = 0.01f;
private const float  DEFAULT_ALTITUDE_THRESHOLD  = 5f;
private const float  DEFAULT_ALTITUDE_MAX_SPEED  = 15f;

private const string DEFAULT_BRAKE_GROUP   = "";
private const int    DEFAULT_COCKPIT_SCREEN = 0;

private const int    BOOT_TICKS = 12;
private const string VERSION   = "1.1";

// -------------------------------------------------------------------------
// Display colors  (same palette as AGM for consistency)
// -------------------------------------------------------------------------

private readonly Color COL_BG      = new Color(  1,  8, 13);
private readonly Color COL_PANEL   = new Color(  2, 18, 28);
private readonly Color COL_PANEL2  = new Color(  3, 58, 78);
private readonly Color COL_ACCENT  = new Color( 38,239,255);
private readonly Color COL_ACCENT2 = new Color(112,247,255);
private readonly Color COL_TEXT    = new Color(126,246,255);
private readonly Color COL_DIM     = new Color( 44,177,195);
private readonly Color COL_OK      = new Color( 97,255,214);
private readonly Color COL_WARN    = new Color(255,202, 34);
private readonly Color COL_BAD     = new Color(255, 79, 66);
private readonly Color COL_PROG_BG = new Color( 18, 48, 32);
private readonly Color COL_PROG_FG = new Color(255,204, 36);

// -------------------------------------------------------------------------
// Config fields
// -------------------------------------------------------------------------

private float  _horizonCorrection  = DEFAULT_HORIZON_CORRECTION;
private float  _horizonDamping     = DEFAULT_HORIZON_DAMPING;
private float  _horizonThreshold   = DEFAULT_HORIZON_THRESHOLD;

private float  _altitudeTarget     = DEFAULT_ALTITUDE_TARGET;
private float  _altitudeCorrection = DEFAULT_ALTITUDE_CORRECTION;
private float  _altitudeDamping    = DEFAULT_ALTITUDE_DAMPING;
private float  _altitudeThreshold  = DEFAULT_ALTITUDE_THRESHOLD;
private float  _altitudeMaxSpeed   = DEFAULT_ALTITUDE_MAX_SPEED;

private string _brakeGroup    = DEFAULT_BRAKE_GROUP;
private int    _cockpitScreen = DEFAULT_COCKPIT_SCREEN;

// -------------------------------------------------------------------------
// State
// -------------------------------------------------------------------------

private bool   _horizonActive  = false;
private bool   _altitudeActive = false;
private int    _bootPhase      = 0;
private string _horizonStatus  = "---";
private string _altitudeStatus = "---";

// -------------------------------------------------------------------------
// Blocks
// -------------------------------------------------------------------------

private IMyShipController             _controller;
private readonly List<IMyGyro>        _gyros          = new List<IMyGyro>();
private readonly List<IMyThrust>      _thrusters      = new List<IMyThrust>();
private readonly List<IMyThrust>      _upThrusters    = new List<IMyThrust>();
private readonly List<IMyThrust>      _brakeThrusters = new List<IMyThrust>();
private readonly List<IMyTextSurface> _surfaces       = new List<IMyTextSurface>();

// -------------------------------------------------------------------------
// Lifecycle
// -------------------------------------------------------------------------

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.None;
    ParseConfig();
    InitSurfaces();
    DrawOffline();
}

public void Main(string argument, UpdateType updateSource)
{
    switch (argument)
    {
        case "PTA_ON":
            ParseConfig();
            InitBlocks();
            _horizonActive  = false;
            _altitudeActive = false;
            _bootPhase      = 1;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            DrawBoot(0);
            return;

        case "PTA_OFF":
            _horizonActive  = false;
            _altitudeActive = false;
            _bootPhase      = 0;
            ReleaseGyros();
            ReleaseThrusters();
            foreach (var t in _brakeThrusters) t.Enabled = true;
            _brakeThrusters.Clear();
            Runtime.UpdateFrequency = UpdateFrequency.None;
            DrawOffline();
            return;

        case "HORIZON_ON":
            ParseConfig();
            InitBlocks();
            _horizonActive = true;
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "HORIZON_OFF":
            _horizonActive = false;
            ReleaseGyros();
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "ALTITUDE_ON":
            ParseConfig();
            InitBlocks();
            _altitudeActive = true;
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "ALTITUDE_OFF":
            _altitudeActive = false;
            ReleaseThrusters();
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "SET_ALTITUDE":
            SetAltitudeFromCurrentPosition();
            DrawStatus();
            return;

        case "CRUISE_ON":
            ParseConfig();
            InitBlocks();
            FindBrakeThrusters();
            foreach (var t in _brakeThrusters) t.Enabled = false;
            _horizonActive  = true;
            _altitudeActive = true;
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "CRUISE_OFF":
            _horizonActive  = false;
            _altitudeActive = false;
            ReleaseGyros();
            ReleaseThrusters();
            foreach (var t in _brakeThrusters) t.Enabled = true;
            _brakeThrusters.Clear();
            ApplyUpdateFrequency();
            DrawStatus();
            return;
    }

    // Boot animation ticks
    if (_bootPhase > 0)
    {
        DrawBoot(_bootPhase);
        _bootPhase++;
        if (_bootPhase > BOOT_TICKS)
        {
            _bootPhase = 0;
            ApplyUpdateFrequency();
            DrawStatus();
        }
        return;
    }

    // Feature ticks
    if (_horizonActive)  HorizonTick();
    if (_altitudeActive) AltitudeTick();
    DrawStatus();
}

// -------------------------------------------------------------------------
// Horizon
// -------------------------------------------------------------------------

private void HorizonTick()
{
    if (_controller == null || _gyros.Count == 0) return;

    Vector3D gravity = _controller.GetNaturalGravity();
    if (gravity.LengthSquared() < 0.001) { _horizonStatus = "NO GRAVITY"; return; }

    Vector3D desiredUp      = -Vector3D.Normalize(gravity);
    Vector3D currentUp      = _controller.WorldMatrix.Up;
    Vector3D tiltCorrection = Vector3D.Cross(currentUp, desiredUp);
    double   tiltError      = tiltCorrection.Length();

    if (tiltError < _horizonThreshold)
    {
        _horizonStatus = "ALIGNED";
        ReleaseGyros();
        return;
    }

    _horizonStatus = "CORRECTING " + tiltError.ToString("F3");

    Vector3D angularVelocity = _controller.GetShipVelocities().AngularVelocity;
    Vector3D gyroCommand     = tiltCorrection * _horizonCorrection - angularVelocity * _horizonDamping;

    foreach (var gyro in _gyros)
    {
        Vector3D local = Vector3D.TransformNormal(gyroCommand, MatrixD.Transpose(gyro.WorldMatrix));
        gyro.GyroOverride = true;
        gyro.Pitch = -(float)local.X;
        gyro.Yaw   = -(float)local.Y;
        gyro.Roll  = -(float)local.Z;
    }
}

// -------------------------------------------------------------------------
// Altitude
// -------------------------------------------------------------------------

private void AltitudeTick()
{
    if (_controller == null) return;

    Vector3D gravity = _controller.GetNaturalGravity();
    if (gravity.LengthSquared() < 0.001) { _altitudeStatus = "NO GRAVITY"; return; }

    double currentAltitude;
    if (!_controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out currentAltitude))
    {
        _altitudeStatus = "NO SURFACE";
        return;
    }

    Vector3D upDir         = -Vector3D.Normalize(gravity);
    double   altitudeError = _altitudeTarget - currentAltitude;
    double   verticalSpeed = Vector3D.Dot(_controller.GetShipVelocities().LinearVelocity, upDir);

    FindUpThrusters(gravity);
    if (_upThrusters.Count == 0) { _altitudeStatus = "NO UP THRUSTERS"; return; }

    float totalMaxThrust = 0f;
    foreach (var t in _upThrusters) totalMaxThrust += t.MaxEffectiveThrust;
    if (totalMaxThrust < 1f)       { _altitudeStatus = "NO THRUST"; return; }

    float mass          = _controller.CalculateShipMass().TotalMass;
    float hoverFraction = (mass * (float)gravity.Length()) / totalMaxThrust;

    float pdCorrection = 0f;
    if (Math.Abs(altitudeError) >= _altitudeThreshold || Math.Abs(verticalSpeed) >= 1.0)
        pdCorrection = (float)(altitudeError * _altitudeCorrection - verticalSpeed * _altitudeDamping);

    // 0.001 keeps override active so dampeners cannot steal upward thrusters during descent.
    // Setting 0f releases the override back to the game, which is the bug that causes
    // slow/no descent and upward drift.
    float thrustFraction = Math.Max(0.001f, Math.Min(1f, hoverFraction + pdCorrection));

    // Speed limiter: applied after PD so it acts as a hard cap regardless of altitude error.
    // Braking gain scales with excess speed relative to the configured limit.
    if (verticalSpeed < -_altitudeMaxSpeed)
    {
        float excess = (float)(-verticalSpeed - _altitudeMaxSpeed);
        float brake  = Math.Min(1f, hoverFraction + excess / _altitudeMaxSpeed);
        if (brake > thrustFraction) thrustFraction = brake;
    }
    else if (verticalSpeed > _altitudeMaxSpeed)
    {
        float excess = (float)(verticalSpeed - _altitudeMaxSpeed);
        float limit  = Math.Max(0.001f, hoverFraction - excess / _altitudeMaxSpeed);
        if (limit < thrustFraction) thrustFraction = limit;
    }

    foreach (var t in _upThrusters)
        t.ThrustOverridePercentage = thrustFraction;

    string cur = currentAltitude.ToString("F0");
    string tgt = _altitudeTarget.ToString("F0");

    if (Math.Abs(altitudeError) < _altitudeThreshold)
        _altitudeStatus = "HOLD " + cur + "m";
    else if (altitudeError > 0)
        _altitudeStatus = "CLIMB " + cur + "/" + tgt + "m";
    else
        _altitudeStatus = "DESCEND " + cur + "/" + tgt + "m";
}

// -------------------------------------------------------------------------
// SET_ALTITUDE
// -------------------------------------------------------------------------

private void SetAltitudeFromCurrentPosition()
{
    if (_controller == null) InitBlocks();

    double currentAltitude;
    if (!_controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out currentAltitude))
    {
        Echo("SET_ALTITUDE failed: no planet elevation available");
        return;
    }

    _altitudeTarget = (float)currentAltitude;
    var ini = new MyIni();
    ini.TryParse(Me.CustomData);
    ini.Set(SEC_ALTITUDE, "target", _altitudeTarget);
    Me.CustomData = ini.ToString();
    Echo("Altitude target set to " + _altitudeTarget.ToString("F0") + "m");
}

// -------------------------------------------------------------------------
// Display — sprite helpers  (ported from AGM)
// -------------------------------------------------------------------------

private RectangleF VP(IMyTextSurface s)
{
    return new RectangleF((s.TextureSize - s.SurfaceSize) * 0.5f, s.SurfaceSize);
}

private RectangleF Inset(RectangleF r, float a)
{
    return new RectangleF(r.X + a, r.Y + a, r.Width - a * 2f, r.Height - a * 2f);
}

private void Fill(MySpriteDrawFrame fr, RectangleF r, Color c)
{
    fr.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple",
        r.Position + r.Size * 0.5f, r.Size, c));
}

private void DrawBorder(MySpriteDrawFrame fr, RectangleF r, Color c, float t)
{
    Fill(fr, new RectangleF(r.X,         r.Y,          r.Width, t), c);
    Fill(fr, new RectangleF(r.X,         r.Bottom - t, r.Width, t), c);
    Fill(fr, new RectangleF(r.X,         r.Y,          t, r.Height), c);
    Fill(fr, new RectangleF(r.Right - t, r.Y,          t, r.Height), c);
}

private void Txt(MySpriteDrawFrame fr, string text, float x, float y,
    Color c, float sc, TextAlignment al)
{
    fr.Add(new MySprite(SpriteType.TEXT, text ?? "",
        new Vector2(x, y), null, c, "Monospace", al, sc));
}

private void StatusRow(MySpriteDrawFrame fr, RectangleF panel, float y,
    string label, string value, Color valueColor)
{
    float lx = panel.X + 26f;
    float rx = panel.Right - 26f;
    Txt(fr, label, lx, y + 4f, COL_TEXT,    0.44f, TextAlignment.LEFT);
    Txt(fr, value, rx, y + 4f, valueColor,  0.44f, TextAlignment.RIGHT);
}

// -------------------------------------------------------------------------
// Display — boot screen
// -------------------------------------------------------------------------

private void DrawBoot(int phase)
{
    string label;
    int    percent;
    if      (phase <= 3)  { label = "INITIALISING...";    percent =   0; }
    else if (phase <= 6)  { label = "SCANNING BLOCKS..."; percent =  33; }
    else if (phase <= 9)  { label = "LOADING CONFIG...";  percent =  66; }
    else                  { label = "ALL SYSTEMS GO";     percent = 100; }

    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var cx  = vp.X + vp.Width  * 0.5f;
        var cy  = vp.Y + vp.Height * 0.5f;
        var pan = Inset(vp, 10f);

        float startY = cy - (percent == 100 ? 100f : 72f);

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);

            Txt(fr, "╔═╗ ╔╦╗ ╔═╗", cx, startY,        COL_ACCENT2, 0.72f, TextAlignment.CENTER);
            Txt(fr, "╠═╝  ║  ╠═╣",  cx, startY + 22f,  COL_ACCENT2, 0.72f, TextAlignment.CENTER);
            Txt(fr, "╩    ╩  ╩ ╩",  cx, startY + 44f,  COL_ACCENT2, 0.72f, TextAlignment.CENTER);
            Txt(fr, "Planetary Travel Assistant  v" + VERSION, cx, startY + 68f, COL_DIM, 0.38f, TextAlignment.CENTER);

            Fill(fr, new RectangleF(pan.X + 20f, startY + 84f, pan.Width - 40f, 1f), COL_ACCENT);

            Txt(fr, label, cx, startY + 94f, COL_TEXT, 0.44f, TextAlignment.CENTER);

            float barW = pan.Width - 60f;
            var   bar  = new RectangleF(cx - barW * 0.5f, startY + 116f, barW, 8f);
            Fill(fr, bar, COL_PROG_BG);
            if (percent > 0)
                Fill(fr, new RectangleF(bar.X, bar.Y, bar.Width * percent / 100f, bar.Height), COL_PROG_FG);
            DrawBorder(fr, bar, COL_DIM, 1f);

            if (percent == 100)
            {
                float iy = startY + 138f;
                Color ctrlCol = _controller != null ? COL_OK : COL_BAD;
                Txt(fr, "Controller : " + (_controller != null ? "OK" : "MISSING"), cx, iy,        ctrlCol,  0.38f, TextAlignment.CENTER);
                Txt(fr, "Gyros      : " + _gyros.Count,                             cx, iy + 18f,  COL_TEXT, 0.38f, TextAlignment.CENTER);
                Txt(fr, "Thrusters  : " + _thrusters.Count,                         cx, iy + 36f,  COL_TEXT, 0.38f, TextAlignment.CENTER);
                string bg = string.IsNullOrWhiteSpace(_brakeGroup) ? "auto-detect" : _brakeGroup;
                Txt(fr, "Brake grp  : " + bg,                                       cx, iy + 54f,  COL_DIM,  0.36f, TextAlignment.CENTER);
            }
        }
    }
}

// -------------------------------------------------------------------------
// Display — offline screen
// -------------------------------------------------------------------------

private void DrawOffline()
{
    foreach (var s in _surfaces)
    {
        var vp = VP(s);
        var cx = vp.X + vp.Width  * 0.5f;
        var cy = vp.Y + vp.Height * 0.5f;
        var pan = Inset(vp, 10f);
        float startY = cy - 70f;

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, COL_BAD, 3f);

            Txt(fr, "╔═╗ ╔╦╗ ╔═╗", cx, startY,        COL_ACCENT2, 0.72f, TextAlignment.CENTER);
            Txt(fr, "╠═╝  ║  ╠═╣",  cx, startY + 22f,  COL_ACCENT2, 0.72f, TextAlignment.CENTER);
            Txt(fr, "╩    ╩  ╩ ╩",  cx, startY + 44f,  COL_ACCENT2, 0.72f, TextAlignment.CENTER);

            Fill(fr, new RectangleF(pan.X + 10f, startY + 64f, pan.Width - 20f, 1f), COL_ACCENT);

            Txt(fr, "OFFLINE",                    cx, startY + 76f,  COL_BAD,  0.90f, TextAlignment.CENTER);
            Txt(fr, "Planetary Travel Assistant  v" + VERSION, cx, startY + 110f, COL_DIM,  0.36f, TextAlignment.CENTER);
        }
    }
}

// -------------------------------------------------------------------------
// Display — status screen
// -------------------------------------------------------------------------

private void DrawStatus()
{
    if (_controller == null) { DrawOffline(); return; }

    bool correcting =
        (_horizonActive  && _horizonStatus.StartsWith("CORRECTING")) ||
        (_altitudeActive && (_altitudeStatus.StartsWith("CLIMB") || _altitudeStatus.StartsWith("DESCEND")));
    bool anyActive = _horizonActive || _altitudeActive || _brakeThrusters.Count > 0;

    Color borderCol = correcting ? COL_WARN : anyActive ? COL_ACCENT : COL_DIM;

    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var pan = Inset(vp, 10f);
        float cx = vp.X + vp.Width * 0.5f;

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, borderCol, 3f);

            // Header
            Txt(fr, "PTA", pan.X + 20f, pan.Y + 14f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);
            if (pan.Width >= 350f)
                Txt(fr, "Planetary Travel Assistant", pan.Right - 20f, pan.Y + 20f, COL_DIM, 0.34f, TextAlignment.RIGHT);

            // Mode sub-header
            string modeLabel = GetModeLabel();
            if (modeLabel.Length > 0)
                Txt(fr, modeLabel, pan.X + 20f, pan.Y + 42f, COL_WARN, 0.44f, TextAlignment.LEFT);

            // Divider
            float dy = pan.Y + 58f;
            Fill(fr, new RectangleF(pan.X + 10f, dy, pan.Width - 20f, 1f), COL_ACCENT);
            dy += 14f;

            // Feature rows
            StatusRow(fr, pan, dy,
                "HOR",
                _horizonActive ? _horizonStatus : "OFF",
                _horizonActive ? HorizonColor() : COL_DIM);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            StatusRow(fr, pan, dy,
                "ALT",
                _altitudeActive ? _altitudeStatus : "OFF",
                _altitudeActive ? AltitudeColor() : COL_DIM);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            StatusRow(fr, pan, dy,
                "CRUISE",
                _brakeThrusters.Count > 0 ? _brakeThrusters.Count + " BRAKES OFF" : "OFF",
                _brakeThrusters.Count > 0 ? COL_OK : COL_DIM);

            // Footer
            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

private string GetModeLabel()
{
    if (_brakeThrusters.Count > 0) return "CRUISE";
    return "";
}

private Color HorizonColor()
{
    if (_horizonStatus.StartsWith("CORRECTING")) return COL_WARN;
    if (_horizonStatus == "ALIGNED")             return COL_OK;
    return COL_DIM;
}

private Color AltitudeColor()
{
    if (_altitudeStatus.StartsWith("CLIMB") || _altitudeStatus.StartsWith("DESCEND")) return COL_WARN;
    if (_altitudeStatus.StartsWith("HOLD"))                                            return COL_OK;
    return COL_DIM;
}

// -------------------------------------------------------------------------
// Config
// -------------------------------------------------------------------------

private void ParseConfig()
{
    if (string.IsNullOrWhiteSpace(Me.CustomData))
        WriteDefaultConfig();

    var ini = new MyIni();
    MyIniParseResult result;
    if (!ini.TryParse(Me.CustomData, out result))
        throw new Exception("Custom Data parse error at line " + result.LineNo);

    _horizonCorrection  = (float)ini.Get(SEC_HORIZON,  "correction").ToDouble(DEFAULT_HORIZON_CORRECTION);
    _horizonDamping     = (float)ini.Get(SEC_HORIZON,  "damping").ToDouble(DEFAULT_HORIZON_DAMPING);
    _horizonThreshold   = (float)ini.Get(SEC_HORIZON,  "threshold").ToDouble(DEFAULT_HORIZON_THRESHOLD);

    _altitudeTarget     = (float)ini.Get(SEC_ALTITUDE, "target").ToDouble(DEFAULT_ALTITUDE_TARGET);
    _altitudeCorrection = (float)ini.Get(SEC_ALTITUDE, "correction").ToDouble(DEFAULT_ALTITUDE_CORRECTION);
    _altitudeDamping    = (float)ini.Get(SEC_ALTITUDE, "damping").ToDouble(DEFAULT_ALTITUDE_DAMPING);
    _altitudeThreshold  = (float)ini.Get(SEC_ALTITUDE, "threshold").ToDouble(DEFAULT_ALTITUDE_THRESHOLD);
    _altitudeMaxSpeed   = (float)ini.Get(SEC_ALTITUDE, "max_speed").ToDouble(DEFAULT_ALTITUDE_MAX_SPEED);

    _brakeGroup    = ini.Get(SEC_CRUISE,  "brake_group").ToString(DEFAULT_BRAKE_GROUP);
    _cockpitScreen = ini.Get(SEC_DISPLAY, "cockpit_screen").ToInt32(DEFAULT_COCKPIT_SCREEN);
}

private void WriteDefaultConfig()
{
    var ini = new MyIni();
    ini.Set(SEC_HORIZON,  "correction",     DEFAULT_HORIZON_CORRECTION);
    ini.Set(SEC_HORIZON,  "damping",        DEFAULT_HORIZON_DAMPING);
    ini.Set(SEC_HORIZON,  "threshold",      DEFAULT_HORIZON_THRESHOLD);
    ini.Set(SEC_ALTITUDE, "target",         DEFAULT_ALTITUDE_TARGET);
    ini.Set(SEC_ALTITUDE, "correction",     DEFAULT_ALTITUDE_CORRECTION);
    ini.Set(SEC_ALTITUDE, "damping",        DEFAULT_ALTITUDE_DAMPING);
    ini.Set(SEC_ALTITUDE, "threshold",      DEFAULT_ALTITUDE_THRESHOLD);
    ini.Set(SEC_ALTITUDE, "max_speed",      DEFAULT_ALTITUDE_MAX_SPEED);
    ini.Set(SEC_CRUISE,   "brake_group",    DEFAULT_BRAKE_GROUP);
    ini.Set(SEC_DISPLAY,  "cockpit_screen", DEFAULT_COCKPIT_SCREEN);
    Me.CustomData = ini.ToString();
}

// -------------------------------------------------------------------------
// Block init
// -------------------------------------------------------------------------

private void InitBlocks()
{
    _controller = null;
    _gyros.Clear();
    _thrusters.Clear();

    var controllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(controllers, b => b.IsSameConstructAs(Me));
    foreach (var c in controllers)
    {
        if (c.IsMainCockpit) { _controller = c; break; }
    }
    if (_controller == null && controllers.Count > 0)
        _controller = controllers[0];

    GridTerminalSystem.GetBlocksOfType(_gyros,     b => b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(_thrusters, b => b.IsSameConstructAs(Me));

    InitSurfaces();

    if (_controller == null)
        throw new Exception("PTA: no ship controller found");
    if (_gyros.Count == 0)
        throw new Exception("PTA: no gyroscopes found");
}

private void InitSurfaces()
{
    _surfaces.Clear();

    _surfaces.Add(Me.GetSurface(0));

    var lcds = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType(lcds,
        b => b.IsSameConstructAs(Me) && b.CustomName.Contains("[PTA]"));
    foreach (var lcd in lcds)
    {
        var provider = lcd as IMyTextSurfaceProvider;
        if (provider != null) _surfaces.Add(provider.GetSurface(0));
    }

    var seats = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(seats,
        b => b.IsSameConstructAs(Me) && b.CustomName.Contains("[PTA]"));
    foreach (var seat in seats)
    {
        var provider = seat as IMyTextSurfaceProvider;
        if (provider == null) continue;
        if (_cockpitScreen < provider.SurfaceCount)
            _surfaces.Add(provider.GetSurface(_cockpitScreen));
    }

    foreach (var surface in _surfaces)
        ConfigureSurface(surface);
}

private void ConfigureSurface(IMyTextSurface s)
{
    s.ContentType           = ContentType.SCRIPT;
    s.Script                = "";
    s.BackgroundColor       = COL_BG;
    s.ScriptBackgroundColor = COL_BG;
    s.Font                  = "Monospace";
    s.FontSize              = 1.0f;
    s.TextPadding           = 1f;
}

// -------------------------------------------------------------------------
// Thruster helpers
// -------------------------------------------------------------------------

private void FindBrakeThrusters()
{
    _brakeThrusters.Clear();

    if (!string.IsNullOrWhiteSpace(_brakeGroup))
    {
        IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(_brakeGroup);
        if (group != null) group.GetBlocksOfType(_brakeThrusters);
        return;
    }

    Vector3D shipForward = _controller.WorldMatrix.Forward;
    foreach (var t in _thrusters)
    {
        Vector3D thrustDir = -t.WorldMatrix.Forward;
        if (Vector3D.Dot(thrustDir, -shipForward) > 0.7)
            _brakeThrusters.Add(t);
    }
}

private void FindUpThrusters(Vector3D gravity)
{
    Vector3D gravityDir = Vector3D.Normalize(gravity);
    _upThrusters.Clear();
    foreach (var t in _thrusters)
    {
        Vector3D thrustDir = -t.WorldMatrix.Forward;
        if (Vector3D.Dot(thrustDir, -gravityDir) > 0.7)
            _upThrusters.Add(t);
    }
}

// -------------------------------------------------------------------------
// Helpers
// -------------------------------------------------------------------------

private void ApplyUpdateFrequency()
{
    if (_altitudeActive)
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    else if (_horizonActive)
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    else
        Runtime.UpdateFrequency = UpdateFrequency.None;
}

private void ReleaseGyros()
{
    foreach (var gyro in _gyros)
    {
        gyro.Pitch        = 0f;
        gyro.Yaw          = 0f;
        gyro.Roll         = 0f;
        gyro.GyroOverride = false;
    }
}

private void ReleaseThrusters()
{
    foreach (var t in _thrusters)
        t.ThrustOverridePercentage = 0f;
}
