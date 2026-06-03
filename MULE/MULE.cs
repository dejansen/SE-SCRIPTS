// -------------------------------------------------------------------------
// MULE — Cargo Weight Guardian
// -------------------------------------------------------------------------
// Prevents cargo overload by monitoring sorters and turning them off
// when weight limits are reached.
//
// Setup:
// - Front connector + sorter: loading (user activates via event controller)
// - Bottom connector + sorter: unloading (user activates via event controller)
// - Script: monitors weight and shuts off sorter when limit reached
//
// Arguments: CALIBRATE | STATUS
// -------------------------------------------------------------------------

private const string VERSION = "2.0";

// -------------------------------------------------------------------------
// Config defaults
// -------------------------------------------------------------------------
private const string DEFAULT_FRONT_SORTER_NAME      = "SorterFront";
private const string DEFAULT_BOTTOM_SORTER_NAME     = "SorterBottom";
private const string DEFAULT_CONNECTOR_NAME         = "ConnectorFront";
private const string DEFAULT_LCD_NAME               = "DroneLCD";
private const string DEFAULT_COCKPIT_NAME           = "";
private const string DEFAULT_DROPOFF_FLIGHT_TIMER   = "TimerDropoffFlight";
private const string DEFAULT_PICKUP_FLIGHT_TIMER    = "TimerPickupFlight";
private const int    DEFAULT_COCKPIT_SCREEN         = 0;
private const float  DEFAULT_CARGO_THRESHOLD        = 90f;
private const float  DEFAULT_EMPTY_THRESHOLD        = 5f;
private const float  DEFAULT_SAFETY_FACTOR          = 1.2f;
private const float  DEFAULT_MIN_BATTERY_TO_FLY     = 30f;

// -------------------------------------------------------------------------
// Fields
// -------------------------------------------------------------------------
private int _ticksSinceBlockRefresh = 0;
private const int BLOCK_REFRESH_INTERVAL = 300;

// config
private string _frontSorterName        = DEFAULT_FRONT_SORTER_NAME;
private string _bottomSorterName       = DEFAULT_BOTTOM_SORTER_NAME;
private string _connectorName          = DEFAULT_CONNECTOR_NAME;
private string _lcdName                = DEFAULT_LCD_NAME;
private string _cockpitName            = DEFAULT_COCKPIT_NAME;
private string _dropoffFlightTimerName = DEFAULT_DROPOFF_FLIGHT_TIMER;
private string _pickupFlightTimerName  = DEFAULT_PICKUP_FLIGHT_TIMER;
private int    _cockpitScreen          = DEFAULT_COCKPIT_SCREEN;
private float  _cargoThreshold         = DEFAULT_CARGO_THRESHOLD;
private float  _emptyThreshold         = DEFAULT_EMPTY_THRESHOLD;
private float  _safetyFactor           = DEFAULT_SAFETY_FACTOR;
private float  _minBatteryToFly        = DEFAULT_MIN_BATTERY_TO_FLY;

// block references
private IMyTerminalBlock _frontSorter;
private IMyTerminalBlock _bottomSorter;
private IMyShipConnector _connector;
private IMyTextPanel     _lcd;
private IMyCockpit       _cockpit;
private IMyShipController _shipController;
private IMyTimerBlock    _dropoffFlightTimer;
private IMyTimerBlock    _pickupFlightTimer;
private IMyRadioAntenna  _antenna;

private readonly List<IMyBatteryBlock>   _batteries  = new List<IMyBatteryBlock>();
private readonly List<IMyCargoContainer> _cargo      = new List<IMyCargoContainer>();
private readonly List<IMyThrust>         _thrusters  = new List<IMyThrust>();

// calibration
private float _maxCargoKg = 0f;
private float _baseMassKg = 0f;
private float _thrustKN   = 0f;

// -------------------------------------------------------------------------
// Constructor
// -------------------------------------------------------------------------
public Program()
{
    Echo("=== MULE v" + VERSION + " ===");
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    RefreshBlocks();
    ParseCustomData();
    EnsureCustomDataDefaults();
    LoadCalibration();
    UpdateDisplays();
}

// -------------------------------------------------------------------------
// Save
// -------------------------------------------------------------------------
public void Save()
{
    SaveCalibration();
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

    MonitorAndControlSorters();
    UpdateDisplays();
}

