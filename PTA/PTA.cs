// -------------------------------------------------------------------------
// PTA — Planetary Travel Assistant
// Hotbar commands:
//   PTA_ON                     — initialise system, start fresh
//   PTA_OFF                    — stop everything, full manual control
//   HORIZON_ON / HORIZON_OFF   — keep ship level with horizon
//   ALTITUDE_ON / ALTITUDE_OFF — hold terrain-relative cruise altitude
//   SET_ALTITUDE               — lock current terrain altitude as target
//   SET_ALTITUDE <meters>      — set a specific altitude target (e.g. SET_ALTITUDE 2000)
//   CRUISE_ON / CRUISE_OFF     — disable brake thrusters, enable both features
//   ASCEND_ON / ASCEND_OFF     — climb to space at constant speed
//   DESCEND_ON / DESCEND_OFF   — descend to 3000m, keeping level
// Display: tag any LCD or cockpit with [PTA] in the name
// -------------------------------------------------------------------------

// -------------------------------------------------------------------------
// Config constants
// -------------------------------------------------------------------------

private const string SEC_HORIZON  = "horizon";
private const string SEC_ALTITUDE = "altitude";
private const string SEC_CRUISE   = "cruise";
private const string SEC_ASCEND   = "ascend";
private const string SEC_DESCEND  = "descend";
private const string SEC_DISPLAY  = "display";

private const float  DEFAULT_HORIZON_CORRECTION  = 0.5f;
private const float  DEFAULT_HORIZON_DAMPING     = 0.2f;
private const float  DEFAULT_HORIZON_THRESHOLD   = 0.02f;

private const float  DEFAULT_ALTITUDE_TARGET         = 1000f;
private const float  DEFAULT_ALTITUDE_CORRECTION     = 0.005f;
private const float  DEFAULT_ALTITUDE_DAMPING        = 0.01f;
private const float  DEFAULT_ALTITUDE_THRESHOLD      = 5f;
private const float  DEFAULT_ALTITUDE_MAX_SPEED      = 15f;
private const float  DEFAULT_ALTITUDE_PITCH_MAX      = 5f;
private const float  DEFAULT_ALTITUDE_PITCH_MIN_SPEED = 20f;
private const float  DEFAULT_ALTITUDE_PITCH_GAIN     = 0.002f;

private const float  DEFAULT_DESCEND_TARGET    = 3000f;

private const string DEFAULT_BRAKE_GROUP        = "";
private const string DEFAULT_ASCEND_UP_GROUP   = "";
private const string DEFAULT_ASCEND_DOWN_GROUP = "";
private const int    DEFAULT_COCKPIT_SCREEN     = 0;
private const string DEFAULT_THEME             = "cyber";

private const int    BOOT_TICKS = 12;
private const string VERSION   = "1.7";

// -------------------------------------------------------------------------
// Display colors  (mutable — overwritten by ApplyTheme on config load)
// Themes: cyber (default), amber, matrix, heat, royal
// -------------------------------------------------------------------------

private Color COL_BG      = new Color(  1,  8, 13);
private Color COL_PANEL   = new Color(  2, 18, 28);
private Color COL_PANEL2  = new Color(  3, 58, 78);
private Color COL_ACCENT  = new Color( 38,239,255);
private Color COL_ACCENT2 = new Color(112,247,255);
private Color COL_TEXT    = new Color(126,246,255);
private Color COL_DIM     = new Color( 44,177,195);
private Color COL_OK      = new Color( 97,255,214);
private Color COL_WARN    = new Color(255,202, 34);
private Color COL_BAD     = new Color(255, 79, 66);
private Color COL_PROG_BG = new Color( 18, 48, 32);
private Color COL_PROG_FG = new Color(255,204, 36);

// -------------------------------------------------------------------------
// Config fields
// -------------------------------------------------------------------------

private float  _horizonCorrection  = DEFAULT_HORIZON_CORRECTION;
private float  _horizonDamping     = DEFAULT_HORIZON_DAMPING;
private float  _horizonThreshold   = DEFAULT_HORIZON_THRESHOLD;

private float  _altitudeTarget        = DEFAULT_ALTITUDE_TARGET;
private float  _altitudeCorrection    = DEFAULT_ALTITUDE_CORRECTION;
private float  _altitudeDamping       = DEFAULT_ALTITUDE_DAMPING;
private float  _altitudeThreshold     = DEFAULT_ALTITUDE_THRESHOLD;
private float  _altitudeMaxSpeed      = DEFAULT_ALTITUDE_MAX_SPEED;
private float  _altitudePitchMax      = DEFAULT_ALTITUDE_PITCH_MAX;
private float  _altitudePitchMinSpeed = DEFAULT_ALTITUDE_PITCH_MIN_SPEED;
private float  _altitudePitchGain     = DEFAULT_ALTITUDE_PITCH_GAIN;

