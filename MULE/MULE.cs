// -------------------------------------------------------------------------
// MULE — Autonomous Planetary Cargo Drone
// -------------------------------------------------------------------------
// Moves cargo (ice) from a pickup connector to a dropoff connector and back.
// Requires: Remote Control block, front Connector, Batteries, Thrusters,
//           Gyroscopes. Optional: LCD panel, Antenna, Cockpit.
//
// Arguments: SET_PICKUP | SET_DROPOFF | START | STOP | STATUS
// -------------------------------------------------------------------------

// -------------------------------------------------------------------------
// Config defaults (overridden by Custom Data)
// -------------------------------------------------------------------------
private const string DEFAULT_AI_BASIC_NAME   = "Drone AI Basic";
private const string DEFAULT_AI_FLIGHT_NAME  = "Drone AI Flight";
private const string DEFAULT_CONNECTOR_NAME  = "Connector Front";
private const string DEFAULT_LCD_NAME        = "Drone LCD";
private const string DEFAULT_COCKPIT_NAME    = "";
private const int    DEFAULT_COCKPIT_SCREEN  = 0;
private const float  DEFAULT_CARGO_THRESHOLD = 90f;
private const float  DEFAULT_EMPTY_THRESHOLD = 5f;
private const float  DEFAULT_CRUISE_ALTITUDE = 200f;
private const float  DEFAULT_BACKUP_DISTANCE = 15f;
private const float  DEFAULT_MIN_BATTERY     = 20f;
private const float  DEFAULT_SAFETY_FACTOR   = 1.2f;
private const float  DEFAULT_RESUME_BATTERY  = 80f;
private const float  DEFAULT_CRUISE_SPEED    = 15f;
private const float  DEFAULT_APPROACH_SPEED  = 5f;
private const float  DEFAULT_DOCKING_SPEED   = 2f;
private const float  DEFAULT_BRAKING_DISTANCE = 100f;
private const float  ARRIVE_TOLERANCE        = 5f;

// -------------------------------------------------------------------------
// State machine
// -------------------------------------------------------------------------
private enum DroneState
{
    Idle,
    Loading,
    DepartingPickup,
    ClimbingFromPickup,
    FlyingToDropoff,
    ApproachingDropoff,
    DockingAtDropoff,
    Unloading,
    DepartingDropoff,
    ClimbingFromDropoff,
    FlyingToPickup,
    ApproachingPickup,
    DockingAtPickup,
    EmergencyReturn,
    WaitingForCharge,
    Error
}

// -------------------------------------------------------------------------
// Saved location data
// -------------------------------------------------------------------------
private struct DockPoint
{
    public Vector3D ConnectorPos;
    public Vector3D ApproachPos;   // ConnectorPos offset backward by backup distance
    public Vector3D ClimbPos;      // approach pos at cruise altitude
    public bool     IsSet;
}

// -------------------------------------------------------------------------
// Fields
// -------------------------------------------------------------------------
private DroneState _state    = DroneState.Idle;
private DockPoint  _pickup   = new DockPoint();
private DockPoint  _dropoff  = new DockPoint();
private bool       _running  = false;
private bool       _testMode   = false;
private bool       _singleTrip = false;
private int        _ticksSinceBlockRefresh = 0;
private const int  BLOCK_REFRESH_INTERVAL  = 300;

// config
private string _aiBasicName   = DEFAULT_AI_BASIC_NAME;
private string _aiFlightName  = DEFAULT_AI_FLIGHT_NAME;
private string _connectorName = DEFAULT_CONNECTOR_NAME;
private string _lcdName        = DEFAULT_LCD_NAME;
private string _cockpitName   = DEFAULT_COCKPIT_NAME;
private int    _cockpitScreen = DEFAULT_COCKPIT_SCREEN;
private float  _cargoThreshold  = DEFAULT_CARGO_THRESHOLD;
private float  _emptyThreshold  = DEFAULT_EMPTY_THRESHOLD;
private float  _cruiseAltitude  = DEFAULT_CRUISE_ALTITUDE;
private float  _backupDistance  = DEFAULT_BACKUP_DISTANCE;
private float  _minBattery      = DEFAULT_MIN_BATTERY;
private float  _safetyFactor   = DEFAULT_SAFETY_FACTOR;
private float  _resumeBattery  = DEFAULT_RESUME_BATTERY;
private float  _cruiseSpeed    = DEFAULT_CRUISE_SPEED;
private float  _approachSpeed  = DEFAULT_APPROACH_SPEED;
private float  _dockingSpeed    = DEFAULT_DOCKING_SPEED;
private float  _brakingDistance = DEFAULT_BRAKING_DISTANCE;

// block references
private IMyRemoteControl              _aiBasic;
private IMyFunctionalBlock            _aiFlightBlock;
private IMyShipConnector              _connector;
private IMyTextPanel                  _lcd;
private IMyRadioAntenna               _antenna;
private readonly List<IMyBatteryBlock>    _batteries  = new List<IMyBatteryBlock>();
private readonly List<IMyCargoContainer>  _cargo      = new List<IMyCargoContainer>();
private readonly List<IMyThrust>          _thrusters  = new List<IMyThrust>();
private IMyCockpit                        _cockpit;

// stats
private int    _runsCompleted = 0;
private double _lastRunSeconds = 0;
private double _runStartTime   = 0;

// emergency return sub-phase
private int _emergencyPhase = 0;

// last waypoint sent to AI Basic — prevents resetting autopilot every tick
private Vector3D _lastWaypoint = Vector3D.Zero;
private Base6Directions.Direction _lastDir = Base6Directions.Direction.Forward;

// calibrated values (0 = not calibrated)
private float _maxCargoKg   = 0f;
private float _baseMassKg   = 0f;
private float _thrustKN     = 0f;

