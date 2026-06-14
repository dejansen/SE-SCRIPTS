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
//   SAVE <name>                — while connected, save dock target under name
//   DOCK <name>                — fly to and connect to saved dock target
//   ABORT                      — abort any active feature, return to standby
// Display: tag any LCD or cockpit with [PTA] in the name
// Docking: tag connectors with [PTA_DOCK] to mark them as dock connectors
//          (falls back to all connectors if none are tagged)
// -------------------------------------------------------------------------

// -------------------------------------------------------------------------
// Config constants
// -------------------------------------------------------------------------

private const string SEC_HORIZON   = "horizon";
private const string SEC_ALTITUDE  = "altitude";
private const string SEC_CRUISE    = "cruise";
private const string SEC_ASCEND    = "ascend";
private const string SEC_DESCEND   = "descend";
private const string SEC_DISPLAY   = "display";
private const string SEC_DOCK      = "dock";
private const string SEC_BROADCAST = "broadcast";

private const bool   DEFAULT_BC_ENABLED         = true;
private const int    DEFAULT_BC_INDEX           = 8;
private const string DEFAULT_BC_PTA_ON          = "Planetary Travel Assistant online";
private const string DEFAULT_BC_PTA_OFF         = "Planetary Travel Assistant offline";
private const string DEFAULT_BC_CRUISE_ON       = "Cruise activated";
private const string DEFAULT_BC_CRUISE_OFF      = "Cruise off";
private const string DEFAULT_BC_ASCEND_ON       = "Ascending to orbit";
private const string DEFAULT_BC_ASCEND_COMPLETE = "Orbit reached";
private const string DEFAULT_BC_ASCEND_ABORT    = "Ascend aborted";
private const string DEFAULT_BC_DESCEND_ON      = "Descending";
private const string DEFAULT_BC_DESCEND_COMPLETE= "Descent complete";
private const string DEFAULT_BC_DESCEND_ABORT   = "Descend aborted";
private const string DEFAULT_BC_DOCK_START      = "Initiating docking";
private const string DEFAULT_BC_DOCK_COMPLETE   = "Docking complete";
private const string DEFAULT_BC_DOCK_ABORT      = "Docking aborted";

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

private const float  DEFAULT_DOCK_APPROACH_SPEED    = 10f;
private const float  DEFAULT_DOCK_FINAL_SPEED       = 1.5f;
private const float  DEFAULT_DOCK_WAYPOINT_DISTANCE = 15f;
private const float  DOCK_ALIGN_GAIN                = 0.4f;
private const float  DOCK_ALIGN_DAMPING             = 0.6f;
private const float  DOCK_ALIGN_FWD_THRESHOLD       = 0.03f;
private const float  DOCK_ALIGN_ANG_THRESHOLD       = 0.03f;
private const float  DOCK_VEL_GAIN                  = 2.0f;
private const float  DOCK_MAX_ACCEL                 = 3.0f;

private const string DEFAULT_BRAKE_GROUP        = "";
private const string DEFAULT_ASCEND_UP_GROUP    = "";
private const string DEFAULT_ASCEND_DOWN_GROUP  = "";
private const int    DEFAULT_COCKPIT_SCREEN     = 0;
private const string DEFAULT_THEME             = "cyber";

private const int    BOOT_TICKS = 12;
private const string VERSION   = "2.1";

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

private float  _dockApproachSpeed    = DEFAULT_DOCK_APPROACH_SPEED;
private float  _dockFinalSpeed       = DEFAULT_DOCK_FINAL_SPEED;
private float  _dockWaypointDistance = DEFAULT_DOCK_WAYPOINT_DISTANCE;

private bool   _bcEnabled         = DEFAULT_BC_ENABLED;
private int    _bcIndex           = DEFAULT_BC_INDEX;
private string _bcPtaOn           = DEFAULT_BC_PTA_ON;
private string _bcPtaOff          = DEFAULT_BC_PTA_OFF;
private string _bcCruiseOn        = DEFAULT_BC_CRUISE_ON;
private string _bcCruiseOff       = DEFAULT_BC_CRUISE_OFF;
private string _bcAscendOn        = DEFAULT_BC_ASCEND_ON;
private string _bcAscendComplete  = DEFAULT_BC_ASCEND_COMPLETE;
private string _bcAscendAbort     = DEFAULT_BC_ASCEND_ABORT;
private string _bcDescendOn       = DEFAULT_BC_DESCEND_ON;
private string _bcDescendComplete = DEFAULT_BC_DESCEND_COMPLETE;
private string _bcDescendAbort    = DEFAULT_BC_DESCEND_ABORT;
private string _bcDockStart       = DEFAULT_BC_DOCK_START;
private string _bcDockComplete    = DEFAULT_BC_DOCK_COMPLETE;
private string _bcDockAbort       = DEFAULT_BC_DOCK_ABORT;

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