// -------------------------------------------------------------------------
// Argument handling
// -------------------------------------------------------------------------
private void HandleArgument(string arg)
{
    switch (arg)
    {
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
}

// -------------------------------------------------------------------------
// Main logic: monitor cargo weight, control sorters, and start flight timers
// -------------------------------------------------------------------------
private void MonitorAndControlSorters()
{
    RefreshBlocks();

    float cargoKg, fillPct;
    GetCargoMassInfo(out cargoKg, out fillPct);
    float batteryPct = GetBatteryPercent();

    // Check if sorters are active
    bool frontActive = _frontSorter != null && _frontSorter.GetValue<bool>("OnOff");
    bool bottomActive = _bottomSorter != null && _bottomSorter.GetValue<bool>("OnOff");

    // Turn off front sorter if max weight reached (loading)
    if (frontActive && _maxCargoKg > 0f && fillPct >= _cargoThreshold)
    {
        _frontSorter.SetValue<bool>("OnOff", false);
        Echo("Front sorter OFF — max load reached (" + fillPct.ToString("F0") + "%)");
    }

    // Turn off bottom sorter if cargo empty (unloading)
    if (bottomActive && GetCargoVolumePct() <= _emptyThreshold)
    {
        _bottomSorter.SetValue<bool>("OnOff", false);
        Echo("Bottom sorter OFF — cargo empty");
    }

    // Check connector status and control timers
    bool isDocked = _connector != null && _connector.Status == MyShipConnectorStatus.Connected;

    if (batteryPct < _minBatteryToFly)
    {
        // Battery low - don't start any timers, halt operations
        return;
    }

    // At pickup: cargo full (≥90%) → start dropoff flight timer
    if (isDocked && fillPct >= _cargoThreshold && _maxCargoKg > 0f)
    {
        if (_dropoffFlightTimer != null && !_dropoffFlightTimer.IsCountingDown)
        {
            _dropoffFlightTimer.ApplyAction("Start");
            Echo("Dropoff flight timer started");
        }
    }

    // At dropoff: cargo empty (≤5%) → start pickup flight timer
    if (isDocked && GetCargoVolumePct() <= _emptyThreshold)
    {
        if (_pickupFlightTimer != null && !_pickupFlightTimer.IsCountingDown)
        {
            _pickupFlightTimer.ApplyAction("Start");
            Echo("Pickup flight timer started");
        }
    }
}

// -------------------------------------------------------------------------
// Cargo / battery readings
// -------------------------------------------------------------------------

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

private void GetCargoMassInfo(out float cargoKg, out float fillPct)
{
    cargoKg = 0f;
    fillPct = 0f;
    if (_shipController == null) return;
    var shipMass = _shipController.CalculateShipMass();
    cargoKg = (float)(shipMass.TotalMass - shipMass.BaseMass);
    if (_maxCargoKg > 0f)
        fillPct = cargoKg / _maxCargoKg * 100f;
}

private void Calibrate()
{
    RefreshBlocks();

    if (_shipController == null)
    {
        Echo("CALIBRATE failed: no ship controller found");
        return;
    }

    var gravity = _shipController.GetNaturalGravity();
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

    var shipMass   = _shipController.CalculateShipMass();
    double maxTotal = upwardThrust / (gravMag * _safetyFactor);
    double maxCargo = maxTotal - shipMass.BaseMass;

    if (maxCargo <= 0)
    {
        Echo("CALIBRATE failed: base ship mass exceeds thrust capacity");
        return;
    }

    _maxCargoKg = (float)maxCargo;
    _baseMassKg = shipMass.BaseMass;
    _thrustKN   = (float)(upwardThrust / 1000.0);

    SaveCalibration();

    var sb = new StringBuilder();
    sb.AppendLine("=== MULE CALIBRATION ===");
    sb.AppendLine("Status  : OK");
    sb.AppendLine("");
    sb.AppendLine("Thrust  : " + _thrustKN.ToString("F1") + " kN");
    sb.AppendLine("Base    : " + _baseMassKg.ToString("F0") + " kg");
    sb.AppendLine("Max load: " + _maxCargoKg.ToString("F0") + " kg");
    sb.AppendLine("Safety  : " + _safetyFactor.ToString("F1") + "x");
    sb.AppendLine("Gravity : " + gravMag.ToString("F2") + " m/s²");
    ShowMessage(sb.ToString());
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
// Display
// -------------------------------------------------------------------------
private void UpdateDisplays()
{
    ShowStatus();
}

private void ShowStatus()
{
    float cargoKg, fillPct;
    GetCargoMassInfo(out cargoKg, out fillPct);
    float batteryPct = GetBatteryPercent();

    bool frontActive = _frontSorter != null && _frontSorter.GetValue<bool>("OnOff");
    bool bottomActive = _bottomSorter != null && _bottomSorter.GetValue<bool>("OnOff");
    bool isDocked = _connector != null && _connector.Status == MyShipConnectorStatus.Connected;

    var sb = new StringBuilder();
    sb.AppendLine("=== MULE WEIGHT GUARDIAN v" + VERSION + " ===");
    sb.AppendLine("Cargo  : " + fillPct.ToString("F0") + "%  ("
        + cargoKg.ToString("F0") + " / " + _maxCargoKg.ToString("F0") + " kg)");
    sb.AppendLine("Volume : " + GetCargoVolumePct().ToString("F0") + "%");
    sb.AppendLine("Battery: " + batteryPct.ToString("F0") + "%");
    if (batteryPct < _minBatteryToFly)
    {
        sb.AppendLine("  ⚠ LOW - Operations halted");
    }
    sb.AppendLine("------------------------");
    sb.AppendLine("Docked : " + (isDocked ? "YES" : "NO"));
    sb.AppendLine("Front sorter : " + (frontActive ? "ON" : "OFF"));
    sb.AppendLine("Bottom sorter: " + (bottomActive ? "ON" : "OFF"));
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

    ShowMessage(sb.ToString());
}

private void WriteToSurface(IMyCockpit cockpit, int screen, string text)
{
    if (cockpit == null || cockpit.SurfaceCount <= screen) return;
    var surface = cockpit.GetSurface(screen);
    surface.ContentType = ContentType.TEXT_AND_IMAGE;
    surface.WriteText(text);
}

private void WriteScreens(string text)
{
    if (_lcd != null)
    {
        _lcd.ContentType = ContentType.TEXT_AND_IMAGE;
        _lcd.WriteText(text);
    }
    WriteToSurface(_cockpit, _cockpitScreen, text);
}

private void ShowMessage(string text)
{
    WriteScreens(text);
    Echo(text);
    BroadcastStatus();
}

private void BroadcastStatus()
{
    if (_antenna == null) return;

    float batteryPct = GetBatteryPercent();
    float cargoKg, fillPct;
    GetCargoMassInfo(out cargoKg, out fillPct);

    string status = "MULE | ";
    if (batteryPct < _minBatteryToFly)
    {
        status += "Battery " + batteryPct.ToString("F0") + "% (min " + _minBatteryToFly.ToString("F0") + "%)";
    }
    else if (_maxCargoKg <= 0f)
    {
        status += "NOT CALIBRATED";
    }
    else if (fillPct >= _cargoThreshold)
    {
        status += "FULL (" + fillPct.ToString("F0") + "%) Ready for dropoff";
    }
    else if (GetCargoVolumePct() <= _emptyThreshold)
    {
        status += "EMPTY (" + GetCargoVolumePct().ToString("F0") + "%) Ready for pickup";
    }
    else
    {
        status += "Loading " + fillPct.ToString("F0") + "%";
    }

    _antenna.HudText = status;
}

// -------------------------------------------------------------------------
// Block refresh
// -------------------------------------------------------------------------
private void RefreshBlocks()
{
    _frontSorter  = GridTerminalSystem.GetBlockWithName(_frontSorterName);
    _bottomSorter = GridTerminalSystem.GetBlockWithName(_bottomSorterName);
    _connector    = GridTerminalSystem.GetBlockWithName(_connectorName) as IMyShipConnector;
    _lcd          = GridTerminalSystem.GetBlockWithName(_lcdName) as IMyTextPanel;
    _dropoffFlightTimer = GridTerminalSystem.GetBlockWithName(_dropoffFlightTimerName) as IMyTimerBlock;
    _pickupFlightTimer  = GridTerminalSystem.GetBlockWithName(_pickupFlightTimerName) as IMyTimerBlock;

    // Find first antenna for HUD broadcasts
    var antennas = new List<IMyRadioAntenna>();
    GridTerminalSystem.GetBlocksOfType(antennas, b => b.IsSameConstructAs(Me));
    _antenna = antennas.Count > 0 ? antennas[0] : null;

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

    // Find any ship controller for calibration
    var shipControllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers);
    _shipController = shipControllers.Count > 0 ? shipControllers[0] : null;
}

// -------------------------------------------------------------------------
// Custom Data parsing
// -------------------------------------------------------------------------
private void ParseCustomData()
{
    var ini = new MyIni();
    if (!ini.TryParse(Me.CustomData)) return;

    _frontSorterName        = ini.Get("mule", "front_sorter_name").ToString(DEFAULT_FRONT_SORTER_NAME);
    _bottomSorterName       = ini.Get("mule", "bottom_sorter_name").ToString(DEFAULT_BOTTOM_SORTER_NAME);
    _connectorName          = ini.Get("mule", "connector_name").ToString(DEFAULT_CONNECTOR_NAME);
    _lcdName                = ini.Get("mule", "lcd_name").ToString(DEFAULT_LCD_NAME);
    _cockpitName            = ini.Get("mule", "cockpit_name").ToString(DEFAULT_COCKPIT_NAME);
    _dropoffFlightTimerName = ini.Get("mule", "dropoff_flight_timer").ToString(DEFAULT_DROPOFF_FLIGHT_TIMER);
    _pickupFlightTimerName  = ini.Get("mule", "pickup_flight_timer").ToString(DEFAULT_PICKUP_FLIGHT_TIMER);
    _cockpitScreen          = ini.Get("mule", "cockpit_screen").ToInt32(DEFAULT_COCKPIT_SCREEN);
    _cargoThreshold         = (float)ini.Get("mule", "cargo_threshold").ToDouble(DEFAULT_CARGO_THRESHOLD);
    _emptyThreshold         = (float)ini.Get("mule", "empty_threshold").ToDouble(DEFAULT_EMPTY_THRESHOLD);
    _safetyFactor           = (float)ini.Get("mule", "safety_factor").ToDouble(DEFAULT_SAFETY_FACTOR);
    _minBatteryToFly        = (float)ini.Get("mule", "min_battery_to_fly").ToDouble(DEFAULT_MIN_BATTERY_TO_FLY);
}

private void EnsureCustomDataDefaults()
{
    var ini = new MyIni();
    ini.TryParse(Me.CustomData);

    bool changed = false;
    changed |= SetDefault(ini, "mule", "front_sorter_name",      DEFAULT_FRONT_SORTER_NAME);
    changed |= SetDefault(ini, "mule", "bottom_sorter_name",     DEFAULT_BOTTOM_SORTER_NAME);
    changed |= SetDefault(ini, "mule", "connector_name",         DEFAULT_CONNECTOR_NAME);
    changed |= SetDefault(ini, "mule", "lcd_name",               DEFAULT_LCD_NAME);
    changed |= SetDefault(ini, "mule", "cockpit_name",           DEFAULT_COCKPIT_NAME);
    changed |= SetDefault(ini, "mule", "dropoff_flight_timer",   DEFAULT_DROPOFF_FLIGHT_TIMER);
    changed |= SetDefault(ini, "mule", "pickup_flight_timer",    DEFAULT_PICKUP_FLIGHT_TIMER);
    changed |= SetDefault(ini, "mule", "cockpit_screen",         DEFAULT_COCKPIT_SCREEN.ToString());
    changed |= SetDefault(ini, "mule", "cargo_threshold",        DEFAULT_CARGO_THRESHOLD.ToString());
    changed |= SetDefault(ini, "mule", "empty_threshold",        DEFAULT_EMPTY_THRESHOLD.ToString());
    changed |= SetDefault(ini, "mule", "safety_factor",          DEFAULT_SAFETY_FACTOR.ToString());
    changed |= SetDefault(ini, "mule", "min_battery_to_fly",     DEFAULT_MIN_BATTERY_TO_FLY.ToString());

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
// Calibration storage
// -------------------------------------------------------------------------
private void SaveCalibration()
{
    var ini = new MyIni();
    ini.TryParse(Me.CustomData);

    ini.Set("calibration", "max_cargo_kg", _maxCargoKg.ToString("R"));
    ini.Set("calibration", "base_mass_kg", _baseMassKg.ToString("R"));
    ini.Set("calibration", "thrust_kn",    _thrustKN.ToString("R"));

    Me.CustomData = ini.ToString();
}

private void LoadCalibration()
{
    var ini = new MyIni();
    if (!ini.TryParse(Me.CustomData)) return;
    if (!ini.ContainsSection("calibration")) return;

    float f;
    if (float.TryParse(ini.Get("calibration", "max_cargo_kg").ToString("0"),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out f)) _maxCargoKg = f;
    if (float.TryParse(ini.Get("calibration", "base_mass_kg").ToString("0"),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out f)) _baseMassKg = f;
    if (float.TryParse(ini.Get("calibration", "thrust_kn").ToString("0"),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out f)) _thrustKN = f;
}