// -------------------------------------------------------------------------
// Constructor
// -------------------------------------------------------------------------
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.None;
    LoadStorage();
    RefreshBlocks();
    ParseCustomData();
    EnsureCustomDataDefaults();
    LoadSetup();
    if (_running)
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    UpdateDisplays();
}

// -------------------------------------------------------------------------
// Save
// -------------------------------------------------------------------------
public void Save()
{
    SaveStorage();
}

// -------------------------------------------------------------------------
// Main
// -------------------------------------------------------------------------
public void Main(string argument, UpdateType updateSource)
{
    if (!string.IsNullOrEmpty(argument))
    {
        HandleArgument(argument.Trim().ToUpper());
        return;
    }

    _ticksSinceBlockRefresh++;
    if (_ticksSinceBlockRefresh >= BLOCK_REFRESH_INTERVAL)
    {
        _ticksSinceBlockRefresh = 0;
        RefreshBlocks();
        ParseCustomData();
    }

    if (_running)
        RunStateMachine();

    UpdateDisplays();
}

// -------------------------------------------------------------------------
// Argument handling
// -------------------------------------------------------------------------
private void HandleArgument(string arg)
{
    switch (arg)
    {
        case "SET_PICKUP":
            SetDockPoint(ref _pickup, "Pickup");
            break;
        case "SET_DROPOFF":
            SetDockPoint(ref _dropoff, "Dropoff");
            break;
        case "START":
            StartDrone();
            break;
        case "STOP":
            StopDrone();
            break;
        case "CALIBRATE":
            Calibrate();
            break;
        case "TEST":
            _testMode = !_testMode;
            Echo("Test mode " + (_testMode ? "ON" : "OFF"));
            UpdateDisplays();
            break;
        case "TRIP_OUT":
            StartTrip(true);
            break;
        case "TRIP_BACK":
            StartTrip(false);
            break;
        case "RESET":
            ResetSetup();
            break;
        case "STATUS":
            UpdateDisplays();
            break;
        default:
            Echo("Unknown argument: " + arg);
            break;
    }
    SaveStorage();
}

// -------------------------------------------------------------------------
// Set dock point from current connector position
// -------------------------------------------------------------------------
private void SetDockPoint(ref DockPoint point, string label)
{
    RefreshBlocks();
    if (_connector == null)
    {
        SetError("Connector '" + _connectorName + "' not found");
        return;
    }
    if (_connector.Status != MyShipConnectorStatus.Connected)
    {
        Echo("WARNING: Connect to a dock before running SET_" + label.ToUpper());
        return;
    }

    var gravity  = _aiBasic.GetNaturalGravity();
    var upDir    = gravity.Length() > 0.01
        ? Vector3D.Normalize(-gravity)
        : (Vector3D)_aiBasic.WorldMatrix.Up;

    // Save the AI Basic block position as the docking target — the autopilot flies
    // the AI Basic block to the waypoint, not the connector, so this is what matters.
    var aiPos    = _aiBasic.GetPosition();
    var backward = -_connector.WorldMatrix.Forward;

    point.ConnectorPos = aiPos;
    point.ApproachPos  = aiPos + backward * _backupDistance;
    point.ClimbPos     = point.ApproachPos + upDir * _cruiseAltitude;
    point.IsSet = true;

    var sb = new StringBuilder();
    sb.AppendLine("=== MULE SETUP ===");
    sb.AppendLine(label + " saved.");
    sb.AppendLine("");
    sb.AppendLine("X : " + aiPos.X.ToString("F0"));
    sb.AppendLine("Y : " + aiPos.Y.ToString("F0"));
    sb.AppendLine("Z : " + aiPos.Z.ToString("F0"));
    sb.AppendLine("");
    sb.AppendLine("Approach offset: " + _backupDistance.ToString("F0") + " m");
    sb.AppendLine("Cruise altitude: " + _cruiseAltitude.ToString("F0") + " m");
    ShowMessage(sb.ToString());
    SaveSetup();
    UpdateDisplays();
}

// -------------------------------------------------------------------------
// Start / Stop
// -------------------------------------------------------------------------
private void StartDrone()
{
    RefreshBlocks();
    if (!_pickup.IsSet || !_dropoff.IsSet)
    {
        SetError("Set pickup and dropoff before starting");
        return;
    }
    if (_maxCargoKg <= 0f)
    {
        SetError("Run CALIBRATE before starting");
        return;
    }
    float bat = GetBatteryPercent();
    if (bat < _minBattery)
    {
        SetError("Battery " + bat.ToString("F0") + "% (min " + _minBattery.ToString("F0") + "%)");
        return;
    }
    string reason;
    if (!SafeToFly(out reason))
    {
        SetError("Cannot start: " + reason);
        return;
    }

    _running = true;
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    if (_connector != null && _connector.Status == MyShipConnectorStatus.Connected)
    {
        var pos = _connector.GetPosition();
        double distToPickup  = Vector3D.Distance(pos, _pickup.ConnectorPos);
        double distToDropoff = Vector3D.Distance(pos, _dropoff.ConnectorPos);

        if (distToPickup <= distToDropoff)
            _state = DroneState.Loading;
        else
            _state = DroneState.DepartingDropoff;
    }
    else
    {
        _state = DroneState.ClimbingFromPickup;
    }

    _runStartTime = Runtime.TimeSinceLastRun.TotalSeconds;
    Echo("MULE started.");
}

