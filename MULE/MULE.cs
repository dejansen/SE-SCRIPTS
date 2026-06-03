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
private const string DEFAULT_RC_NAME        = "Drone RC";
private const string DEFAULT_CONNECTOR_NAME = "Connector Front";
private const string DEFAULT_LCD_NAME       = "Drone LCD";
private const float  DEFAULT_CARGO_THRESHOLD  = 90f;
private const float  DEFAULT_EMPTY_THRESHOLD  = 5f;
private const float  DEFAULT_CRUISE_ALTITUDE  = 200f;
private const float  DEFAULT_BACKUP_DISTANCE  = 15f;
private const float  DEFAULT_MIN_BATTERY      = 20f;
private const float  DEFAULT_SAFETY_FACTOR    = 1.2f;

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
private int        _ticksSinceBlockRefresh = 0;
private const int  BLOCK_REFRESH_INTERVAL  = 300;

// config
private string _rcName        = DEFAULT_RC_NAME;
private string _connectorName = DEFAULT_CONNECTOR_NAME;
private string _lcdName       = DEFAULT_LCD_NAME;
private float  _cargoThreshold  = DEFAULT_CARGO_THRESHOLD;
private float  _emptyThreshold  = DEFAULT_EMPTY_THRESHOLD;
private float  _cruiseAltitude  = DEFAULT_CRUISE_ALTITUDE;
private float  _backupDistance  = DEFAULT_BACKUP_DISTANCE;
private float  _minBattery      = DEFAULT_MIN_BATTERY;
private float  _safetyFactor   = DEFAULT_SAFETY_FACTOR;

// block references
private IMyRemoteControl              _rc;
private IMyShipConnector              _connector;
private IMyTextPanel                  _lcd;
private IMyRadioAntenna               _antenna;
private readonly List<IMyBatteryBlock>    _batteries  = new List<IMyBatteryBlock>();
private readonly List<IMyCargoContainer>  _cargo      = new List<IMyCargoContainer>();
private readonly List<IMyThrust>          _thrusters  = new List<IMyThrust>();
private readonly List<IMyCockpit>         _cockpits   = new List<IMyCockpit>();

// stats
private int    _runsCompleted = 0;
private double _lastRunSeconds = 0;
private double _runStartTime   = 0;

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
    if (_running)
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
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

    var connPos  = _connector.GetPosition();
    var backward = -_connector.WorldMatrix.Forward;

    point.ConnectorPos = connPos;
    point.ApproachPos  = connPos + backward * _backupDistance;
    point.ClimbPos     = new Vector3D(
        point.ApproachPos.X,
        point.ApproachPos.Y + _cruiseAltitude,
        point.ApproachPos.Z);
    point.IsSet = true;

    var sb = new StringBuilder();
    sb.AppendLine("=== MULE SETUP ===");
    sb.AppendLine(label + " saved.");
    sb.AppendLine("");
    sb.AppendLine("X : " + connPos.X.ToString("F0"));
    sb.AppendLine("Y : " + connPos.Y.ToString("F0"));
    sb.AppendLine("Z : " + connPos.Z.ToString("F0"));
    sb.AppendLine("");
    sb.AppendLine("Approach offset: " + _backupDistance.ToString("F0") + " m");
    sb.AppendLine("Cruise altitude: " + _cruiseAltitude.ToString("F0") + " m");
    ShowMessage(sb.ToString());
}

// -------------------------------------------------------------------------
// Start / Stop
// -------------------------------------------------------------------------
private void StartDrone()
{
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
    if (!SafeToFly(out string reason))
    {
        SetError("Cannot start: " + reason);
        return;
    }

    _running = true;
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    if (_connector != null && _connector.Status == MyShipConnectorStatus.Connected)
        _state = DroneState.Loading;
    else
        _state = DroneState.DockingAtPickup;

    _runStartTime = Runtime.TimeSinceLastRun.TotalSeconds;
    Echo("MULE started.");
}

private void StopDrone()
{
    _running = false;
    Runtime.UpdateFrequency = UpdateFrequency.None;
    if (_rc != null)
        _rc.SetAutoPilotEnabled(false);
    _state = DroneState.Idle;
    Echo("MULE stopped.");
}

// -------------------------------------------------------------------------
// State machine
// -------------------------------------------------------------------------
private void RunStateMachine()
{
    switch (_state)
    {
        case DroneState.Loading:           StateLoading();           break;
        case DroneState.DepartingPickup:   StateDeparting(true);     break;
        case DroneState.ClimbingFromPickup:StateClimbing(true);      break;
        case DroneState.FlyingToDropoff:   StateFlyingTo(false);     break;
        case DroneState.ApproachingDropoff:StateApproaching(false);  break;
        case DroneState.DockingAtDropoff:  StateDocking(false);      break;
        case DroneState.Unloading:         StateUnloading();         break;
        case DroneState.DepartingDropoff:  StateDeparting(false);    break;
        case DroneState.ClimbingFromDropoff:StateClimbing(false);    break;
        case DroneState.FlyingToPickup:    StateFlyingTo(true);      break;
        case DroneState.ApproachingPickup: StateApproaching(true);   break;
        case DroneState.DockingAtPickup:   StateDocking(true);       break;
        case DroneState.Error:             break;
        default:                           break;
    }
}