private bool          _ptaActive        = false;
private int           _dockedAlertTicks = 0;

private enum DockPhase     { Aligning, Approaching, Final, Connecting }
private enum AlignSubPhase { Forward, Roll }
private bool          _dockActive      = false;
private DockPhase     _dockPhase       = DockPhase.Aligning;
private AlignSubPhase _alignSubPhase   = AlignSubPhase.Forward;
private string    _dockStatus           = "---";
private string    _dockTargetName       = "";
private Vector3D  _dockTargetPos        = Vector3D.Zero;
private Vector3D  _dockApproachDir      = Vector3D.Forward;
private Vector3D  _dockTargetUp         = Vector3D.Up;
private float     _activeDockWaypointDist = DEFAULT_DOCK_WAYPOINT_DISTANCE;

// -------------------------------------------------------------------------
// Blocks
// -------------------------------------------------------------------------

private IMyShipController               _controller;
private IMyBroadcastController          _broadcastController;
private IMyShipConnector                _dockConnector;
private readonly List<IMyShipConnector> _dockConnectors = new List<IMyShipConnector>();
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
    // Gate 1: PTA offline — only PTA_ON passes
    if (!_ptaActive && argument != "PTA_ON")
    {
        Echo("PTA is offline — run PTA_ON first");
        DrawOffline();
        return;
    }

    // Gate 2: ship connected — only PTA_OFF, PTA_ON, and SAVE pass through.
    // Skip during boot so the boot animation completes before the docked screen takes over.
    if (_bootPhase == 0 && IsShipDocked()
        && argument != "PTA_OFF" && argument != "PTA_ON"
        && argument != "ABORT"
        && !argument.StartsWith("SAVE"))
    {
        if (argument.Length > 0)
            _dockedAlertTicks = 5;
        else if (_dockedAlertTicks > 0)
            _dockedAlertTicks--;
        // Slow tick so we notice when the connector drops without hammering the server
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
        DrawDockedScreen();
        return;
    }

    if (argument.StartsWith("SAVE "))
    {
        SaveDock(argument.Substring(5).Trim());
        return;
    }

    if (argument.StartsWith("DOCK "))
    {
        StartDock(argument.Substring(5).Trim());
        return;
    }

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
            _ptaActive      = true;
            _horizonActive  = false;
            _altitudeActive = false;
            _ascendActive   = false;
            Broadcast(_bcPtaOn);
            if (IsShipDocked())
            {
                _bootPhase = 0;
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                DrawDockedScreen();
            }
            else
            {
                _bootPhase = 1;
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                DrawBoot(0);
            }
            return;

        case "PTA_OFF":
            _ptaActive      = false;
            _horizonActive  = false;
            _altitudeActive = false;
            _bootPhase      = 0;
            if (_dockActive)
            {
                _dockActive = false;
                _dockStatus = "---";
            }
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
            Broadcast(_bcPtaOff);
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
            if (_ascendActive)  { Echo("CRUISE_ON blocked: ascend mode active");  DrawStatus(); return; }
            if (_descendActive) { Echo("CRUISE_ON blocked: descend mode active"); DrawStatus(); return; }
            if (_dockActive)    { Echo("CRUISE_ON blocked: dock mode active");    DrawStatus(); return; }
            ParseConfig();
            InitBlocks();
            FindBrakeThrusters();
            foreach (var t in _brakeThrusters) t.Enabled = false;
            bool inGravity  = _controller.GetNaturalGravity().LengthSquared() > 0.001;
            _horizonActive  = inGravity;
            _altitudeActive = inGravity;
            Broadcast(_bcCruiseOn);
            ApplyUpdateFrequency();
            DrawStatus();
            return;
        }

        case "CRUISE_OFF":
            if (_ascendActive) { Echo("CRUISE_OFF blocked: ascend mode active"); DrawStatus(); return; }
            if (_descendActive) { Echo("CRUISE_OFF blocked: descend mode active"); DrawStatus(); return; }
            if (_dockActive)   { Echo("CRUISE_OFF blocked: dock mode active");   DrawStatus(); return; }
            _horizonActive      = false;
            _altitudeActive     = false;
            _desiredPitchOffset = 0f;
            ReleaseGyros();
            ReleaseThrusters();
            foreach (var t in _brakeThrusters) t.Enabled = true;
            _brakeThrusters.Clear();
            Broadcast(_bcCruiseOff);
            ApplyUpdateFrequency();
            DrawStatus();
            return;

        case "ASCEND_ON":
        {
            if (_brakeThrusters.Count > 0) { Echo("ASCEND_ON blocked: cruise mode active"); DrawStatus(); return; }
            if (BlockedByGroupMode("ASCEND_ON")) return;
            ParseConfig();
            InitBlocks();
            if (_controller.GetNaturalGravity().LengthSquared() < 0.001)
            {
                ShowFlash("NOT AVAILABLE IN SPACE", "Requires planetary gravity", COL_BAD, 6);
                DrawStatus();
                return;
            }
            if (!CheckAscendRequirements()) { DrawAscendUnavailable(); return; }
            InitAscend();
            _ascendActive = true;
            Broadcast(_bcAscendOn);
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
            if (BlockedByGroupMode("DESCEND_ON")) return;
            ParseConfig();
            InitBlocks();
            if (_controller.GetNaturalGravity().LengthSquared() < 0.001)
            {
                ShowFlash("NOT AVAILABLE IN SPACE", "Requires planetary gravity", COL_BAD, 6);
                DrawStatus();
                return;
            }
            if (!CheckDescendRequirements()) { DrawDescendUnavailable(); return; }
            InitDescend();
            _descendActive = true;
            Broadcast(_bcDescendOn);
            ApplyUpdateFrequency();
            DrawStatus();
            return;
        }

        case "DESCEND_OFF":
            CompleteDescend(manual: true);
            return;

        case "ABORT":
        {
            string action  = null;
            bool   handled = false;

            if (_dockActive)
            {
                ReleaseThrusters();
                ReleaseGyros();
                _dockActive = false;
                _dockStatus = "---";
                action  = "DOCK";
                handled = true;
            }
            else if (_ascendActive)
            {
                foreach (var t in _ascendUpThrusters)  { t.ThrustOverridePercentage = 0f; t.Enabled = true; }
                foreach (var t in _ascendDownThrusters)   t.Enabled = true;
                _ascendActive  = false;
                _ascendStatus  = "---";
                _horizonActive = false;
                ReleaseGyros();
                action  = "ASCEND";
                handled = true;
            }
            else if (_descendActive)
            {
                foreach (var t in _descendUpThrusters) t.ThrustOverridePercentage = 0f;
                _descendUpThrusters.Clear();
                _descendActive  = false;
                _descendStatus  = "---";
                _horizonActive  = false;
                ReleaseGyros();
                action  = "DESCEND";
                handled = true;
            }
            else if (_brakeThrusters.Count > 0)
            {
                foreach (var t in _brakeThrusters) t.Enabled = true;
                _brakeThrusters.Clear();
                _horizonActive      = false;
                _altitudeActive     = false;
                _desiredPitchOffset = 0f;
                ReleaseGyros();
                ReleaseThrusters();
                action  = "CRUISE";
                handled = true;
            }
            else if (_horizonActive || _altitudeActive)
            {
                _horizonActive      = false;
                _altitudeActive     = false;
                _desiredPitchOffset = 0f;
                ReleaseGyros();
                ReleaseThrusters();
                handled = true;
            }

            if (action != null)
            {
                ShowFlash(action, "ABORTED", COL_BAD, 8);
                Broadcast(action + " ABORTED");
            }
            else if (!handled)
                ShowFlash("NOTHING TO ABORT", "", COL_DIM, 4);

            ApplyUpdateFrequency();
            DrawStatus();
            return;
        }
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

    // Cruise in space: drop horizon and altitude — they serve no purpose without gravity
    if (_brakeThrusters.Count > 0 && (_horizonActive || _altitudeActive)
        && _controller.GetNaturalGravity().LengthSquared() < 0.001)
    {
        _horizonActive      = false;
        _altitudeActive     = false;
        _desiredPitchOffset = 0f;
        ReleaseGyros();
        ReleaseThrusters();
    }

    // Feature ticks — altitude runs first so pitch offset is set before horizon reads it
    if (_altitudeActive) AltitudeTick();
    if (_horizonActive)  HorizonTick();
    if (_ascendActive)   AscendTick();
    if (_descendActive)  DescendTick();
    if (_dockActive)     DockTick();
    DrawStatus();
    // Rebalance frequency every tick — catches the undock transition and drops to None
    // when no features are active (e.g. immediately after connector disconnects)
    ApplyUpdateFrequency();
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

    ApplyGyroCommand(gyroCommand);
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
// Dock — save and start
// -------------------------------------------------------------------------