private void StartTrip(bool toDropoff)
{
    if (!_testMode)
    {
        Echo("TRIP_OUT / TRIP_BACK only available in test mode. Run TEST first.");
        return;
    }
    if (!_pickup.IsSet || !_dropoff.IsSet)
    {
        SetError("Set pickup and dropoff before starting");
        return;
    }
    if (_maxCargoKg <= 0f)
    {
        SetError("Run CALIBRATE before starting");
        return;
    }
    float bat = GetBatteryPercent();
    if (bat < _minBattery)
    {
        SetError("Battery " + bat.ToString("F0") + "% (min " + _minBattery.ToString("F0") + "%)");
        return;
    }
    string reason;
    if (!SafeToFly(out reason))
    {
        SetError("Cannot start: " + reason);
        return;
    }

    _singleTrip = true;
    _running    = true;
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    _state = toDropoff ? DroneState.DepartingPickup : DroneState.DepartingDropoff;
    Echo("Single trip " + (toDropoff ? "to dropoff" : "to pickup") + " started.");
}

private void StopDrone()
{
    _running = false;
    Runtime.UpdateFrequency = UpdateFrequency.None;
    StopAutopilot();
    _state = DroneState.Idle;
    Echo("MULE stopped.");
}

// -------------------------------------------------------------------------
// State machine
// -------------------------------------------------------------------------
private void RunStateMachine()
{
    bool isDocked = _connector != null && _connector.Status == MyShipConnectorStatus.Connected;
    if (!isDocked
        && _state != DroneState.EmergencyReturn
        && _state != DroneState.WaitingForCharge
        && _state != DroneState.Error
        && GetBatteryPercent() < _minBattery)
    {
        _emergencyPhase = 0;
        _state = DroneState.EmergencyReturn;
        if (_antenna != null) _antenna.HudText = "MULE | LOW BATTERY — RETURNING TO DROPOFF";
    }

    switch (_state)
    {
        case DroneState.Loading:             StateLoading();              break;
        case DroneState.DepartingPickup:     StateDeparting(true);        break;
        case DroneState.ClimbingFromPickup:  StateClimbing(true);         break;
        case DroneState.FlyingToDropoff:     StateFlyingTo(false);        break;
        case DroneState.ApproachingDropoff:  StateApproaching(false);     break;
        case DroneState.DockingAtDropoff:    StateDocking(false);         break;
        case DroneState.Unloading:           StateUnloading();            break;
        case DroneState.DepartingDropoff:    StateDeparting(false);       break;
        case DroneState.ClimbingFromDropoff: StateClimbing(false);        break;
        case DroneState.FlyingToPickup:      StateFlyingTo(true);         break;
        case DroneState.ApproachingPickup:   StateApproaching(true);      break;
        case DroneState.DockingAtPickup:     StateDocking(true);          break;
        case DroneState.EmergencyReturn:     StateEmergencyReturn();      break;
        case DroneState.WaitingForCharge:    StateWaitingForCharge();     break;
        case DroneState.Error:               break;
        default:                             break;
    }
}

private void StateLoading()
{
    if (GetBatteryPercent() < _resumeBattery)
        return;

    string reason;
    if (!SafeToFly(out reason))
    {
        SetError("Hold: " + reason);
        return;
    }
    float cargoKg, fillPct;
    GetCargoMassInfo(out cargoKg, out fillPct);
    if (_testMode || fillPct >= _cargoThreshold)
    {
        _runStartTime = Runtime.TimeSinceLastRun.TotalSeconds;
        _state = DroneState.DepartingPickup;
    }
}

private void StateDeparting(bool fromPickup)
{
    if (_connector != null && _connector.Status == MyShipConnectorStatus.Connected)
    {
        Disconnect();
        return;
    }
    // Depart with a diagonal path: back away AND climb together to avoid unsafe rotations.
    // Compute a departure point that's at the approach position but 50m higher.
    var approachBase = fromPickup ? _pickup.ApproachPos : _dropoff.ApproachPos;
    var gravity  = _aiBasic != null ? _aiBasic.GetNaturalGravity() : Vector3D.Zero;
    var upDir    = gravity.Length() > 0.01
        ? Vector3D.Normalize(-gravity)
        : (Vector3D)_aiBasic.WorldMatrix.Up;
    var departureTarget = approachBase + upDir * 50.0;

    FlyTo(departureTarget, _approachSpeed);
    if (HasArrived(departureTarget))
        _state = fromPickup ? DroneState.ClimbingFromPickup : DroneState.ClimbingFromDropoff;
}

private void StateClimbing(bool fromPickup)
{
    var target = fromPickup ? _pickup.ClimbPos : _dropoff.ClimbPos;
    FlyTo(target, _cruiseSpeed);
    if (HasArrived(target))
        _state = fromPickup ? DroneState.FlyingToDropoff : DroneState.FlyingToPickup;
}

private void StateFlyingTo(bool toPickup)
{
    var target = toPickup ? _pickup.ClimbPos : _dropoff.ClimbPos;
    FlyTo(target, _cruiseSpeed);
    if (HasArrived(target))
        _state = toPickup ? DroneState.ApproachingPickup : DroneState.ApproachingDropoff;
}

private void StateApproaching(bool toPickup)
{
    var target = toPickup ? _pickup.ApproachPos : _dropoff.ApproachPos;
    FlyTo(target, _approachSpeed);
    if (HasArrived(target))
        _state = toPickup ? DroneState.DockingAtPickup : DroneState.DockingAtDropoff;
}

private void StateDocking(bool atPickup)
{
    var target = atPickup ? _pickup.ConnectorPos : _dropoff.ConnectorPos;
    FlyTo(target, _dockingSpeed);

    if (_connector != null) _connector.Connect();

    if (_connector != null && _connector.Status == MyShipConnectorStatus.Connected)
    {
        StopAutopilot();
        if (_singleTrip)
        {
            _singleTrip = false;
            StopDrone();
        }
        else
            _state = atPickup ? DroneState.Loading : DroneState.Unloading;
    }
}

private void StateUnloading()
{
    float fill = GetCargoVolumePct();
    if (_testMode || fill <= _emptyThreshold)
    {
        _runsCompleted++;
        _lastRunSeconds += Runtime.TimeSinceLastRun.TotalSeconds - _runStartTime;
        _state = DroneState.DepartingDropoff;
    }
}