private void StateLoading()
{
    if (!SafeToFly(out string reason))
    {
        SetError("Hold: " + reason);
        return;
    }
    float cargoKg, fillPct;
    GetCargoMassInfo(out cargoKg, out fillPct);
    if (fillPct >= _cargoThreshold)
    {
        _runStartTime = Runtime.TimeSinceLastRun.TotalSeconds;
        Disconnect();
        _state = DroneState.DepartingPickup;
    }
}

private void StateDeparting(bool fromPickup)
{
    var target = fromPickup ? _pickup.ApproachPos : _dropoff.ApproachPos;
    FlyTo(target, 5f);
    if (HasArrived(target, 3f))
        _state = fromPickup ? DroneState.ClimbingFromPickup : DroneState.ClimbingFromDropoff;
}

private void StateClimbing(bool fromPickup)
{
    var climbTarget = fromPickup ? _pickup.ClimbPos : _dropoff.ClimbPos;
    FlyTo(climbTarget, 10f);
    if (HasArrived(climbTarget, 10f))
        _state = fromPickup ? DroneState.FlyingToDropoff : DroneState.FlyingToPickup;
}

private void StateFlyingTo(bool toPickup)
{
    var target = toPickup ? _pickup.ClimbPos : _dropoff.ClimbPos;
    FlyTo(target, 15f);
    if (HasArrived(target, 15f))
        _state = toPickup ? DroneState.ApproachingPickup : DroneState.ApproachingDropoff;
}

private void StateApproaching(bool toPickup)
{
    var target = toPickup ? _pickup.ApproachPos : _dropoff.ApproachPos;
    FlyTo(target, 5f);
    if (HasArrived(target, 3f))
    {
        _approachStarted = false;
        _state = toPickup ? DroneState.DockingAtPickup : DroneState.DockingAtDropoff;
    }
}

private void StateDocking(bool atPickup)
{
    var connTarget = atPickup ? _pickup.ConnectorPos : _dropoff.ConnectorPos;
    FlyTo(connTarget, 2f);

    if (_connector != null)
        _connector.Connect();

    if (_connector != null && _connector.Status == MyShipConnectorStatus.Connected)
    {
        if (_rc != null)
            _rc.SetAutoPilotEnabled(false);

        if (atPickup)
            _state = DroneState.Loading;
        else
            _state = DroneState.Unloading;
    }
}

private void StateUnloading()
{
    float fill = GetCargoVolumePct();
    if (fill <= _emptyThreshold)
    {
        _runsCompleted++;
        _lastRunSeconds += Runtime.TimeSinceLastRun.TotalSeconds - _runStartTime;
        Disconnect();
        _state = DroneState.DepartingDropoff;
    }
}

// -------------------------------------------------------------------------
// Flight helpers
// -------------------------------------------------------------------------
private void FlyTo(Vector3D target, float speed)
{
    if (_rc == null) return;
    _rc.ClearWaypoints();
    _rc.AddWaypoint(target, "WP");
    _rc.SetValueFloat("SpeedLimit", speed);
    _rc.FlightMode = FlightMode.OneWay;
    _rc.SetAutoPilotEnabled(true);
}