private void SaveDock(string name)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        Echo("SAVE failed: no name given");
        ShowFlash("SAVE FAILED", "NO NAME", COL_BAD, 5);
        return;
    }

    if (_controller == null) InitBlocks();

    IMyShipConnector connected = null;
    int count = 0;
    foreach (var c in _dockConnectors)
    {
        if (c.Status == MyShipConnectorStatus.Connected) { connected = c; count++; }
    }

    if (count == 0) { Echo("SAVE failed: no connector connected"); ShowFlash("SAVE FAILED", "NOT CONNECTED", COL_BAD, 5); return; }
    if (count > 1)  { Echo("SAVE failed: multiple connectors connected"); ShowFlash("SAVE FAILED", "MULTI CONNECT", COL_BAD, 5); return; }

    var ini = new MyIni();
    ini.TryParse(Me.CustomData);
    string section = "dock:" + name;
    ini.Set(section, "connector",         connected.CustomName);
    ini.Set(section, "position",          V3Str(connected.WorldMatrix.Translation));
    ini.Set(section, "approach",          V3Str(connected.WorldMatrix.Forward));
    ini.Set(section, "up",                V3Str(connected.WorldMatrix.Up));
    ini.Set(section, "waypoint_distance", _dockWaypointDistance);
    Me.CustomData = ini.ToString();

    ShowFlash("DOCK SAVED", name, COL_OK, 5);
    Echo("Dock saved: " + name + " via " + connected.CustomName);
}