private void StateEmergencyReturn()
{
    switch (_emergencyPhase)
    {
        case 0:
        {
            var gravity  = _aiBasic != null ? _aiBasic.GetNaturalGravity() : Vector3D.Zero;
            var upDir    = gravity.Length() > 0.01
                ? Vector3D.Normalize(-gravity)
                : (Vector3D)_aiBasic.WorldMatrix.Up;
            var safeClimb = _aiBasic.GetPosition() + upDir * (_cruiseAltitude + 50.0);
            FlyTo(safeClimb, _cruiseSpeed);
            if (HasArrived(safeClimb)) _emergencyPhase = 1;
            break;
        }
        case 1:
            FlyTo(_dropoff.ClimbPos, _cruiseSpeed);
            if (HasArrived(_dropoff.ClimbPos)) _emergencyPhase = 2;
            break;
        case 2:
            FlyTo(_dropoff.ApproachPos, _approachSpeed);
            if (HasArrived(_dropoff.ApproachPos)) _emergencyPhase = 3;
            break;
        case 3:
            FlyTo(_dropoff.ConnectorPos, _dockingSpeed);
            if (_connector != null) _connector.Connect();
            if (_connector != null && _connector.Status == MyShipConnectorStatus.Connected)
            {
                StopAutopilot();
                _emergencyPhase = 0;
                _state = DroneState.WaitingForCharge;
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }
            break;
    }
}

private void StateWaitingForCharge()
{
    if (GetBatteryPercent() >= _resumeBattery)
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
        Disconnect();
        _state = DroneState.DepartingDropoff;
    }
}

// -------------------------------------------------------------------------
// Flight helpers
// -------------------------------------------------------------------------

// Sets a waypoint on the AI Basic block only when the target or direction changes.
// Enables the AI Flight block for physics-based flight control.
private void FlyTo(Vector3D target, float speed,
    Base6Directions.Direction dir = Base6Directions.Direction.Forward)
{
    if (_aiBasic == null || _aiFlightBlock == null) return;
    if (Vector3D.DistanceSquared(target, _lastWaypoint) < 0.01 && dir == _lastDir)
        return;
    _lastWaypoint = target;
    _lastDir      = dir;

    // Enable AI Flight block for physics-based control
    _aiFlightBlock.Enabled = true;
    var flightTerm = _aiFlightBlock as IMyTerminalBlock;
    if (flightTerm != null)
    {
        flightTerm.SetValue<bool>("CollisionAvoidance", true);
        flightTerm.SetValue<bool>("AlignToGravity", true);
    }

    // Feed waypoint to AI Basic block
    _aiBasic.ClearWaypoints();
    _aiBasic.AddWaypoint(target, "WP");
    _aiBasic.SpeedLimit = speed;
    _aiBasic.FlightMode = FlightMode.OneWay;
    _aiBasic.Direction  = dir;
    _aiBasic.SetAutoPilotEnabled(true);
}

private void StopAutopilot()
{
    _lastWaypoint = Vector3D.Zero;
    if (_aiBasic != null)
        _aiBasic.SetAutoPilotEnabled(false);
    if (_aiFlightBlock != null)
        _aiFlightBlock.Enabled = false;
}

private bool HasArrived(Vector3D target)
{
    var pos = _aiBasic != null ? _aiBasic.GetPosition() : Me.GetPosition();
    return Vector3D.Distance(pos, target) <= ARRIVE_TOLERANCE;
}

private void Disconnect()
{
    if (_connector != null)
        _connector.Disconnect();
}

// -------------------------------------------------------------------------
// Safety check
// -------------------------------------------------------------------------
private bool SafeToFly(out string reason)
{
    if (_aiBasic == null || !_aiBasic.IsFunctional)
    {
        reason = "AI Basic block missing or damaged";
        return false;
    }
    if (_aiFlightBlock == null || !_aiFlightBlock.IsFunctional)
    {
        reason = "AI Flight block missing or damaged";
        return false;
    }
    if (_connector == null || !_connector.IsFunctional)
    {
        reason = "Connector missing or damaged";
        return false;
    }
    reason = string.Empty;
    return true;
}

// -------------------------------------------------------------------------
// Cargo / battery readings
// -------------------------------------------------------------------------

// Volume-based fill — used only for unloading detection (sorter empties by volume)
private float GetCargoVolumePct()
{
    double current = 0, max = 0;
    foreach (var c in _cargo)
    {
        var inv = c.GetInventory(0);
        current += (double)inv.CurrentVolume;
        max     += (double)inv.MaxVolume;
    }
    if (max <= 0) return 0f;
    return (float)(current / max * 100.0);
}

// Mass-based fill against the calibrated max — cheap per-tick call
private void GetCargoMassInfo(out float cargoKg, out float fillPct)
{
    cargoKg = 0f;
    fillPct = 0f;
    if (_aiBasic == null) return;
    var shipMass = _aiBasic.CalculateShipMass();
    cargoKg = (float)(shipMass.TotalMass - shipMass.BaseMass);
    if (_maxCargoKg > 0f)
        fillPct = cargoKg / _maxCargoKg * 100f;
}