private bool HasArrived(Vector3D target, float tolerance)
{
    return Vector3D.Distance(Me.GetPosition(), target) <= tolerance;
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
    float bat = GetBatteryPercent();
    if (bat < _minBattery)
    {
        reason = "Battery " + bat.ToString("F0") + "% (min " + _minBattery.ToString("F0") + "%)";
        return false;
    }
    if (_rc == null || !_rc.IsFunctional)
    {
        reason = "RC block missing or damaged";
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
    if (_rc == null) return;
    var shipMass = _rc.CalculateShipMass();
    cargoKg = (float)(shipMass.TotalMass - shipMass.BaseMass);
    if (_maxCargoKg > 0f)
        fillPct = cargoKg / _maxCargoKg * 100f;
}

// One-time calibration: measure gravity + upward thrust + base mass,
// compute max safe cargo, store the result. Re-run after adding thrusters.
private void Calibrate()
{
    RefreshBlocks();

    if (_rc == null)
    {
        Echo("CALIBRATE failed: RC block '" + _rcName + "' not found");
        return;
    }

    var gravity = _rc.GetNaturalGravity();
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

    var shipMass     = _rc.CalculateShipMass();
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
    SaveStorage();

    var sb = new StringBuilder();
    sb.AppendLine("=== MULE CALIBRATION ===");
    sb.AppendLine("Status  : OK");
    sb.AppendLine("");
    sb.AppendLine("Thrust  : " + _thrustKN.ToString("F1") + " kN");
    sb.AppendLine("Base    : " + _baseMassKg.ToString("F0") + " kg");
    sb.AppendLine("Max load: " + _maxCargoKg.ToString("F0") + " kg");
    sb.AppendLine("Safety  : " + _safetyFactor.ToString("F1") + "x");
    sb.AppendLine("Gravity : " + (gravMag).ToString("F2") + " m/s²");
    ShowMessage(sb.ToString());
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
private void UpdateDisplays()
{
    float cargoKg, fillPct;
    GetCargoMassInfo(out cargoKg, out fillPct);

    var sb = new StringBuilder();
    sb.AppendLine("=== MULE CARGO DRONE ===");
    sb.AppendLine("State  : " + StateLabel());
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

    string lcdText = sb.ToString();

    if (_lcd != null)
    {
        _lcd.ContentType = ContentType.TEXT_AND_IMAGE;
        _lcd.WriteText(lcdText);
    }

    foreach (var cockpit in _cockpits)
    {
        if (cockpit.SurfaceCount > 0)
        {
            var surface = cockpit.GetSurface(0);
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.WriteText(lcdText);
        }
    }

    string antennaText = "MULE | " + StateLabel();
    if (_antenna != null)
        _antenna.HudText = antennaText;

    Echo(lcdText);
}

private string StateLabel()
{
    switch (_state)
    {
        case DroneState.Idle:               return "IDLE";
        case DroneState.Loading:            return "LOADING";
        case DroneState.DepartingPickup:    return "DEPARTING PICKUP";
        case DroneState.ClimbingFromPickup: return "CLIMBING";
        case DroneState.FlyingToDropoff:    return "FLYING TO DROPOFF";
        case DroneState.ApproachingDropoff: return "APPROACHING DROPOFF";
        case DroneState.DockingAtDropoff:   return "DOCKING AT DROPOFF";
        case DroneState.Unloading:          return "UNLOADING";
        case DroneState.DepartingDropoff:   return "DEPARTING DROPOFF";
        case DroneState.ClimbingFromDropoff:return "CLIMBING";
        case DroneState.FlyingToPickup:     return "FLYING TO PICKUP";
        case DroneState.ApproachingPickup:  return "APPROACHING PICKUP";
        case DroneState.DockingAtPickup:    return "DOCKING AT PICKUP";
        case DroneState.Error:              return "ERROR";
        default:                            return "UNKNOWN";
    }
}

private void ShowMessage(string text)
{
    if (_lcd != null)
    {
        _lcd.ContentType = ContentType.TEXT_AND_IMAGE;
        _lcd.WriteText(text);
    }
    foreach (var cockpit in _cockpits)
    {
        if (cockpit.SurfaceCount > 0)
        {
            var surface = cockpit.GetSurface(0);
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.WriteText(text);
        }
    }
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
    if (_rc != null) _rc.SetAutoPilotEnabled(false);
    if (_antenna != null) _antenna.HudText = "MULE | ERROR: " + message;
    Echo("ERROR: " + message);
    UpdateDisplays();
}

// -------------------------------------------------------------------------
// Block refresh
// -------------------------------------------------------------------------
private void RefreshBlocks()
{
    _rc        = GridTerminalSystem.GetBlockWithName(_rcName)        as IMyRemoteControl;
    _connector = GridTerminalSystem.GetBlockWithName(_connectorName) as IMyShipConnector;
    _lcd       = GridTerminalSystem.GetBlockWithName(_lcdName)       as IMyTextPanel;

    _batteries.Clear();
    GridTerminalSystem.GetBlocksOfType(_batteries, b => b.IsSameConstructAs(Me));

    _cargo.Clear();
    GridTerminalSystem.GetBlocksOfType(_cargo, b => b.IsSameConstructAs(Me));

    _thrusters.Clear();
    GridTerminalSystem.GetBlocksOfType(_thrusters, b => b.IsSameConstructAs(Me));

    _cockpits.Clear();
    GridTerminalSystem.GetBlocksOfType(_cockpits, b => b.IsSameConstructAs(Me));

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

    _rcName          = ini.Get("drone", "rc_name").ToString(DEFAULT_RC_NAME);
    _connectorName   = ini.Get("drone", "connector_name").ToString(DEFAULT_CONNECTOR_NAME);
    _lcdName         = ini.Get("drone", "lcd_name").ToString(DEFAULT_LCD_NAME);
    _cargoThreshold  = (float)ini.Get("drone", "cargo_threshold").ToDouble(DEFAULT_CARGO_THRESHOLD);
    _emptyThreshold  = (float)ini.Get("drone", "empty_threshold").ToDouble(DEFAULT_EMPTY_THRESHOLD);
    _cruiseAltitude  = (float)ini.Get("drone", "cruise_altitude").ToDouble(DEFAULT_CRUISE_ALTITUDE);
    _backupDistance  = (float)ini.Get("drone", "backup_distance").ToDouble(DEFAULT_BACKUP_DISTANCE);
    _minBattery      = (float)ini.Get("drone", "min_battery").ToDouble(DEFAULT_MIN_BATTERY);
    _safetyFactor    = (float)ini.Get("drone", "safety_factor").ToDouble(DEFAULT_SAFETY_FACTOR);
}

private void EnsureCustomDataDefaults()
{
    var ini = new MyIni();
    ini.TryParse(Me.CustomData);

    if (!ini.ContainsSection("drone"))
    {
        ini.Set("drone", "rc_name",          DEFAULT_RC_NAME);
        ini.Set("drone", "connector_name",   DEFAULT_CONNECTOR_NAME);
        ini.Set("drone", "lcd_name",         DEFAULT_LCD_NAME);
        ini.Set("drone", "cargo_threshold",  DEFAULT_CARGO_THRESHOLD);
        ini.Set("drone", "empty_threshold",  DEFAULT_EMPTY_THRESHOLD);
        ini.Set("drone", "cruise_altitude",  DEFAULT_CRUISE_ALTITUDE);
        ini.Set("drone", "backup_distance",  DEFAULT_BACKUP_DISTANCE);
        ini.Set("drone", "min_battery",      DEFAULT_MIN_BATTERY);
        ini.Set("drone", "safety_factor",    DEFAULT_SAFETY_FACTOR);
        Me.CustomData = ini.ToString();
    }
}

// -------------------------------------------------------------------------
// Storage persistence
// -------------------------------------------------------------------------
private void SaveStorage()
{
    var sb = new StringBuilder();
    sb.Append(_state).Append(';');
    sb.Append(_running).Append(';');
    sb.Append(_runsCompleted).Append(';');
    sb.Append(_pickup.IsSet).Append(';');
    AppendVec(sb, _pickup.ConnectorPos); sb.Append(';');
    AppendVec(sb, _pickup.ApproachPos);  sb.Append(';');
    AppendVec(sb, _pickup.ClimbPos);     sb.Append(';');
    sb.Append(_dropoff.IsSet).Append(';');
    AppendVec(sb, _dropoff.ConnectorPos); sb.Append(';');
    AppendVec(sb, _dropoff.ApproachPos);  sb.Append(';');
    AppendVec(sb, _dropoff.ClimbPos); sb.Append(';');
    sb.Append(_maxCargoKg.ToString("R")).Append(';');
    sb.Append(_baseMassKg.ToString("R")).Append(';');
    sb.Append(_thrustKN.ToString("R"));
    Storage = sb.ToString();
}

private void LoadStorage()
{
    if (string.IsNullOrEmpty(Storage)) return;
    var parts = Storage.Split(';');
    if (parts.Length < 17) return;
    try
    {
        _state        = (DroneState)Enum.Parse(typeof(DroneState), parts[0]);
        _running      = bool.Parse(parts[1]);
        _runsCompleted= int.Parse(parts[2]);

        _pickup.IsSet        = bool.Parse(parts[3]);
        _pickup.ConnectorPos = ParseVec(parts[4]);
        _pickup.ApproachPos  = ParseVec(parts[5]);
        _pickup.ClimbPos     = ParseVec(parts[6]);

        _dropoff.IsSet        = bool.Parse(parts[7]);
        _dropoff.ConnectorPos = ParseVec(parts[8]);
        _dropoff.ApproachPos  = ParseVec(parts[9]);
        _dropoff.ClimbPos     = ParseVec(parts[10]);

        if (parts.Length > 11)
            float.TryParse(parts[11], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _maxCargoKg);
        if (parts.Length > 12)
            float.TryParse(parts[12], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _baseMassKg);
        if (parts.Length > 13)
            float.TryParse(parts[13], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _thrustKN);
    }
    catch { /* corrupted storage — start fresh */ }
}

private void AppendVec(StringBuilder sb, Vector3D v)
{
    sb.Append(v.X.ToString("R")).Append(',')
      .Append(v.Y.ToString("R")).Append(',')
      .Append(v.Z.ToString("R"));
}

private Vector3D ParseVec(string s)
{
    var p = s.Split(',');
    return new Vector3D(
        double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
        double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
        double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture));
}