private void StartDock(string name)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        Echo("DOCK failed: no name given");
        ShowFlash("DOCK FAILED", "NO NAME", COL_BAD, 5);
        return;
    }

    if (BlockedByGroupMode("DOCK")) return;

    var ini = new MyIni();
    ini.TryParse(Me.CustomData);
    string section = "dock:" + name;

    if (!ini.ContainsSection(section))
    {
        Echo("DOCK failed: no saved dock named " + name);
        ShowFlash("DOCK FAILED", "UNKNOWN: " + name, COL_BAD, 5);
        return;
    }

    Vector3D pos, approach, up;
    if (!TryParseV3(ini.Get(section, "position").ToString(""), out pos)    ||
        !TryParseV3(ini.Get(section, "approach").ToString(""), out approach) ||
        !TryParseV3(ini.Get(section, "up").ToString(""),       out up))
    {
        Echo("DOCK failed: corrupt data for " + name);
        ShowFlash("DOCK FAILED", "BAD DATA", COL_BAD, 5);
        return;
    }

    string connName = ini.Get(section, "connector").ToString("");
    var connectors  = new List<IMyShipConnector>();
    GridTerminalSystem.GetBlocksOfType(connectors,
        b => b.IsSameConstructAs(Me) && b.CustomName == connName);

    if (connectors.Count == 0)
    {
        Echo("DOCK failed: connector not found: " + connName);
        ShowFlash("DOCK FAILED", "NO CONNECTOR", COL_BAD, 5);
        return;
    }

    ParseConfig();
    InitBlocks();

    _dockTargetName         = name;
    _dockTargetPos          = pos;
    _dockApproachDir        = Vector3D.Normalize(approach);
    _dockTargetUp           = Vector3D.Normalize(up);
    string wdStr;
    float  wdParsed;
    wdStr = ini.Get(section, "waypoint_distance").ToString("");
    _activeDockWaypointDist = (wdStr.Length > 0
        && float.TryParse(wdStr, System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out wdParsed)
        && wdParsed > 0f)
        ? wdParsed
        : _dockWaypointDistance;
    _dockConnector          = connectors[0];
    _dockPhase       = DockPhase.Aligning;
    _alignSubPhase   = AlignSubPhase.Forward;
    _dockStatus      = "ALIGNING";
    _dockActive      = true;
    _horizonActive   = false;
    _altitudeActive  = false;

    Broadcast(_bcDockStart);
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    DrawStatus();
}

// -------------------------------------------------------------------------
// Dock — tick
// -------------------------------------------------------------------------

private void DockTick()
{
    if (_controller == null || _dockConnector == null) return;

    Vector3D gravity   = _controller.GetNaturalGravity();
    bool     inGravity = gravity.LengthSquared() > 0.001;

    switch (_dockPhase)
    {
        case DockPhase.Aligning:    DockAlignTick(inGravity, gravity);   break;
        case DockPhase.Approaching: DockApproachTick(inGravity, gravity); break;
        case DockPhase.Final:       DockFinalTick(inGravity, gravity);    break;
        case DockPhase.Connecting:  DockConnectTick();                    break;
    }
}