// One-time calibration: measure gravity + upward thrust + base mass,
// compute max safe cargo, store the result. Re-run after adding thrusters.
private void Calibrate()
{
    RefreshBlocks();

    if (_aiBasic == null)
    {
        Echo("CALIBRATE failed: AI Basic block '" + _aiBasicName + "' not found");
        return;
    }

    var gravity = _aiBasic.GetNaturalGravity();
    double gravMag = gravity.Length();
    if (gravMag < 0.01)
    {
        Echo("CALIBRATE failed: no gravity detected — land on a planet first");
        return;
    }

    var gravDir = Vector3D.Normalize(gravity);

    double upwardThrust = 0;
    foreach (var t in _thrusters)
    {
        if (!t.IsWorking) continue;
        var thrustDir = -t.WorldMatrix.Forward;
        double dot = Vector3D.Dot(thrustDir, -gravDir);
        if (dot > 0.7)
            upwardThrust += t.MaxEffectiveThrust * dot;
    }

    if (upwardThrust < 1.0)
    {
        Echo("CALIBRATE failed: no upward thrust detected");
        return;
    }

    var shipMass     = _aiBasic.CalculateShipMass();
    double maxTotal  = upwardThrust / (gravMag * _safetyFactor);
    double maxCargo  = maxTotal - shipMass.BaseMass;

    if (maxCargo <= 0)
    {
        Echo("CALIBRATE failed: base ship mass already exceeds thrust capacity");
        return;
    }

    _maxCargoKg = (float)maxCargo;
    _baseMassKg = shipMass.BaseMass;
    _thrustKN   = (float)(upwardThrust / 1000.0);

    // Calculate max safe cruise speed from horizontal braking thrust and loaded mass.
    // Only thrusters roughly perpendicular to gravity contribute to horizontal braking.
    // Divide by 2: symmetric builds have ~half their horizontal thrust available in
    // any single braking direction.
    double horizontalThrust = 0;
    var gravNorm = Vector3D.Normalize(gravity);
    foreach (var t in _thrusters)
    {
        if (!t.IsWorking) continue;
        var thrustDir = -t.WorldMatrix.Forward;
        if (Math.Abs(Vector3D.Dot(thrustDir, gravNorm)) < 0.3)
            horizontalThrust += t.MaxEffectiveThrust;
    }
    horizontalThrust /= 2.0;

    double loadedMass   = shipMass.BaseMass + _maxCargoKg;
    double brakingAccel = horizontalThrust > 0 ? horizontalThrust / loadedMass : 1.0;

    // v_max² = v_approach² + 2 * a * d  →  v_max = sqrt(v_approach² + 2ad)
    double maxSafeCruise = Math.Sqrt(
        _approachSpeed * _approachSpeed + 2.0 * brakingAccel * _brakingDistance);
    maxSafeCruise = Math.Floor(maxSafeCruise);

    // Auto-apply to Custom Data so it takes effect immediately
    _cruiseSpeed = (float)maxSafeCruise;
    var iniUpdate = new MyIni();
    iniUpdate.TryParse(Me.CustomData);
    iniUpdate.Set("drone", "cruise_speed", _cruiseSpeed);
    Me.CustomData = iniUpdate.ToString();

    SaveStorage();

    var sb = new StringBuilder();
    sb.AppendLine("=== MULE CALIBRATION ===");
    sb.AppendLine("Status  : OK");
    sb.AppendLine("");
    sb.AppendLine("Thrust  : " + _thrustKN.ToString("F1") + " kN");
    sb.AppendLine("Base    : " + _baseMassKg.ToString("F0") + " kg");
    sb.AppendLine("Max load: " + _maxCargoKg.ToString("F0") + " kg");
    sb.AppendLine("Safety  : " + _safetyFactor.ToString("F1") + "x");
    sb.AppendLine("Gravity : " + gravMag.ToString("F2") + " m/s²");
    sb.AppendLine("");
    sb.AppendLine("H.thrust: " + (horizontalThrust / 1000.0).ToString("F1") + " kN");
    sb.AppendLine("Brake a : " + brakingAccel.ToString("F1") + " m/s²");
    sb.AppendLine("Max spd : " + _cruiseSpeed.ToString("F0") + " m/s  (set)");
    ShowMessage(sb.ToString());
    SaveSetup();
    UpdateDisplays();
}

private float GetBatteryPercent()
{
    float stored = 0f, maxStored = 0f;
    foreach (var b in _batteries)
    {
        stored    += b.CurrentStoredPower;
        maxStored += b.MaxStoredPower;
    }
    if (maxStored <= 0f) return 100f;
    return stored / maxStored * 100f;
}

// -------------------------------------------------------------------------
// Displays
// -------------------------------------------------------------------------
private bool SetupComplete()
{
    return _pickup.IsSet && _dropoff.IsSet && _maxCargoKg > 0f;
}

private void UpdateDisplays()
{
    if (!_running && _state != DroneState.Error)
    {
        if (!SetupComplete())
        {
            ShowWizard();
            return;
        }
        if (_state == DroneState.Idle)
        {
            ShowReady();
            return;
        }
    }
    ShowStatus();
}

private void ShowWizard()
{
    var sb = new StringBuilder();
    sb.AppendLine("=== MULE SETUP WIZARD ===");
    sb.AppendLine("");
    sb.AppendLine(_pickup.IsSet   ? "[OK] Pickup set"   : "[ ] Pickup not set");
    sb.AppendLine(_dropoff.IsSet  ? "[OK] Dropoff set"  : "[ ] Dropoff not set");
    sb.AppendLine(_maxCargoKg > 0 ? "[OK] Calibrated"   : "[ ] Not calibrated");
    sb.AppendLine("");
    sb.AppendLine("------------------------");

    if (!_pickup.IsSet)
    {
        sb.AppendLine("STEP 1: SET PICKUP");
        sb.AppendLine("");
        sb.AppendLine("1. Fly to the mining rig");
        sb.AppendLine("2. Align front connector");
        sb.AppendLine("3. Connect to rig");
        sb.AppendLine("4. Run argument:");
        sb.AppendLine("   SET_PICKUP");
    }
    else if (!_dropoff.IsSet)
    {
        sb.AppendLine("STEP 2: SET DROPOFF");
        sb.AppendLine("");
        sb.AppendLine("1. Fly to the station");
        sb.AppendLine("2. Align front connector");
        sb.AppendLine("3. Connect to station");
        sb.AppendLine("4. Run argument:");
        sb.AppendLine("   SET_DROPOFF");
    }
    else
    {
        sb.AppendLine("STEP 3: CALIBRATE");
        sb.AppendLine("");
        sb.AppendLine("Stay docked at pickup or");
        sb.AppendLine("dropoff. Ensure all");
        sb.AppendLine("thrusters are functional.");
        sb.AppendLine("");
        sb.AppendLine("Run argument:");
        sb.AppendLine("   CALIBRATE");
    }

    WriteScreens(sb.ToString());
}

