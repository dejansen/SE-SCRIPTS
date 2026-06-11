using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using VRage.Game.GUI.TextPanel;
using VRage.Collections;
using VRage;

namespace Script
{
    public sealed class Program : MyGridProgram
    {
        private const string VERSION    = "1.5";
        private const string SCREEN_TAG = "[AGM-S]";
        private const string LIGHT_TAG  = "[AGM-LIGHT]";
        private static readonly StringComparison SC = StringComparison.OrdinalIgnoreCase;

        private readonly Color COL_BG        = new Color(1,8,13);
        private readonly Color COL_PANEL     = new Color(2,18,28);
        private readonly Color COL_PANEL2    = new Color(3,58,78);
        private readonly Color COL_ACCENT    = new Color(38,239,255);
        private readonly Color COL_ACCENT2   = new Color(112,247,255);
        private readonly Color COL_TEXT      = new Color(126,246,255);
        private readonly Color COL_DIM       = new Color(44,177,195);
        private readonly Color COL_ROW_TEXT  = new Color(126,246,255);
        private readonly Color COL_ROW_DIM   = new Color(63,207,222);
        private readonly Color COL_OK        = new Color(97,255,214);
        private readonly Color COL_WARN      = new Color(255,202,34);
        private readonly Color COL_BAD       = new Color(255,79,66);
        private readonly Color COL_PROG_BG   = new Color(18,48,32);
        private readonly Color COL_PROG_FILL = new Color(255,204,36);

        private readonly Color LIGHT_GREEN = new Color(0,255,0);
        private readonly Color LIGHT_AMBER = new Color(255,160,0);
        private readonly Color LIGHT_RED   = new Color(255,0,0);

        private const int ALERT_OK       = 0;
        private const int ALERT_WARNING  = 1;
        private const int ALERT_CRITICAL = 2;

        private class StockEntry  { public string Category,Name,Icon; public double Amount; }
        private class CargoInfo   { public IMyTerminalBlock Block; public IMyInventory Inv; public string Type; public int Index; public bool Locked,Hidden,Manual; }
        private class SourceInfo  { public IMyTerminalBlock Block; public IMyInventory Inv; public string Type; }
        private class PowerProfile{ public string Name="Base",Batteries="",Reactors="",Solar="",Wind="",Hydrogen=""; public bool IncludeUngrouped=false; }
        private class PowerStats  { public int Batteries,Reactors,Solar,Wind,Hydrogen,Producers; public double Stored,Capacity,Input,Output,MaxOutput; }
        private class ReactorInfo { public IMyReactor Reactor; public double UraniumKg; }

        private readonly List<IMyTerminalBlock> _blocks  = new List<IMyTerminalBlock>();
        private readonly List<IMyTerminalBlock> _screens = new List<IMyTerminalBlock>();
        private readonly List<MyInventoryItem>  _invItems= new List<MyInventoryItem>();
        private readonly List<MyInventoryItem>  _srcItems= new List<MyInventoryItem>();
        private readonly List<IMyReactor>       _reactorsCtl = new List<IMyReactor>();
        private readonly List<IMyBatteryBlock>  _batteriesCtl= new List<IMyBatteryBlock>();
        private readonly List<ReactorInfo>      _reactorInfos= new List<ReactorInfo>();

        // Alert corner LCDs — drawn every tick, state updated by RunWarningLights
        private class AlertLcdEntry { public IMyTextSurface Surface; public int State; public string Watch; }
        private readonly List<AlertLcdEntry> _alertLcds = new List<AlertLcdEntry>();
        private int    _asmScroll  = 0;
        private double _asmScrollT = 0.0;
        private const double PROD_SCROLL_INTERVAL = 3.0;
        private readonly StringBuilder          _sb      = new StringBuilder();

        private int    _drawTick    = 0;
        private int    _workStage   = 0;
        private int    _bootDrawIdx = 0;
        private bool   _booting     = true;
        private double _bootElapsed = 0.0;
        private int    _bootDots    = 0;
        private double _bootDotTimer= 0.0;

        private bool   _globalPause        = false;
        private bool   _includeDockedGrids = false;
        private string _noSortTag  = "[No Sorting]";
        private readonly HashSet<IMyCubeGrid> _dockedGridIds = new HashSet<IMyCubeGrid>();
        private string _lockedTag  = "{Locked}";
        private string _manualTag  = "{Manual}";
        private string _hiddenTag  = "{Hidden}";

        private readonly List<PowerProfile>     _powerProfiles = new List<PowerProfile>();
        private readonly List<IMyTerminalBlock> _groupBuf      = new List<IMyTerminalBlock>();
        private readonly HashSet<long>          _selectedIds   = new HashSet<long>();
        private bool _powerEnabled = true;
        private bool   _reactorRefuelEnabled = true;
        private bool   _reactorAutoRefuel    = false;
        private double _minUraniumPerReactor = 2.0;
        private double _targetUraniumPerReactor = 10.0;
        private double _reactorUraniumLowKg  = 5.0;
        private double _uraniumStockKg       = 0.0;
        private string _lowestReactorName    = "-";
        private double _lowestReactorKg      = 0.0;
        private string _reactorRefuelStatus  = "OK";

        private bool   _powerControlEnabled  = true;
        private bool   _autoReactorCharge    = true;
        private double _pcBattLow            = 25.0;
        private double _pcBattFull           = 100.0;
        private string _pcReactors           = "";
        private string _pcBatteries          = "";
        private bool   _turnReactorsOffWhenFull = true;
        private bool   _amberWhileCharging   = true;
        private int    _minimumReactorsOnline= 0;
        private double _neverOffOutputPct    = 80.0;
        private string _powerControlStatus   = "MONITOR";
        private bool   _reactorsForcedOn     = false;
        private bool   _powerSafetyHold      = false;
        private int    _controlledReactors   = 0;
        private int    _controlledReactorsOn = 0;

        private readonly List<CargoInfo>  _cargos  = new List<CargoInfo>();
        private readonly List<SourceInfo> _sources = new List<SourceInfo>();
        private bool   _logisticsEnabled = true;
        private bool   _autoAssign       = true;
        private int    _maxMoves         = 2;
        private int    _srcIndex         = 0;
        private int    _lastMoves        = 0;
        private string _lastItem="",_lastFrom="",_lastTo="",_logWarning="";
        private string _logStatus = "boot";

        private string _fuelGenerators="",_fuelH2Tanks="",_fuelO2Tanks="";
        private bool   _fuelUngrouped = true;

        private double _h2Now=0,_h2Max=0,_o2Now=0,_o2Max=0,_genIce=0;
        private int    _h2Count=0,_o2Count=0,_genCount=0,_genOnline=0,_genWorking=0;
        private int    _ventOk=0,_ventLeak=0;
        private string _ventLeaks="";
        private double _uraniumKg=0;

        private readonly List<IMyAssembler>        _assemblers        = new List<IMyAssembler>();
        private readonly List<IMyAssembler>        _basicAssemblers   = new List<IMyAssembler>();
        private readonly List<IMyAssembler>        _advAssemblers     = new List<IMyAssembler>();
        private readonly List<IMyAssembler>        _asmSorted         = new List<IMyAssembler>();
        private readonly List<IMyRefinery>         _refineries        = new List<IMyRefinery>();
        private readonly List<MyProductionItem>    _queue             = new List<MyProductionItem>();
        private readonly Dictionary<string,MyDefinitionId> _bpCache = new Dictionary<string,MyDefinitionId>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string>              _refineryPriority  = new List<string>();
        private readonly List<string>              _assemblerPriority = new List<string>();
        private readonly Dictionary<string,double> _compQuotas  = new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,double> _compStock   = new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,double> _compQueued  = new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        private bool   _prodEnabled=true,_monitorOnly=true,_autocraftComps=true,_autoDisassemble=false,_sortAsmQueue=true,_sortRefInput=true;
        private bool   _prodV2=true,_prodShowDetails=true,_prodShowMissing=true,_prodShowBlockedAsm=true,_prodShowBlockedRef=true;
        private string _prodAssemblers="",_prodRefineries="";
        private double _prodWarnBelow=0.90;
        private int    _maxQueuePerRun=5,_maxQueueAmount=5000,_lastQueued=0,_lastAsmMoves=0;
        private string _prodWarning="",_prodStatus="boot",_lastQueuedItem="";

        private readonly List<StockEntry>              _stockEntries = new List<StockEntry>();
        private readonly Dictionary<string,StockEntry> _stockByKey   = new Dictionary<string,StockEntry>(StringComparer.OrdinalIgnoreCase);

        private bool   _alertsEnabled     = true;
        private bool   _warningLights     = true;
        private double _alertBattLow      = 25.0;
        private double _alertH2Low        = 20.0;
        private double _alertO2Low        = 20.0;
        private double _alertUraniumLowKg = 5.0;
        private double _alertIngotLow     = 20.0;
        private double _alertComponentLow = 20.0;
        private double _alertAmmoLow      = 20.0;

        private int    _alertBattery    = ALERT_OK;
        private int    _alertCargo      = ALERT_OK;
        private int    _alertHydrogen   = ALERT_OK;
        private int    _alertOxygen     = ALERT_OK;
        private int    _alertUranium    = ALERT_OK;
        private int    _alertProduction = ALERT_OK;
        private int    _alertOverall    = ALERT_OK;

        private string _cargoAlertDetail = "";

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100;
            EnsureConfig();
            Reload();
        }

        public void Save() { }

        public void Main(string argument, UpdateType updateSource)
        {
            string arg = argument == null ? "" : argument.Trim();
            if (arg.Length > 0) HandleArgument(arg);

            if ((updateSource & UpdateType.Update100) != 0)
            {
                Reload();
            }

            double dt = Runtime.TimeSinceLastRun.TotalSeconds;
            if (dt <= 0) dt = 0.1667;
            TickPbAnim(dt);

            if (_booting)
            {
                _bootElapsed += dt; _bootDotTimer += dt;
                if (_bootDotTimer >= 0.4) { _bootDotTimer=0; _bootDots=(_bootDots+1)%4; }
                double prog = Math.Min(1.0, _bootElapsed/4.0);
                try{var _bs=Me.GetSurface(0);if(_bs!=null)DrawBootSurface(_bs,prog);}catch{}
                if (_screens.Count>0)
                {
                    if (_bootDrawIdx<0||_bootDrawIdx>=_screens.Count) _bootDrawIdx=0;
                    var prov=_screens[_bootDrawIdx] as IMyTextSurfaceProvider;
                    if (prov!=null&&prov.SurfaceCount>0){try{DrawBootSurface(prov.GetSurface(0),prog);}catch{}}
                    _bootDrawIdx=(_bootDrawIdx+1)%_screens.Count;
                }
                if (_bootElapsed >= 4.0) _booting = false;
                return;
            }

            if ((updateSource & (UpdateType.Update10|UpdateType.Update100)) != 0)
            {
                RunStagedWork();
                DrawPbScreen();
                DrawAlertLcds();
            }
        }

        private void RunStagedWork()
        {
            switch (_workStage)
            {
                case 0: RunLogistics(); break;
                case 1: RunProduction(); break;
                case 2: RunFuelScan(); break;
                case 3: RunPowerControl(); break;
                case 4: RunAlerts(); break;
                case 5: if (_warningLights && _alertsEnabled) RunWarningLights(); break;
                case 6: if (_sortRefInput && _prodEnabled && !_globalPause) SortRefineryInputs(); break;
                default:
                    int bi=_drawTick*2;
                    _drawTick=(_drawTick+1)%Math.Max(1,(_screens.Count+1)/2);
                    if (bi<_screens.Count) DrawScreen(_screens[bi]);
                    if (bi+1<_screens.Count) DrawScreen(_screens[bi+1]);
                    break;
            }
            _workStage++;
            int drawStages=Math.Max(1,(_screens.Count+1)/2);
            if (_workStage>=7+drawStages) _workStage=0;
        }

        private void HandleArgument(string arg)
        {
            if (arg.Equals("reboot",SC)||arg.Equals("boot",SC)) { _booting=true;_bootElapsed=0;_bootDots=0;Reload();return; }
            if (arg.Equals("reload",SC)||arg.Equals("rescan",SC)) { Reload(); return; }
            if (arg.Equals("pause",SC))  { _globalPause=true;  WriteCoreValue("global_pause","true");  return; }
            if (arg.Equals("resume",SC)) { _globalPause=false; WriteCoreValue("global_pause","false"); return; }
        }

        private void Reload()
        {
            ReadConfig();
            ScanBlocks();
            BuildCargoAndSources();
            BuildProductionLists();
            _stockByKey.Clear();
            _stockEntries.Clear();
            _bpCache.Clear();
            Echo("AutoGrid Manager v"+VERSION+" | RevGamer");
            Echo("Power:      "+(_powerEnabled?"ONLINE":"OFF"));
            Echo("Logistics:  "+(_logisticsEnabled?"ONLINE":"OFF"));
            Echo("Production: "+(_prodEnabled?"ONLINE":"OFF"));
            Echo("Alerts:     "+(_alertsEnabled?"ONLINE":"OFF"));
            Echo("Log: "  +_logStatus.ToUpperInvariant());
            Echo("Prod: " +_prodStatus.ToUpperInvariant());
            Echo("Screens: "+_screens.Count);
        }

        private void ScanBlocks()
        {
            _blocks.Clear(); _screens.Clear(); _alertLcds.Clear();
            _dockedGridIds.Clear();
            var _cons=new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(_cons);
            for (int ci=0;ci<_cons.Count;ci++)
            {
                var con=_cons[ci];
                if (con.Status!=MyShipConnectorStatus.Connected||con.OtherConnector==null) continue;
                bool noSort=HasToken(con.CustomData,_noSortTag)||HasToken(con.CustomName,_noSortTag);
                if (!_includeDockedGrids||noSort)
                {
                    if (con.OtherConnector.CubeGrid.IsSameConstructAs(Me.CubeGrid))
                        _dockedGridIds.Add(con.CubeGrid);
                    else
                        _dockedGridIds.Add(con.OtherConnector.CubeGrid);
                }
            }
            _dockedGridIds.Remove(Me.CubeGrid);
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(_blocks, b =>
            {
                foreach(var dg in _dockedGridIds){if(b.CubeGrid==dg||b.CubeGrid.IsSameConstructAs(dg))return false;}
                if (_includeDockedGrids) return true;
                return b.IsSameConstructAs(Me);
            });
            for (int i=0; i<_blocks.Count; i++)
            {
                var b=_blocks[i]; if (b==Me) continue;
                // Skip alert/light blocks — they are managed by RunWarningLights, not DrawScreen
                if (b.CustomData.IndexOf(LIGHT_TAG,SC)>=0||b.CustomName.IndexOf(LIGHT_TAG,SC)>=0) continue;
                if ((b.CustomName.IndexOf(SCREEN_TAG,SC)>=0||HasDashboardCmd(b)) && b is IMyTextSurfaceProvider)
                { var p=b as IMyTextSurfaceProvider; if (p.SurfaceCount>0) _screens.Add(b); }
            }
        }

        private void EnsureConfig()
        {
            if (!string.IsNullOrWhiteSpace(Me.CustomData)) return;
            Me.CustomData =
@"[Core]
enabled=true
power=true
logistics=true
production=true
global_pause=false
include_docked_grids=false

[Alerts]
enabled=true
warning_lights=true
battery_low_percent=25
hydrogen_low_percent=20
oxygen_low_percent=20
uranium_low_kg=5
ingot_low_percent=20
component_low_percent=20
ammo_low_percent=20

[Power:Base]
batteries=G:Base Batteries
reactors=
solar=
wind=
hydrogen=
include_ungrouped=false

[ReactorRefuel]
enabled=true
min_uranium_per_reactor=2
target_uranium_per_reactor=10
uranium_low_warning_kg=5
auto_refuel=false

[PowerControl]
enabled=true
auto_reactor_charge=true
battery_low_percent=25
battery_full_percent=100
control_reactors=G:Base Reactors
control_batteries=G:Base Batteries
turn_reactors_off_when_full=true
amber_while_charging=true
minimum_reactors_online=0
never_turn_off_reactors_if_output_above_percent=80

[Logistics]
auto_assign=true
max_moves_per_run=2

[FuelLifeSupport]
o2h2_generators=
h2_tanks=
o2_tanks=
include_ungrouped=true

[Production]
monitor_only=true
autocraft_components=true
auto_disassemble=false
sort_assembler_queue=true
sort_refinery_input=true
max_queue_per_run=5
max_queue_amount=5000
assemblers=G:Base Assemblers
refineries=G:Base Refineries
enabled=true
show_machine_details=true
show_current_blueprint=true
show_refinery_input=true
show_missing_resources=false
show_blocked_assemblers=true
show_blocked_refineries=true
missing_warning_below_percent=90

[RefineryPriority]
Stone
Iron
Nickel
Cobalt
Silicon
Magnesium
Silver
Gold
Platinum
Uranium

[AssemblerPriority]
SteelPlate
InteriorPlate
Construction
Computer
Motor
Display
MetalGrid
SmallTube
LargeTube
GravityGenerator
Superconductor

AutoCrafting=Component
SteelPlate=70000
InteriorPlate=70000
Construction=70000
Computer=10000
Motor=15000
MetalGrid=10000
Girder=10000
SmallTube=10000
LargeTube=10000
Display=5000
BulletproofGlass=5000
PowerCell=5000
SolarCell=1000
Detector=1000
RadioCommunication=1000
Medical=200
Reactor=10000
Thrust=12000
GravityGenerator=500
Superconductor=10000
Explosives=500
Canvas=200
ShieldComponent=2000";
        }