private string _brakeGroup       = DEFAULT_BRAKE_GROUP;
private string _ascendUpGroup   = DEFAULT_ASCEND_UP_GROUP;
private string _ascendDownGroup = DEFAULT_ASCEND_DOWN_GROUP;
private float  _descendTarget  = DEFAULT_DESCEND_TARGET;
private int    _cockpitScreen   = DEFAULT_COCKPIT_SCREEN;
private string _theme           = DEFAULT_THEME;

// -------------------------------------------------------------------------
// State
// -------------------------------------------------------------------------

private bool   _horizonActive        = false;
private bool   _altitudeActive       = false;
private bool   _ascendActive         = false;
private bool   _descendActive        = false;
private int    _bootPhase            = 0;
private float  _desiredPitchOffset   = 0f;
private string _horizonStatus        = "---";
private string _altitudeStatus       = "---";
private string _ascendStatus         = "---";
private string _descendStatus        = "---";
private bool   _flashActive   = false;
private string _flashTitle    = "";
private string _flashSubtitle = "";
private Color  _flashColor    = new Color(97, 255, 214);
private int    _flashTicks    = 0;

// -------------------------------------------------------------------------
// Blocks
// -------------------------------------------------------------------------

private IMyShipController             _controller;
private readonly List<IMyGyro>        _gyros          = new List<IMyGyro>();
private readonly List<IMyThrust>      _thrusters      = new List<IMyThrust>();
private readonly List<IMyThrust>      _upThrusters    = new List<IMyThrust>();
private readonly List<IMyThrust>      _brakeThrusters = new List<IMyThrust>();
private readonly List<IMyTextSurface> _surfaces            = new List<IMyTextSurface>();
private readonly List<string>         _ascendIssues        = new List<string>();
private readonly List<IMyThrust>      _ascendUpThrusters   = new List<IMyThrust>();
private readonly List<IMyThrust>      _ascendDownThrusters = new List<IMyThrust>();
private readonly List<IMyGasTank>     _hydroTanks          = new List<IMyGasTank>();
private readonly List<string>         _descendIssues       = new List<string>();
private readonly List<IMyThrust>      _descendUpThrusters  = new List<IMyThrust>();

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
    if (argument == "SET_ALTITUDE" || argument.StartsWith("SET_ALTITUDE "))
    {
        string suffix = argument.Length > "SET_ALTITUDE".Length
            ? argument.Substring("SET_ALTITUDE".Length).Trim()
            : "";
        float parsed;
        if (suffix.Length > 0 && float.TryParse(suffix, out parsed))
            SetAltitudeTo(parsed);
        else
            SetAltitudeFromCurrentPosition();
        DrawStatus();
        return;
    }

    switch (argument)
    {
        case "PTA_ON":
            ParseConfig();
            InitBlocks();
            _horizonActive  = false;
            _altitudeActive = false;
            _ascendActive   = false;
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
            _desiredPitchOffset = 0f;
            if (_ascendActive)
            {
                foreach (var t in _ascendUpThrusters)   { t.ThrustOverridePercentage = 0f; t.Enabled = true; }
                foreach (var t in _ascendDownThrusters)   t.Enabled = true;
                _ascendActive = false;
            }
            if (_descendActive)
            {
                foreach (var t in _descendUpThrusters) t.ThrustOverridePercentage = 0f;
                _descendUpThrusters.Clear();
                _descendActive = false;
            }
            Runtime.UpdateFrequency = UpdateFrequency.None;
            DrawOffline();
            return;

        case "HORIZON_ON":
            if (_descendActive) { Echo("HORIZON_ON blocked: descend mode active"); DrawStatus(); return; }
            if (BlockedByGroupMode("HORIZON_ON")) return;
            ParseConfig();
            InitBlocks();
            _horizonActive = true;
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "HORIZON_OFF":
            if (_descendActive) { Echo("HORIZON_OFF blocked: descend mode active"); DrawStatus(); return; }
            if (BlockedByGroupMode("HORIZON_OFF")) return;
            _horizonActive = false;
            ReleaseGyros();
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "ALTITUDE_ON":
            if (_descendActive) { Echo("ALTITUDE_ON blocked: descend mode active"); DrawStatus(); return; }
            if (BlockedByGroupMode("ALTITUDE_ON")) return;
            ParseConfig();
            InitBlocks();
            _altitudeActive = true;
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "ALTITUDE_OFF":
            if (_descendActive) { Echo("ALTITUDE_OFF blocked: descend mode active"); DrawStatus(); return; }
            if (BlockedByGroupMode("ALTITUDE_OFF")) return;
            _altitudeActive     = false;
            _desiredPitchOffset = 0f;
            ReleaseThrusters();
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "CRUISE_ON":
        {
            if (_ascendActive) { Echo("CRUISE_ON blocked: ascend mode active"); DrawStatus(); return; }
            if (_descendActive) { Echo("CRUISE_ON blocked: descend mode active"); DrawStatus(); return; }
            ParseConfig();
            InitBlocks();
            FindBrakeThrusters();
            foreach (var t in _brakeThrusters) t.Enabled = false;
            bool inGravity  = _controller.GetNaturalGravity().LengthSquared() > 0.001;
            _horizonActive  = inGravity;
            _altitudeActive = inGravity;
            ApplyUpdateFrequency();
            DrawStatus();
            return;
        }

        case "CRUISE_OFF":
            if (_ascendActive) { Echo("CRUISE_OFF blocked: ascend mode active"); DrawStatus(); return; }
            if (_descendActive) { Echo("CRUISE_OFF blocked: descend mode active"); DrawStatus(); return; }
            _horizonActive      = false;
            _altitudeActive     = false;
            _desiredPitchOffset = 0f;
            ReleaseGyros();
            ReleaseThrusters();
            foreach (var t in _brakeThrusters) t.Enabled = true;
            _brakeThrusters.Clear();
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "ASCEND_ON":
        {
            if (_brakeThrusters.Count > 0) { Echo("ASCEND_ON blocked: cruise mode active"); DrawStatus(); return; }
            ParseConfig();
            InitBlocks();
            if (!CheckAscendRequirements()) { DrawAscendUnavailable(); return; }
            InitAscend();
            _ascendActive = true;
            ApplyUpdateFrequency();
            DrawStatus();
            return;
        }

        case "ASCEND_OFF":
            CompleteAscend(manual: true);
            return;

        case "DESCEND_ON":
        {
            if (_ascendActive)             { Echo("DESCEND_ON blocked: ascend mode active");  DrawStatus(); return; }
            if (_brakeThrusters.Count > 0) { Echo("DESCEND_ON blocked: cruise mode active"); DrawStatus(); return; }
            ParseConfig();
            InitBlocks();
            if (!CheckDescendRequirements()) { DrawDescendUnavailable(); return; }
            InitDescend();
            _descendActive = true;
            ApplyUpdateFrequency();
            DrawStatus();
            return;
        }

        case "DESCEND_OFF":
            CompleteDescend(manual: true);
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

    // Dismiss flash message after N ticks when features are running
    if (_flashActive && _flashTicks > 0)
    {
        if (--_flashTicks == 0) _flashActive = false;
    }

    // Feature ticks — altitude runs first so pitch offset is set before horizon reads it
    if (_altitudeActive) AltitudeTick();
    if (_horizonActive)  HorizonTick();
    if (_ascendActive)   AscendTick();
    if (_descendActive)  DescendTick();
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

    Vector3D trueUp    = -Vector3D.Normalize(gravity);
    Vector3D desiredUp = trueUp;

    if (Math.Abs(_desiredPitchOffset) > 0.001f)
    {
        Vector3D forward  = _controller.WorldMatrix.Forward;
        Vector3D horizFwd = forward - Vector3D.Dot(forward, trueUp) * trueUp;
        if (horizFwd.LengthSquared() > 0.001)
        {
            horizFwd  = Vector3D.Normalize(horizFwd);
            double cosP = Math.Cos(_desiredPitchOffset);
            double sinP = Math.Sin(_desiredPitchOffset);
            desiredUp = trueUp * cosP + horizFwd * sinP;
        }
    }

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

    float pdCorrection      = 0f;
    bool  usingPitchDescent = false;

    if (altitudeError < -_altitudeThreshold && _horizonActive)
    {
        double speed = _controller.GetShipVelocities().LinearVelocity.Length();
        if (speed >= _altitudePitchMinSpeed && _altitudePitchMax > 0.001f)
        {
            double maxPitchRad  = _altitudePitchMax * Math.PI / 180.0;
            double pitchAngle   = Math.Min(-altitudeError * _altitudePitchGain, maxPitchRad);
            _desiredPitchOffset = (float)pitchAngle;
            usingPitchDescent   = true;
        }
    }

    if (!usingPitchDescent)
    {
        _desiredPitchOffset = 0f;
        if (Math.Abs(altitudeError) >= _altitudeThreshold || Math.Abs(verticalSpeed) >= 1.0)
            pdCorrection = (float)(altitudeError * _altitudeCorrection - verticalSpeed * _altitudeDamping);
    }

    // 0.001 keeps override active so dampeners cannot steal upward thrusters during descent.
    // Setting 0f releases the override back to the game, which is the bug that causes
    // slow/no descent and upward drift.
    float thrustFraction = usingPitchDescent
        ? Math.Max(0.001f, hoverFraction)
        : Math.Max(0.001f, Math.Min(1f, hoverFraction + pdCorrection));

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
    else if (usingPitchDescent)
        _altitudeStatus = "GLIDE " + cur + "/" + tgt + "m";
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

    SetAltitudeTo((float)currentAltitude);
}