private void ShowReady()
{
    var sb = new StringBuilder();
    sb.AppendLine("=== MULE READY ===");
    sb.AppendLine("");
    sb.AppendLine("[OK] Pickup set");
    sb.AppendLine("[OK] Dropoff set");
    sb.AppendLine("[OK] Calibrated");
    sb.AppendLine("");
    sb.AppendLine("Max load: " + _maxCargoKg.ToString("F0") + " kg");
    sb.AppendLine("Battery : " + GetBatteryPercent().ToString("F0") + "%");
    sb.AppendLine("");
    sb.AppendLine("------------------------");
    sb.AppendLine("Run START to begin.");
    sb.AppendLine("  At pickup  -> loads and goes");
    sb.AppendLine("  At dropoff -> returns to pickup");
    sb.AppendLine("  Undocked   -> flies to pickup");
    WriteScreens(sb.ToString());
}

private void ShowStatus()
{
    float cargoKg, fillPct;
    GetCargoMassInfo(out cargoKg, out fillPct);

    var sb = new StringBuilder();
    sb.AppendLine("=== MULE CARGO DRONE ===");
    sb.AppendLine("State  : " + StateLabel() + (_testMode ? "  [TEST]" : ""));
    sb.AppendLine("Cargo  : " + fillPct.ToString("F0") + "%  ("
        + cargoKg.ToString("F0") + " / " + _maxCargoKg.ToString("F0") + " kg)");
    sb.AppendLine("Battery: " + GetBatteryPercent().ToString("F0") + "%");
    sb.AppendLine("------------------------");
    sb.AppendLine("Pickup : " + (_pickup.IsSet  ? FormatPos(_pickup.ConnectorPos)  : "not set"));
    sb.AppendLine("Dropoff: " + (_dropoff.IsSet ? FormatPos(_dropoff.ConnectorPos) : "not set"));
    sb.AppendLine("------------------------");
    sb.AppendLine("Runs   : " + _runsCompleted);
    sb.AppendLine("------------------------");
    if (_maxCargoKg > 0f)
    {
        sb.AppendLine("Calibrated");
        sb.AppendLine("  Thrust  : " + _thrustKN.ToString("F1") + " kN");
        sb.AppendLine("  Base    : " + _baseMassKg.ToString("F0") + " kg");
        sb.AppendLine("  Max load: " + _maxCargoKg.ToString("F0") + " kg");
    }
    else
    {
        sb.AppendLine("NOT CALIBRATED");
        sb.AppendLine("Run CALIBRATE argument");
    }

    string antennaText = "MULE | " + StateLabel() + (_testMode ? " [TEST]" : "");
    if (_antenna != null)
        _antenna.HudText = antennaText;

    ShowMessage(sb.ToString());
}

private string StateLabel()
{
    switch (_state)
    {
        case DroneState.Idle:                return "IDLE";
        case DroneState.Loading:             return "LOADING";
        case DroneState.DepartingPickup:     return "DEPARTING PICKUP";
        case DroneState.ClimbingFromPickup:  return "CLIMBING";
        case DroneState.FlyingToDropoff:     return "FLYING TO DROPOFF";
        case DroneState.ApproachingDropoff:  return "APPROACHING DROPOFF";
        case DroneState.DockingAtDropoff:    return "DOCKING AT DROPOFF";
        case DroneState.Unloading:           return "UNLOADING";
        case DroneState.DepartingDropoff:    return "DEPARTING DROPOFF";
        case DroneState.ClimbingFromDropoff: return "CLIMBING";
        case DroneState.FlyingToPickup:      return "FLYING TO PICKUP";
        case DroneState.ApproachingPickup:   return "APPROACHING PICKUP";
        case DroneState.DockingAtPickup:     return "DOCKING AT PICKUP";
        case DroneState.EmergencyReturn:     return "LOW BATTERY — RETURNING";
        case DroneState.WaitingForCharge:    return "WAITING FOR CHARGE";
        case DroneState.Error:               return "ERROR";
        default:                            return "UNKNOWN";
    }
}

private void WriteToSurface(IMyCockpit cockpit, int screen, string text)
{
    if (cockpit == null || cockpit.SurfaceCount <= screen) return;
    var surface = cockpit.GetSurface(screen);
    surface.ContentType = ContentType.TEXT_AND_IMAGE;
    surface.WriteText(text);
}

// Writes to LCD and cockpit only — no K menu output
private void WriteScreens(string text)
{
    if (_lcd != null)
    {
        _lcd.ContentType = ContentType.TEXT_AND_IMAGE;
        _lcd.WriteText(text);
    }
    WriteToSurface(_cockpit, _cockpitScreen, text);
}

// Writes to LCD, cockpit, and K menu — used for command confirmations
private void ShowMessage(string text)
{
    WriteScreens(text);
    Echo(text);
}

private string FormatPos(Vector3D v)
{
    return v.X.ToString("F0") + " : " + v.Y.ToString("F0") + " : " + v.Z.ToString("F0");
}

private void SetError(string message)
{
    _state = DroneState.Error;
    _running = false;
    Runtime.UpdateFrequency = UpdateFrequency.None;
    StopAutopilot();
    if (_antenna != null) _antenna.HudText = "MULE | ERROR: " + message;
    Echo("ERROR: " + message);
    UpdateDisplays();
}