        private void ReadConfig()
        {
            _globalPause=false; _includeDockedGrids=false;
            _noSortTag="[No Sorting]"; _lockedTag="{Locked}"; _manualTag="{Manual}"; _hiddenTag="{Hidden}";
            _powerEnabled=true; _logisticsEnabled=true; _prodEnabled=true;
            _autoAssign=true; _maxMoves=2; _monitorOnly=true; _autocraftComps=true;
            _sortAsmQueue=true; _sortRefInput=true; _maxQueuePerRun=2; _maxQueueAmount=500;
            _prodV2=true; _prodShowDetails=true; _prodShowMissing=false; _prodShowBlockedAsm=true; _prodShowBlockedRef=true;
            _prodAssemblers=""; _prodRefineries=""; _prodWarnBelow=0.90;
            _fuelGenerators=""; _fuelH2Tanks=""; _fuelO2Tanks=""; _fuelUngrouped=true;
            _alertsEnabled=true; _warningLights=true;
            _alertBattLow=25; _alertH2Low=20; _alertO2Low=20; _alertUraniumLowKg=5;
            _alertIngotLow=20; _alertComponentLow=20; _alertAmmoLow=20;
            _reactorRefuelEnabled=true; _reactorAutoRefuel=false; _minUraniumPerReactor=2; _targetUraniumPerReactor=10; _reactorUraniumLowKg=5;
            _powerControlEnabled=true; _autoReactorCharge=true; _pcBattLow=25; _pcBattFull=100; _pcReactors=""; _pcBatteries="";
            _turnReactorsOffWhenFull=true; _amberWhileCharging=true; _minimumReactorsOnline=0; _neverOffOutputPct=80;
            _powerProfiles.Clear(); _refineryPriority.Clear(); _assemblerPriority.Clear(); _compQuotas.Clear();

            PowerProfile activePower=null; string section="";
            string[] lines=Me.CustomData.Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
            for (int i=0; i<lines.Length; i++)
            {
                string line=StripComment(lines[i]).Trim(); if (line.Length==0) continue;
                if (line.StartsWith("[")&&line.EndsWith("]"))
                {
                    section=line.Substring(1,line.Length-2).Trim(); activePower=null;
                    if (section.StartsWith("Power:",SC))
                    {
                        activePower=new PowerProfile();
                        activePower.Name=section.Substring(6).Trim();
                        if (activePower.Name.Length==0) activePower.Name="Base";
                        _powerProfiles.Add(activePower);
                    }
                    continue;
                }
                string key,value;
                if (activePower!=null&&TrySplit(line,'=',out key,out value))
                {
                    if      (key.Equals("batteries",SC))         activePower.Batteries=value;
                    else if (key.Equals("reactors",SC))          activePower.Reactors=value;
                    else if (key.Equals("solar",SC))             activePower.Solar=value;
                    else if (key.Equals("wind",SC))              activePower.Wind=value;
                    else if (key.Equals("hydrogen",SC))          activePower.Hydrogen=value;
                    else if (key.Equals("include_ungrouped",SC)) activePower.IncludeUngrouped=ParseBool(value,false);
                    continue;
                }
                if (!TrySplit(line,'=',out key,out value))
                {
                    if (section.Equals("RefineryPriority",SC))  _refineryPriority.Add(line);
                    if (section.Equals("AssemblerPriority",SC)) _assemblerPriority.Add(line);
                    continue;
                }
                if (key.Equals("AutoCrafting",SC)&&value.Trim().Equals("Component",SC)) { section="ComponentQuotas"; continue; }
                if (section.Equals("Core",SC))
                {
                    if      (key.Equals("global_pause",SC))         _globalPause=ParseBool(value,false);
                    else if (key.Equals("include_docked_grids",SC)) _includeDockedGrids=ParseBool(value,false);
                    else if (key.Equals("no_sorting_tag",SC))       _noSortTag=value;
                    else if (key.Equals("locked_tag",SC))           _lockedTag=value;
                    else if (key.Equals("manual_tag",SC))           _manualTag=value;
                    else if (key.Equals("hidden_tag",SC))           _hiddenTag=value;
                    else if (key.Equals("power",SC))                _powerEnabled=ParseBool(value,true);
                    else if (key.Equals("logistics",SC))            _logisticsEnabled=ParseBool(value,true);
                    else if (key.Equals("production",SC))           _prodEnabled=ParseBool(value,true);
                }
                else if (section.Equals("Alerts",SC))
                {
                    if      (key.Equals("enabled",SC))               _alertsEnabled=ParseBool(value,true);
                    else if (key.Equals("warning_lights",SC))        _warningLights=ParseBool(value,true);
                    else if (key.Equals("battery_low_percent",SC))   double.TryParse(value,out _alertBattLow);
                    else if (key.Equals("hydrogen_low_percent",SC))  double.TryParse(value,out _alertH2Low);
                    else if (key.Equals("oxygen_low_percent",SC))    double.TryParse(value,out _alertO2Low);
                    else if (key.Equals("uranium_low_kg",SC))        double.TryParse(value,out _alertUraniumLowKg);
                    else if (key.Equals("ingot_low_percent",SC))     double.TryParse(value,out _alertIngotLow);
                    else if (key.Equals("component_low_percent",SC)) double.TryParse(value,out _alertComponentLow);
                    else if (key.Equals("ammo_low_percent",SC))      double.TryParse(value,out _alertAmmoLow);
                }
                else if (section.Equals("ReactorRefuel",SC))
                {
                    if      (key.Equals("enabled",SC))                   _reactorRefuelEnabled=ParseBool(value,true);
                    else if (key.Equals("min_uranium_per_reactor",SC))   double.TryParse(value,out _minUraniumPerReactor);
                    else if (key.Equals("target_uranium_per_reactor",SC))double.TryParse(value,out _targetUraniumPerReactor);
                    else if (key.Equals("uranium_low_warning_kg",SC))    double.TryParse(value,out _reactorUraniumLowKg);
                    else if (key.Equals("auto_refuel",SC))               _reactorAutoRefuel=ParseBool(value,false);
                }
                else if (section.Equals("PowerControl",SC))
                {
                    if      (key.Equals("enabled",SC))                                      _powerControlEnabled=ParseBool(value,true);
                    else if (key.Equals("auto_reactor_charge",SC))                          _autoReactorCharge=ParseBool(value,true);
                    else if (key.Equals("battery_low_percent",SC))                          double.TryParse(value,out _pcBattLow);
                    else if (key.Equals("battery_full_percent",SC))                         double.TryParse(value,out _pcBattFull);
                    else if (key.Equals("control_reactors",SC))                             _pcReactors=value;
                    else if (key.Equals("control_batteries",SC))                            _pcBatteries=value;
                    else if (key.Equals("turn_reactors_off_when_full",SC))                  _turnReactorsOffWhenFull=ParseBool(value,true);
                    else if (key.Equals("amber_while_charging",SC))                         _amberWhileCharging=ParseBool(value,true);
                    else if (key.Equals("minimum_reactors_online",SC))                      int.TryParse(value,out _minimumReactorsOnline);
                    else if (key.Equals("never_turn_off_reactors_if_output_above_percent",SC)) double.TryParse(value,out _neverOffOutputPct);
                }
                else if (section.Equals("Logistics",SC))
                {
                    if      (key.Equals("auto_assign",SC))       _autoAssign=ParseBool(value,true);
                    else if (key.Equals("max_moves_per_run",SC)) int.TryParse(value,out _maxMoves);
                }
                else if (section.Equals("Production",SC))
                {
                    if      (key.Equals("monitor_only",SC))             _monitorOnly=ParseBool(value,true);
                    else if (key.Equals("autocraft_components",SC))     _autocraftComps=ParseBool(value,true);
                    else if (key.Equals("auto_disassemble",SC))         _autoDisassemble=ParseBool(value,false);
                    else if (key.Equals("sort_assembler_queue",SC))     _sortAsmQueue=ParseBool(value,true);
                    else if (key.Equals("sort_refinery_input",SC))      _sortRefInput=ParseBool(value,true);
                    else if (key.Equals("max_queue_per_run",SC))        int.TryParse(value,out _maxQueuePerRun);
                    else if (key.Equals("max_queue_amount",SC))         int.TryParse(value,out _maxQueueAmount);
                    else if (key.Equals("assemblers",SC))               _prodAssemblers=value;
                    else if (key.Equals("refineries",SC))               _prodRefineries=value;
                    else if (key.Equals("enabled",SC))                  _prodV2=ParseBool(value,true);
                    else if (key.Equals("show_machine_details",SC))     _prodShowDetails=ParseBool(value,true);
                    else if (key.Equals("show_current_blueprint",SC))   _prodShowDetails=ParseBool(value,true);
                    else if (key.Equals("show_refinery_input",SC))      _prodShowDetails=ParseBool(value,true);
                    else if (key.Equals("show_missing_resources",SC))   _prodShowMissing=ParseBool(value,true);
                    else if (key.Equals("show_blocked_assemblers",SC))  _prodShowBlockedAsm=ParseBool(value,true);
                    else if (key.Equals("show_blocked_refineries",SC))  _prodShowBlockedRef=ParseBool(value,true);
                    else if (key.Equals("missing_warning_below_percent",SC)) { double v; if(double.TryParse(value,out v)) _prodWarnBelow=v/100.0; }
                }
                else if (section.Equals("ComponentQuotas",SC))
                { double quota; if (double.TryParse(value,out quota)&&quota>0) _compQuotas[key]=quota; }
                else if (section.Equals("FuelLifeSupport",SC))
                {
                    if      (key.Equals("o2h2_generators",SC))   _fuelGenerators=value;
                    else if (key.Equals("h2_tanks",SC))          _fuelH2Tanks=value;
                    else if (key.Equals("o2_tanks",SC))          _fuelO2Tanks=value;
                    else if (key.Equals("include_ungrouped",SC)) _fuelUngrouped=ParseBool(value,true);
                }
            }
            if (_powerProfiles.Count==0) { var p=new PowerProfile(); p.IncludeUngrouped=true; _powerProfiles.Add(p); }
            if (_maxMoves<1)_maxMoves=1; if (_maxMoves>10)_maxMoves=10;
            if (_maxQueuePerRun<1)_maxQueuePerRun=1; if (_maxQueuePerRun>20)_maxQueuePerRun=20;
            if (_maxQueueAmount<1)_maxQueueAmount=1; if (_maxQueueAmount>100000)_maxQueueAmount=100000;
            if (_prodWarnBelow<0)_prodWarnBelow=0; if (_prodWarnBelow>1)_prodWarnBelow=1;
            if (_minimumReactorsOnline<0)_minimumReactorsOnline=0;
            if (_pcBattLow<1)_pcBattLow=1; if (_pcBattLow>99)_pcBattLow=99;
            if (_pcBattFull<_pcBattLow)_pcBattFull=_pcBattLow;
            if (_pcBattFull>100)_pcBattFull=100;
        }

        private void WriteCoreValue(string key, string value)
        {
            string[] lines=Me.CustomData.Split(new char[]{'\r','\n'},StringSplitOptions.None);
            _sb.Clear(); bool inCore=false,wrote=false;
            for (int i=0; i<lines.Length; i++)
            {
                string t=lines[i].Trim();
                if (t.StartsWith("[")&&t.EndsWith("]")) { if (inCore&&!wrote){_sb.AppendLine(key+"="+value);wrote=true;} inCore=t.Equals("[Core]",SC); }
                string k,v;
                if (inCore&&TrySplit(t,'=',out k,out v)&&k.Equals(key,SC)) { _sb.AppendLine(key+"="+value);wrote=true;continue; }
                _sb.AppendLine(lines[i]);
            }
            if (!wrote) _sb.AppendLine(key+"="+value);
            Me.CustomData=_sb.ToString().TrimEnd();
        }

        private void RunFuelScan()
        {
            _h2Now=0; _h2Max=0; _o2Now=0; _o2Max=0; _genIce=0;
            _h2Count=0; _o2Count=0; _genCount=0; _genOnline=0; _genWorking=0;
            _ventOk=0; _ventLeak=0; _ventLeaks=""; _uraniumKg=0;
            _uraniumStockKg=0; _lowestReactorName="-"; _lowestReactorKg=0; _reactorRefuelStatus="OK";
            _reactorInfos.Clear();
            var prof=_powerProfiles.Count>0?_powerProfiles[0]:new PowerProfile();
            string reactorSpec=string.IsNullOrWhiteSpace(_pcReactors)?prof.Reactors:_pcReactors;
            bool reactorUngrouped=prof.IncludeUngrouped&&string.IsNullOrWhiteSpace(reactorSpec);
            var ice=MyItemType.MakeOre("Ice"); var uranium=MyItemType.MakeIngot("Uranium");
            for (int i=0; i<_blocks.Count; i++)
            {
                var b=_blocks[i];
                var reactor=b as IMyReactor;
                if (reactor!=null)
                {
                    if (MatchSpec(reactor,reactorSpec,reactorUngrouped))
                    {
                        double kg=reactor.InventoryCount>0?(double)reactor.GetInventory(0).GetItemAmount(uranium):0.0;
                        _uraniumKg+=kg;
                        _reactorInfos.Add(new ReactorInfo{Reactor=reactor,UraniumKg=kg});
                        if (_reactorInfos.Count==1||kg<_lowestReactorKg) { _lowestReactorKg=kg; _lowestReactorName=reactor.CustomName; }
                    }
                    continue;
                }
                var tank=b as IMyGasTank;
                if (tank!=null)
                {
                    double cap=tank.Capacity,fil=cap*tank.FilledRatio;
                    bool isH2=b.BlockDefinition.TypeIdString.IndexOf("Hydrogen",SC)>=0||b.BlockDefinition.SubtypeId.IndexOf("Hydrogen",SC)>=0||b.CustomName.IndexOf("Hydrogen",SC)>=0;
                    if (isH2) { if (!FuelBlockMatches(b,_fuelH2Tanks)) continue; _h2Count++; _h2Max+=cap; _h2Now+=fil; }
                    else      { if (!FuelBlockMatches(b,_fuelO2Tanks)) continue; _o2Count++; _o2Max+=cap; _o2Now+=fil; }
                    continue;
                }
                var gen=b as IMyGasGenerator;
                if (gen!=null)
                {
                    if (!FuelBlockMatches(b,_fuelGenerators)) continue;
                    _genCount++; if (gen.Enabled) _genOnline++; if (gen.IsWorking) _genWorking++;
                    if (gen.InventoryCount>0) _genIce+=(double)gen.GetInventory(0).GetItemAmount(ice);
                    continue;
                }
                var vent=b as IMyAirVent;
                if (vent!=null&&b.CustomData.IndexOf("InteriorVent",SC)>=0)
                {
                    bool ok=vent.IsWorking&&vent.CanPressurize&&vent.GetOxygenLevel()>=0.95f;
                    if (ok) _ventOk++;
                    else { _ventLeak++; if (_ventLeaks.Length<80){if(_ventLeaks.Length>0)_ventLeaks+=", ";_ventLeaks+=vent.CustomName;} }
                }
                if (b.HasInventory)
                {
                    for (int inv=0; inv<b.InventoryCount; inv++) _uraniumStockKg+=(double)b.GetInventory(inv).GetItemAmount(uranium);
                }
            }
            if (_reactorInfos.Count==0) _reactorRefuelStatus="NO REACTORS";
            else if (!_reactorRefuelEnabled) _reactorRefuelStatus="DISABLED";
            else if (_lowestReactorKg<_minUraniumPerReactor||_uraniumKg<=_reactorUraniumLowKg) _reactorRefuelStatus="REFUEL NEEDED";
            else _reactorRefuelStatus="OK";
        }

        private bool FuelBlockMatches(IMyTerminalBlock b, string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return _fuelUngrouped; spec=spec.Trim();
            if (spec.StartsWith("G:",SC)) { string grpName=spec.Substring(2).Trim(); if (grpName.Length==0) return _fuelUngrouped; var grp=GridTerminalSystem.GetBlockGroupWithName(grpName); if (grp==null) return false; _groupBuf.Clear(); grp.GetBlocks(_groupBuf); return _groupBuf.Contains(b); }
            return b.CustomName.IndexOf(spec,SC)>=0;
        }