private void ShowFlash(string title, string subtitle, Color color, int ticks = 5)
{
    _flashActive   = true;
    _flashTitle    = title;
    _flashSubtitle = subtitle;
    _flashColor    = color;
    _flashTicks    = ticks;
}

private void SetAltitudeTo(float target)
{
    _altitudeTarget = target;
    ShowFlash("TARGET ALTITUDE SET", target.ToString("F0") + " m", COL_OK);
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

private void DrawAscendStatus()
{
    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var pan = Inset(vp, 10f);
        float cx = vp.X + vp.Width * 0.5f;

        double altitude = 0;
        bool   hasAlt   = _controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);
        double gravity  = _controller.GetNaturalGravity().Length();
        double speed    = _controller.GetShipVelocities().LinearVelocity.Length();
        bool   thrusting = _ascendUpThrusters.Count > 0 &&
                           _ascendUpThrusters[0].ThrustOverridePercentage > 0.5f;

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, COL_ACCENT, 3f);

            Txt(fr, "PTA",    pan.X + 20f,    pan.Y + 14f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);
            Txt(fr, "ASCEND", pan.Right - 20f, pan.Y + 20f, COL_WARN,   0.44f, TextAlignment.RIGHT);

            float dy = pan.Y + 52f;
            Fill(fr, new RectangleF(pan.X + 10f, dy, pan.Width - 20f, 1f), COL_ACCENT);
            dy += 14f;

            StatusRow(fr, pan, dy, "ALT",
                hasAlt ? altitude.ToString("F0") + "m" : "---",
                COL_TEXT);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            StatusRow(fr, pan, dy, "GRAVITY",
                gravity.ToString("F2") + " m/s2",
                COL_TEXT);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            string speedStr = speed.ToString("F0") + "m/s  " + (thrusting ? "THRUST" : "COAST");
            StatusRow(fr, pan, dy, "SPEED", speedStr, thrusting ? COL_OK : COL_WARN);

            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