// -------------------------------------------------------------------------
// Block refresh
// -------------------------------------------------------------------------
private void RefreshBlocks()
{
    _aiBasic       = GridTerminalSystem.GetBlockWithName(_aiBasicName)   as IMyRemoteControl;
    _aiFlightBlock = GridTerminalSystem.GetBlockWithName(_aiFlightName)  as IMyFunctionalBlock;
    _connector     = GridTerminalSystem.GetBlockWithName(_connectorName) as IMyShipConnector;
    _lcd           = GridTerminalSystem.GetBlockWithName(_lcdName)       as IMyTextPanel;

    _batteries.Clear();
    GridTerminalSystem.GetBlocksOfType(_batteries, b => b.IsSameConstructAs(Me));

    _cargo.Clear();
    GridTerminalSystem.GetBlocksOfType(_cargo, b => b.IsSameConstructAs(Me));

    _thrusters.Clear();
    GridTerminalSystem.GetBlocksOfType(_thrusters, b => b.IsSameConstructAs(Me));

    if (!string.IsNullOrEmpty(_cockpitName))
    {
        _cockpit = GridTerminalSystem.GetBlockWithName(_cockpitName) as IMyCockpit;
    }
    else
    {
        var cockpits = new List<IMyCockpit>();
        GridTerminalSystem.GetBlocksOfType(cockpits, b => b.IsSameConstructAs(Me));
        _cockpit = cockpits.Count > 0 ? cockpits[0] : null;
    }

    // first antenna found
    var antennas = new List<IMyRadioAntenna>();
    GridTerminalSystem.GetBlocksOfType(antennas, b => b.IsSameConstructAs(Me));
    _antenna = antennas.Count > 0 ? antennas[0] : null;
}

// -------------------------------------------------------------------------
// Custom Data parsing
// -------------------------------------------------------------------------
private void ParseCustomData()
{
    var ini = new MyIni();
    if (!ini.TryParse(Me.CustomData)) return;

    _aiBasicName     = ini.Get("drone", "ai_basic_name").ToString(DEFAULT_AI_BASIC_NAME);
    _aiFlightName    = ini.Get("drone", "ai_flight_name").ToString(DEFAULT_AI_FLIGHT_NAME);
    _connectorName   = ini.Get("drone", "connector_name").ToString(DEFAULT_CONNECTOR_NAME);
    _lcdName         = ini.Get("drone", "lcd_name").ToString(DEFAULT_LCD_NAME);
    _cockpitName     = ini.Get("drone", "cockpit_name").ToString(DEFAULT_COCKPIT_NAME);
    _cockpitScreen   = ini.Get("drone", "cockpit_screen").ToInt32(DEFAULT_COCKPIT_SCREEN);
    _cargoThreshold  = (float)ini.Get("drone", "cargo_threshold").ToDouble(DEFAULT_CARGO_THRESHOLD);
    _emptyThreshold  = (float)ini.Get("drone", "empty_threshold").ToDouble(DEFAULT_EMPTY_THRESHOLD);
    _cruiseAltitude  = (float)ini.Get("drone", "cruise_altitude").ToDouble(DEFAULT_CRUISE_ALTITUDE);
    _backupDistance  = (float)ini.Get("drone", "backup_distance").ToDouble(DEFAULT_BACKUP_DISTANCE);
    _minBattery      = (float)ini.Get("drone", "min_battery").ToDouble(DEFAULT_MIN_BATTERY);
    _safetyFactor    = (float)ini.Get("drone", "safety_factor").ToDouble(DEFAULT_SAFETY_FACTOR);
    _resumeBattery   = (float)ini.Get("drone", "resume_battery").ToDouble(DEFAULT_RESUME_BATTERY);
    _cruiseSpeed     = (float)ini.Get("drone", "cruise_speed").ToDouble(DEFAULT_CRUISE_SPEED);
    _approachSpeed   = (float)ini.Get("drone", "approach_speed").ToDouble(DEFAULT_APPROACH_SPEED);
    _dockingSpeed    = (float)ini.Get("drone", "docking_speed").ToDouble(DEFAULT_DOCKING_SPEED);
    _brakingDistance = (float)ini.Get("drone", "braking_distance").ToDouble(DEFAULT_BRAKING_DISTANCE);
}

private void EnsureCustomDataDefaults()
{
    var ini = new MyIni();
    ini.TryParse(Me.CustomData);

    bool changed = false;
    changed |= SetDefault(ini, "drone", "ai_basic_name",   DEFAULT_AI_BASIC_NAME);
    changed |= SetDefault(ini, "drone", "ai_flight_name",  DEFAULT_AI_FLIGHT_NAME);
    changed |= SetDefault(ini, "drone", "connector_name",  DEFAULT_CONNECTOR_NAME);
    changed |= SetDefault(ini, "drone", "lcd_name",        DEFAULT_LCD_NAME);
    changed |= SetDefault(ini, "drone", "cockpit_name",    DEFAULT_COCKPIT_NAME);
    changed |= SetDefault(ini, "drone", "cockpit_screen",  DEFAULT_COCKPIT_SCREEN.ToString());
    changed |= SetDefault(ini, "drone", "cargo_threshold", DEFAULT_CARGO_THRESHOLD.ToString());
    changed |= SetDefault(ini, "drone", "empty_threshold", DEFAULT_EMPTY_THRESHOLD.ToString());
    changed |= SetDefault(ini, "drone", "cruise_altitude", DEFAULT_CRUISE_ALTITUDE.ToString());
    changed |= SetDefault(ini, "drone", "backup_distance", DEFAULT_BACKUP_DISTANCE.ToString());
    changed |= SetDefault(ini, "drone", "min_battery",     DEFAULT_MIN_BATTERY.ToString());
    changed |= SetDefault(ini, "drone", "safety_factor",   DEFAULT_SAFETY_FACTOR.ToString());
    changed |= SetDefault(ini, "drone", "resume_battery",  DEFAULT_RESUME_BATTERY.ToString());
    changed |= SetDefault(ini, "drone", "cruise_speed",    DEFAULT_CRUISE_SPEED.ToString());
    changed |= SetDefault(ini, "drone", "approach_speed",  DEFAULT_APPROACH_SPEED.ToString());
    changed |= SetDefault(ini, "drone", "docking_speed",   DEFAULT_DOCKING_SPEED.ToString());
    changed |= SetDefault(ini, "drone", "braking_distance", DEFAULT_BRAKING_DISTANCE.ToString());

    if (changed)
        Me.CustomData = ini.ToString();
}