private void DockAlignTick(bool inGravity, Vector3D gravity)
{
    Vector3D angVel  = _controller.GetShipVelocities().AngularVelocity;
    Vector3D connFwd = _dockConnector.WorldMatrix.Forward;

    if (inGravity)
    {
        // Planet: align both forward and roll to the saved connector orientation
        Vector3D connUp    = _dockConnector.WorldMatrix.Up;
        Vector3D alignCorr = Vector3D.Cross(connFwd, _dockApproachDir);
        Vector3D rollCorr  = Vector3D.Cross(connUp,  _dockTargetUp);
        double   fwdErr    = alignCorr.Length();
        double   rollErr   = rollCorr.Length();

        Vector3D gyroCmd = (alignCorr + rollCorr) * DOCK_ALIGN_GAIN
                           - angVel * DOCK_ALIGN_DAMPING;
        ApplyGyroCommand(gyroCmd);
        ApplyVelocityTarget(Vector3D.Zero, gravity);

        _dockStatus = "ALIGN " + (fwdErr + rollErr).ToString("F3");
        if (fwdErr < DOCK_ALIGN_FWD_THRESHOLD && rollErr < DOCK_ALIGN_FWD_THRESHOLD
            && angVel.Length() < DOCK_ALIGN_ANG_THRESHOLD)
        {
            _dockPhase  = DockPhase.Approaching;
            _dockStatus = "APPROACHING";
        }
    }
    else if (_alignSubPhase == AlignSubPhase.Forward)
    {
        // Space phase 1: align connector forward vector only
        Vector3D alignCorr = Vector3D.Cross(connFwd, _dockApproachDir);
        double   alignErr  = alignCorr.Length();

        Vector3D gyroCmd = alignCorr * DOCK_ALIGN_GAIN - angVel * DOCK_ALIGN_DAMPING;
        ApplyGyroCommand(gyroCmd);
        ApplyVelocityTarget(Vector3D.Zero, gravity);

        _dockStatus = "ALIGN FWD " + alignErr.ToString("F3");
        if (alignErr < DOCK_ALIGN_FWD_THRESHOLD && angVel.Length() < DOCK_ALIGN_ANG_THRESHOLD)
            _alignSubPhase = AlignSubPhase.Roll;
    }
    else
    {
        // Space phase 2: align roll once forward is settled
        Vector3D connUp   = _dockConnector.WorldMatrix.Up;
        Vector3D rollCorr = Vector3D.Cross(connUp, _dockTargetUp);
        double   rollErr  = rollCorr.Length();

        Vector3D gyroCmd = rollCorr * DOCK_ALIGN_GAIN - angVel * DOCK_ALIGN_DAMPING;
        ApplyGyroCommand(gyroCmd);
        ApplyVelocityTarget(Vector3D.Zero, gravity);

        _dockStatus = "ALIGN ROLL " + rollErr.ToString("F3");
        if (rollErr < DOCK_ALIGN_FWD_THRESHOLD && angVel.Length() < DOCK_ALIGN_ANG_THRESHOLD)
        {
            _dockPhase  = DockPhase.Approaching;
            _dockStatus = "APPROACHING";
        }
    }
}

private void DockApproachTick(bool inGravity, Vector3D gravity)
{
    Vector3D connPos    = _dockConnector.WorldMatrix.Translation;
    Vector3D waypoint   = _dockTargetPos - _dockApproachDir * _activeDockWaypointDist;
    Vector3D toWaypoint = waypoint - connPos;
    double   dist       = toWaypoint.Length();

    double   speed      = Math.Min(_dockApproachSpeed, Math.Max(1.0, dist * 0.5));
    Vector3D desiredVel = dist > 0.5
        ? Vector3D.Normalize(toWaypoint) * speed
        : Vector3D.Zero;

    ApplyVelocityTarget(desiredVel, gravity);
    MaintainDockOrientation();

    _dockStatus = "APPROACHING " + dist.ToString("F0") + "m";

    if (dist < 1.0)
    {
        _dockPhase  = DockPhase.Final;
        _dockStatus = "FINAL";
    }
}

private void DockFinalTick(bool inGravity, Vector3D gravity)
{
    Vector3D connPos     = _dockConnector.WorldMatrix.Translation;
    Vector3D toTarget    = _dockTargetPos - connPos;
    double   dist        = toTarget.Length();
    Vector3D currentVel  = _controller.GetShipVelocities().LinearVelocity;
    double   approachSpd = Vector3D.Dot(currentVel, _dockApproachDir);

    if (approachSpd > _dockFinalSpeed * 2.0 && dist < 5.0)
    {
        CompleteDock(abort: true, reason: "OVERSPEED");
        return;
    }

    // Point directly at the actual connector position — corrects any lateral offset
    // from the approach phase rather than flying along a fixed axis and missing
    double   targetSpeed = Math.Min(_dockFinalSpeed, dist);
    Vector3D desiredDir  = dist > 0.01 ? Vector3D.Normalize(toTarget) : _dockApproachDir;
    ApplyVelocityTarget(desiredDir * targetSpeed, gravity);
    MaintainDockOrientation();

    _dockStatus = "FINAL " + dist.ToString("F1") + "m";

    if (_dockConnector.Status == MyShipConnectorStatus.Connectable)
    {
        _dockPhase  = DockPhase.Connecting;
        _dockStatus = "CONNECTING";
    }
    else if (dist < 0.1)
    {
        CompleteDock(abort: true, reason: "NOT CONNECTABLE");
    }
}