        private void RunPowerControl()
        {
            _reactorsForcedOn=false; _powerSafetyHold=false; _powerControlStatus="MONITOR";
            _controlledReactors=0; _controlledReactorsOn=0;
            if (!_powerEnabled) { _powerControlStatus="POWER OFF"; return; }
            if (!_powerControlEnabled) { _powerControlStatus="DISABLED"; return; }
            if (_globalPause) { _powerControlStatus="PAUSED"; return; }

            var prof=_powerProfiles.Count>0?_powerProfiles[0]:new PowerProfile();
            string battSpec=string.IsNullOrWhiteSpace(_pcBatteries)?prof.Batteries:_pcBatteries;
            string reactorSpec=string.IsNullOrWhiteSpace(_pcReactors)?prof.Reactors:_pcReactors;
            bool includeUngrouped=prof.IncludeUngrouped&&string.IsNullOrWhiteSpace(_pcBatteries)&&string.IsNullOrWhiteSpace(_pcReactors);

            BuildControlLists(battSpec,reactorSpec,includeUngrouped);
            _controlledReactors=_reactorsCtl.Count;
            for (int i=0;i<_reactorsCtl.Count;i++) if (_reactorsCtl[i].Enabled) _controlledReactorsOn++;

            double stored=0,cap=0,input=0,output=0,maxOutput=0;
            for (int i=0;i<_batteriesCtl.Count;i++) { stored+=_batteriesCtl[i].CurrentStoredPower; cap+=_batteriesCtl[i].MaxStoredPower; input+=_batteriesCtl[i].CurrentInput; output+=_batteriesCtl[i].CurrentOutput; }
            var st=BuildPowerStats(prof);
            if (cap<=0) { stored=st.Stored; cap=st.Capacity; input=st.Input; output=st.Output; }
            maxOutput=st.MaxOutput;
            double battPct=cap>0?stored/cap*100.0:100.0;
            double outputPct=maxOutput>0?st.Output/maxOutput*100.0:0.0;

            if (!_autoReactorCharge) { _powerControlStatus="MONITOR"; return; }
            if (_reactorsCtl.Count==0) { _powerControlStatus="NO REACTORS"; return; }
            if (cap<=0) { _powerControlStatus="NO BATTERIES"; return; }

            if (battPct<=_pcBattLow)
            {
                SetReactors(true);
                _reactorsForcedOn=true;
                _powerControlStatus="BATTERY LOW";
                _controlledReactorsOn=_reactorsCtl.Count;
                return;
            }

            if (battPct<_pcBattFull && input>output)
            {
                _reactorsForcedOn=AnyControlledReactorOn();
                _powerControlStatus=_reactorsForcedOn?"REACTOR CHARGING":"CHARGING";
                return;
            }

            if (battPct>=_pcBattFull)
            {
                if (_turnReactorsOffWhenFull)
                {
                    if (outputPct>=_neverOffOutputPct)
                    {
                        _powerSafetyHold=true;
                        _powerControlStatus="SAFETY HOLD";
                        return;
                    }
                    SetReactorsMinimum(_minimumReactorsOnline);
                    _controlledReactorsOn=Math.Min(_minimumReactorsOnline,_reactorsCtl.Count);
                }
                _powerControlStatus="BATTERY FULL";
                return;
            }

            _powerControlStatus="STABLE";
        }

        private void BuildControlLists(string battSpec, string reactorSpec, bool includeUngrouped)
        {
            _batteriesCtl.Clear(); _reactorsCtl.Clear();
            for (int i=0;i<_blocks.Count;i++)
            {
                var bat=_blocks[i] as IMyBatteryBlock;
                if (bat!=null&&MatchSpec(bat,battSpec,includeUngrouped)) { _batteriesCtl.Add(bat); continue; }
                var reactor=_blocks[i] as IMyReactor;
                if (reactor!=null&&MatchSpec(reactor,reactorSpec,includeUngrouped)) _reactorsCtl.Add(reactor);
            }
        }

        private bool AnyControlledReactorOn()
        { for (int i=0;i<_reactorsCtl.Count;i++) if (_reactorsCtl[i].Enabled) return true; return false; }

        private void SetReactors(bool enabled)
        { for (int i=0;i<_reactorsCtl.Count;i++) _reactorsCtl[i].Enabled=enabled; }

        private void SetReactorsMinimum(int keepOnline)
        {
            if (keepOnline<0) keepOnline=0;
            for (int i=0;i<_reactorsCtl.Count;i++) _reactorsCtl[i].Enabled=i<keepOnline;
        }

        private void RunAlerts()
        {
            if (!_alertsEnabled) { _alertOverall=ALERT_OK; return; }
            var prof=_powerProfiles.Count>0?_powerProfiles[0]:new PowerProfile();
            if (string.IsNullOrWhiteSpace(prof.Batteries)&&string.IsNullOrWhiteSpace(prof.Reactors)&&string.IsNullOrWhiteSpace(prof.Solar)&&string.IsNullOrWhiteSpace(prof.Wind)&&string.IsNullOrWhiteSpace(prof.Hydrogen)) prof.IncludeUngrouped=true;
            var ps=BuildPowerStats(prof);
            double battPct=ps.Capacity>0?ps.Stored/ps.Capacity*100.0:0.0;
            if      (battPct<=_alertBattLow*0.5) _alertBattery=ALERT_CRITICAL;
            else if (battPct<=_alertBattLow)     _alertBattery=ALERT_WARNING;
            else                                 _alertBattery=ALERT_OK;

            _alertCargo=ALERT_OK; _cargoAlertDetail="";
            double worstFill=101.0;
            CheckCargoLow("Ingot",     _alertIngotLow,     ref worstFill);
            CheckCargoLow("Component", _alertComponentLow, ref worstFill);
            CheckCargoLow("Ammo",      _alertAmmoLow,      ref worstFill);

            double h2Pct=_h2Max>0?_h2Now/_h2Max*100.0:101.0;
            if      (h2Pct<=100&&h2Pct<=_alertH2Low*0.5) _alertHydrogen=ALERT_CRITICAL;
            else if (h2Pct<=100&&h2Pct<=_alertH2Low)     _alertHydrogen=ALERT_WARNING;
            else                                          _alertHydrogen=ALERT_OK;

            double o2Pct=_o2Max>0?_o2Now/_o2Max*100.0:101.0;
            if      (o2Pct<=100&&o2Pct<=_alertO2Low*0.5) _alertOxygen=ALERT_CRITICAL;
            else if (o2Pct<=100&&o2Pct<=_alertO2Low)     _alertOxygen=ALERT_WARNING;
            else                                          _alertOxygen=ALERT_OK;

            if      (_reactorInfos.Count>0&&_uraniumKg<=_alertUraniumLowKg*0.5) _alertUranium=ALERT_CRITICAL;
            else if (_reactorInfos.Count>0&&_uraniumKg<=_alertUraniumLowKg)     _alertUranium=ALERT_WARNING;
            else                                                                _alertUranium=ALERT_OK;

            if      (_prodWarning.Length>0)                    _alertProduction=ALERT_WARNING;
            else if (ProdAsmQueued()>0&&ProdAsmProducing()==0) _alertProduction=ALERT_WARNING;
            else                                               _alertProduction=ALERT_OK;

            _alertOverall=ALERT_OK;
            int[] all={_alertBattery,_alertCargo,_alertHydrogen,_alertOxygen,_alertUranium,_alertProduction};
            for (int i=0;i<all.Length;i++) if (all[i]>_alertOverall) _alertOverall=all[i];
        }

        private void CheckCargoLow(string type, double threshold, ref double worstFill)
        {
            double cur=0,max=0;
            for (int i=0;i<_cargos.Count;i++) { var c=_cargos[i]; if (c.Locked||c.Manual||c.Hidden||!c.Type.Equals(type,SC)) continue; cur+=(double)c.Inv.CurrentVolume; max+=(double)c.Inv.MaxVolume; }
            if (max<=0) return;
            double fill=cur/max*100.0;
            int level=ALERT_OK;
            if      (fill<=threshold*0.5) level=ALERT_CRITICAL;
            else if (fill<=threshold)     level=ALERT_WARNING;
            if (level==ALERT_OK) return;
            if (fill<worstFill) { worstFill=fill; _cargoAlertDetail=type+" "+fill.ToString("0.0")+"% LOW"; }
            if (level>_alertCargo) _alertCargo=level;
        }

        private void RunWarningLights()
        {
            for (int i=0; i<_blocks.Count; i++)
            {
                var b=_blocks[i];
                // Accept [AGM-LIGHT] in Custom Data (preferred) or block name (legacy)
                bool inData=b.CustomData.IndexOf(LIGHT_TAG,SC)>=0;
                bool inName=b.CustomName.IndexOf(LIGHT_TAG,SC)>=0;
                if (!inData&&!inName) continue;

                // Resolve watch target — Custom Data watch= key, else block name keywords
                string watch="";
                if (inData)
                {
                    string[] dlines=b.CustomData.Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
                    for (int dl=0;dl<dlines.Length;dl++)
                    {
                        string dk,dv;
                        if (TrySplit(dlines[dl],'=',out dk,out dv)&&dk.Equals("watch",SC)) { watch=dv.Trim(); break; }
                    }
                }
                // Fallback: read keyword from block name
                if (watch.Length==0) watch=b.CustomName.ToUpperInvariant();

                int state=ALERT_OK;
                if      (watch.IndexOf("CHARGING",SC)>=0)   state=(_reactorsForcedOn&&_amberWhileCharging)?ALERT_WARNING:ALERT_OK;
                else if (watch.IndexOf("POWER OK",SC)>=0)   state=(_alertBattery==ALERT_OK&&!_powerSafetyHold)?ALERT_OK:ALERT_WARNING;
                else if (watch.IndexOf("BATTERY",SC)>=0)    state=_alertBattery;
                else if (watch.IndexOf("CARGO",SC)>=0)      state=_alertCargo;
                else if (watch.IndexOf("HYDROGEN",SC)>=0)   state=_alertHydrogen;
                else if (watch.IndexOf("OXYGEN",SC)>=0)     state=_alertOxygen;
                else if (watch.IndexOf("URANIUM",SC)>=0)    state=_alertUranium;
                else if (watch.IndexOf("PRODUCTION",SC)>=0) state=_alertProduction;
                else                                        state=_alertOverall;

                // Apply to light block
                var light=b as IMyLightingBlock;
                if (light!=null)
                {
                    switch (state)
                    {
                        case ALERT_CRITICAL: light.Color=LIGHT_RED;   light.Enabled=true; light.BlinkIntervalSeconds=1.0f; light.BlinkLength=50f; light.BlinkOffset=0f; break;
                        case ALERT_WARNING:  light.Color=LIGHT_AMBER; light.Enabled=true; light.BlinkIntervalSeconds=0f; break;
                        default:             light.Color=LIGHT_GREEN; light.Enabled=true; light.BlinkIntervalSeconds=0f; break;
                    }
                }

                // Apply to corner LCD — update state cache, drawn every tick in DrawAlertLcds
                if (light==null)
                {
                    var sp=b as IMyTextSurfaceProvider;
                    if (sp!=null&&sp.SurfaceCount>0)
                    {
                        IMyTextSurface surf; try{surf=sp.GetSurface(0);}catch{surf=null;}
                        if (surf==null) continue;
                        bool found=false;
                        for (int al=0;al<_alertLcds.Count;al++)
                        {
                            if (_alertLcds[al].Surface==surf)
                            { _alertLcds[al].State=state; _alertLcds[al].Watch=watch; found=true; break; }
                        }
                        if (!found) _alertLcds.Add(new AlertLcdEntry{Surface=surf,State=state,Watch=watch});
                    }
                }
            }
        }

        private void DrawAlertLcds()
        {
            for (int i=_alertLcds.Count-1;i>=0;i--)
            {
                try
                {
                    if (_alertLcds[i].Surface==null){_alertLcds.RemoveAt(i);continue;}
                    DrawAlertCornerLcd(_alertLcds[i].Surface,_alertLcds[i].State,_alertLcds[i].Watch);
                }
                catch { _alertLcds.RemoveAt(i); }
            }
        }