private void DrawDescendStatus()
{
    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var pan = Inset(vp, 10f);
        float cx = vp.X + vp.Width * 0.5f;

        double altitude = 0;
        bool   hasAlt   = _controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);
        double gravity  = _controller.GetNaturalGravity().Length();
        double speed    = _controller.GetShipVelocities().LinearVelocity.Length();

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, COL_ACCENT, 3f);

            Txt(fr, "PTA",     pan.X + 20f,    pan.Y + 14f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);
            Txt(fr, "DESCEND", pan.Right - 20f, pan.Y + 20f, COL_WARN,   0.44f, TextAlignment.RIGHT);

            float dy = pan.Y + 52f;
            Fill(fr, new RectangleF(pan.X + 10f, dy, pan.Width - 20f, 1f), COL_ACCENT);
            dy += 14f;

            StatusRow(fr, pan, dy, "ALT",
                hasAlt ? altitude.ToString("F0") + "m" : "---",
                COL_TEXT);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            StatusRow(fr, pan, dy, "GRAVITY",
                gravity.ToString("F2") + " m/s2",
                COL_TEXT);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            string speedStr = speed.ToString("F0") + "m/s";
            StatusRow(fr, pan, dy, "SPEED", speedStr, COL_TEXT);

            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

private void DrawFlash()
{
    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var pan = Inset(vp, 10f);
        float cx = vp.X + vp.Width * 0.5f;

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, _flashColor, 3f);

            Txt(fr, "PTA", pan.X + 20f, pan.Y + 14f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);

            float dy = pan.Y + 52f;
            Fill(fr, new RectangleF(pan.X + 10f, dy, pan.Width - 20f, 1f), COL_ACCENT);
            dy += 28f;

            Txt(fr, _flashTitle, cx, dy, COL_TEXT, 0.40f, TextAlignment.CENTER);
            dy += 50f;

            Txt(fr, _flashSubtitle, cx, dy, _flashColor, 1.0f, TextAlignment.CENTER);

            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