private void MaintainDockOrientation()
{
    Vector3D connFwd = _dockConnector.WorldMatrix.Forward;
    Vector3D connUp  = _dockConnector.WorldMatrix.Up;
    Vector3D angVel  = _controller.GetShipVelocities().AngularVelocity;
    Vector3D gyroCmd = (Vector3D.Cross(connFwd, _dockApproachDir) + Vector3D.Cross(connUp, _dockTargetUp))
                       * (DOCK_ALIGN_GAIN * 0.5f)
                       - angVel * DOCK_ALIGN_DAMPING;
    ApplyGyroCommand(gyroCmd);
}

private void DockConnectTick()
{
    if (_dockConnector.Status == MyShipConnectorStatus.Connectable)
        _dockConnector.Connect();

    if (_dockConnector.Status == MyShipConnectorStatus.Connected)
        CompleteDock(abort: false, reason: "");
}

private void CompleteDock(bool abort, string reason)
{
    ReleaseThrusters();
    ReleaseGyros();
    _horizonActive = false;
    _dockActive    = false;
    _dockStatus    = "---";

    if (abort) { ShowFlash("DOCK ABORTED", reason, COL_BAD, 8); Broadcast(_bcDockAbort); }
    else       Broadcast(_bcDockComplete);
    // Success: IsShipDocked() is now true — DrawStatus() and ApplyUpdateFrequency()
    // in the tick section pick it up immediately on the same frame
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
    if (_ascendActive)       { DrawAscendStatus(); return; }
    if (_descendActive)      { DrawDescendStatus(); return; }
    if (_dockActive)         { DrawDockStatus(); return; }
    if (IsShipDocked())      { DrawDockedScreen(); return; }
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

// -------------------------------------------------------------------------
// Display — dock status screen
// -------------------------------------------------------------------------

private void DrawDockStatus()
{
    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var pan = Inset(vp, 10f);
        float cx = vp.X + vp.Width * 0.5f;

        Vector3D connPos = _dockConnector != null
            ? _dockConnector.WorldMatrix.Translation
            : Vector3D.Zero;
        double dist  = (_dockTargetPos - connPos).Length();
        double speed = _controller.GetShipVelocities().LinearVelocity.Length();

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, COL_ACCENT, 3f);

            Txt(fr, "PTA",  pan.X + 20f,    pan.Y + 14f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);
            Txt(fr, "DOCK", pan.Right - 20f, pan.Y + 20f, COL_WARN,   0.44f, TextAlignment.RIGHT);

            float dy = pan.Y + 52f;
            Fill(fr, new RectangleF(pan.X + 10f, dy, pan.Width - 20f, 1f), COL_ACCENT);
            dy += 14f;

            StatusRow(fr, pan, dy, "TARGET", _dockTargetName, COL_TEXT);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            StatusRow(fr, pan, dy, "PHASE", _dockStatus, COL_WARN);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            StatusRow(fr, pan, dy, "DIST",  dist.ToString("F1") + "m",  COL_TEXT);
            dy += 28f;
            Fill(fr, new RectangleF(pan.X + 16f, dy, pan.Width - 32f, 1f), COL_PANEL2);
            dy += 6f;

            StatusRow(fr, pan, dy, "SPEED", speed.ToString("F1") + "m/s", COL_TEXT);

            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

private void DrawDockedScreen()
{
    Color borderCol = _dockedAlertTicks > 0 ? COL_BAD : COL_OK;
    Color msgCol    = _dockedAlertTicks > 0 ? COL_BAD : COL_OK;

    foreach (var s in _surfaces)
    {
        var vp  = VP(s);
        var pan = Inset(vp, 10f);
        float cx = vp.X + vp.Width  * 0.5f;
        float cy = vp.Y + vp.Height * 0.5f;

        using (var fr = s.DrawFrame())
        {
            Fill(fr, vp, COL_BG);
            Fill(fr, pan, COL_PANEL);
            DrawBorder(fr, pan, borderCol, 3f);

            Txt(fr, "PTA", pan.X + 20f, pan.Y + 14f, COL_ACCENT2, 0.82f, TextAlignment.LEFT);

            float dy = pan.Y + 52f;
            Fill(fr, new RectangleF(pan.X + 10f, dy, pan.Width - 20f, 1f), borderCol);
            dy += 50f;

            Txt(fr, "SHIP IS CONNECTED", cx, dy, msgCol, 0.72f, TextAlignment.CENTER);
            dy += 46f;

            Txt(fr, "Undock to use PTA features", cx, dy, COL_DIM, 0.38f, TextAlignment.CENTER);

            Txt(fr, "Planetary Travel Assistant  v" + VERSION,
                cx, pan.Bottom - 16f, COL_DIM, 0.30f, TextAlignment.CENTER);
        }
    }
}

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

private bool IsShipDocked()
{
    foreach (var c in _dockConnectors)
        if (c.Status == MyShipConnectorStatus.Connected) return true;
    return false;
}

private bool BlockedByGroupMode(string cmd)
{
    if (_ascendActive)             { Echo(cmd + " blocked: ascend mode active");  DrawStatus(); return true; }
    if (_descendActive)            { Echo(cmd + " blocked: descend mode active"); DrawStatus(); return true; }
    if (_brakeThrusters.Count > 0) { Echo(cmd + " blocked: cruise mode active");  DrawStatus(); return true; }
    if (_dockActive)               { Echo(cmd + " blocked: dock mode active");    DrawStatus(); return true; }
    return false;
}

private string GetModeLabel()
{
    if (_ascendActive)             return "ASCEND";
    if (_descendActive)            return "DESCEND";
    if (_brakeThrusters.Count > 0) return "CRUISE";
    if (_dockActive)               return "DOCK";
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
    if (manual) { ShowFlash("ASCEND ABORTED",  "",              COL_WARN, 8); Broadcast(_bcAscendAbort); }
    else        { ShowFlash("ASCEND COMPLETE", "ORBIT REACHED", COL_OK,   8); Broadcast(_bcAscendComplete); }
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
    if (manual) { ShowFlash("DESCEND ABORTED",  "",                                                      COL_WARN, 8); Broadcast(_bcDescendAbort); }
    else        { ShowFlash("DESCEND COMPLETE", _descendTarget.ToString("F0") + " m — MANUAL CONTROL", COL_OK,   8); Broadcast(_bcDescendComplete); }
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

    dirty |= EnsureFloat (ini, SEC_DOCK,     "approach_speed",     DEFAULT_DOCK_APPROACH_SPEED,    ref _dockApproachSpeed);
    dirty |= EnsureFloat (ini, SEC_DOCK,     "final_speed",        DEFAULT_DOCK_FINAL_SPEED,       ref _dockFinalSpeed);
    dirty |= EnsureFloat (ini, SEC_DOCK,     "waypoint_distance",  DEFAULT_DOCK_WAYPOINT_DISTANCE, ref _dockWaypointDistance);

    dirty |= EnsureBool  (ini, SEC_BROADCAST, "enabled",          DEFAULT_BC_ENABLED,          ref _bcEnabled);
    dirty |= EnsureInt   (ini, SEC_BROADCAST, "index",            DEFAULT_BC_INDEX,            ref _bcIndex);
    _bcIndex = Math.Max(1, Math.Min(8, _bcIndex));
    dirty |= EnsureString(ini, SEC_BROADCAST, "pta_on",           DEFAULT_BC_PTA_ON,           ref _bcPtaOn);
    dirty |= EnsureString(ini, SEC_BROADCAST, "pta_off",          DEFAULT_BC_PTA_OFF,          ref _bcPtaOff);
    dirty |= EnsureString(ini, SEC_BROADCAST, "cruise_on",        DEFAULT_BC_CRUISE_ON,        ref _bcCruiseOn);
    dirty |= EnsureString(ini, SEC_BROADCAST, "cruise_off",       DEFAULT_BC_CRUISE_OFF,       ref _bcCruiseOff);
    dirty |= EnsureString(ini, SEC_BROADCAST, "ascend_on",        DEFAULT_BC_ASCEND_ON,        ref _bcAscendOn);
    dirty |= EnsureString(ini, SEC_BROADCAST, "ascend_complete",  DEFAULT_BC_ASCEND_COMPLETE,  ref _bcAscendComplete);
    dirty |= EnsureString(ini, SEC_BROADCAST, "ascend_abort",     DEFAULT_BC_ASCEND_ABORT,     ref _bcAscendAbort);
    dirty |= EnsureString(ini, SEC_BROADCAST, "descend_on",       DEFAULT_BC_DESCEND_ON,       ref _bcDescendOn);
    dirty |= EnsureString(ini, SEC_BROADCAST, "descend_complete", DEFAULT_BC_DESCEND_COMPLETE, ref _bcDescendComplete);
    dirty |= EnsureString(ini, SEC_BROADCAST, "descend_abort",    DEFAULT_BC_DESCEND_ABORT,    ref _bcDescendAbort);
    dirty |= EnsureString(ini, SEC_BROADCAST, "dock_start",       DEFAULT_BC_DOCK_START,       ref _bcDockStart);
    dirty |= EnsureString(ini, SEC_BROADCAST, "dock_complete",    DEFAULT_BC_DOCK_COMPLETE,    ref _bcDockComplete);
    dirty |= EnsureString(ini, SEC_BROADCAST, "dock_abort",       DEFAULT_BC_DOCK_ABORT,       ref _bcDockAbort);

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

private bool EnsureBool(MyIni ini, string sec, string key, bool def, ref bool field)
{
    if (!ini.ContainsKey(sec, key)) { ini.Set(sec, key, def); field = def; return true; }
    field = ini.Get(sec, key).ToBoolean(def);
    return false;
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

    _dockConnectors.Clear();
    GridTerminalSystem.GetBlocksOfType(_dockConnectors,
        b => b.IsSameConstructAs(Me) && b.CustomName.Contains("[PTA_DOCK]"));
    if (_dockConnectors.Count == 0)
        GridTerminalSystem.GetBlocksOfType(_dockConnectors, b => b.IsSameConstructAs(Me));

    var bcList = new List<IMyBroadcastController>();
    GridTerminalSystem.GetBlocksOfType(bcList,
        b => b.IsSameConstructAs(Me) && b.CustomName.Contains("[PTA_BC]"));
    _broadcastController = bcList.Count > 0 ? bcList[0] : null;

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

private void ApplyThrustForce(Vector3D force)
{
    foreach (var t in _thrusters)
    {
        if (t.MaxEffectiveThrust < 1f) continue;
        Vector3D td  = -t.WorldMatrix.Forward;
        double   dot = Vector3D.Dot(force, td);

        if (dot > 0)
        {
            // Sum all thrusters pointing the same way so the group collectively
            // produces exactly the required force — not N times it
            double total = 0;
            foreach (var t2 in _thrusters)
            {
                if (t2.MaxEffectiveThrust >= 1f && Vector3D.Dot(td, -t2.WorldMatrix.Forward) > 0.9)
                    total += t2.MaxEffectiveThrust;
            }
            t.ThrustOverridePercentage = total > 0
                ? (float)Math.Min(1.0, dot / total)
                : 0.001f;
        }
        else
        {
            // 0.001f keeps override active so dampeners cannot fight our velocity commands
            t.ThrustOverridePercentage = 0.001f;
        }
    }
}

private void ApplyVelocityTarget(Vector3D desiredVelocity, Vector3D gravity)
{
    Vector3D currentVel = _controller.GetShipVelocities().LinearVelocity;
    float    mass       = _controller.CalculateShipMass().TotalMass;
    Vector3D velError   = desiredVelocity - currentVel;

    // Cap correction to DOCK_MAX_ACCEL so high-thrust ships don't overshoot
    Vector3D correction  = velError * (mass * DOCK_VEL_GAIN);
    float    maxCorrForce = mass * DOCK_MAX_ACCEL;
    if (correction.LengthSquared() > (double)(maxCorrForce * maxCorrForce))
        correction = Vector3D.Normalize(correction) * maxCorrForce;

    ApplyThrustForce(correction - gravity * mass);
}

// -------------------------------------------------------------------------
// Helpers
// -------------------------------------------------------------------------

private void ApplyUpdateFrequency()
{
    if (_altitudeActive || _ascendActive || _descendActive || _dockActive || _flashActive)
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    else if (_horizonActive || IsShipDocked())
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    else
        Runtime.UpdateFrequency = UpdateFrequency.None;
}

private void ApplyGyroCommand(Vector3D worldCmd)
{
    foreach (var gyro in _gyros)
    {
        Vector3D local = Vector3D.TransformNormal(worldCmd, MatrixD.Transpose(gyro.WorldMatrix));
        gyro.GyroOverride = true;
        gyro.Pitch = -(float)local.X;
        gyro.Yaw   = -(float)local.Y;
        gyro.Roll  = -(float)local.Z;
    }
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

private string V3Str(Vector3D v)
{
    return v.X.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ":" +
           v.Y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ":" +
           v.Z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
}

private void Broadcast(string message)
{
    if (!_bcEnabled || _broadcastController == null || string.IsNullOrEmpty(message)) return;
    var sb = new StringBuilder();
    sb.Append(message);
    _broadcastController.SetValue<StringBuilder>("Message" + (_bcIndex - 1), sb);
    _broadcastController.ApplyAction("Transmit Message " + _bcIndex);
}

private bool TryParseV3(string s, out Vector3D v)
{
    v = Vector3D.Zero;
    if (string.IsNullOrWhiteSpace(s)) return false;
    var parts = s.Split(':');
    if (parts.Length != 3) return false;
    double x, y, z;
    var ic = System.Globalization.CultureInfo.InvariantCulture;
    var ns = System.Globalization.NumberStyles.Float;
    if (!double.TryParse(parts[0], ns, ic, out x) ||
        !double.TryParse(parts[1], ns, ic, out y) ||
        !double.TryParse(parts[2], ns, ic, out z)) return false;
    v = new Vector3D(x, y, z);
    return true;
}