        private void DrawAlertCornerLcd(IMyTextSurface s, int state, string watch)
        {
            if (s.ContentType!=ContentType.SCRIPT){s.ContentType=ContentType.SCRIPT;s.ScriptBackgroundColor=COL_BG;s.BackgroundColor=COL_BG;s.Font="Monospace";s.Script="";}
            var vp=VP(s); var panel=Inset(vp,6f);
            Color borderCol=state==ALERT_CRITICAL?LIGHT_RED:state==ALERT_WARNING?LIGHT_AMBER:LIGHT_GREEN;
            Color statusCol=state==ALERT_CRITICAL?COL_BAD  :state==ALERT_WARNING?COL_WARN  :COL_OK;
            string status  =state==ALERT_CRITICAL?"CRITICAL":state==ALERT_WARNING?"WARNING":"ONLINE";
            string topic="AGM";
            if      (watch.IndexOf("BATTERY",SC)>=0)    topic="BATTERY";
            else if (watch.IndexOf("CARGO",SC)>=0)      topic="CARGO";
            else if (watch.IndexOf("HYDROGEN",SC)>=0)   topic="HYDROGEN";
            else if (watch.IndexOf("OXYGEN",SC)>=0)     topic="OXYGEN";
            else if (watch.IndexOf("URANIUM",SC)>=0)    topic="URANIUM";
            else if (watch.IndexOf("PRODUCTION",SC)>=0) topic="PRODUCTION";
            else if (watch.IndexOf("CHARGING",SC)>=0)   topic="REACTOR";
            else if (watch.IndexOf("POWER OK",SC)>=0)   topic="POWER";
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);
                Fill(fr,panel,COL_PANEL);
                DrawBorder(fr,panel,borderCol,4f);
                float cx=panel.X+panel.Width*0.5f;
                float cy=panel.Y+panel.Height*0.5f;
                bool wide=panel.Width>panel.Height*2.0f;
                bool small=panel.Height<100f;
                if (wide)
                {
                    // Wide LCD: BATTERY left big, ONLINE right, version bottom-left
                    float ts=Math.Min(0.90f,panel.Height/50f*0.72f);
                    float ss=Math.Min(0.70f,panel.Height/50f*0.55f);
                    float textY=cy-ts*22f;
                    Txt(fr,topic, panel.X+16f,    textY, statusCol, ts, TextAlignment.LEFT);
                    Txt(fr,status,panel.Right-16f, textY, statusCol, ss, TextAlignment.RIGHT);
                    Txt(fr,"AGM v"+VERSION,panel.X+16f,panel.Bottom-12f,COL_DIM,0.26f,TextAlignment.LEFT);
                }
                else
                {
                    // Square/tall LCD: topic large, status below
                    float ts=Math.Min(small?0.70f:1.00f, panel.Width/(topic.Length*16f));
                    float ss=small?0.45f:0.60f;
                    float topY   =cy-ts*24f-(small?4f:10f);
                    float statusY=cy+(small?6f:14f);
                    Txt(fr,topic, cx,topY,   statusCol,ts,TextAlignment.CENTER);
                    Txt(fr,status,cx,statusY,statusCol,ss,TextAlignment.CENTER);
                    Txt(fr,"AGM v"+VERSION,cx,panel.Bottom-12f,COL_DIM,0.26f,TextAlignment.CENTER);
                }
            }
        }

        private void BuildCargoAndSources()
        {
            _cargos.Clear(); _sources.Clear();
            for (int i=0; i<_blocks.Count; i++)
            {
                var b=_blocks[i]; if (b==null||!b.HasInventory||IsNoSort(b)) continue;
                if (b is IMyCargoContainer)
                {
                    var c=new CargoInfo(); c.Block=b; c.Inv=b.GetInventory(0);
                    c.Type=CargoTypeFromBlock(b); c.Index=CargoNumber(b,c.Type);
                    c.Locked=HasToken(b.CustomName,_lockedTag)||HasToken(b.CustomData,_lockedTag);
                    c.Manual=HasToken(b.CustomName,_manualTag)||HasToken(b.CustomData,_manualTag);
                    c.Hidden=HasToken(b.CustomName,_hiddenTag)||HasToken(b.CustomData,_hiddenTag);
                    _cargos.Add(c); if (!c.Locked&&!c.Manual&&!c.Hidden) AddSource(b,c.Inv,c.Type); continue;
                }
                if (b is IMyReactor||b is IMyGasGenerator||b is IMyGasTank) continue;
                if (HasToken(b.CustomName,_lockedTag)||HasToken(b.CustomName,_manualTag)||HasToken(b.CustomName,_hiddenTag)) continue;
                if (b.InventoryCount>=2&&(b is IMyAssembler||b is IMyRefinery)) AddSource(b,b.GetInventory(1),"");
                else if (b.InventoryCount==1) AddSource(b,b.GetInventory(0),"");
            }
            _cargos.Sort((a,b2)=>((float)b2.Inv.MaxVolume).CompareTo((float)a.Inv.MaxVolume));
        }

        private void AddSource(IMyTerminalBlock b, IMyInventory inv, string type)
        { if (inv!=null) _sources.Add(new SourceInfo{Block=b,Inv=inv,Type=type}); }

        private void RunLogistics()
        {
            _lastMoves=0; _logWarning="";
            if (!_logisticsEnabled){_logStatus="disabled";return;}
            if (_globalPause){_logStatus="paused";return;}
            if (_autoAssign) EnsureBaselineDestinations();
            if (_sources.Count==0){_logStatus="no sources";return;}
            int moves=0; if (_srcIndex<0||_srcIndex>=_sources.Count) _srcIndex=0;
            int checked2=0;
            while (checked2<_sources.Count&&moves<_maxMoves)
            {
                var src=_sources[_srcIndex]; _srcIndex=(_srcIndex+1)%_sources.Count; checked2++;
                _invItems.Clear(); src.Inv.GetItems(_invItems);
                for (int i=_invItems.Count-1; i>=0&&moves<_maxMoves; i--)
                {
                    var item=_invItems[i]; string type=ItemCategory(item.Type);
                    if (type.Length==0||src.Type.Equals(type,SC)) continue;
                    var dest=FindDest(type)??(_autoAssign?AssignCargo(type):null);
                    if (dest==null){_logWarning=type+" storage full";continue;}
                    if (TryMove(src.Inv,dest.Inv,i,item.Amount))
                    { moves++; _lastItem=item.Type.SubtypeId.ToString(); _lastFrom=src.Block.CustomName; _lastTo=dest.Block.CustomName; }
                }
            }
            _lastMoves=moves; _logStatus=moves>0?"moved "+moves:"idle";
        }

        private bool TryMove(IMyInventory src, IMyInventory dst, int idx, MyFixedPoint amt)
        {
            if (src.TransferItemTo(dst,idx,null,true)) return true;
            double a=(double)amt,b=Math.Min(a,1000.0);
            if (b>0&&src.TransferItemTo(dst,idx,null,true,(MyFixedPoint)b)) return true;
            b=Math.Min(a,100.0);
            if (b>0&&src.TransferItemTo(dst,idx,null,true,(MyFixedPoint)b)) return true;
            return src.TransferItemTo(dst,idx,null,true,(MyFixedPoint)1.0);
        }

        private CargoInfo FindDest(string type)
        {
            CargoInfo best=null; int bestN=int.MaxValue;
            for (int i=0; i<_cargos.Count; i++)
            { var c=_cargos[i]; if (c.Locked||c.Manual||c.Hidden||!c.Type.Equals(type,SC)||!HasSpace(c.Inv)) continue; int n=c.Index>0?c.Index:CargoNumber(c.Block,type); if (n<bestN){best=c;bestN=n;} }
            return best;
        }

        private void EnsureBaselineDestinations()
        { string[] types={"Ore","Ingot","Component","Ammo","Tool","Bottle","Food","Seed","Ingredient"}; for (int i=0;i<types.Length;i++) if (FindDest(types[i])==null) AssignCargo(types[i]); }

        private CargoInfo AssignCargo(string type)
        {
            for (int i=0; i<_cargos.Count; i++)
            { var c=_cargos[i]; if (c.Locked||c.Manual||c.Hidden||c.Type.Length>0||c.Inv.ItemCount>0) continue; int n=NextCargoNumber(type); c.Block.CustomName=CleanCargoName(c.Block.CustomName)+" {"+TagType(type)+" "+n+"}"; c.Type=type;c.Index=n; return c; }
            return null;
        }

        private int NextCargoNumber(string type)
        { int max=0; for (int i=0;i<_cargos.Count;i++) if (_cargos[i].Type.Equals(type,SC)&&_cargos[i].Index>max) max=_cargos[i].Index; return max+1; }

        private string CargoTypeFromBlock(IMyTerminalBlock b)
        {
            string n=b.CustomName; string d=b.CustomData??"";
            if (HasTagType(n,"Ore")||HasTagType(d,"Ore"))             return "Ore";
            if (HasTagType(n,"Ingot")||HasTagType(d,"Ingot"))         return "Ingot";
            if (HasTagType(n,"Component")||HasTagType(d,"Component")) return "Component";
            if (HasTagType(n,"Ammo")||HasTagType(d,"Ammo"))           return "Ammo";
            if (HasTagType(n,"Tools")||HasTagType(n,"Tool")||HasTagType(d,"Tools")||HasTagType(d,"Tool")) return "Tool";
            if (HasTagType(n,"Bottle")||HasTagType(d,"Bottle"))       return "Bottle";
            if (HasTagType(n,"Food")||HasTagType(d,"Food"))           return "Food";
            if (HasTagType(n,"Seed")||HasTagType(d,"Seed"))           return "Seed";
            if (HasTagType(n,"Ingredient")||HasTagType(d,"Ingredient")) return "Ingredient";
            return "";
        }

        private int CargoNumber(IMyTerminalBlock block, string type)
        {
            string tag=TagType(type),name=block.CustomName;
            int idx=name.IndexOf("{"+tag,SC); if (idx<0) return 999;
            int start=idx+tag.Length+1; while (start<name.Length&&name[start]==' ') start++;
            int end=start; while (end<name.Length&&char.IsDigit(name[end])) end++;
            int n; return int.TryParse(name.Substring(start,end-start),out n)?n:999;
        }

        private bool HasTagType(string name,string tag){return name.IndexOf("{"+tag,SC)>=0||name.IndexOf("["+tag+"]",SC)>=0;}
        private bool HasSpace(IMyInventory inv){return (float)inv.CurrentVolume<(float)inv.MaxVolume*0.98f;}
        private string TagType(string t){return t.Equals("Tool",SC)?"Tools":t;}
        private string CleanCargoName(string n){int i=n.IndexOf("{");return i>=0?n.Substring(0,i).Trim():n.Trim();}

        private void BuildProductionLists()
        {
            _assemblers.Clear(); _basicAssemblers.Clear(); _advAssemblers.Clear(); _asmSorted.Clear(); _refineries.Clear();
            for (int i=0; i<_blocks.Count; i++)
            {
                var b=_blocks[i]; if (b==null||IsNoSort(b)||HasToken(b.CustomName,_hiddenTag)) continue;
                var asm=b as IMyAssembler; if (asm!=null&&asm.IsFunctional&&!HasToken(asm.CustomName,_manualTag)&&MatchSpec(asm,_prodAssemblers,true))
                { _assemblers.Add(asm);
                  bool _isBasicAsm=asm.BlockDefinition.SubtypeId.IndexOf("Basic",SC)>=0;
                  if (_isBasicAsm) _basicAssemblers.Add(asm); else _advAssemblers.Add(asm);
                  continue; }
                var ref2=b as IMyRefinery; if (ref2!=null&&ref2.IsFunctional&&!HasToken(ref2.CustomName,_manualTag)&&MatchSpec(ref2,_prodRefineries,true)) _refineries.Add(ref2);
            }
            BuildAsmSorted();
        }

        private void RunProduction()
        {
            _lastQueued=0;_lastAsmMoves=0;_prodWarning="";_lastQueuedItem="";
            if (!_prodEnabled){_prodStatus="disabled";return;}
            if (_globalPause){_prodStatus="paused";return;}
            if (_assemblers.Count==0&&_refineries.Count==0){_prodStatus="no machines";return;}
            BuildAsmSorted(); UpdateCompStock(); UpdateQueuedComps();
            if (_sortAsmQueue) _lastAsmMoves=SortAssemblerQueues();
            UpdateProdWarning();
            if (_monitorOnly){_prodStatus="monitoring";return;}
            if (_autocraftComps) _lastQueued=QueueCompQuotas();
            if (_autoDisassemble) _lastQueued+=DisassembleExcess();
            _prodStatus=(_lastQueued+_lastAsmMoves>0)?"active":"idle";
            UpdateProdWarning();
        }

        private void UpdateCompStock()
        {
            _compStock.Clear();
            for (int i=0;i<_blocks.Count;i++)
            { var b=_blocks[i]; if (b==null||!b.HasInventory||IsNoSort(b)||HasToken(b.CustomName,_hiddenTag)) continue;
              for (int inv=0;inv<b.InventoryCount;inv++) { _invItems.Clear(); b.GetInventory(inv).GetItems(_invItems);
                for (int j=0;j<_invItems.Count;j++) { var t=_invItems[j].Type; if (!t.TypeId.ToString().EndsWith("_Component")) continue;
                  string sub=t.SubtypeId.ToString(); double cur; _compStock.TryGetValue(sub,out cur); _compStock[sub]=cur+(double)_invItems[j].Amount; } } }
        }

        private void UpdateQueuedComps()
        {
            _compQueued.Clear();
            for (int i=0;i<_assemblers.Count;i++) { _queue.Clear(); _assemblers[i].GetQueue(_queue);
              for (int q=0;q<_queue.Count;q++) { string item=CompFromBP(_queue[q].BlueprintId); if (item.Length==0) continue;
                double cur; _compQueued.TryGetValue(item,out cur); _compQueued[item]=cur+(double)_queue[q].Amount; } }
        }

        private int QueueCompQuotas()
        {
            int queued=0;
            foreach (var quota in _compQuotas)
            {
                if (queued>=_maxQueuePerRun) break;
                double stock,alreadyQueued;
                _compStock.TryGetValue(quota.Key,out stock);
                _compQueued.TryGetValue(quota.Key,out alreadyQueued);
                // If already queued enough, skip this item entirely
                if (alreadyQueued>=quota.Value-stock) continue;
                double need=quota.Value-stock-alreadyQueued; if (need<1) continue;
                MyDefinitionId bp;
                if (!FindBpFor(quota.Key,out bp)){_prodWarning="No blueprint for "+quota.Key;continue;}
                // Queue the full need up to _maxQueueAmount per cycle
                double amount=Math.Min(Math.Ceiling(need),_maxQueueAmount);
                QueueToAllMasters(bp,amount,IsBasicComp(quota.Key));
                queued++; _lastQueuedItem=amount.ToString("0")+" "+quota.Key;
            }
            return queued;
        }


        private void BuildAsmSorted()
        { _asmSorted.Clear(); var adv=new List<IMyAssembler>(_advAssemblers); var basic=new List<IMyAssembler>(_basicAssemblers); adv.Sort((a,b)=>a.CustomName.CompareTo(b.CustomName)); basic.Sort((a,b)=>a.CustomName.CompareTo(b.CustomName)); _asmSorted.AddRange(adv); _asmSorted.AddRange(basic); }

        private int DisassembleExcess()
        {
            int queued=0;
            foreach (var quota in _compQuotas)
            {
                if (queued>=_maxQueuePerRun) break;
                double stock,alreadyAssembling;
                _compStock.TryGetValue(quota.Key,out stock);
                _compQueued.TryGetValue(quota.Key,out alreadyAssembling);
                // Skip if assemblers are already producing this - do not fight autocrafting
                if (alreadyAssembling>0) continue;
                double excess=stock-quota.Value; if (excess<1) continue;
                MyDefinitionId bp; if (!FindBpFor(quota.Key,out bp)) continue;
                double amount=Math.Min(Math.Ceiling(excess),_maxQueueAmount);
                QueueDisassemble(bp,amount);
                queued++; _lastQueuedItem="Disasm "+amount.ToString("0")+" "+quota.Key;
            }
            return queued;
        }

        private void QueueDisassemble(MyDefinitionId bp, double amount)
        {
            List<IMyAssembler> pool=_assemblers;
            for (int i=0;i<pool.Count;i++)
            { var a=pool[i]; if (a.CooperativeMode) continue; if (a.CustomName.IndexOf("!assemble-only",SC)>=0) continue; if (!a.CanUseBlueprint(bp)) continue;
              if (a.Mode!=MyAssemblerMode.Disassembly){if(a.IsProducing)continue;a.Mode=MyAssemblerMode.Disassembly;}
              a.AddQueueItem(bp,(MyFixedPoint)amount); return; }
        }

        private void UpdateProdWarning()
        {
            if (_prodWarning.Length>0) return;
            if (_prodShowBlockedAsm)
                for (int i=0;i<_assemblers.Count;i++) if (!_assemblers[i].IsProducing&&!_assemblers[i].IsQueueEmpty){_prodWarning="Assembler blocked: "+_assemblers[i].CustomName;return;}
            if (_prodShowBlockedRef)
                for (int i=0;i<_refineries.Count;i++) { var inv=_refineries[i].GetInventory(0); if (!_refineries[i].IsProducing&&inv.ItemCount>0){_prodWarning="Refinery blocked: "+_refineries[i].CustomName;return;} }
            if (_prodShowMissing)
                foreach (var q in _compQuotas) { double stock,queued; _compStock.TryGetValue(q.Key,out stock); _compQueued.TryGetValue(q.Key,out queued); if (q.Value>0&&(stock+queued)/q.Value<_prodWarnBelow){_prodWarning="Low component: "+q.Key;return;} }
        }

        private bool IsBasicComp(string item)
        {
            string[] basic={"SteelPlate","InteriorPlate","Construction","SmallTube","LargeTube",
                            "Motor","Display","BulletproofGlass","Girder","MetalGrid"};
            for (int i=0;i<basic.Length;i++) if (item.Equals(basic[i],SC)) return true;
            return false;
        }

        private bool FindBpFor(string item, out MyDefinitionId bp)
        {
            bp=new MyDefinitionId();
            if (_bpCache.TryGetValue(item,out bp)) return true;
            // Try all known SE blueprint ID patterns and validate against a real assembler
            string pre="MyObjectBuilder_BlueprintDefinition/";
            string[] cands={pre+item, pre+item+"Component", pre+"Position0010_"+item, pre+"Position0010_"+item+"Component"};
            for (int ci=0;ci<cands.Length;ci++)
            {
                MyDefinitionId id;
                if (!MyDefinitionId.TryParse(cands[ci],out id)) continue;
                // Validate: at least one assembler accepts this blueprint
                bool valid=false;
                for (int ai=0;ai<_assemblers.Count;ai++)
                    if (_assemblers[ai].CanUseBlueprint(id)){valid=true;break;}
                if (!valid) continue;
                bp=id; _bpCache[item]=bp; return true;
            }
            return false;
        }

        private void QueueToAllMasters(MyDefinitionId bp, double amount, bool isBasic)
        {
            List<IMyAssembler> preferred=isBasic&&_basicAssemblers.Count>0?_basicAssemblers:_advAssemblers.Count>0?_advAssemblers:_assemblers;
            bool queued=false;
            // Queue to first available idle master; coop assemblers pick up automatically
            for (int i=0;i<preferred.Count;i++)
            {
                var a=preferred[i];
                if (a.CooperativeMode) continue;
                if (a.CustomName.IndexOf("!disassemble-only",SC)>=0) continue;
                if (a.Mode==MyAssemblerMode.Disassembly){if(a.IsProducing)continue;a.Mode=MyAssemblerMode.Assembly;}
                a.AddQueueItem(bp,(MyFixedPoint)amount);
                queued=true; break;
            }
            if (!queued)
            {
                for (int i=0;i<_assemblers.Count;i++)
                {
                    var a=_assemblers[i];
                    if (a.CooperativeMode) continue;
                    if (a.CustomName.IndexOf("!disassemble-only",SC)>=0) continue;
                    if (a.Mode==MyAssemblerMode.Disassembly){if(a.IsProducing)continue;a.Mode=MyAssemblerMode.Assembly;}
                    a.AddQueueItem(bp,(MyFixedPoint)amount);
                    break;
                }
            }
        }

        private IMyAssembler FindAsmFor(string item, out MyDefinitionId bp)
        {
            bp=new MyDefinitionId();
            if (!FindBpFor(item,out bp)) return null;
            bool isBasic=IsBasicComp(item);
            List<IMyAssembler> pool = isBasic&&_basicAssemblers.Count>0 ? _basicAssemblers : _advAssemblers.Count>0 ? _advAssemblers : _assemblers;
            for (int i=0;i<pool.Count;i++) if (!pool[i].CooperativeMode&&pool[i].CustomName.IndexOf("!disassemble-only",SC)<0&&pool[i].CanUseBlueprint(bp)) return pool[i];
            return null;
        }

        private int SortAssemblerQueues()
        {
            int moved=0;
            for (int i=0;i<_assemblers.Count&&moved<_maxQueuePerRun;i++)
            { _queue.Clear();_assemblers[i].GetQueue(_queue); if (_queue.Count<2) continue; int best=BestAsmQueueIdx(_queue,_assemblers[i].IsProducing); if (best<=0) continue; _assemblers[i].MoveQueueItemRequest(_queue[best].ItemId,0);moved++; }
            return moved;
        }

        private int BestAsmQueueIdx(List<MyProductionItem> q, bool producing)
        { int best=-1,bestP=int.MaxValue; for (int i=producing?0:1;i<q.Count;i++){int p=PriorityIdx(_assemblerPriority,CompFromBP(q[i].BlueprintId));if(p<bestP){bestP=p;best=i;}} return bestP==int.MaxValue?-1:best; }

        private int SortRefineryInputs()
        {
            int moved=0;
            for (int r=0;r<_refineries.Count;r++)
            {
                var input=_refineries[r].GetInventory(0);
                _invItems.Clear(); input.GetItems(_invItems);
                int bestInRef=BestRefPriority(_invItems);
                if (_refineryPriority.Count>0)
                {
                    IMyInventory bestInv=null; int bestIdx=-1,bestPri=bestInRef;
                    for (int s=0;s<_sources.Count;s++)
                    {
                        var src=_sources[s];
                        if (!src.Type.Equals("Ore",SC)&&src.Type.Length>0) continue;
                        _srcItems.Clear(); src.Inv.GetItems(_srcItems);
                        for (int i=0;i<_srcItems.Count;i++)
                        {
                            int p=OrePriority(_srcItems[i].Type);
                            if (p<bestPri) { bestPri=p; bestInv=src.Inv; bestIdx=i; }
                        }
                    }
                    if (bestInv!=null&&bestIdx>=0&&bestInv.TransferItemTo(input,bestIdx,null,true)) moved++;
                }
                _invItems.Clear(); input.GetItems(_invItems);
                if (_invItems.Count<2) continue;
                int best=BestRefIdx(_invItems); if (best<=0) continue;
                input.TransferItemTo(input,best,0,true);
                if (moved<_maxQueuePerRun) moved++;
            }
            return moved;
        }

        private int BestRefIdx(List<MyInventoryItem> items)
        { int best=-1,bestP=int.MaxValue; for (int i=0;i<items.Count;i++){int p=OrePriority(items[i].Type);if(p<bestP){bestP=p;best=i;}} return bestP==int.MaxValue?-1:best; }

        private int BestRefPriority(List<MyInventoryItem> items)
        { int best=int.MaxValue; for(int i=0;i<items.Count;i++){int p=OrePriority(items[i].Type);if(p<best)best=p;} return best; }

        private int OrePriority(MyItemType t)
        { if(!t.TypeId.ToString().EndsWith("_Ore"))return int.MaxValue; return PriorityIdx(_refineryPriority,t.SubtypeId.ToString()); }

        private int PriorityIdx(List<string> list, string item)
        { for (int i=0;i<list.Count;i++) if (item.IndexOf(list[i],SC)>=0||list[i].IndexOf(item,SC)>=0) return i; return int.MaxValue; }

        private string CompFromBP(MyDefinitionId bp)
        { string s=bp.SubtypeName; if (s.StartsWith("Position",SC)){int idx=s.IndexOf("_");if(idx>=0)s=s.Substring(idx+1);} if (s.EndsWith("Component",SC)) s=s.Substring(0,s.Length-9); return s; }

        private string AsmJob(IMyAssembler a)
        { _queue.Clear(); a.GetQueue(_queue); if(_queue.Count==0)return a.IsProducing?"WORKING":a.CooperativeMode?"COOP":"IDLE"; return Trim(CompFromBP(_queue[0].BlueprintId),24); }

        private string RefOre(IMyRefinery r)
        { _invItems.Clear(); r.GetInventory(0).GetItems(_invItems); if(_invItems.Count==0)return r.IsProducing?"WORKING":"IDLE"; return Trim(SplitName(_invItems[0].Type.SubtypeId.ToString()),24); }

        private string MachineName(string n)
        {
            if(string.IsNullOrEmpty(n))return "";
            _sb.Clear();
            for(int i=0;i<n.Length;i++)
            {
                if(n[i]=='[')
                {
                    int e=n.IndexOf(']',i);
                    if(e>i)
                    {
                        string tag=n.Substring(i,e-i+1);
                        if(tag.Length<=6) _sb.Append(tag);
                        i=e; continue;
                    }
                }
                _sb.Append(n[i]);
            }
            return _sb.ToString().Replace("  "," ").Trim();
        }

        private PowerStats BuildPowerStats(PowerProfile prof)
        {
            var st=new PowerStats(); _selectedIds.Clear();
            AddBatteries(st,prof.Batteries,prof.IncludeUngrouped);
            AddProducers(st,prof.Reactors,"reactor",prof.IncludeUngrouped);
            AddProducers(st,prof.Solar,"solar",prof.IncludeUngrouped);
            AddProducers(st,prof.Wind,"wind",prof.IncludeUngrouped);
            AddProducers(st,prof.Hydrogen,"hydrogen",prof.IncludeUngrouped);
            return st;
        }

        private void AddBatteries(PowerStats st, string spec, bool ungrouped)
        {
            for (int i=0;i<_blocks.Count;i++)
            { var b=_blocks[i] as IMyBatteryBlock; if (b==null||!MatchSpec(b,spec,ungrouped)||!_selectedIds.Add(b.EntityId)) continue;
              st.Batteries++;st.Stored+=b.CurrentStoredPower;st.Capacity+=b.MaxStoredPower;st.Input+=b.CurrentInput;st.Output+=b.CurrentOutput; }
        }

        private void AddProducers(PowerStats st, string spec, string kind, bool ungrouped)
        {
            for (int i=0;i<_blocks.Count;i++)
            { var p=_blocks[i] as IMyPowerProducer; var b=_blocks[i];
              if (p==null||p is IMyBatteryBlock||!IsProducerKind(b,kind)||!MatchSpec(b,spec,ungrouped)||!_selectedIds.Add(b.EntityId)) continue;
              st.Producers++;st.Output+=p.CurrentOutput;st.MaxOutput+=p.MaxOutput;
              if (kind=="reactor")st.Reactors++;else if (kind=="solar")st.Solar++;else if (kind=="wind")st.Wind++;else if (kind=="hydrogen")st.Hydrogen++; }
        }

        private bool MatchSpec(IMyTerminalBlock b, string spec, bool ungrouped)
        {
            if (string.IsNullOrWhiteSpace(spec)) return ungrouped; spec=spec.Trim(); if (spec.Length==0) return ungrouped;
            if (spec.StartsWith("G:",SC)) { string grpName=spec.Substring(2).Trim(); if (grpName.Length==0) return ungrouped; var grp=GridTerminalSystem.GetBlockGroupWithName(grpName); if (grp==null) return false; _groupBuf.Clear();grp.GetBlocks(_groupBuf);return _groupBuf.Contains(b); }
            return b.CustomName.IndexOf(spec,SC)>=0;
        }

        private bool IsProducerKind(IMyTerminalBlock b, string kind)
        {
            if (kind=="reactor") return b is IMyReactor; if (kind=="solar") return b is IMySolarPanel;
            string def=b.BlockDefinition.TypeIdString+"/"+b.BlockDefinition.SubtypeId;
            if (kind=="wind") return def.IndexOf("WindTurbine",SC)>=0; if (kind=="hydrogen") return def.IndexOf("HydrogenEngine",SC)>=0;
            return false;
        }

        private int CountReactorsOnline(PowerProfile prof)
        {
            int count=0;
            for (int i=0;i<_blocks.Count;i++)
            {
                var r=_blocks[i] as IMyReactor;
                if (r!=null&&r.Enabled&&MatchSpec(r,prof.Reactors,prof.IncludeUngrouped)) count++;
            }
            return count;
        }

        private static readonly string[] _knownOreIds  = {"Cobalt","Gold","Ice","Iron","Magnesium","Nickel","Platinum","Scrap","Silicon","Silver","Stone","Uranium"};
        private static readonly string[] _knownIngotIds = {"Cobalt","Gold","Stone","Iron","Magnesium","Nickel","Platinum","Silicon","Silver","Uranium"};
        private static readonly string[] _knownAmmoIds  = {"AutocannonClip","LargeCalibreAmmo","MediumCalibreAmmo","Missile200mm","NATO_25x184mm","SemiAutoPistolMagazine","FullAutoPistolMagazine","ElitePistolMagazine"};
        private static readonly string[] _knownCompIds  = {"BulletproofGlass","Canvas","Computer","Construction","Detector","Display","Explosives","Girder","GravityGenerator","InteriorPlate","LargeTube","Medical","MetalGrid","Motor","PowerCell","RadioCommunication","Reactor","SmallTube","SolarCell","SteelPlate","Superconductor","Thrust"};
        private static readonly string[] _knownFoodIds  = {"Burrito","Curry","FrontierStew","MammalMeatCooked","Lasagna","KelpCrisp","PowerKit","Medkit"};
        private static readonly string[] _knownSeedIds  = {"CoffeeBeanSeed","FruitSeeds","GrainSeeds","MushroomSpores","VegetableSeeds"};
        private static readonly string[] _knownIngredientIds = {"FakeMeat","Wheat","Meat"};

        private void EnsureKnownItems()
        {
            foreach (var n in _knownOreIds)       { string nm=n=="Stone"?"Stone":n=="Scrap"?"Scrap":n=="Ice"?"Ice":SplitName(n)+" Ore"; EnsureStock("Ore",nm,"MyObjectBuilder_Ore/"+n); }
            foreach (var n in _knownIngotIds)      { string nm=n=="Stone"?"Gravel":SplitName(n); EnsureStock("Ingot",nm,"MyObjectBuilder_Ingot/"+n); }
            foreach (var n in _knownAmmoIds)       EnsureStock("Ammo",SplitName(n),"MyObjectBuilder_AmmoMagazine/"+n);
            foreach (var n in _knownCompIds)       EnsureStock("Component",SplitName(n),"MyObjectBuilder_Component/"+n);
            foreach (var n in _knownFoodIds)       EnsureStock("Food",SplitName(n),"MyObjectBuilder_ConsumableItem/"+n);
            foreach (var n in _knownSeedIds)       EnsureStock("Seed",SplitName(n),"MyObjectBuilder_TreeObject/"+n);
            foreach (var n in _knownIngredientIds) EnsureStock("Ingredient",SplitName(n),"MyObjectBuilder_Ingot/"+n);
        }

        private void EnsureStock(string cat, string name, string icon)
        { string key=cat+"/"+name; if (_stockByKey.ContainsKey(key)) return; var e=new StockEntry{Category=cat,Name=name,Icon=icon,Amount=0}; _stockByKey[key]=e; _stockEntries.Add(e); }

        private void BuildStockCache()
        {
            if (_stockEntries.Count>0) return;
            EnsureKnownItems();
            for (int b=0;b<_blocks.Count;b++)
            { var block=_blocks[b]; if (block==null||!block.HasInventory||IsNoSort(block)||HasToken(block.CustomName,_hiddenTag)) continue;
              for (int inv=0;inv<block.InventoryCount;inv++) { _invItems.Clear();block.GetInventory(inv).GetItems(_invItems);
                for (int i=0;i<_invItems.Count;i++) { var t=_invItems[i].Type; string cat=ItemCategory(t);if(cat.Length==0)continue;
                  string name=DisplayName(t),key=cat+"/"+name; StockEntry entry;
                  if (!_stockByKey.TryGetValue(key,out entry)){entry=new StockEntry{Category=cat,Name=name,Icon=ItemIcon(t)};_stockByKey[key]=entry;_stockEntries.Add(entry);}
                  entry.Amount+=(double)_invItems[i].Amount; } } }
        }

        private double StockAmount(string cat, string item)
        { BuildStockCache(); string wanted=NormKey(item); for (int i=0;i<_stockEntries.Count;i++) if (_stockEntries[i].Category.Equals(cat,SC)&&NormKey(_stockEntries[i].Name).Equals(wanted,SC)) return _stockEntries[i].Amount; return 0; }

        private bool HasDashboardCmd(IMyTerminalBlock b)
        { string d=b.CustomData??"";
          if (d.IndexOf(LIGHT_TAG,SC)>=0) return false; // alert blocks are never dashboard screens
          return d.IndexOf("CoreDashboard",SC)>=0||d.IndexOf("PowerDashboard",SC)>=0||d.IndexOf("ReactorRefuel",SC)>=0||d.IndexOf("BatteryControl",SC)>=0||d.IndexOf("LogisticsDashboard",SC)>=0||d.IndexOf("ProductionDashboard",SC)>=0||d.IndexOf("ProductionDetails",SC)>=0||d.IndexOf("ProductionWarnings",SC)>=0||d.IndexOf("Stock",SC)>=0||d.IndexOf("Autocrafting",SC)>=0||d.IndexOf("FuelLifeSupport",SC)>=0||d.IndexOf("AlertDashboard",SC)>=0||d.IndexOf("WarningDashboard",SC)>=0||d.IndexOf("FoodStock",SC)>=0||d.IndexOf("SeedStock",SC)>=0||d.IndexOf("IngredientStock",SC)>=0||d.IndexOf("AGM-",SC)>=0; }

        private void DrawScreen(IMyTerminalBlock block)
        {
            if (block==null) return;
            var prov=block as IMyTextSurfaceProvider; if (prov==null||prov.SurfaceCount<=0) return;
            IMyTextSurface surf; try{surf=prov.GetSurface(0);}catch{return;}
            if (surf==null) return;
            string d=block.CustomData??"";
            try
            {
                if      (d.IndexOf("AlertDashboard",SC)>=0||d.IndexOf("WarningDashboard",SC)>=0||d.IndexOf("AGM-Alerts",SC)>=0) DrawAlertDash(surf);
                else if (d.IndexOf("ReactorRefuel",SC)>=0||d.IndexOf("AGM-Reactor",SC)>=0) DrawPowerDash(surf,2);
                else if (d.IndexOf("BatteryControl",SC)>=0||d.IndexOf("AGM-Battery",SC)>=0) DrawPowerDash(surf,3);
                else if (d.IndexOf("PowerDashboard",SC)>=0||d.IndexOf("AGM-Power",SC)>=0) DrawPowerDash(surf,DashPage(block,"PowerDashboard"));
                else if (d.IndexOf("LogisticsDashboard",SC)>=0)  DrawLogisticsDash(surf);
                else if (d.IndexOf("ProductionDetails",SC)>=0||d.IndexOf("AGM-Production2",SC)>=0) DrawProductionDash(surf,2);
                else if (d.IndexOf("ProductionWarnings",SC)>=0||d.IndexOf("AGM-ProductionWarnings",SC)>=0) DrawProductionDash(surf,3);
                else if (d.IndexOf("ProductionDashboard",SC)>=0) DrawProductionDash(surf,DashPage(block,"ProductionDashboard"));
                else if (d.IndexOf("FuelLifeSupport",SC)>=0||d.IndexOf("LifeSupport",SC)>=0) DrawFuelDash(surf);
                else if (d.IndexOf("Autocrafting",SC)>=0||d.IndexOf("AutoCrafting",SC)>=0) DrawAutocraftDash(surf,DashPage(block,"Autocrafting"));
                else { string sk=StockKind(block); if (sk.Length>0) DrawStockDash(surf,sk,StockPage(block,sk)); else DrawCoreDash(surf); }
            }
            catch (Exception ex)
            {
                DrawErrorScreen(surf, ex.Message);
            }
        }

        private void DrawErrorScreen(IMyTextSurface s, string msg)
        {
            try
            {
                PrepSurf(s);
                var vp=VP(s); var panel=Inset(vp,10f);
                using (var fr=s.DrawFrame())
                {
                    Fill(fr,vp,COL_BG); Fill(fr,panel,COL_PANEL);
                    DrawBorder(fr,vp,COL_BAD,6f);
                    float cx=panel.X+panel.Width*0.5f;
                    Txt(fr,"AGM ERROR",cx,panel.Y+20f,COL_BAD,0.70f,TextAlignment.CENTER);
                    Fill(fr,new RectangleF(panel.X+10f,panel.Y+52f,panel.Width-20f,1f),COL_BAD);
                    int maxChars=(int)(panel.Width/9f); if (maxChars<10) maxChars=10;
                    float y=panel.Y+64f;
                    while (msg.Length>0&&y<panel.Bottom-20f)
                    {
                        string line=msg.Length<=maxChars?msg:msg.Substring(0,maxChars);
                        Txt(fr,line,panel.X+14f,y,COL_WARN,0.34f,TextAlignment.LEFT);
                        msg=msg.Length<=maxChars?"":msg.Substring(maxChars);
                        y+=18f;
                    }
                    Txt(fr,"AGM v"+VERSION,cx,panel.Bottom-16f,COL_DIM,0.28f,TextAlignment.CENTER);
                }
            }
            catch { }
        }

        private void DrawPbScreen()
        {
            IMyTextSurface surf; try{surf=Me.GetSurface(0);}catch{return;} if(surf==null)return;
            PrepSurf(surf); var vp=VP(surf); var panel=Inset(vp,10f);
            bool small=panel.Height<200f;
            using (var fr=surf.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                var cx=panel.X+panel.Width*0.5f;
                if (small)
                {
                    // Compact layout for small grid PB — everything relative
                    float titleSc=panel.Height<130f?0.45f:0.58f;
                    float rowH   =panel.Height<130f?18f:22f;
                    float rowSc  =panel.Height<130f?0.28f:0.34f;
                    Txt(fr,"AGM",cx,panel.Y+panel.Height*0.06f,COL_ACCENT2,titleSc,TextAlignment.CENTER);
                    float y=panel.Y+panel.Height*0.22f;
                    SmallRow(fr,panel,y,rowH,"Pwr", _powerEnabled?"ON":"OFF",      _powerEnabled?COL_OK:COL_DIM,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Log", _logisticsEnabled?"ON":"OFF",  _logisticsEnabled?COL_OK:COL_DIM,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Prod",_prodEnabled?"ON":"OFF",       _prodEnabled?COL_OK:COL_DIM,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Alrt",_alertsEnabled?"ON":"OFF",     _alertsEnabled?AlertColor(_alertOverall):COL_DIM,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"v"+VERSION,"",COL_DIM,rowSc);
                }
                else
                {
                    // Full layout for large grid PB
                    float titleY=panel.Y+panel.Height*0.07f;
                    float authorY=panel.Y+panel.Height*0.17f;
                    float busTop=panel.Y+panel.Height*0.24f;
                    float busH=panel.Height*0.26f;
                    float rowStart=busTop+busH+8f;
                    float rowH=Math.Min(30f,(panel.Bottom-16f-rowStart)/6f);
                    float titleSc=panel.Width>400f?0.85f:0.60f;
                    Txt(fr,"AUTOGRID MANAGER",cx,titleY,COL_ACCENT2,titleSc,TextAlignment.CENTER);
                    Txt(fr,"RevGamer",cx,authorY,COL_TEXT,0.42f,TextAlignment.CENTER);
                    DrawBusAnim(fr,new RectangleF(panel.X,busTop,panel.Width,busH));
                    float y=rowStart;
                    Row(fr,panel,y,"Power",      _powerEnabled?"ONLINE":"OFF",      _powerEnabled?COL_OK:COL_DIM);y+=rowH;
                    Row(fr,panel,y,"Logistics",  _logisticsEnabled?"ONLINE":"OFF",  _logisticsEnabled?COL_OK:COL_DIM);y+=rowH;
                    Row(fr,panel,y,"Production", _prodEnabled?"ONLINE":"OFF",       _prodEnabled?COL_OK:COL_DIM);y+=rowH;
                    Row(fr,panel,y,"Alerts",     _alertsEnabled?"ONLINE":"OFF",     _alertsEnabled?AlertColor(_alertOverall):COL_DIM);y+=rowH;
                    Row(fr,panel,y,"Log",        _logStatus.ToUpperInvariant(),     COL_TEXT);y+=rowH;
                    Row(fr,panel,y,"Screens",    _screens.Count.ToString(),         COL_TEXT);
                    Txt(fr,"AutoGrid Manager v"+VERSION,cx,panel.Bottom-18f,COL_DIM,0.34f,TextAlignment.CENTER);
                }
            }
        }
        private void SmallRow(MySpriteDrawFrame fr,RectangleF panel,float y,float h,string lbl,string val,Color vc,float sc2)
        {
            var row=new RectangleF(panel.X+4f,y,panel.Width-8f,h);
            Fill(fr,row,COL_PANEL2);DrawBorder(fr,row,COL_DIM,1f);
            Txt(fr,lbl,row.X+4f,   row.Y+2f,COL_DIM,sc2,TextAlignment.LEFT);
            if (val.Length>0) Txt(fr,val,row.Right-4f,row.Y+2f,vc,sc2,TextAlignment.RIGHT);
        }

        private void DrawCoreDash(IMyTextSurface s)
        {
            PrepSurf(s);var vp=VP(s);var panel=Inset(vp,10f);
            bool wide=panel.Width>panel.Height*2.5f; // wide short LCD like a name plate
            bool small=panel.Height<160f;
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                if (wide)
                {
                    // Wide short LCD — single status bar layout
                    float cx=panel.X+panel.Width*0.5f;
                    float cy=panel.Y+panel.Height*0.5f;
                    float titleSc=Math.Min(0.85f,panel.Height/80f*0.7f);
                    float valSc  =Math.Min(0.65f,panel.Height/80f*0.55f);
                    float gap    =panel.Width*0.22f;
                    Txt(fr,"AGM",panel.X+20f,cy-titleSc*28f,COL_ACCENT2,titleSc,TextAlignment.LEFT);
                    Txt(fr,AlertLabel(_alertOverall),panel.X+20f,cy+2f,AlertColor(_alertOverall),valSc,TextAlignment.LEFT);
                    Txt(fr,"PWR",panel.X+gap,cy-titleSc*28f,COL_DIM,valSc*0.8f,TextAlignment.LEFT);
                    Txt(fr,_powerEnabled?"ON":"OFF",panel.X+gap,cy+2f,_powerEnabled?COL_OK:COL_DIM,valSc,TextAlignment.LEFT);
                    Txt(fr,"LOG",panel.X+gap*1.6f,cy-titleSc*28f,COL_DIM,valSc*0.8f,TextAlignment.LEFT);
                    Txt(fr,_logisticsEnabled?"ON":"OFF",panel.X+gap*1.6f,cy+2f,_logisticsEnabled?COL_OK:COL_DIM,valSc,TextAlignment.LEFT);
                    Txt(fr,"PROD",panel.X+gap*2.2f,cy-titleSc*28f,COL_DIM,valSc*0.8f,TextAlignment.LEFT);
                    Txt(fr,_prodEnabled?"ON":"OFF",panel.X+gap*2.2f,cy+2f,_prodEnabled?COL_OK:COL_DIM,valSc,TextAlignment.LEFT);
                    Txt(fr,"ALRT",panel.X+gap*2.8f,cy-titleSc*28f,COL_DIM,valSc*0.8f,TextAlignment.LEFT);
                    Txt(fr,_alertsEnabled?"ON":"OFF",panel.X+gap*2.8f,cy+2f,_alertsEnabled?AlertColor(_alertOverall):COL_DIM,valSc,TextAlignment.LEFT);
                }
                else
                {
                    // Normal/tall LCD — full row layout, scale to fit height
                    float titleSc=small?0.60f:0.82f;
                    float rowH   =small?22f:30f;
                    float rowSc  =small?0.34f:0.44f;
                    float titleY =panel.Y+panel.Height*0.06f;
                    float firstY =panel.Y+panel.Height*0.20f;
                    Txt(fr,"CORE STATUS",panel.X+16f,titleY,COL_ACCENT2,titleSc,TextAlignment.LEFT);
                    float y=firstY;
                    SmallRow(fr,panel,y,rowH,"Global pause",_globalPause?"ON":"OFF",      _globalPause?COL_WARN:COL_OK,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Power",        _powerEnabled?"ON":"OFF",     _powerEnabled?COL_OK:COL_DIM,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Logistics",    _logisticsEnabled?"ON":"OFF", _logisticsEnabled?COL_OK:COL_DIM,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Production",   _prodEnabled?"ON":"OFF",      _prodEnabled?COL_OK:COL_DIM,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Alerts",       _alertsEnabled?"ON":"OFF",    _alertsEnabled?COL_OK:COL_DIM,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Alert",        AlertLabel(_alertOverall),    AlertColor(_alertOverall),rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Log",          _logStatus.ToUpperInvariant(),COL_TEXT,rowSc);y+=rowH+2f;
                    SmallRow(fr,panel,y,rowH,"Screens",      _screens.Count.ToString(),    COL_TEXT,rowSc);
                    Txt(fr,"AGM v"+VERSION,panel.X+10f,panel.Bottom-14f,COL_DIM,0.28f,TextAlignment.LEFT);
                }
            }
        }

        private void DrawAlertDash(IMyTextSurface s)
        {
            PrepSurf(s); var vp=VP(s); var panel=Inset(vp,10f);
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG); Fill(fr,panel,COL_PANEL); DrawBorder(fr,vp,COL_ACCENT,6f);
                Txt(fr,"AGM ALERTS",panel.X+24f,panel.Y+24f,COL_ACCENT2,0.95f,TextAlignment.LEFT);
                Txt(fr,AlertLabel(_alertOverall),panel.Right-24f,panel.Y+28f,AlertColor(_alertOverall),0.52f,TextAlignment.RIGHT);
                float y=panel.Y+72f;
                var prof=_powerProfiles.Count>0?_powerProfiles[0]:new PowerProfile();
                if (string.IsNullOrWhiteSpace(prof.Batteries)&&string.IsNullOrWhiteSpace(prof.Reactors)&&string.IsNullOrWhiteSpace(prof.Solar)&&string.IsNullOrWhiteSpace(prof.Wind)&&string.IsNullOrWhiteSpace(prof.Hydrogen)) prof.IncludeUngrouped=true;
                var ps=BuildPowerStats(prof);
                double battPct=ps.Capacity>0?ps.Stored/ps.Capacity*100.0:0.0;
                double h2Pct=_h2Max>0?_h2Now/_h2Max*100.0:0.0;
                double o2Pct=_o2Max>0?_o2Now/_o2Max*100.0:0.0;
                AlertRowDetail(fr,panel,y,"Battery",   _alertBattery,   battPct.ToString("0.0")+"%");                              y+=32f;
                AlertRowDetail(fr,panel,y,"Cargo",     _alertCargo,     _alertCargo==ALERT_OK?"OK":_cargoAlertDetail);             y+=32f;
                AlertRowDetail(fr,panel,y,"Hydrogen",  _alertHydrogen,  _h2Count==0?"no tanks":h2Pct.ToString("0.0")+"%");        y+=32f;
                AlertRowDetail(fr,panel,y,"Oxygen",    _alertOxygen,    _o2Count==0?"no tanks":o2Pct.ToString("0.0")+"%");        y+=32f;
                AlertRowDetail(fr,panel,y,"Uranium",   _alertUranium,   FmtKg(_uraniumKg));                                       y+=32f;
                AlertRowDetail(fr,panel,y,"Production",_alertProduction,_prodWarning.Length>0?_prodWarning:"OK");        y+=40f;
                if (_alertCargo>ALERT_OK&&_cargoAlertDetail.Length>0)
                { Txt(fr,"Low stock: "+_cargoAlertDetail,panel.X+24f,y,AlertColor(_alertCargo),0.38f,TextAlignment.LEFT); y+=20f; }
                Txt(fr,"Lights: "+(_warningLights?"ON":"OFF")+"  Screens: "+_screens.Count,panel.X+24f,y,COL_DIM,0.34f,TextAlignment.LEFT);
                Txt(fr,"AutoGrid Manager v"+VERSION,panel.X+24f,panel.Bottom-24f,COL_DIM,0.34f,TextAlignment.LEFT);
            }
        }

        private void AlertRowDetail(MySpriteDrawFrame fr, RectangleF panel, float y, string label, int level, string detail)
        {
            var row=new RectangleF(panel.X+16f,y,panel.Width-32f,26f);
            Fill(fr,row,COL_PANEL2); DrawBorder(fr,row,COL_DIM,1f);
            Txt(fr,label,row.X+10f,row.Y+4f,AlertColor(level),0.46f,TextAlignment.LEFT);
            Txt(fr,Trim(detail,34),row.Right-10f,row.Y+4f,level>ALERT_OK?AlertColor(level):COL_DIM,0.34f,TextAlignment.RIGHT);
        }

        private string AlertLabel(int level){ if(level==ALERT_CRITICAL)return "CRITICAL"; if(level==ALERT_WARNING)return "WARNING"; return "OK"; }
        private Color  AlertColor(int level){ if(level==ALERT_CRITICAL)return COL_BAD; if(level==ALERT_WARNING)return COL_WARN; return COL_OK; }

        private void DrawPowerDash(IMyTextSurface s, int page)
        {
            PrepSurf(s);
            var prof=_powerProfiles.Count>0?_powerProfiles[0]:new PowerProfile();
            if (string.IsNullOrWhiteSpace(prof.Batteries)&&string.IsNullOrWhiteSpace(prof.Reactors)&&string.IsNullOrWhiteSpace(prof.Solar)&&string.IsNullOrWhiteSpace(prof.Wind)&&string.IsNullOrWhiteSpace(prof.Hydrogen)) prof.IncludeUngrouped=true;
            var st=BuildPowerStats(prof);
            double bPct=st.Capacity>0?st.Stored/st.Capacity:0, oPct=st.MaxOutput>0?st.Output/st.MaxOutput:0;
            var vp=VP(s);var panel=Inset(vp,2f);
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                string title=page==2?"REACTOR REFUEL":(page==3?"BATTERY CONTROL":"POWER STATUS");
                Txt(fr,title,panel.X+24f,panel.Y+24f,COL_ACCENT2,0.82f,TextAlignment.LEFT);
                Txt(fr,"P"+Math.Max(1,Math.Min(3,page)),panel.Right-24f,panel.Y+28f,COL_DIM,0.42f,TextAlignment.RIGHT);
                float y=panel.Y+72f;
                if (page==2)
                {
                    Row(fr,panel,y,"Reactors",_reactorInfos.Count.ToString(),COL_TEXT);y+=32f;
                    Row(fr,panel,y,"Uranium in reactors",FmtKg(_uraniumKg),_alertUranium==ALERT_OK?COL_ACCENT2:AlertColor(_alertUranium));y+=32f;
                    Row(fr,panel,y,"Uranium stock",FmtKg(_uraniumStockKg),_uraniumStockKg<=_reactorUraniumLowKg?COL_WARN:COL_TEXT);y+=32f;
                    Row(fr,panel,y,"Lowest reactor",_lowestReactorName+" - "+FmtKg(_lowestReactorKg),_lowestReactorKg<_minUraniumPerReactor?COL_BAD:COL_TEXT);y+=32f;
                    Row(fr,panel,y,"Minimum each",FmtKg(_minUraniumPerReactor),COL_DIM);y+=32f;
                    Row(fr,panel,y,"Target each",FmtKg(_targetUraniumPerReactor),COL_DIM);y+=32f;
                    Row(fr,panel,y,"Auto refuel",_reactorAutoRefuel?"ON":"OFF",_reactorAutoRefuel?COL_WARN:COL_DIM);y+=32f;
                    Row(fr,panel,y,"Status",_reactorRefuelStatus,_reactorRefuelStatus=="OK"?COL_OK:COL_WARN);
                }
                else if (page==3)
                {
                    Row(fr,panel,y,"Mode",_autoReactorCharge?"AUTO REACTOR CHARGE":"MONITOR",_autoReactorCharge?COL_OK:COL_DIM);y+=32f;
                    Row(fr,panel,y,"Battery",Pct(bPct),BatCol(bPct));y+=32f;
                    Row(fr,panel,y,"Low trigger",_pcBattLow.ToString("0.#")+"%",COL_DIM);y+=32f;
                    Row(fr,panel,y,"Full trigger",_pcBattFull.ToString("0.#")+"%",COL_DIM);y+=32f;
                    Row(fr,panel,y,"Reactors",_controlledReactorsOn+" on / "+_controlledReactors,COL_TEXT);y+=32f;
                    Row(fr,panel,y,"Load safety",_powerSafetyHold?"HOLD":_neverOffOutputPct.ToString("0.#")+"%",_powerSafetyHold?COL_WARN:COL_DIM);y+=32f;
                    Row(fr,panel,y,"Light state",_reactorsForcedOn?"AMBER/RED":"GREEN",_reactorsForcedOn?COL_WARN:COL_OK);y+=32f;
                    Row(fr,panel,y,"Status",_powerControlStatus,PowerControlColor());
                }
                else
                {
                    Row(fr,panel,y,"Battery",Pct(bPct),BatCol(bPct));y+=32f;
                    Row(fr,panel,y,"Stored",FmtPow(st.Stored)+"Wh / "+FmtPow(st.Capacity)+"Wh",COL_ACCENT2);y+=32f;
                    Row(fr,panel,y,"Input",FmtPow(st.Input)+"W",COL_TEXT);y+=32f;
                    Row(fr,panel,y,"Output",FmtPow(st.Output)+"W / "+FmtPow(st.MaxOutput)+"W",OutCol(oPct));y+=32f;
                    Row(fr,panel,y,"Reactors",CountReactorsOnline(prof)+" online / "+st.Reactors+" total",COL_TEXT);y+=32f;
                    Row(fr,panel,y,"Solar",st.Solar.ToString(),st.Solar>0?COL_TEXT:COL_DIM);y+=32f;
                    Row(fr,panel,y,"Wind Turbine",st.Wind.ToString(),st.Wind>0?COL_TEXT:COL_DIM);y+=32f;
                    Row(fr,panel,y,"H2 Engine",st.Hydrogen.ToString(),st.Hydrogen>0?COL_TEXT:COL_DIM);y+=32f;
                    Row(fr,panel,y,"State",PowerStateText(bPct,oPct),PowerStateColor(bPct,oPct));
                }
                Txt(fr,"AutoGrid Manager v"+VERSION,panel.X+panel.Width*0.5f,panel.Bottom-18f,COL_DIM,0.34f,TextAlignment.CENTER);
            }
        }

        private void DrawLogisticsDash(IMyTextSurface s)
        {
            PrepSurf(s);var vp=VP(s);var panel=Inset(vp,10f);
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                Txt(fr,"LOGISTICS",panel.X+24f,panel.Y+24f,COL_ACCENT2,0.95f,TextAlignment.LEFT);
                Txt(fr,_logisticsEnabled?"ONLINE":"OFF",panel.Right-24f,panel.Y+28f,_logisticsEnabled?COL_OK:COL_BAD,0.44f,TextAlignment.RIGHT);
                float y=panel.Y+72f;
                Row(fr,panel,y,"State",_logStatus.ToUpperInvariant(),_logisticsEnabled?COL_OK:COL_DIM);y+=32f;
                Row(fr,panel,y,"Cargo",_cargos.Count.ToString(),COL_TEXT);y+=32f;
                Row(fr,panel,y,"Sources",_sources.Count.ToString(),COL_TEXT);y+=32f;
                DrawLogTypeRow(fr,panel,y,"Ore/Ingot","Ore","Ingot");y+=32f;
                DrawLogTypeRow(fr,panel,y,"Component","Component","");y+=32f;
                DrawLogTypeRow(fr,panel,y,"Ammo/Tool","Ammo","Tool");y+=32f;
                DrawLogTypeRow(fr,panel,y,"Bottle","Bottle","");y+=32f;
                Row(fr,panel,y,"Moved/run",_lastMoves+"/"+_maxMoves,_lastMoves>0?COL_OK:COL_DIM);y+=32f;
                Row(fr,panel,y,"Last item",_lastItem.Length>0?_lastItem:"-",COL_TEXT);y+=32f;
                Row(fr,panel,y,"From",_lastFrom.Length>0?MachineName(_lastFrom):"-",COL_TEXT);y+=32f;
                Row(fr,panel,y,"To",_lastTo.Length>0?MachineName(_lastTo):"-",COL_TEXT);
                if (_logWarning.Length>0){y+=32f;Row(fr,panel,y,"Warning",_logWarning,COL_BAD);}
                Txt(fr,"AutoGrid Manager v"+VERSION,panel.X+24f,panel.Bottom-24f,COL_DIM,0.34f,TextAlignment.LEFT);
            }
        }

        private void DrawProductionDash(IMyTextSurface s, int page)
        {
            PrepSurf(s);var vp=VP(s);var panel=Inset(vp,10f);
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                string title=page==2?"ASSEMBLER DETAILS":(page==3?"REFINERY DETAILS":"PRODUCTION");
                Txt(fr,title,panel.X+24f,panel.Y+24f,COL_ACCENT2,0.80f,TextAlignment.LEFT);
                Txt(fr,page>1?"P"+page:(_prodEnabled?"ONLINE":"OFF"),panel.Right-24f,panel.Y+28f,_prodEnabled?COL_OK:COL_BAD,0.44f,TextAlignment.RIGHT);
                float y=panel.Y+72f;
                if (page==2&&_prodV2&&_prodShowDetails)
                {
                    int rows=Math.Max(1,(int)((panel.Bottom-y-34f)/32f)),used=0;
                    if (_asmSorted.Count==0&&_assemblers.Count>0) BuildAsmSorted();
                    _asmScrollT+=Runtime.TimeSinceLastRun.TotalSeconds;
                    if (_asmScrollT>=PROD_SCROLL_INTERVAL&&_asmSorted.Count>rows){_asmScrollT=0;_asmScroll=(_asmScroll+1)%Math.Max(1,_asmSorted.Count);}
                    if (_asmScroll>=_asmSorted.Count) _asmScroll=0;
                    for(int i=0;i<rows&&i<_asmSorted.Count;i++,used++,y+=32f){
                        int idx=(_asmScroll+i)%_asmSorted.Count; var _a=_asmSorted[idx];
                        string _lbl=Trim(MachineName(_a.CustomName),18)+(_a.CooperativeMode?"":" [M]");
                        string _aj=AsmJob(_a); Color _clr=_a.IsProducing?COL_OK:_a.CooperativeMode?COL_DIM:COL_WARN;
                        Row(fr,panel,y,_lbl,_aj,_clr);}
                    if(used==0) Row(fr,panel,y,"Status","NO ASSEMBLERS",COL_DIM);
                }
                else if (page==3&&_prodV2)
                {
                    int rows=Math.Max(1,(int)((panel.Bottom-y-34f)/32f)),used=0;
                    var _refSorted=new List<IMyRefinery>(_refineries); _refSorted.Sort((a,b)=>a.CustomName.CompareTo(b.CustomName));
                    for(int i=0;i<_refSorted.Count&&used<rows;i++,used++,y+=32f) Row(fr,panel,y,Trim(MachineName(_refSorted[i].CustomName),24),RefOre(_refSorted[i]),_refSorted[i].IsProducing?COL_OK:COL_DIM);
                    if(used==0) Row(fr,panel,y,"Status","NO REFINERIES",COL_DIM);
                }
                else
                {
                    Row(fr,panel,y,"State",_prodStatus.ToUpperInvariant(),_prodEnabled?COL_OK:COL_DIM);y+=32f;
                    Row(fr,panel,y,"Mode",_monitorOnly?"MONITOR ONLY":"ACTIVE",_monitorOnly?COL_DIM:COL_OK);y+=32f;
                    Row(fr,panel,y,"Assemblers",ProdAsmProducing()+"/"+_assemblers.Count,COL_ACCENT2);y+=32f;
                    Row(fr,panel,y,"Queued",ProdAsmQueued()+" machines",COL_TEXT);y+=32f;
                    Row(fr,panel,y,"Refineries",ProdRefProducing()+"/"+_refineries.Count,COL_ACCENT2);y+=32f;
                    Row(fr,panel,y,"Ref input",ProdRefInputFill().ToString("0.0")+"%",COL_TEXT);y+=32f;
                    Row(fr,panel,y,"Autocrafting",_autocraftComps?"Online":"Offline",_autocraftComps?COL_OK:COL_DIM);y+=32f;
                    Row(fr,panel,y,"Disassembly",_autoDisassemble?"Online":"Offline",_autoDisassemble?COL_OK:COL_DIM);y+=32f;
                    Row(fr,panel,y,"Last queued",_lastQueuedItem.Length>0?_lastQueuedItem:"none",COL_TEXT);
                    if (_prodWarning.Length>0){y+=32f;Row(fr,panel,y,"Warning",_prodWarning,COL_BAD);}
                }
                Txt(fr,"AutoGrid Manager v"+VERSION,panel.X+24f,panel.Bottom-24f,COL_DIM,0.34f,TextAlignment.LEFT);
            }
        }

        private void DrawStockDash(IMyTextSurface s, string cat, int page)
        {
            BuildStockCache();
            var filtered=new List<StockEntry>(); for(int i=0;i<_stockEntries.Count;i++) if(cat.Equals("Inventory",SC)||_stockEntries[i].Category.Equals(cat,SC)) filtered.Add(_stockEntries[i]);
            filtered.Sort((a,b)=>{int c=a.Category.CompareTo(b.Category);return c!=0?c:a.Name.CompareTo(b.Name);});
            PrepSurf(s);var vp=VP(s);var panel=Inset(vp,10f);
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                int rows=Math.Max(1,(int)((panel.Height-116f)/34f)),pages=Math.Max(1,(int)Math.Ceiling(filtered.Count/(double)rows));
                if(page<1)page=1;if(page>pages)page=pages;int start=(page-1)*rows,end=Math.Min(filtered.Count,start+rows);
                Txt(fr,cat.ToUpperInvariant()+" STOCK",panel.X+24f,panel.Y+24f,COL_ACCENT2,0.85f,TextAlignment.LEFT);
                Txt(fr,"P"+page+"/"+pages,panel.Right-24f,panel.Y+28f,COL_DIM,0.44f,TextAlignment.RIGHT);
                float y=panel.Y+70f; for(int i=start;i<end;i++){DrawStockRow(fr,panel,y,filtered[i]);y+=34f;}
                Txt(fr,"AutoGrid Manager v"+VERSION,panel.X+24f,panel.Bottom-24f,COL_DIM,0.36f,TextAlignment.LEFT);
            }
        }

        private void DrawStockRow(MySpriteDrawFrame fr, RectangleF panel, float y, StockEntry e)
        {
            var row=new RectangleF(panel.X+16f,y,panel.Width-32f,28f); Fill(fr,row,COL_PANEL2);DrawBorder(fr,row,COL_DIM,1f);
            double quota=StockQuota(e);double pct=quota>0?Math.Min(1.0,e.Amount/quota):0.0; Color bc=pct>=0.35?COL_PROG_FILL:COL_WARN;
            string icon=string.IsNullOrEmpty(e.Icon)?"IconInventory":e.Icon;
            fr.Add(new MySprite(SpriteType.TEXTURE,icon,new Vector2(row.X+14f,row.Y+14f),new Vector2(20f,20f),COL_ROW_TEXT));
            FitTxt(fr,e.Name,row.X+30f,y+5f,COL_ROW_TEXT,0.43f,TextAlignment.LEFT,row.Width-150f);
            Txt(fr,FmtAmt(e.Amount),row.Right-108f,y+5f,COL_ROW_TEXT,0.43f,TextAlignment.RIGHT);
            var bar=new RectangleF(row.Right-96f,y+8f,82f,10f); Fill(fr,bar,COL_PROG_BG);
            Fill(fr,new RectangleF(bar.X,bar.Y,bar.Width*(float)pct,bar.Height),bc); DrawBorder(fr,bar,COL_DIM,1f);
        }

        private void DrawAutocraftDash(IMyTextSurface s, int page)
        {
            BuildStockCache(); var names=new List<string>(_compQuotas.Keys);names.Sort();
            PrepSurf(s);var vp=VP(s);var panel=Inset(vp,10f);
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                int rows=Math.Max(1,(int)((panel.Height-128f)/34f)),pages=Math.Max(1,(int)Math.Ceiling(names.Count/(double)rows));
                if(page<1)page=1;if(page>pages)page=pages;int start=(page-1)*rows,end=Math.Min(names.Count,start+rows);
                Txt(fr,"AUTOCRAFTING",panel.X+24f,panel.Y+24f,COL_ACCENT2,0.95f,TextAlignment.LEFT);
                float y=panel.Y+86f;
                for(int i=start;i<end;i++)
                { string item=names[i];double quota=_compQuotas[item],stock=StockAmount("Component",item),pct=quota>0?Math.Min(1.0,stock/quota):0.0;
                  var row=new RectangleF(panel.X+16f,y,panel.Width-32f,28f);Fill(fr,row,COL_PANEL2);DrawBorder(fr,row,COL_DIM,1f);
                  fr.Add(new MySprite(SpriteType.TEXTURE,"MyObjectBuilder_Component/"+item,new Vector2(row.X+14f,row.Y+14f),new Vector2(20f,20f),COL_ROW_TEXT));
                  FitTxt(fr,SplitName(item),row.X+30f,y+5f,COL_ROW_TEXT,0.43f,TextAlignment.LEFT,row.Width-180f);
                  Txt(fr,FmtAmt(stock)+" / "+FmtAmt(quota),row.Right-10f,y+5f,COL_ROW_TEXT,0.43f,TextAlignment.RIGHT);
                  var bar=new RectangleF(row.X+10f,row.Bottom-6f,row.Width-20f,4f);Fill(fr,bar,COL_PROG_BG);
                  Fill(fr,new RectangleF(bar.X,bar.Y,bar.Width*(float)pct,bar.Height),pct>=0.5?COL_PROG_FILL:COL_WARN);y+=34f; }
            }
        }

        private void DrawFuelDash(IMyTextSurface s)
        {
            BuildStockCache();
            PrepSurf(s);var vp=VP(s);var panel=Inset(vp,10f);
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                Txt(fr,"FUEL & LIFE SUPPORT",panel.X+24f,panel.Y+24f,COL_ACCENT2,0.92f,TextAlignment.LEFT);
                Txt(fr,"ONLINE",panel.Right-24f,panel.Y+28f,COL_OK,0.48f,TextAlignment.RIGHT);
                float y=panel.Y+64f;
                DrawTankRow(fr,panel,y,"H2 Hydrogen",_h2Now,_h2Max,_h2Count);y+=62f;
                DrawTankRow(fr,panel,y,"O2 Oxygen",_o2Now,_o2Max,_o2Count);y+=62f;
                Row(fr,panel,y,"Generators",_genWorking+" working / "+_genOnline+" on / "+_genCount,COL_TEXT);y+=32f;
                Row(fr,panel,y,"Ice in gens",FmtAmt(_genIce),COL_ACCENT2);y+=32f;
                Row(fr,panel,y,"Ice stock",FmtAmt(StockAmount("Ore","Ice")),COL_ACCENT2);y+=32f;
                Row(fr,panel,y,"Vents",_ventOk+" OK / "+_ventLeak+" leaking",_ventLeak>0?COL_BAD:COL_OK);
            }
        }

        private void DrawTankRow(MySpriteDrawFrame fr, RectangleF panel, float y, string label, double filled, double cap, int count)
        {
            var row=new RectangleF(panel.X+16f,y,panel.Width-32f,54f);Fill(fr,row,COL_PANEL2);DrawBorder(fr,row,COL_DIM,1f);
            double pct=cap>0?Math.Min(1.0,filled/cap):0.0;
            Txt(fr,label,row.X+10f,row.Y+6f,COL_ROW_TEXT,0.48f,TextAlignment.LEFT);
            Txt(fr,Pct(pct)+" "+FmtGas(filled)+"/"+FmtGas(cap),row.Right-10f,row.Y+6f,COL_ROW_TEXT,0.38f,TextAlignment.RIGHT);
            Txt(fr,count+" tanks",row.Right-10f,row.Y+24f,COL_ROW_DIM,0.30f,TextAlignment.RIGHT);
            var bar=new RectangleF(row.X+10f,row.Y+40f,row.Width-20f,8f);Fill(fr,bar,COL_PROG_BG);
            Fill(fr,new RectangleF(bar.X,bar.Y,bar.Width*(float)pct,bar.Height),pct>=0.20?COL_PROG_FILL:COL_WARN);DrawBorder(fr,bar,COL_ROW_DIM,1f);
        }

        private double _animPulse=0.0;

        private void TickPbAnim(double dt){ _animPulse+=dt*26.0; if(_animPulse>1000.0)_animPulse-=1000.0; }

        private void DrawBusAnim(MySpriteDrawFrame fr, RectangleF panel)
        {
            float cx=panel.X+panel.Width*0.5f, cy=panel.Y+panel.Height*0.5f;
            float sc=Math.Min(panel.Width/310f,panel.Height/130f), box=68f*sc, gap=14f*sc, seg=22f*sc, t=3f*sc;
            float x1=cx-box*0.5f, x2=cx+box*0.5f, y1=cy-20f*sc, y2=cy+20f*sc, ph=(float)(_animPulse%(seg+gap));
            DrawBorder(fr,new RectangleF(cx-box*0.5f,cy-box*0.5f,box,box),COL_ACCENT,2f*sc);
            BusH(fr,panel.X+28f*sc,x1-10f*sc,y1,t,seg,gap,ph);
            BusH(fr,x2+10f*sc,panel.Right-28f*sc,y1,t,seg,gap,ph);
            BusH(fr,panel.X+28f*sc,x1-10f*sc,y2,t,seg,gap,ph);
            BusH(fr,x2+10f*sc,panel.Right-28f*sc,y2,t,seg,gap,ph);
            BusV(fr,cx,panel.Y+12f*sc,cy-box*0.5f-10f*sc,t,12f*sc,16f*sc,ph,COL_PROG_FILL);
            BusV(fr,cx,cy+box*0.5f+10f*sc,panel.Bottom-12f*sc,t,12f*sc,16f*sc,ph,COL_PROG_FILL);
            fr.Add(new MySprite(SpriteType.TEXTURE,"CircleHollow",new Vector2(cx,cy),new Vector2(34f*sc,34f*sc),COL_ACCENT));
            fr.Add(new MySprite(SpriteType.TEXTURE,"Circle",new Vector2(cx,cy),new Vector2(9f*sc,9f*sc),new Color(213,197,66)));
        }

        private void BusH(MySpriteDrawFrame fr,float a,float b,float y,float t,float seg,float gap,float ph)
        { for(float x=a-ph;x<b;x+=seg+gap){float l=Math.Min(seg,b-x);if(l>1f)Fill(fr,new RectangleF(Math.Max(a,x),y-t*0.5f,l,t),COL_ACCENT);} }
        private void BusV(MySpriteDrawFrame fr,float x,float a,float b,float t,float seg,float gap,float ph,Color c)
        { for(float y=a-ph;y<b;y+=seg+gap){float l=Math.Min(seg,b-y);if(l>1f)Fill(fr,new RectangleF(x-t*0.5f,Math.Max(a,y),t,l),c);} }

        private void DrawBootSurface(IMyTextSurface s, double progress)
        {
            if (s==null) return; PrepSurf(s);var vp=VP(s);var panel=Inset(vp,10f);
            using (var fr=s.DrawFrame())
            {
                Fill(fr,vp,COL_BG);Fill(fr,panel,COL_PANEL);DrawBorder(fr,vp,COL_ACCENT,6f);
                var cx=panel.X+panel.Width*0.5f;
                Txt(fr,"AUTOGRID MANAGER",cx,panel.Y+panel.Height*0.30f,COL_ACCENT2,0.85f,TextAlignment.CENTER);
                Txt(fr,"RevGamer",cx,panel.Y+panel.Height*0.42f,COL_TEXT,0.44f,TextAlignment.CENTER);
                Txt(fr,"BOOTING"+new string('.',(int)(_bootDots)),cx,panel.Y+panel.Height*0.52f,COL_OK,0.52f,TextAlignment.CENTER);
                float bw=panel.Width*0.6f; var bar=new RectangleF(cx-bw*0.5f,panel.Y+panel.Height*0.62f,bw,12f);
                Fill(fr,bar,COL_PROG_BG);Fill(fr,new RectangleF(bar.X,bar.Y,bar.Width*(float)progress,bar.Height),progress>=1.0?COL_OK:COL_PROG_FILL);
                DrawBorder(fr,bar,COL_ACCENT2,1f);
                Txt(fr,((int)(progress*100)).ToString()+"%",cx,bar.Bottom+20f,COL_DIM,0.40f,TextAlignment.CENTER);
                Txt(fr,"AutoGrid Manager v"+VERSION,cx,panel.Bottom-18f,COL_DIM,0.34f,TextAlignment.CENTER);
            }
        }

        private void PrepSurf(IMyTextSurface s){if(s==null)return;try{s.ContentType=ContentType.SCRIPT;s.Script="";s.Font="Monospace";s.FontSize=1.0f;s.TextPadding=1f;s.ScriptBackgroundColor=COL_BG;s.BackgroundColor=COL_BG;}catch{}}
        private RectangleF VP(IMyTextSurface s){if(s==null)return new RectangleF(0,0,512,512);var ss=s.SurfaceSize;if(ss.X<1f||ss.Y<1f)ss=new Vector2(512f,512f);return new RectangleF((s.TextureSize-ss)*0.5f,ss);}
        private RectangleF Inset(RectangleF r,float a){return new RectangleF(r.X+a,r.Y+a,r.Width-a*2f,r.Height-a*2f);}
        private void Fill(MySpriteDrawFrame fr,RectangleF r,Color c){fr.Add(new MySprite(SpriteType.TEXTURE,"SquareSimple",r.Position+r.Size*0.5f,r.Size,c));}
        private void DrawBorder(MySpriteDrawFrame fr,RectangleF r,Color c,float t){Fill(fr,new RectangleF(r.X,r.Y,r.Width,t),c);Fill(fr,new RectangleF(r.X,r.Bottom-t,r.Width,t),c);Fill(fr,new RectangleF(r.X,r.Y,t,r.Height),c);Fill(fr,new RectangleF(r.Right-t,r.Y,t,r.Height),c);}
        private void Txt(MySpriteDrawFrame fr,string text,float x,float y,Color c,float sc2,TextAlignment al){fr.Add(new MySprite(SpriteType.TEXT,text??"",new Vector2(x,y),null,c,"Monospace",al,sc2));}
        private void FitTxt(MySpriteDrawFrame fr,string text,float x,float y,Color c,float sc2,TextAlignment al,float width){if(text==null)text="";float s=sc2;if(text.Length>0){float need=text.Length*19f*s;if(need>width)s=Math.Max(0.24f,s*width/need);}Txt(fr,text,x,y,c,s,al);}
        private void Row(MySpriteDrawFrame fr,RectangleF panel,float y,string label,string value,Color vc)
        { var row=new RectangleF(panel.X+16f,y,panel.Width-32f,26f);Fill(fr,row,COL_PANEL2);DrawBorder(fr,row,COL_DIM,1f);
          Txt(fr,label,row.X+10f,row.Y+4f,COL_ROW_TEXT,0.46f,TextAlignment.LEFT);FitTxt(fr,value,row.Right-10f,row.Y+4f,RVC(vc),0.46f,TextAlignment.RIGHT,row.Width-150f); }
        private Color RVC(Color c){if(c.PackedValue==COL_OK.PackedValue||c.PackedValue==COL_WARN.PackedValue||c.PackedValue==COL_BAD.PackedValue)return c;return COL_ROW_TEXT;}
        private void DrawLogTypeRow(MySpriteDrawFrame fr,RectangleF panel,float y,string label,string type1,string type2)
        { int count=0;double cur=0,max2=0; for(int i=0;i<_cargos.Count;i++){if(!_cargos[i].Type.Equals(type1,SC)&&(type2.Length==0||!_cargos[i].Type.Equals(type2,SC)))continue;count++;cur+=(double)_cargos[i].Inv.CurrentVolume;max2+=(double)_cargos[i].Inv.MaxVolume;}
          double pct=max2>0?cur/max2*100.0:0.0; var row=new RectangleF(panel.X+16f,y,panel.Width-32f,26f);Fill(fr,row,COL_PANEL2);DrawBorder(fr,row,COL_DIM,1f);
          Txt(fr,label,row.X+10f,row.Y+4f,COL_ROW_TEXT,0.46f,TextAlignment.LEFT);Txt(fr,count+" cargo  "+pct.ToString("0.0")+"%",row.Right-10f,row.Y+4f,pct>97?COL_BAD:COL_ROW_TEXT,0.46f,TextAlignment.RIGHT); }

        private string ItemCategory(MyItemType t){string s=t.TypeId.ToString(),sub=t.SubtypeId.ToString();if(s.EndsWith("_Ore"))return "Ore";if(s.EndsWith("_Ingot"))return "Ingot";if(s.EndsWith("_Component"))return "Component";if(s.EndsWith("_AmmoMagazine"))return "Ammo";if(s.EndsWith("_PhysicalGunObject"))return "Tool";if(s.EndsWith("_GasContainerObject")||s.EndsWith("_OxygenContainerObject"))return "Bottle";if(s.EndsWith("_ConsumableItem")||s.EndsWith("_Consumable"))return "Food";if(s.EndsWith("_TreeObject"))return "Seed";if(IsFoodIngredient(sub))return "Ingredient";return "";}
        private string DisplayName(MyItemType t){string n=t.SubtypeId.ToString();if(n=="Stone"&&t.TypeId.ToString().EndsWith("_Ingot"))return "Gravel";return SplitName(n);}
        private bool IsFoodIngredient(string sub){string[]ids={"FakeMeat","Wheat","Meat","FakeMeat"};for(int i=0;i<ids.Length;i++)if(sub.IndexOf(ids[i],StringComparison.OrdinalIgnoreCase)>=0)return true;return false;}
        private string ItemIcon(MyItemType t){string s=t.TypeId.ToString(),sub=t.SubtypeId.ToString();if(s.EndsWith("_Ore"))return "MyObjectBuilder_Ore/"+sub;if(s.EndsWith("_Ingot"))return "MyObjectBuilder_Ingot/"+sub;if(s.EndsWith("_Component"))return "MyObjectBuilder_Component/"+sub;if(s.EndsWith("_AmmoMagazine"))return "MyObjectBuilder_AmmoMagazine/"+sub;if(s.EndsWith("_PhysicalGunObject"))return "MyObjectBuilder_PhysicalGunObject/"+sub;if(s.EndsWith("_GasContainerObject"))return "MyObjectBuilder_GasContainerObject/"+sub;if(s.EndsWith("_OxygenContainerObject"))return "MyObjectBuilder_OxygenContainerObject/"+sub;if(s.EndsWith("_ConsumableItem")||s.EndsWith("_Consumable"))return "MyObjectBuilder_ConsumableItem/"+sub;if(s.EndsWith("_TreeObject"))return "MyObjectBuilder_TreeObject/"+sub;return "IconInventory";}
        private string SplitName(string n){if(string.IsNullOrEmpty(n))return "";if(n.EndsWith("Item",SC))n=n.Substring(0,n.Length-4);_sb.Clear();for(int i=0;i<n.Length;i++){char c=n[i];bool nd=char.IsDigit(c);bool pd=i>0&&char.IsDigit(n[i-1]);if(i>0&&((char.IsUpper(c)&&char.IsLower(n[i-1]))||(nd&&!pd)||(!nd&&pd)))_sb.Append(' ');_sb.Append(c);}return _sb.ToString().Trim();}
        private double StockQuota(StockEntry e){if(e.Category.Equals("Component",SC)){double q;if(_compQuotas.TryGetValue(e.Name.Replace(" ",""),out q))return q;if(e.Name.IndexOf("Steel Plate",SC)>=0)return 50000;if(e.Name.IndexOf("Interior Plate",SC)>=0)return 50000;}return Math.Max(1,e.Amount);}
        private string StockKind(IMyTerminalBlock b){string d=b.CustomData??"";if(d.IndexOf("InventoryStock",SC)>=0||d.IndexOf("Inventory Stock",SC)>=0)return "Inventory";if(d.IndexOf("OreStock",SC)>=0)return "Ore";if(d.IndexOf("IngotStock",SC)>=0)return "Ingot";if(d.IndexOf("ComponentStock",SC)>=0)return "Component";if(d.IndexOf("AmmoStock",SC)>=0)return "Ammo";if(d.IndexOf("ToolStock",SC)>=0)return "Tool";if(d.IndexOf("BottleStock",SC)>=0)return "Bottle";if(d.IndexOf("FoodStock",SC)>=0)return "Food";if(d.IndexOf("SeedStock",SC)>=0)return "Seed";if(d.IndexOf("IngredientStock",SC)>=0)return "Ingredient";return "";}
        private int StockPage(IMyTerminalBlock b,string kind){string d=b.CustomData??"",compact=(kind.Equals("Inventory",SC)?"InventoryStock":kind+"Stock");string[]lines=d.Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);for(int i=0;i<lines.Length;i++){string line=StripComment(lines[i]).Trim();if(line.IndexOf(compact,SC)<0)continue;int n=PageNum(line,compact);if(n>0)return n;}return 1;}
        private int DashPage(IMyTerminalBlock b,string cmd){string d=b.CustomData??"";string[]lines=d.Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);for(int i=0;i<lines.Length;i++){string line=StripComment(lines[i]).Trim();if(line.IndexOf(cmd,SC)<0)continue;int n=PageNum(line,cmd);if(n>0)return n;}return 1;}
        private int PageNum(string s,string cmd){int p=s.IndexOf("page",SC);if(p>=0)return FirstNum(s.Substring(p));int c=s.IndexOf(cmd,SC);if(c>=0)return FirstNum(s.Substring(c+cmd.Length));return FirstNum(s);}
        private int FirstNum(string s){int v=0;bool f=false;for(int i=0;i<s.Length;i++){if(char.IsDigit(s[i])){f=true;v=v*10+(s[i]-'0');}else if(f)break;}return v;}
        private bool IsNoSort(IMyTerminalBlock b){if(HasToken(b.CustomName,_noSortTag)||b.CubeGrid.CustomName.IndexOf(_noSortTag,SC)>=0)return true;foreach(var dg in _dockedGridIds){if(b.CubeGrid==dg||b.CubeGrid.IsSameConstructAs(dg))return true;}return false;}
        private bool HasToken(string s,string t){return !string.IsNullOrEmpty(s)&&!string.IsNullOrEmpty(t)&&s.IndexOf(t,SC)>=0;}
        private string NormKey(string v){if(v==null)return "";_sb.Clear();for(int i=0;i<v.Length;i++)if(char.IsLetterOrDigit(v[i]))_sb.Append(char.ToLowerInvariant(v[i]));return _sb.ToString();}
        private string Trim(string s,int max){if(s==null)return "";return s.Length<=max?s:s.Substring(0,max-1)+"~";}
        private string Pct(double r){return (r*100.0).ToString("0.0")+"%";}
        private string FmtKg(double kg){if(kg>=1000)return (kg/1000.0).ToString("0.##")+"t";return kg.ToString("0.#")+"kg";}
        private string FmtAmt(double v){if(v>=1e9)return (v/1e9).ToString("0.##")+"B";if(v>=1e6)return (v/1e6).ToString("0.##")+"M";if(v>=1e3)return (v/1e3).ToString("0.##")+"K";return v.ToString("0.##");}
        private string FmtPow(double mw){double w=mw*1e6;if(w>=1e9)return (w/1e9).ToString("0.##")+"G";if(w>=1e6)return (w/1e6).ToString("0.##")+"M";if(w>=1e3)return (w/1e3).ToString("0.##")+"k";return w.ToString("0");}
        private string FmtGas(double l){if(l>=1e9)return (l/1e9).ToString("0.##")+"GL";if(l>=1e6)return (l/1e6).ToString("0.##")+"ML";if(l>=1e3)return (l/1e3).ToString("0.##")+"KL";return l.ToString("0.##")+"L";}
        private Color BatCol(double p){if(p<0.25)return COL_BAD;if(p<0.50)return COL_WARN;return COL_OK;}
        private Color OutCol(double p){if(p>0.90)return COL_BAD;if(p>0.75)return COL_WARN;return COL_OK;}
        private string PowerStateText(double battPct,double outPct){if(!_powerEnabled)return "OFF";if(_powerSafetyHold)return "SAFETY HOLD";if(battPct*100.0<=_pcBattLow)return "BATTERY LOW";if(outPct>=0.90)return "HIGH LOAD";if(_reactorsForcedOn)return "REACTOR CHARGING";return "STABLE";}
        private Color PowerStateColor(double battPct,double outPct){string s=PowerStateText(battPct,outPct);if(s=="STABLE")return COL_OK;if(s=="HIGH LOAD"||s=="REACTOR CHARGING"||s=="SAFETY HOLD")return COL_WARN;return COL_BAD;}
        private Color PowerControlColor(){if(_powerControlStatus=="BATTERY LOW"||_powerControlStatus=="NO REACTORS"||_powerControlStatus=="NO BATTERIES")return COL_BAD;if(_powerControlStatus=="SAFETY HOLD"||_powerControlStatus=="REACTOR CHARGING"||_powerControlStatus=="CHARGING")return COL_WARN;if(_powerControlStatus=="DISABLED"||_powerControlStatus=="PAUSED"||_powerControlStatus=="MONITOR")return COL_DIM;return COL_OK;}
        private bool TrySplit(string line,char sep,out string key,out string val){key="";val="";int i=line.IndexOf(sep);if(i<0)return false;key=line.Substring(0,i).Trim();val=line.Substring(i+1).Trim();return key.Length>0;}
        private bool ParseBool(string v,bool fb){if(v==null)return fb;v=v.Trim().ToLowerInvariant();if(v=="true"||v=="yes"||v=="on"||v=="1")return true;if(v=="false"||v=="no"||v=="off"||v=="0")return false;return fb;}
        private string StripComment(string line){int i=line.IndexOf("//");if(i>=0)return line.Substring(0,i);i=line.IndexOf(';');if(i>=0)return line.Substring(0,i);return line;}
        private int ProdAsmProducing(){int n=0;for(int i=0;i<_assemblers.Count;i++)if(_assemblers[i].IsProducing)n++;return n;}
        private int ProdAsmQueued()   {int n=0;for(int i=0;i<_assemblers.Count;i++)if(!_assemblers[i].IsQueueEmpty)n++;return n;}
        private int ProdRefProducing(){int n=0;for(int i=0;i<_refineries.Count;i++)if(_refineries[i].IsProducing)n++;return n;}
        private double ProdRefInputFill(){double c=0,m=0;for(int i=0;i<_refineries.Count;i++){var inv=_refineries[i].GetInventory(0);c+=(double)inv.CurrentVolume;m+=(double)inv.MaxVolume;}return m>0?c/m*100.0:0.0;}
    }
}