private void DrawStatus()
{
    if (_controller == null) { DrawOffline(); return; }
    if (_ascendActive)       { DrawAscendStatus(); return; }
    if (_descendActive)      { DrawDescendStatus(); return; }
    if (_flashActive)        { DrawFlash(); return; }

    bool correcting =
        (_horizonActive  && _horizonStatus.StartsWith("CORRECTING")) ||
        (_altitudeActive && (_altitudeStatus.StartsWith("CLIMB") || _altitudeStatus.StartsWith("DESCEND") || _altitudeStatus.StartsWith("GLIDE")));
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
            string modeLabel = GetModeLabel();
            if (modeLabel.Length > 0)
                Txt(fr, modeLabel, pan.Right - 20f, pan.Y + 20f, COL_WARN, 0.44f, TextAlignment.RIGHT);

            // Divider
            float dy = pan.Y + 52f;
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
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            StatusRow(fr, pan, dy,
                "ASCEND",
                _ascendActive ? _ascendStatus : "OFF",
                _ascendActive ? COL_OK : COL_DIM);

            // Footer
            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

private bool BlockedByGroupMode(string cmd)
{
    if (_ascendActive)             { Echo(cmd + " blocked: ascend mode active");  DrawStatus(); return true; }
    if (_descendActive)            { Echo(cmd + " blocked: descend mode active"); DrawStatus(); return true; }
    if (_brakeThrusters.Count > 0) { Echo(cmd + " blocked: cruise mode active");  DrawStatus(); return true; }
    return false;
}

private string GetModeLabel()
{
    if (_ascendActive)             return "ASCEND";
    if (_descendActive)            return "DESCEND";
    if (_brakeThrusters.Count > 0) return "CRUISE";
    return "";
}

// -------------------------------------------------------------------------
// Ascend — requirements check and unavailable screen
// -------------------------------------------------------------------------

private bool CheckAscendRequirements()
{
    _ascendIssues.Clear();

    if (string.IsNullOrWhiteSpace(_ascendUpGroup))
        _ascendIssues.Add("no up_group set");

    if (string.IsNullOrWhiteSpace(_ascendDownGroup))
        _ascendIssues.Add("no down_group set");

    var hydroThrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType(hydroThrusters,
        b => b.IsSameConstructAs(Me) && b.DefinitionDisplayNameText.Contains("Hydrogen"));
    if (hydroThrusters.Count == 0)
        _ascendIssues.Add("No Hydro Thrusters");

    _hydroTanks.Clear();
    GridTerminalSystem.GetBlocksOfType(_hydroTanks,
        b => b.IsSameConstructAs(Me) && b.DefinitionDisplayNameText.Contains("Hydrogen"));
    if (_hydroTanks.Count == 0)
    {
        _ascendIssues.Add("No Tanks");
    }
    else
    {
        bool hasFuel = false;
        foreach (var tank in _hydroTanks)
            if (tank.FilledRatio > 0.001) { hasFuel = true; break; }
        if (!hasFuel)
            _ascendIssues.Add("No gas in tanks");
    }

    return _ascendIssues.Count == 0;
}

private void DrawAscendUnavailable()
{
    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var pan = Inset(vp, 10f);
        float cx = vp.X + vp.Width * 0.5f;

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, COL_WARN, 3f);

            Txt(fr, "PTA",    pan.X + 20f,    pan.Y + 14f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);
            Txt(fr, "ASCEND", pan.Right - 20f, pan.Y + 20f, COL_BAD,    0.44f, TextAlignment.RIGHT);

            float dy = pan.Y + 52f;
            Fill(fr, new RectangleF(pan.X + 10f, dy, pan.Width - 20f, 1f), COL_ACCENT);
            dy += 22f;

            Txt(fr, "ASCEND MODE UNAVAILABLE", cx, dy, COL_WARN, 0.44f, TextAlignment.CENTER);
            dy += 28f;

            float bx = pan.X + 40f;
            foreach (var reason in _ascendIssues)
            {
                Txt(fr, "• " + reason, bx, dy, COL_DIM, 0.38f, TextAlignment.LEFT);
                dy += 22f;
            }

            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

// -------------------------------------------------------------------------
// Ascend — init, tick, complete
// -------------------------------------------------------------------------

private void InitAscend()
{
    // Tanks: ensure stockpile is off so hydrogen flows to thrusters freely
    foreach (var tank in _hydroTanks)
        tank.Stockpile = false;

    // Populate thruster groups
    _ascendUpThrusters.Clear();
    _ascendDownThrusters.Clear();
    IMyBlockGroup upGroup = GridTerminalSystem.GetBlockGroupWithName(_ascendUpGroup);
    if (upGroup != null) upGroup.GetBlocksOfType(_ascendUpThrusters);
    IMyBlockGroup downGroup = GridTerminalSystem.GetBlockGroupWithName(_ascendDownGroup);
    if (downGroup != null) downGroup.GetBlocksOfType(_ascendDownThrusters);

    // Keep ship level during climb
    _horizonActive = true;

    // Disable down thrusters
    foreach (var t in _ascendDownThrusters)
        t.Enabled = false;

    // Up thrusters: full override
    foreach (var t in _ascendUpThrusters)
    {
        t.Enabled = true;
        t.ThrustOverridePercentage = 1f;
    }

    _ascendStatus = "LAUNCH";
}

private void AscendTick()
{
    if (_controller == null) return;

    // Completion: gravity near zero means we've cleared the atmosphere
    double gravity = _controller.GetNaturalGravity().Length();
    if (gravity < 0.04)
    {
        CompleteAscend();
        return;
    }

    // Bang-bang speed limiter at 95 m/s
    double speed      = _controller.GetShipVelocities().LinearVelocity.Length();
    float  thrustPct  = speed > 95.0 ? 0.001f : 1f;
    foreach (var t in _ascendUpThrusters)
        t.ThrustOverridePercentage = thrustPct;

    string phase = thrustPct > 0.5f ? "THRUST" : "COAST";
    _ascendStatus = phase + " " + speed.ToString("F0") + "m/s  g:" + gravity.ToString("F2");
}

private void CompleteAscend(bool manual = false)
{
    foreach (var t in _ascendUpThrusters)   { t.ThrustOverridePercentage = 0f; t.Enabled = true; }
    foreach (var t in _ascendDownThrusters)   t.Enabled = true;
    _ascendActive = false;
    _ascendStatus = "---";
    if (manual) ShowFlash("ASCEND ABORTED",   "",               COL_WARN, 8);
    else        ShowFlash("ASCEND COMPLETE",   "ORBIT REACHED",  COL_OK,   8);
    ApplyUpdateFrequency();
    DrawStatus();
}

private Color HorizonColor()
{
    if (_horizonStatus.StartsWith("CORRECTING")) return COL_WARN;
    if (_horizonStatus == "ALIGNED")             return COL_OK;
    return COL_DIM;
}

private Color AltitudeColor()
{
    if (_altitudeStatus.StartsWith("CLIMB") || _altitudeStatus.StartsWith("DESCEND") || _altitudeStatus.StartsWith("GLIDE")) return COL_WARN;
    if (_altitudeStatus.StartsWith("HOLD"))                                            return COL_OK;
    return COL_DIM;
}

// -------------------------------------------------------------------------
// Descend — requirements check and unavailable screen
// -------------------------------------------------------------------------

private bool CheckDescendRequirements()
{
    _descendIssues.Clear();

    if (_controller.GetNaturalGravity().LengthSquared() < 0.001)
        _descendIssues.Add("no gravity (in space)");

    if (string.IsNullOrWhiteSpace(_ascendUpGroup))
        _descendIssues.Add("no up_group set in config");
    else
    {
        var tempList = new List<IMyThrust>();
        IMyBlockGroup upGroup = GridTerminalSystem.GetBlockGroupWithName(_ascendUpGroup);
        if (upGroup == null)
            _descendIssues.Add("up_group not found: " + _ascendUpGroup);
        else
        {
            upGroup.GetBlocksOfType(tempList);
            if (tempList.Count == 0)
                _descendIssues.Add("up_group has no thrusters");
        }
    }

    return _descendIssues.Count == 0;
}

private void DrawDescendUnavailable()
{
    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var pan = Inset(vp, 10f);
        float cx = vp.X + vp.Width * 0.5f;

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, COL_WARN, 3f);

            Txt(fr, "PTA",     pan.X + 20f,    pan.Y + 14f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);
            Txt(fr, "DESCEND", pan.Right - 20f, pan.Y + 20f, COL_BAD,    0.44f, TextAlignment.RIGHT);

            float dy = pan.Y + 52f;
            Fill(fr, new RectangleF(pan.X + 10f, dy, pan.Width - 20f, 1f), COL_ACCENT);
            dy += 22f;

            Txt(fr, "DESCEND MODE UNAVAILABLE", cx, dy, COL_WARN, 0.44f, TextAlignment.CENTER);
            dy += 28f;

            float bx = pan.X + 40f;
            foreach (var reason in _descendIssues)
            {
                Txt(fr, "• " + reason, bx, dy, COL_DIM, 0.38f, TextAlignment.LEFT);
                dy += 22f;
            }

            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

// -------------------------------------------------------------------------
// Descend — init, tick, complete
// -------------------------------------------------------------------------

private void InitDescend()
{
    _descendUpThrusters.Clear();
    IMyBlockGroup upGroup = GridTerminalSystem.GetBlockGroupWithName(_ascendUpGroup);
    if (upGroup != null) upGroup.GetBlocksOfType(_descendUpThrusters);

    // 0.001f keeps override active so dampeners cannot fire the up thrusters.
    // Setting 0f releases the override back to the game, causing dampeners to
    // counteract gravity and prevent descent.
    foreach (var t in _descendUpThrusters)
        t.ThrustOverridePercentage = 0.001f;

    _altitudeActive = false;
    _horizonActive  = true;
    _descendStatus  = "DESCENDING";
}

private void DescendTick()
{
    if (_controller == null) return;

    double altitude;
    if (!_controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude))
    {
        _descendStatus = "NO SURFACE";
        return;
    }

    if (altitude <= _descendTarget)
    {
        CompleteDescend();
        return;
    }

    // Re-apply every tick so the override cannot be reclaimed by the game
    foreach (var t in _descendUpThrusters)
        t.ThrustOverridePercentage = 0.001f;

    double speed = _controller.GetShipVelocities().LinearVelocity.Length();
    _descendStatus = "FALLING " + altitude.ToString("F0") + "m  " + speed.ToString("F0") + "m/s";
}