private bool SetDefault(MyIni ini, string section, string key, string value)
{
    if (ini.ContainsKey(section, key)) return false;
    ini.Set(section, key, value);
    return true;
}

// -------------------------------------------------------------------------
// Storage — flight state only (setup data lives in Custom Data [setup])
// -------------------------------------------------------------------------
private void SaveStorage()
{
    var sb = new StringBuilder();
    sb.Append(_state).Append(';');
    sb.Append(_running).Append(';');
    sb.Append(_runsCompleted);
    Storage = sb.ToString();
}

private void LoadStorage()
{
    if (string.IsNullOrEmpty(Storage)) return;
    var parts = Storage.Split(';');
    if (parts.Length < 3) return;
    try
    {
        _state         = (DroneState)Enum.Parse(typeof(DroneState), parts[0]);
        _running       = bool.Parse(parts[1]);
        _runsCompleted = int.Parse(parts[2]);
    }
    catch { }
}

// -------------------------------------------------------------------------
// Setup persistence — Custom Data [setup] section
// -------------------------------------------------------------------------
private void SaveSetup()
{
    var ini = new MyIni();
    ini.TryParse(Me.CustomData);

    ini.Set("setup", "pickup_set",        _pickup.IsSet.ToString());
    ini.Set("setup", "pickup_connector",  VecToString(_pickup.ConnectorPos));
    ini.Set("setup", "pickup_approach",   VecToString(_pickup.ApproachPos));
    ini.Set("setup", "pickup_climb",      VecToString(_pickup.ClimbPos));
    ini.Set("setup", "dropoff_set",       _dropoff.IsSet.ToString());
    ini.Set("setup", "dropoff_connector", VecToString(_dropoff.ConnectorPos));
    ini.Set("setup", "dropoff_approach",  VecToString(_dropoff.ApproachPos));
    ini.Set("setup", "dropoff_climb",     VecToString(_dropoff.ClimbPos));
    ini.Set("setup", "max_cargo_kg",      _maxCargoKg.ToString("R"));
    ini.Set("setup", "base_mass_kg",      _baseMassKg.ToString("R"));
    ini.Set("setup", "thrust_kn",         _thrustKN.ToString("R"));

    Me.CustomData = ini.ToString();
}

private void LoadSetup()
{
    var ini = new MyIni();
    if (!ini.TryParse(Me.CustomData)) return;
    if (!ini.ContainsSection("setup")) return;

    bool b;
    bool.TryParse(ini.Get("setup", "pickup_set").ToString("False"),  out b);
    _pickup.IsSet = b;
    _pickup.ConnectorPos = ParseVec(ini.Get("setup", "pickup_connector").ToString("0,0,0"));
    _pickup.ApproachPos  = ParseVec(ini.Get("setup", "pickup_approach").ToString("0,0,0"));
    _pickup.ClimbPos     = ParseVec(ini.Get("setup", "pickup_climb").ToString("0,0,0"));

    bool.TryParse(ini.Get("setup", "dropoff_set").ToString("False"), out b);
    _dropoff.IsSet = b;
    _dropoff.ConnectorPos = ParseVec(ini.Get("setup", "dropoff_connector").ToString("0,0,0"));
    _dropoff.ApproachPos  = ParseVec(ini.Get("setup", "dropoff_approach").ToString("0,0,0"));
    _dropoff.ClimbPos     = ParseVec(ini.Get("setup", "dropoff_climb").ToString("0,0,0"));

    float f;
    if (float.TryParse(ini.Get("setup", "max_cargo_kg").ToString("0"),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out f)) _maxCargoKg = f;
    if (float.TryParse(ini.Get("setup", "base_mass_kg").ToString("0"),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out f)) _baseMassKg = f;
    if (float.TryParse(ini.Get("setup", "thrust_kn").ToString("0"),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out f)) _thrustKN = f;
}

private void ResetSetup()
{
    StopDrone();
    _pickup   = new DockPoint();
    _dropoff  = new DockPoint();
    _maxCargoKg = 0f;
    _baseMassKg = 0f;
    _thrustKN   = 0f;
    _cruiseSpeed = DEFAULT_CRUISE_SPEED;
    Storage = "";

    var ini = new MyIni();
    ini.TryParse(Me.CustomData);
    ini.DeleteSection("setup");
    // restore cruise_speed to default in config section
    ini.Set("drone", "cruise_speed", DEFAULT_CRUISE_SPEED.ToString());
    Me.CustomData = ini.ToString();

    Echo("Reset complete. Setup data cleared.");
    UpdateDisplays();
}

private void AppendVec(StringBuilder sb, Vector3D v)
{
    sb.Append(v.X.ToString("R")).Append(',')
      .Append(v.Y.ToString("R")).Append(',')
      .Append(v.Z.ToString("R"));
}

private string VecToString(Vector3D v)
{
    return v.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
           v.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
           v.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

private Vector3D ParseVec(string s)
{
    var p = s.Split(',');
    return new Vector3D(
        double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
        double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
        double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture));
}