private void CompleteDescend(bool manual = false)
{
    foreach (var t in _descendUpThrusters)
        t.ThrustOverridePercentage = 0f;
    _descendUpThrusters.Clear();
    _horizonActive = false;
    ReleaseGyros();
    _descendActive = false;
    _descendStatus = "---";
    if (manual) ShowFlash("DESCEND ABORTED",   "",                                              COL_WARN, 8);
    else        ShowFlash("DESCEND COMPLETE",   _descendTarget.ToString("F0") + " m — MANUAL CONTROL", COL_OK, 8);
    ApplyUpdateFrequency();
    DrawStatus();
}

// -------------------------------------------------------------------------
// Config
// -------------------------------------------------------------------------

private void ParseConfig()
{
    var ini = new MyIni();
    if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        MyIniParseResult result;
        if (!ini.TryParse(Me.CustomData, out result))
            throw new Exception("Custom Data parse error at line " + result.LineNo);
    }

    bool dirty = false;

    dirty |= EnsureFloat (ini, SEC_HORIZON,  "correction",      DEFAULT_HORIZON_CORRECTION,      ref _horizonCorrection);
    dirty |= EnsureFloat (ini, SEC_HORIZON,  "damping",         DEFAULT_HORIZON_DAMPING,         ref _horizonDamping);
    dirty |= EnsureFloat (ini, SEC_HORIZON,  "threshold",       DEFAULT_HORIZON_THRESHOLD,       ref _horizonThreshold);

    dirty |= EnsureFloat (ini, SEC_ALTITUDE, "target",          DEFAULT_ALTITUDE_TARGET,         ref _altitudeTarget);
    dirty |= EnsureFloat (ini, SEC_ALTITUDE, "correction",      DEFAULT_ALTITUDE_CORRECTION,     ref _altitudeCorrection);
    dirty |= EnsureFloat (ini, SEC_ALTITUDE, "damping",         DEFAULT_ALTITUDE_DAMPING,        ref _altitudeDamping);
    dirty |= EnsureFloat (ini, SEC_ALTITUDE, "threshold",       DEFAULT_ALTITUDE_THRESHOLD,      ref _altitudeThreshold);
    dirty |= EnsureFloat (ini, SEC_ALTITUDE, "max_speed",       DEFAULT_ALTITUDE_MAX_SPEED,      ref _altitudeMaxSpeed);
    dirty |= EnsureFloat (ini, SEC_ALTITUDE, "pitch_max",       DEFAULT_ALTITUDE_PITCH_MAX,      ref _altitudePitchMax);
    dirty |= EnsureFloat (ini, SEC_ALTITUDE, "pitch_min_speed", DEFAULT_ALTITUDE_PITCH_MIN_SPEED,ref _altitudePitchMinSpeed);
    dirty |= EnsureFloat (ini, SEC_ALTITUDE, "pitch_gain",      DEFAULT_ALTITUDE_PITCH_GAIN,     ref _altitudePitchGain);

    dirty |= EnsureString(ini, SEC_CRUISE,   "brake_group",     DEFAULT_BRAKE_GROUP,             ref _brakeGroup);
    dirty |= EnsureString(ini, SEC_ASCEND,   "up_group",        DEFAULT_ASCEND_UP_GROUP,         ref _ascendUpGroup);
    dirty |= EnsureString(ini, SEC_ASCEND,   "down_group",      DEFAULT_ASCEND_DOWN_GROUP,       ref _ascendDownGroup);
    dirty |= EnsureFloat (ini, SEC_DESCEND,  "target",          DEFAULT_DESCEND_TARGET,          ref _descendTarget);
    dirty |= EnsureInt   (ini, SEC_DISPLAY,  "cockpit_screen",  DEFAULT_COCKPIT_SCREEN,          ref _cockpitScreen);
    dirty |= EnsureString(ini, SEC_DISPLAY,  "theme",           DEFAULT_THEME,                   ref _theme);

    ApplyTheme(_theme);

    if (dirty) Me.CustomData = ini.ToString();
}

private void ApplyTheme(string name)
{
    switch (name.ToLower())
    {
        case "amber":
            COL_BG      = new Color( 10,  5,  0);
            COL_PANEL   = new Color( 22, 10,  0);
            COL_PANEL2  = new Color( 55, 25,  0);
            COL_ACCENT  = new Color(255,160,  0);
            COL_ACCENT2 = new Color(255,210, 80);
            COL_TEXT    = new Color(255,185, 55);
            COL_DIM     = new Color(160,100,  0);
            COL_OK      = new Color(180,255, 80);
            COL_WARN    = new Color(255,230,  0);
            COL_BAD     = new Color(255, 60, 30);
            COL_PROG_BG = new Color( 20, 10,  0);
            COL_PROG_FG = new Color(255,160,  0);
            break;
        case "matrix":
            COL_BG      = new Color(  0,  5,  0);
            COL_PANEL   = new Color(  0, 14,  0);
            COL_PANEL2  = new Color(  0, 40,  0);
            COL_ACCENT  = new Color(  0,220,  0);
            COL_ACCENT2 = new Color(100,255,100);
            COL_TEXT    = new Color( 80,240, 80);
            COL_DIM     = new Color(  0,120,  0);
            COL_OK      = new Color(150,255,100);
            COL_WARN    = new Color(255,200,  0);
            COL_BAD     = new Color(255, 60, 60);
            COL_PROG_BG = new Color(  0, 20,  0);
            COL_PROG_FG = new Color(  0,220,  0);
            break;
        case "heat":
            COL_BG      = new Color( 10,  2,  0);
            COL_PANEL   = new Color( 24,  6,  0);
            COL_PANEL2  = new Color( 55, 16,  0);
            COL_ACCENT  = new Color(255,100,  0);
            COL_ACCENT2 = new Color(255,165, 55);
            COL_TEXT    = new Color(255,145, 65);
            COL_DIM     = new Color(165, 62,  0);
            COL_OK      = new Color(100,255,160);
            COL_WARN    = new Color(255,220,  0);
            COL_BAD     = new Color(255, 50, 30);
            COL_PROG_BG = new Color( 22,  5,  0);
            COL_PROG_FG = new Color(255,100,  0);
            break;
        case "royal":
            COL_BG      = new Color(  6,  2, 14);
            COL_PANEL   = new Color( 13,  5, 28);
            COL_PANEL2  = new Color( 32, 12, 65);
            COL_ACCENT  = new Color(185, 80,255);
            COL_ACCENT2 = new Color(215,145,255);
            COL_TEXT    = new Color(205,155,255);
            COL_DIM     = new Color(115, 62,165);
            COL_OK      = new Color(100,255,165);
            COL_WARN    = new Color(255,200, 80);
            COL_BAD     = new Color(255, 80, 80);
            COL_PROG_BG = new Color( 13,  5, 28);
            COL_PROG_FG = new Color(185, 80,255);
            break;
        default: // cyber
            COL_BG      = new Color(  1,  8, 13);
            COL_PANEL   = new Color(  2, 18, 28);
            COL_PANEL2  = new Color(  3, 58, 78);
            COL_ACCENT  = new Color( 38,239,255);
            COL_ACCENT2 = new Color(112,247,255);
            COL_TEXT    = new Color(126,246,255);
            COL_DIM     = new Color( 44,177,195);
            COL_OK      = new Color( 97,255,214);
            COL_WARN    = new Color(255,202, 34);
            COL_BAD     = new Color(255, 79, 66);
            COL_PROG_BG = new Color( 18, 48, 32);
            COL_PROG_FG = new Color(255,204, 36);
            break;
    }
}

private bool EnsureFloat(MyIni ini, string sec, string key, float def, ref float field)
{
    if (!ini.ContainsKey(sec, key)) { ini.Set(sec, key, def); field = def; return true; }
    field = (float)ini.Get(sec, key).ToDouble(def);
    return false;
}

private bool EnsureString(MyIni ini, string sec, string key, string def, ref string field)
{
    if (!ini.ContainsKey(sec, key)) { ini.Set(sec, key, def); field = def; return true; }
    field = ini.Get(sec, key).ToString(def);
    return false;
}

private bool EnsureInt(MyIni ini, string sec, string key, int def, ref int field)
{
    if (!ini.ContainsKey(sec, key)) { ini.Set(sec, key, def); field = def; return true; }
    field = ini.Get(sec, key).ToInt32(def);
    return false;
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
    if (_altitudeActive || _ascendActive || _descendActive)
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
