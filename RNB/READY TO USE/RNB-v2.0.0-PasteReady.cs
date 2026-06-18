private const string RNB_VERSION = "2.0.0";
private const double DEFAULT_IDLE_TIMEOUT_SECONDS     = 600.0;
private const double DEFAULT_REINIT_INTERVAL          = 10.0;
private const double DEFAULT_ASSEMBLER_QUEUE_INTERVAL = 0.5;
private const double DEFAULT_BOOT_DURATION            = 6.0;
private const bool   AUTO_PRODUCE_FIX_MODE            = true;
private const bool   DEFAULT_WAKE_ON_PROJECTOR        = true;
private readonly Color COL_BG        = new Color(  0,  8, 18);
private readonly Color COL_PANEL     = new Color(  0, 13, 28);
private readonly Color COL_ACCENT    = new Color(  0,220,255);
private readonly Color COL_ACCENT_DIM= new Color(  0, 85,120);
private readonly Color COL_DIM       = new Color( 20, 95,135);
private readonly Color COL_WHITE     = new Color(235,245,255);
private readonly Color COL_GREEN     = new Color( 45,255,115);
private readonly Color COL_AMBER     = new Color(255,175, 25);
private readonly Color COL_RED       = new Color(255, 70, 70);
private readonly Color COL_HEADER_BG = new Color(  0, 22, 42);
private readonly Color COL_BAR_BG    = new Color(  0, 28, 48);
private readonly Color COL_BAR_FILL  = new Color(  0,220,255);
private readonly Color COL_BAR_DONE  = new Color( 45,255,115);
private const string TAG_ASSEMBLER        = "[RNBAssembler]";
private const string TAG_BASIC_ASSEMBLER  = "[RNBBasicAssembler]";
private const string TAG_NANOBOT          = "[NanoBot]";
private const string TAG_ALERT            = "[RNBAlert]";
private const string TAG_PROJECTOR        = "[RNBProjector]";
private const string TAG_CORNER_LCD       = "[RNBCorner]";
private const string TAG_LCD_STATUS     = "[RNBStatus]";
private const string TAG_LCD_MISSING    = "[RNBMissing]";
private const string TAG_LCD_WELD       = "[RNBWeld]";
private const string TAG_LCD_GRIND      = "[RNBGrind]";
private const string TAG_LCD_WELDERS    = "[RNBWelders]";
private const string TAG_LCD_ASSEMBLERS = "[RNBAssemblers]";
private const string TAG_LCD_PROJECTORS = "[RNBProjectors]";
public enum RNBState { Working, Idle, Offline, Missing }
public enum PageKind
{
Status, Missing, Weld, Grind, Welders, Assemblers, Projectors
}
private class DisplayEntry
{
public IMyTextSurface Surface;
public PageKind        Page;
}
private class ProjectorInfo
{
public IMyProjector Block;
public string       Name      = "";
public bool         Enabled   = false;
public bool         Functional = false;
public bool         Working   = false;
public bool         Projecting = false;
public int          Total     = 0;
public int          Remaining = 0;
public float        Progress  = 0f;
}
private class BaRHandler
{
public readonly List<IMyShipWelder> Welders = new List<IMyShipWelder>();
public int Count { get { return Welders.Count; } }
public int CountWorking
{
get
{
int n = 0;
for (int i = 0; i < Welders.Count; i++)
if (Welders[i].IsWorking && Welders[i].IsFunctional) n++;
return n;
}
}
public int CountEnabled
{
get
{
int n = 0;
for (int i = 0; i < Welders.Count; i++)
if (Welders[i].Enabled && Welders[i].IsFunctional) n++;
return n;
}
}
public static bool IsBaRWelder(IMyShipWelder w)
{
try { var _ = w.GetValueBool("BuildAndRepair.ScriptControlled"); return true; } catch { }
try { var _ = w.GetValue<long>("BuildAndRepair.Mode"); return true; }           catch { }
return false;
}
public T GetValue<T>(string prop)
{
if (Welders.Count == 0) return default(T);
try { return Welders[0].GetValue<T>(prop); } catch { return default(T); }
}
public void SetEnabled(bool on)
{ for (int i = 0; i < Welders.Count; i++) Welders[i].Enabled = on; }
public void ResetProductionCache()
{ EnsureQueuedFn = null; }
public bool AllowBuild
{ get { return GetValue<bool>("BuildAndRepair.AllowBuild"); } }
public IMySlimBlock CurrentTarget
{ get { return FirstSlimValue("BuildAndRepair.CurrentTarget"); } }
public IMySlimBlock CurrentGrindTarget
{ get { return FirstSlimValue("BuildAndRepair.CurrentGrindTarget"); } }
public List<IMySlimBlock> PossibleTargets()
{ return MergeListValue<IMySlimBlock>("BuildAndRepair.PossibleTargets"); }
public List<IMySlimBlock> PossibleGrindTargets()
{ return MergeListValue<IMySlimBlock>("BuildAndRepair.PossibleGrindTargets"); }
public List<IMyEntity> PossibleCollectTargets()
{ return MergeListValue<IMyEntity>("BuildAndRepair.PossibleCollectTargets"); }
private IMySlimBlock FirstSlimValue(string prop)
{
for (int i = 0; i < Welders.Count; i++)
{
try
{
var v = Welders[i].GetValue<IMySlimBlock>(prop);
if (v != null) return v;
}
catch { }
}
return null;
}
private List<T> MergeListValue<T>(string prop)
{
var r = new List<T>();
for (int i = 0; i < Welders.Count; i++)
{
List<T> d = null;
try { d = Welders[i].GetValue<List<T>>(prop); } catch { }
if (d == null) continue;
for (int j = 0; j < d.Count; j++)
if (!r.Contains(d[j])) r.Add(d[j]);
}
return r;
}
public Dictionary<MyDefinitionId, int> MissingComponents()
{
var r = new Dictionary<MyDefinitionId, int>();
for (int i = 0; i < Welders.Count; i++)
{
Dictionary<MyDefinitionId, int> d = null;
try { d = Welders[i].GetValue<Dictionary<MyDefinitionId, int>>("BuildAndRepair.MissingComponents"); } catch { }
if (d == null) continue;
foreach (var kv in d)
{
int cur;
if (r.TryGetValue(kv.Key, out cur)) { if (kv.Value > cur) r[kv.Key] = kv.Value; }
else r[kv.Key] = kv.Value;
}
}
return r;
}
public Func<IEnumerable<long>, MyDefinitionId, int, int> EnsureQueuedFn;
public int EnsureQueued(IEnumerable<long> ids, MyDefinitionId def, int amt)
{
if (Welders.Count == 0) return -2;
if (EnsureQueuedFn == null)
try { EnsureQueuedFn = Welders[0].GetValue<Func<IEnumerable<long>, MyDefinitionId, int, int>>("BuildAndRepair.ProductionBlock.EnsureQueued"); } catch { }
if (EnsureQueuedFn == null) return -3;
try { return EnsureQueuedFn(ids, def, amt); } catch { return -4; }
}
}
private BaRHandler             _welders      = new BaRHandler();
private List<long>             _assemblerIds = new List<long>();
private List<IMyAssembler>     _assemblers   = new List<IMyAssembler>();
private List<DisplayEntry>     _displays     = new List<DisplayEntry>();
private List<IMyLightingBlock> _alertLights  = new List<IMyLightingBlock>();
private List<IMyTextSurface>   _cornerLcds   = new List<IMyTextSurface>();
private List<ProjectorInfo>    _projectors   = new List<ProjectorInfo>();
private bool                   _usingNanoBotTags = false;
private double   _elapsed          = 0.0;
private double   _nextReinit       = 0.0;
private double   _nextAssembler    = 0.0;
private double   _nextEcho         = 0.0;
private double   _nextWake         = 0.0;
private double   _lastActivityTime = 0.0;
private int      _previousEnabledCount = -1;
private double   _idleTimeoutSeconds     = DEFAULT_IDLE_TIMEOUT_SECONDS;
private double   _reinitInterval         = DEFAULT_REINIT_INTERVAL;
private double   _assemblerQueueInterval = DEFAULT_ASSEMBLER_QUEUE_INTERVAL;
private double   _bootDuration           = DEFAULT_BOOT_DURATION;
private bool     _wakeOnProjector        = DEFAULT_WAKE_ON_PROJECTOR;
private bool     _isOffline        = false;
private RNBState _state            = RNBState.Idle;
private int      _drawTick         = 0;
private List<IMySlimBlock> _weldTargets = null;
private List<IMySlimBlock> _grindTargets = null;
private List<IMyEntity> _collectTargets = null;
private Dictionary<MyDefinitionId, int> _missing = new Dictionary<MyDefinitionId, int>();
private IMySlimBlock _currentTarget = null;
private IMySlimBlock _currentGrindTarget = null;
private string _lastStatusEcho = "";
private int _weldPeak = 0;
private int _weldPrev = 0;
private enum BootStage { Booting, Ready }
private BootStage _bootStage    = BootStage.Booting;
private double    _bootElapsed  = 0.0;
private float     _bootProgress = 0f;
private int       _bootDotCount = 0;
private double    _bootDotTimer = 0.0;
private IMyTextSurface _pbSurface = null;
private readonly List<IMyShipWelder>    _wBuf = new List<IMyShipWelder>();
private readonly List<IMyTerminalBlock> _tBuf = new List<IMyTerminalBlock>();
private readonly System.Text.StringBuilder _measureText = new System.Text.StringBuilder(64);
private IMyTextSurface _drawSurface = null;
private List<long> _basicAssemblerIds    = new List<long>();
private List<long> _advancedAssemblerIds = new List<long>();
private readonly string[] BASIC_COMPONENTS = new string[] {
"SteelPlate", "InteriorPlate", "Construction", "SmallTube",
"LargeTube", "Motor", "Display", "BulletproofGlass", "Girder"
};
public Program()
{
Runtime.UpdateFrequency = UpdateFrequency.Update10;
LoadPbConfig();
var pb = Me as IMyTextSurfaceProvider;
if (pb != null && pb.SurfaceCount > 0)
{
_pbSurface = pb.GetSurface(0);
PrepSurface(_pbSurface);
}
Initialise();
DrawBootScreen(0f);
DrawBootDisplays(0f);
}
public void Save() { }
public void Main(string unused, UpdateType updateSource)
{
_elapsed     += Runtime.TimeSinceLastRun.TotalSeconds;
_bootElapsed += Runtime.TimeSinceLastRun.TotalSeconds;
bool pbBooting = _bootStage == BootStage.Booting;
if (pbBooting)
{
_bootProgress = (float)(_bootElapsed / _bootDuration);
if (_bootProgress > 1f) _bootProgress = 1f;
_bootDotTimer += Runtime.TimeSinceLastRun.TotalSeconds;
if (_bootDotTimer >= 0.4) { _bootDotTimer = 0; _bootDotCount = (_bootDotCount + 1) % 4; }
DrawBootScreen(_bootProgress);
DrawBootDisplays(_bootProgress);
if (_bootElapsed >= _bootDuration)
{
_bootStage = BootStage.Ready;
pbBooting = false;
}
else
{
return;
}
}
if (_elapsed >= _nextReinit)
{
Initialise();
_nextReinit = _elapsed + _reinitInterval;
}
RefreshBaRData();
RefreshProjectors();
int wtc      = _weldTargets != null ? _weldTargets.Count : 0;
bool projectorsActive = ProjectorsActive();
bool anyWork = wtc > 0
|| (_grindTargets != null && _grindTargets.Count > 0)
|| (_collectTargets != null && _collectTargets.Count > 0)
|| projectorsActive;
int enabledBeforeWake = _welders.CountEnabled;
bool manuallyEnabled = _previousEnabledCount == 0 && enabledBeforeWake > 0;
if (manuallyEnabled)
{
_isOffline        = false;
_lastActivityTime = _elapsed;
_nextWake         = _elapsed + 5.0;
_state            = RNBState.Idle;
Echo("ONLINE: Manual enable detected; idle timer restarted.");
}
if (_wakeOnProjector && _elapsed >= _nextWake && _welders.Count > 0 && enabledBeforeWake == 0 && anyWork)
BringOnline(projectorsActive ? "Projector needs build/repair." : "BaR work detected.");
if (_isOffline && _welders.CountEnabled > 0)
{
_isOffline        = false;
_lastActivityTime = _elapsed;
_state            = RNBState.Idle;
Echo("ONLINE: BaR welder enabled manually.");
}
if (!_isOffline && _welders.Count > 0 && _welders.CountEnabled == 0)
{
_state = RNBState.Offline;
}
else if (!_isOffline)
{
if (anyWork) _lastActivityTime = _elapsed;
if (_weldPrev == 0 && wtc > 0) _weldPeak = wtc;
if (wtc > _weldPeak)           _weldPeak = wtc;
_weldPrev = wtc;
if (_missing.Count > 0)
_state = RNBState.Missing;
else if (anyWork)
_state = RNBState.Working;
else if ((_elapsed - _lastActivityTime) >= _idleTimeoutSeconds)
BringOffline("Idle timeout.");
else
_state = RNBState.Idle;
}
if (_elapsed >= _nextAssembler)
{
_nextAssembler = _elapsed + _assemblerQueueInterval;
CheckAssemblerQueues();
}
UpdateAlertLights();
DrawDisplays();
DrawCornerLcds();
if (!pbBooting) DrawPBScreen();
_drawTick = (_drawTick + 1) % 1000;
_previousEnabledCount = _welders.CountEnabled;
}
private void Initialise()
{
LoadPbConfig();
_welders.Welders.Clear();
_welders.ResetProductionCache();
_assemblerIds.Clear();
_assemblers.Clear();
_basicAssemblerIds.Clear();
_advancedAssemblerIds.Clear();
_displays.Clear();
_alertLights.Clear();
_cornerLcds.Clear();
_projectors.Clear();
_usingNanoBotTags = false;
_tBuf.Clear();
GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(_tBuf);
for (int i = 0; i < _tBuf.Count; i++)
{
var tb   = _tBuf[i];
string n = tb.CustomName;
bool sameConstruct = tb.IsSameConstructAs(Me);
bool explicitRnb = HasAnyRnbConfig(tb)
|| n.Contains(TAG_NANOBOT)
|| n.Contains(TAG_ASSEMBLER)
|| n.Contains(TAG_BASIC_ASSEMBLER)
|| n.Contains(TAG_ALERT)
|| n.Contains(TAG_CORNER_LCD)
|| n.Contains(TAG_PROJECTOR)
|| n.Contains(TAG_LCD_STATUS)
|| n.Contains(TAG_LCD_MISSING)
|| n.Contains(TAG_LCD_WELD)
|| n.Contains(TAG_LCD_GRIND)
|| n.Contains(TAG_LCD_WELDERS)
|| n.Contains(TAG_LCD_ASSEMBLERS)
|| n.Contains(TAG_LCD_PROJECTORS);
if (!sameConstruct && !explicitRnb) continue;
if (HasRnbRole(tb, TAG_NANOBOT, "NanoBot"))
{
var nw = tb as IMyShipWelder;
if (nw != null && BaRHandler.IsBaRWelder(nw) && !_welders.Welders.Contains(nw))
{
_welders.Welders.Add(nw);
_usingNanoBotTags = true;
}
}
bool hasBasicTag    = HasRnbRole(tb, TAG_BASIC_ASSEMBLER, "BasicAssembler");
bool hasAdvancedTag = !hasBasicTag && HasRnbRole(tb, TAG_ASSEMBLER, "Assembler");
if (hasBasicTag || hasAdvancedTag)
{
var asm = tb as IMyAssembler;
if (asm != null && !_assemblerIds.Contains(asm.EntityId))
{
_assemblerIds.Add(asm.EntityId);
_assemblers.Add(asm);
bool isBasic;
if (hasBasicTag)
{
isBasic = true;
}
else
{
string defSubtype = asm.BlockDefinition.SubtypeName;
isBasic = defSubtype.IndexOf("Basic", System.StringComparison.OrdinalIgnoreCase) >= 0;
}
if (isBasic)
_basicAssemblerIds.Add(asm.EntityId);
else
_advancedAssemblerIds.Add(asm.EntityId);
}
}
if (HasRnbRole(tb, TAG_ALERT, "Alert"))
{
var lt = tb as IMyLightingBlock;
if (lt != null) _alertLights.Add(lt);
}
if (HasRnbRole(tb, TAG_CORNER_LCD, "Corner"))
{
AddCornerSurfaces(tb);
}
if (HasRnbRole(tb, TAG_PROJECTOR, "Projector"))
{
var proj = tb as IMyProjector;
if (proj != null)
{
var ptb  = proj as IMyTerminalBlock;
string pn = ptb != null ? ptb.CustomName : "Projector";
_projectors.Add(new ProjectorInfo {
Block = proj,
Name  = pn.Replace(TAG_PROJECTOR, "").Trim()
});
}
}
PageKind lcdPage;
if (TagToPage(tb, n, out lcdPage))
{
AddDisplaySurfaces(tb, lcdPage);
}
}
if (!_usingNanoBotTags)
{
_wBuf.Clear();
GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(_wBuf);
for (int i = 0; i < _wBuf.Count; i++)
{
if (!_wBuf[i].IsSameConstructAs(Me)) continue;
if (BaRHandler.IsBaRWelder(_wBuf[i])) _welders.Welders.Add(_wBuf[i]);
}
}
SortAssemblersByName();
string msg = "Rev NanoBot Manager v" + RNB_VERSION + " | Welders:" + _welders.Count
+ (_usingNanoBotTags ? " tagged" : " auto")
+ " Asm:" + _assemblerIds.Count
+ " (B:" + _basicAssemblerIds.Count + " A:" + _advancedAssemblerIds.Count + ")"
+ " LCD:" + _displays.Count
+ " Corner:" + _cornerLcds.Count
+ " Proj:" + _projectors.Count;
EchoStatus(msg);
}
private void EchoStatus(string msg)
{
if (msg == _lastStatusEcho && _elapsed < _nextEcho) return;
_lastStatusEcho = msg;
_nextEcho = _elapsed + 30.0;
Echo(msg);
}
private void SortAssemblersByName()
{
for (int i = 1; i < _assemblers.Count; i++)
{
IMyAssembler current = _assemblers[i];
string currentName = SortName(current);
int j = i - 1;
while (j >= 0 && string.Compare(SortName(_assemblers[j]), currentName, System.StringComparison.OrdinalIgnoreCase) > 0)
{
_assemblers[j + 1] = _assemblers[j];
j--;
}
_assemblers[j + 1] = current;
}
}
private static string SortName(IMyTerminalBlock block)
{
return block != null ? CleanBlockName(block.CustomName) : "";
}
private void LoadPbConfig()
{
_idleTimeoutSeconds     = ConfigDouble("AutoOfflineSeconds", DEFAULT_IDLE_TIMEOUT_SECONDS, 30.0, 86400.0);
_reinitInterval         = ConfigDouble("RescanSeconds", DEFAULT_REINIT_INTERVAL, 1.0, 3600.0);
_assemblerQueueInterval = ConfigDouble("AssemblerQueueSeconds", DEFAULT_ASSEMBLER_QUEUE_INTERVAL, 0.1, 60.0);
_bootDuration           = ConfigDouble("BootSeconds", DEFAULT_BOOT_DURATION, 0.5, 60.0);
_wakeOnProjector        = ConfigBool("WakeOnProjector", DEFAULT_WAKE_ON_PROJECTOR);
}
private double ConfigDouble(string key, double fallback, double min, double max)
{
string value = RnbDataValue(Me, key);
double d;
if (!double.TryParse(value, out d)) return fallback;
if (d < min) return min;
if (d > max) return max;
return d;
}
private bool ConfigBool(string key, bool fallback)
{
string value = RnbDataValue(Me, key);
if (string.IsNullOrEmpty(value)) return fallback;
string v = value.Trim();
if (v.Equals("true", System.StringComparison.OrdinalIgnoreCase)) return true;
if (v.Equals("yes", System.StringComparison.OrdinalIgnoreCase)) return true;
if (v.Equals("on", System.StringComparison.OrdinalIgnoreCase)) return true;
if (v.Equals("1", System.StringComparison.OrdinalIgnoreCase)) return true;
if (v.Equals("false", System.StringComparison.OrdinalIgnoreCase)) return false;
if (v.Equals("no", System.StringComparison.OrdinalIgnoreCase)) return false;
if (v.Equals("off", System.StringComparison.OrdinalIgnoreCase)) return false;
if (v.Equals("0", System.StringComparison.OrdinalIgnoreCase)) return false;
return fallback;
}
private static bool TagToPage(IMyTerminalBlock block, string name, out PageKind page)
{
string pageValue = RnbDataValue(block, "Page");
if (TryParsePage(pageValue, out page)) return true;
if (name.Contains(TAG_LCD_ASSEMBLERS)) { page = PageKind.Assemblers; return true; }
if (name.Contains(TAG_LCD_PROJECTORS)) { page = PageKind.Projectors; return true; }
if (name.Contains(TAG_LCD_MISSING))    { page = PageKind.Missing;    return true; }
if (name.Contains(TAG_LCD_WELDERS))    { page = PageKind.Welders;    return true; }
if (name.Contains(TAG_LCD_WELD))       { page = PageKind.Weld;       return true; }
if (name.Contains(TAG_LCD_GRIND))      { page = PageKind.Grind;      return true; }
if (name.Contains(TAG_LCD_STATUS))     { page = PageKind.Status;     return true; }
page = PageKind.Status;
return false;
}
private static bool HasRnbRole(IMyTerminalBlock block, string nameTag, string role)
{
if (block.CustomName.Contains(nameTag)) return true;
string roles = RnbDataValue(block, "Role");
if (string.IsNullOrEmpty(roles)) return false;
roles = roles.Replace(";", ",");
string[] parts = roles.Split(',');
for (int i = 0; i < parts.Length; i++)
{
string r = parts[i].Trim();
if (r.Equals(role, System.StringComparison.OrdinalIgnoreCase)) return true;
}
return false;
}
private static bool HasAnyRnbConfig(IMyTerminalBlock block)
{
string data = block.CustomData;
if (string.IsNullOrEmpty(data)) return false;
bool inSection = false;
string[] lines = data.Replace("\r", "").Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim();
if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
if (line.StartsWith("[") && line.EndsWith("]"))
{
string section = line.Substring(1, line.Length - 2).Trim();
inSection = section.Equals("RNB", System.StringComparison.OrdinalIgnoreCase);
if (inSection) return true;
}
}
return false;
}
private static bool TryParsePage(string value, out PageKind page)
{
page = PageKind.Status;
if (string.IsNullOrEmpty(value)) return false;
string v = value.Trim();
if (v.Equals("Status",     System.StringComparison.OrdinalIgnoreCase)) { page = PageKind.Status;     return true; }
if (v.Equals("Missing",    System.StringComparison.OrdinalIgnoreCase)) { page = PageKind.Missing;    return true; }
if (v.Equals("Weld",       System.StringComparison.OrdinalIgnoreCase)) { page = PageKind.Weld;       return true; }
if (v.Equals("Grind",      System.StringComparison.OrdinalIgnoreCase)) { page = PageKind.Grind;      return true; }
if (v.Equals("Welders",    System.StringComparison.OrdinalIgnoreCase)) { page = PageKind.Welders;    return true; }
if (v.Equals("Assemblers", System.StringComparison.OrdinalIgnoreCase)) { page = PageKind.Assemblers; return true; }
if (v.Equals("Projectors", System.StringComparison.OrdinalIgnoreCase)) { page = PageKind.Projectors; return true; }
return false;
}
private static string RnbDataValue(IMyTerminalBlock block, string key)
{
string data = block.CustomData;
if (string.IsNullOrEmpty(data)) return "";
bool inSection = false;
string[] lines = data.Replace("\r", "").Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim();
if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
if (line.StartsWith("[") && line.EndsWith("]"))
{
string section = line.Substring(1, line.Length - 2).Trim();
inSection = section.Equals("RNB", System.StringComparison.OrdinalIgnoreCase);
continue;
}
if (!inSection) continue;
int eq = line.IndexOf('=');
if (eq < 1) continue;
string k = line.Substring(0, eq).Trim();
if (!k.Equals(key, System.StringComparison.OrdinalIgnoreCase)) continue;
string v = line.Substring(eq + 1).Trim();
int comment = v.IndexOf('#');
if (comment >= 0) v = v.Substring(0, comment).Trim();
comment = v.IndexOf(';');
if (comment >= 0) v = v.Substring(0, comment).Trim();
return v;
}
return "";
}
private void PrepSurface(IMyTextSurface s)
{
s.ContentType           = ContentType.SCRIPT;
s.ScriptBackgroundColor = COL_BG;
s.BackgroundColor       = COL_BG;
s.ScriptForegroundColor = COL_ACCENT;
s.Font                  = "Monospace";
s.FontSize              = 1.0f;
s.TextPadding           = 1f;
s.Script                = "";
}
private void AddCornerSurfaces(IMyTerminalBlock block)
{
var direct = block as IMyTextSurface;
if (direct != null) AddCornerSurface(direct);
var provider = block as IMyTextSurfaceProvider;
if (provider == null) return;
for (int i = 0; i < provider.SurfaceCount; i++)
AddCornerSurface(provider.GetSurface(i));
}
private void AddCornerSurface(IMyTextSurface surface)
{
if (surface == null || _cornerLcds.Contains(surface)) return;
PrepSurface(surface);
_cornerLcds.Add(surface);
}
private void AddDisplaySurfaces(IMyTerminalBlock block, PageKind page)
{
var direct = block as IMyTextSurface;
if (direct != null) AddDisplaySurface(direct, page);
var provider = block as IMyTextSurfaceProvider;
if (provider == null) return;
for (int i = 0; i < provider.SurfaceCount; i++)
AddDisplaySurface(provider.GetSurface(i), page);
}
private void AddDisplaySurface(IMyTextSurface surface, PageKind page)
{
if (surface == null || _cornerLcds.Contains(surface)) return;
for (int d = 0; d < _displays.Count; d++)
if (_displays[d].Surface == surface) return;
PrepSurface(surface);
_displays.Add(new DisplayEntry { Surface = surface, Page = page });
}
private void RefreshBaRData()
{
_weldTargets        = _welders.PossibleTargets();
_grindTargets       = _welders.PossibleGrindTargets();
_collectTargets     = _welders.PossibleCollectTargets();
_missing            = _welders.MissingComponents();
_currentTarget      = _welders.CurrentTarget;
_currentGrindTarget = _welders.CurrentGrindTarget;
}
private void RefreshProjectors()
{
for (int i = 0; i < _projectors.Count; i++)
{
var info = _projectors[i];
var p    = info.Block;
if (p == null)
{
info.Enabled = false;
info.Functional = false;
info.Working = false;
info.Projecting = false;
info.Total = 0;
info.Remaining = 0;
info.Progress = 0f;
continue;
}
info.Enabled    = p.Enabled;
info.Functional = p.IsFunctional;
info.Working    = p.IsWorking;
info.Projecting = p.IsProjecting;
info.Total     = p.TotalBlocks;
info.Remaining = p.RemainingBlocks;
info.Progress  = info.Total > 0
? 1f - (float)info.Remaining / (float)info.Total
: 0f;
}
}
private bool ProjectorsActive()
{
for (int i = 0; i < _projectors.Count; i++)
if (ProjectorActive(_projectors[i]))
return true;
return false;
}
private static bool ProjectorActive(ProjectorInfo info)
{
return info != null
&& info.Enabled
&& info.Functional
&& info.Working
&& info.Projecting
&& info.Total > 0
&& info.Remaining > 0;
}
private static string ProjectorState(ProjectorInfo info)
{
if (info == null || info.Block == null) return "MISSING";
if (!info.Functional) return "DAMAGED";
if (!info.Enabled) return "OFFLINE";
if (!info.Working) return "NO POWER";
if (!info.Projecting || info.Total <= 0) return "NO BLUEPRINT";
if (info.Remaining <= 0) return "COMPLETE";
return "BUILDING";
}
private Color ProjectorStateColor(ProjectorInfo info)
{
string state = ProjectorState(info);
if (state == "BUILDING" || state == "COMPLETE") return COL_GREEN;
if (state == "DAMAGED" || state == "MISSING") return COL_RED;
if (state == "OFFLINE" || state == "NO POWER") return COL_AMBER;
return COL_DIM;
}
private void BringOffline(string reason)
{
_isOffline = true;
_state     = RNBState.Offline;
_welders.SetEnabled(false);
Echo("OFFLINE: " + reason);
}
private void BringOnline(string reason)
{
_welders.SetEnabled(true);
_isOffline        = false;
_lastActivityTime = _elapsed;
_nextWake         = _elapsed + 5.0;
_state            = RNBState.Working;
Echo("ONLINE: " + reason);
}
private void CheckAssemblerQueues()
{
if (_assemblerIds.Count == 0)
{
Echo("QUEUE: No assemblers registered. Tag one with [RNBAssembler].");
return;
}
if (_welders.Count == 0)
{
Echo("QUEUE: No BaR welders found.");
return;
}
if (_missing.Count == 0) return;
if (AUTO_PRODUCE_FIX_MODE) EnsureAssemblyMode();
foreach (var kv in _missing)
{
if (kv.Value <= 0) continue;
string subtype = kv.Key.SubtypeName;
bool basicCanMake = IsBasicComponent(subtype);
List<long> targets;
if (basicCanMake && _basicAssemblerIds.Count > 0)
targets = _basicAssemblerIds;
else if (_advancedAssemblerIds.Count > 0)
targets = _advancedAssemblerIds;
else
targets = _assemblerIds;
int result = _welders.EnsureQueued(targets, kv.Key, kv.Value);
if (result < 0)
Echo("QUEUE FAIL: " + subtype + " code=" + result);
}
}
private bool IsBasicComponent(string subtype)
{
for (int i = 0; i < BASIC_COMPONENTS.Length; i++)
if (BASIC_COMPONENTS[i] == subtype) return true;
return false;
}
private void EnsureAssemblyMode()
{
for (int i = 0; i < _assemblers.Count; i++)
{
var asm = _assemblers[i];
if (!asm.IsFunctional || !asm.Enabled)     continue;
if (asm.Mode == MyAssemblerMode.Disassembly)
{
asm.Mode = MyAssemblerMode.Assembly;
var tb = asm as IMyTerminalBlock;
Echo("Auto-mode: '" + (tb != null ? tb.CustomName : "asm") + "' -> Assembly");
}
}
}
private void UpdateAlertLights()
{
Color col; float blink;
switch (_state)
{
case RNBState.Working: col = COL_GREEN; blink = 0f;   break;
case RNBState.Missing: col = COL_RED;   blink = 1.5f; break;
case RNBState.Offline: col = COL_AMBER; blink = 3f;   break;
default:               col = COL_DIM;   blink = 0f;   break;
}
for (int i = 0; i < _alertLights.Count; i++)
{
_alertLights[i].Color                = col;
_alertLights[i].Intensity            = 2f;
_alertLights[i].Radius               = 3f;
_alertLights[i].BlinkIntervalSeconds = blink;
_alertLights[i].BlinkLength          = 50f;
}
}
private void DrawDisplays()
{
for (int i = 0; i < _displays.Count; i++)
DrawPageClean(_displays[i]);
}
private void DrawPageClean(DisplayEntry entry)
{
var s = entry.Surface;
if (s == null) return;
PrepSurface(s);
_drawSurface = s;
RectangleF vp = Viewport(s);
RectangleF panel = Inset(vp, 8f);
float pad = 14f;
float ix = panel.X + pad;
float right = panel.Right - pad;
float iw = panel.Width - pad * 2f;
using (var frame = s.DrawFrame())
{
Fill(frame, vp, COL_BG);
Fill(frame, panel, COL_PANEL);
DrawBorder(frame, panel, COL_ACCENT, 3f);
DrawText(frame, "RNB v" + RNB_VERSION + " | Rev NanoBot Manager",
ix, panel.Y + 14f, 0.34f, COL_ACCENT, TextAlignment.LEFT);
string stateStr; Color stateCol;
switch (_state)
{
case RNBState.Working: stateStr = "WORKING"; stateCol = COL_GREEN; break;
case RNBState.Missing: stateStr = "MISSING"; stateCol = COL_RED;   break;
case RNBState.Offline: stateStr = "OFFLINE"; stateCol = COL_AMBER; break;
default:               stateStr = "IDLE";    stateCol = COL_WHITE; break;
}
float row2Y = panel.Y + 38f;
DrawText(frame, "Welders: " + _welders.CountWorking + "/" + _welders.Count,
ix, row2Y, 0.30f, COL_DIM, TextAlignment.LEFT);
DrawText(frame, "LIVE", panel.X + panel.Width * 0.56f, row2Y, 0.28f, COL_GREEN, TextAlignment.LEFT);
DrawProgressBar(frame, panel.X + panel.Width * 0.66f, row2Y + 3f, panel.Width * 0.15f, 6f,
(_drawTick % 80) / 80f, COL_BAR_FILL);
DrawText(frame, "[ " + stateStr + " ]", right, row2Y, 0.30f, stateCol, TextAlignment.RIGHT);
DrawRect(frame, panel.X + panel.Width * 0.5f, panel.Y + 58f, iw, 1f, COL_ACCENT);
float cTop = panel.Y + 69f;
float cH = panel.Height - 101f;
switch (entry.Page)
{
case PageKind.Status:     DrawStatusPage    (frame, panel.X, cTop, panel.Width, cH); break;
case PageKind.Missing:    DrawMissingPage   (frame, panel.X, cTop, panel.Width, cH); break;
case PageKind.Weld:       DrawListPage      (frame, panel.X, cTop, panel.Width, cH, "WELD QUEUE",  _weldTargets);  break;
case PageKind.Grind:      DrawListPage      (frame, panel.X, cTop, panel.Width, cH, "GRIND QUEUE", _grindTargets); break;
case PageKind.Welders:    DrawWeldersPage   (frame, panel.X, cTop, panel.Width, cH); break;
case PageKind.Assemblers: DrawAssemblersPage(frame, panel.X, cTop, panel.Width, cH); break;
case PageKind.Projectors: DrawProjectorsPage(frame, panel.X, cTop, panel.Width, cH); break;
}
float footerY = panel.Bottom - 16f;
DrawRect(frame, panel.X + panel.Width * 0.5f, footerY - 7f, iw, 1f, COL_DIM);
DrawText(frame, PageLabel(entry.Page), ix, footerY, 0.30f, COL_DIM, TextAlignment.LEFT);
double idleSec = _elapsed - _lastActivityTime;
string idleStr = _isOffline ? "OFFLINE" : ("IDLE " + FormatTime(idleSec));
DrawText(frame, idleStr, right, footerY, 0.30f,
_isOffline ? COL_AMBER : COL_DIM, TextAlignment.RIGHT);
}
_drawSurface = null;
}
private void DrawStatusPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
float pad = 14f;
float leftX = ox + pad;
float rightX = ox + W * 0.52f;
float leftV = ox + W * 0.45f;
float rightV = ox + W - pad;
float y = top + 2f;
float rowH = 19f;
float fs = 0.34f;
int wtc = _weldTargets != null ? _weldTargets.Count : 0;
int gtc = _grindTargets != null ? _grindTargets.Count : 0;
int ctc = _collectTargets != null ? _collectTargets.Count : 0;
int damaged = 0;
int disabled = 0;
int enabled = _welders.CountEnabled;
int activeProjectors = 0;
int remainingBlocks = 0;
for (int i = 0; i < _welders.Welders.Count; i++)
{
IMyShipWelder w = _welders.Welders[i];
if (!w.IsFunctional) damaged++;
if (!w.Enabled && w.IsFunctional) disabled++;
}
for (int i = 0; i < _projectors.Count; i++)
{
if (ProjectorActive(_projectors[i]))
{
activeProjectors++;
remainingBlocks += _projectors[i].Remaining;
}
}
string stateStr; Color stateCol;
switch (_state)
{
case RNBState.Working: stateStr = "WORKING"; stateCol = COL_GREEN; break;
case RNBState.Missing: stateStr = "MISSING"; stateCol = COL_RED; break;
case RNBState.Offline: stateStr = "OFFLINE"; stateCol = COL_AMBER; break;
default: stateStr = "IDLE"; stateCol = COL_ACCENT; break;
}
DrawText(frame, "SYSTEM", leftX, y, 0.34f, COL_ACCENT, TextAlignment.LEFT);
DrawText(frame, stateStr, leftV, y, 0.34f, stateCol, TextAlignment.RIGHT);
DrawText(frame, "BUILD", rightX, y, 0.34f, COL_ACCENT, TextAlignment.LEFT);
DrawText(frame, activeProjectors + "/" + _projectors.Count, rightV, y, 0.34f,
activeProjectors > 0 ? COL_GREEN : COL_DIM, TextAlignment.RIGHT);
y += 16f;
DrawRect(frame, ox + W/2f, y, W - pad * 2f, 1f, COL_DIM); y += 8f;
float yStart = y;
DrawStatusMetric(frame, leftX, leftV, y, fs, "BaR enabled", enabled + " / " + _welders.Count,
enabled == _welders.Count && _welders.Count > 0 ? COL_GREEN : COL_AMBER); y += rowH;
DrawStatusMetric(frame, leftX, leftV, y, fs, "Working", _welders.CountWorking.ToString(),
_welders.CountWorking > 0 ? COL_GREEN : COL_ACCENT); y += rowH;
DrawStatusMetric(frame, leftX, leftV, y, fs, "Offline", disabled.ToString(),
disabled > 0 ? COL_AMBER : COL_GREEN); y += rowH;
DrawStatusMetric(frame, leftX, leftV, y, fs, "Damaged", damaged.ToString(),
damaged > 0 ? COL_RED : COL_GREEN); y += rowH;
DrawStatusMetric(frame, leftX, leftV, y, fs, "Assemblers", _assemblerIds.Count.ToString(),
_assemblerIds.Count > 0 ? COL_WHITE : COL_AMBER); y += rowH;
DrawStatusMetric(frame, leftX, leftV, y, fs, "Wake projector", _wakeOnProjector ? "ON" : "OFF",
_wakeOnProjector ? COL_GREEN : COL_DIM);
y = yStart;
DrawStatusMetric(frame, rightX, rightV, y, fs, "Weld queue", wtc.ToString(),
wtc > 0 ? COL_GREEN : COL_DIM); y += rowH;
DrawStatusMetric(frame, rightX, rightV, y, fs, "Grind queue", gtc.ToString(),
gtc > 0 ? COL_AMBER : COL_DIM); y += rowH;
DrawStatusMetric(frame, rightX, rightV, y, fs, "Missing", _missing.Count.ToString(),
_missing.Count > 0 ? COL_RED : COL_GREEN); y += rowH;
DrawStatusMetric(frame, rightX, rightV, y, fs, "Floating", ctc.ToString(),
ctc > 0 ? COL_WHITE : COL_DIM); y += rowH;
DrawStatusMetric(frame, rightX, rightV, y, fs, "Projectors", activeProjectors + " / " + _projectors.Count,
activeProjectors > 0 ? COL_GREEN : COL_DIM); y += rowH;
DrawStatusMetric(frame, rightX, rightV, y, fs, "Remaining", remainingBlocks.ToString(),
remainingBlocks > 0 ? COL_ACCENT : COL_DIM);
float alertY = top + H - 22f;
DrawRect(frame, ox + W/2f, alertY - 6f, W - pad * 2f, 1f, COL_DIM);
string alert = "All systems nominal";
Color alertCol = COL_GREEN;
if (damaged > 0) { alert = damaged + " BaR damaged"; alertCol = COL_RED; }
else if (disabled > 0) { alert = disabled + " BaR offline"; alertCol = COL_AMBER; }
else if (_missing.Count > 0) { alert = _missing.Count + " missing component types"; alertCol = COL_RED; }
else if (_assemblerIds.Count == 0) { alert = "No assemblers registered"; alertCol = COL_AMBER; }
else if (activeProjectors > 0 && _welders.CountWorking == 0) { alert = "Projector waiting for BaR"; alertCol = COL_AMBER; }
DrawTextFit(frame, "ALERT", leftX, alertY, W * 0.18f, 0.30f, 0.20f, COL_DIM, TextAlignment.LEFT);
DrawTextFit(frame, alert, rightV, alertY, W * 0.70f, 0.34f, 0.20f, alertCol, TextAlignment.RIGHT);
}
private void DrawMissingPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
float y   = top;
float lx  = ox + 14f;
float vx  = ox + W - 14f;
float rowH = 22f;
DrawText(frame, "MISSING PARTS", lx, y, 0.46f, COL_RED, TextAlignment.LEFT);
DrawText(frame, _missing.Count + " TYPES", vx, y, 0.40f,
_missing.Count > 0 ? COL_RED : COL_GREEN, TextAlignment.RIGHT);
y += 23f;
DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;
if (_missing.Count == 0)
{
DrawText(frame, "ALL PARTS AVAILABLE", ox + W/2f, top + H/2f - 8f, 0.72f, COL_GREEN, TextAlignment.CENTER);
DrawText(frame, "No missing components", ox + W/2f, top + H/2f + 24f, 0.44f, COL_DIM, TextAlignment.CENTER);
return;
}
int maxRows = (int)((top + H - y) / rowH);
if (maxRows <= 0)
{
DrawText(frame, _missing.Count + " missing types", ox + W/2f, top + H/2f, 0.65f, COL_RED, TextAlignment.CENTER);
return;
}
int shown = 0;
foreach (var kv in _missing)
{
if (shown >= maxRows - 1) { DrawText(frame, "...", lx, y, 0.42f, COL_DIM, TextAlignment.LEFT); break; }
Color c = shown % 2 == 0 ? new Color(255,120,120) : new Color(220,80,80);
DrawRect(frame, ox + W/2f, y + 9f, W - 24f, 18f, shown % 2 == 0 ? new Color(20,35,55) : new Color(12,26,44));
DrawTextFit(frame, "x" + kv.Value, lx + 4f, y, 80f, 0.34f, 0.20f, c, TextAlignment.LEFT);
DrawTextFit(frame, DefinitionName(kv.Key), lx + 92f, y, W - 116f, 0.34f, 0.20f, c, TextAlignment.LEFT);
y += rowH; shown++;
}
}
private void DrawListPage(MySpriteDrawFrame frame, float ox, float top, float W, float H,
string title, List<IMySlimBlock> list)
{
float y    = top;
float lx   = ox + 14f;
float rowH = 18f;
int   count = list != null ? list.Count : 0;
DrawText(frame, title, lx, y, 0.46f, COL_ACCENT, TextAlignment.LEFT);
DrawText(frame, count.ToString(), ox + W - 14f, y, 0.46f,
count > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
y += 23f;
DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;
if (title == "WELD QUEUE" && _weldPeak > 0)
{
int built  = _weldPeak - count;
if (built < 0) built = 0;
float pct  = (float)built / (float)_weldPeak;
DrawProgressBar(frame, ox + 14f, y, W - 28f, 14f, pct, pct >= 1f ? COL_BAR_DONE : COL_BAR_FILL);
y += 22f;
DrawText(frame, built + "/" + _weldPeak + " built  " + (int)(pct*100f) + "%",
lx, y, 0.42f, COL_DIM, TextAlignment.LEFT);
y += 20f;
}
if (count == 0)
{
DrawText(frame, "QUEUE EMPTY", ox + W/2f, top + H/2f - 4f, 0.68f, COL_DIM, TextAlignment.CENTER);
return;
}
int maxRows = (int)((top + H - y) / rowH);
int shown   = 0;
for (int i = 0; i < list.Count && shown < maxRows - 1; i++, shown++)
{
Color c = shown % 2 == 0 ? COL_WHITE : new Color(160,190,220);
DrawTextFit(frame, SlimName(list[i]), lx, y, W - 28f, 0.34f, 0.20f, c, TextAlignment.LEFT);
y += rowH;
}
if (count > shown)
DrawText(frame, "+ " + (count - shown) + " more", lx, y, 0.42f, COL_DIM, TextAlignment.LEFT);
}
private void DrawWeldersPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
float y    = top;
float lx   = ox + 14f;
float vx   = ox + W - 14f;
float rowH = 18f;
float fs   = 0.34f;
DrawText(frame, _usingNanoBotTags ? "NANOBOT DETAILS" : "WELDER DETAILS", lx, y, 0.46f, COL_ACCENT, TextAlignment.LEFT);
DrawText(frame, _welders.Count.ToString(), vx, y, 0.46f,
_welders.Count > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
y += 23f;
DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;
if (_welders.Count == 0)
{ DrawText(frame, "No BaR welders found", ox + W/2f, top + H/2f, 0.5f, COL_DIM, TextAlignment.CENTER); return; }
float nameX = lx;
float statusX = ox + W * 0.49f;
float modeX = ox + W * 0.66f;
float reasonX = vx;
float nameW = statusX - nameX - 8f;
float statusW = modeX - statusX - 8f;
float modeW = reasonX - modeX - 82f;
DrawText(frame, "NAME", nameX, y, 0.26f, COL_DIM, TextAlignment.LEFT);
DrawText(frame, "STATE", statusX, y, 0.26f, COL_DIM, TextAlignment.LEFT);
DrawText(frame, "MOVE/WORK", modeX, y, 0.26f, COL_DIM, TextAlignment.LEFT);
DrawText(frame, "INFO", reasonX, y, 0.26f, COL_DIM, TextAlignment.RIGHT);
y += 15f;
for (int i = 0; i < _welders.Welders.Count; i++)
{
if (y + rowH > top + H)
{
DrawText(frame, "+" + (_welders.Welders.Count - i) + " more", lx, y, 0.30f, COL_DIM, TextAlignment.LEFT);
break;
}
var w  = _welders.Welders[i];
var tb = w as IMyTerminalBlock;
string wName = tb != null ? CleanBlockName(tb.CustomName) : "Welder";
Color nameCol;
string statusStr;
if (!w.IsFunctional)       { nameCol = COL_RED;    statusStr = "DAMAGED"; }
else if (!w.Enabled)       { nameCol = COL_AMBER;  statusStr = "OFFLINE"; }
else if (w.IsWorking)      { nameCol = COL_GREEN; statusStr = "WORKING"; }
else                       { nameCol = COL_ACCENT; statusStr = "IDLE";    }
bool hasTarget = WelderSlimValue(w, "BuildAndRepair.CurrentTarget") != null
|| WelderSlimValue(w, "BuildAndRepair.CurrentGrindTarget") != null;
string modeStr = WelderModeSummary(w);
string reasonStr = WelderReason(w);
Color modeCol = statusStr == "WORKING" ? COL_GREEN : (statusStr == "OFFLINE" ? COL_AMBER : COL_ACCENT);
if (hasTarget) reasonStr = "On target";
if (!w.IsFunctional)
DrawRect(frame, ox + W/2f, y + 8f, W - 24f, 16f, new Color(70, 12, 18));
else if (i % 2 == 0)
DrawRect(frame, ox + W/2f, y + 8f, W - 24f, 16f, new Color(5, 20, 34));
DrawTextFit(frame, wName, nameX, y, nameW, fs, 0.22f, nameCol, TextAlignment.LEFT);
DrawTextFit(frame, statusStr, statusX, y, statusW, fs, 0.22f, nameCol, TextAlignment.LEFT);
DrawTextFit(frame, modeStr, modeX, y, modeW, fs, 0.22f, modeCol, TextAlignment.LEFT);
DrawTextFit(frame, reasonStr, reasonX, y, W * 0.28f, fs, 0.22f, COL_DIM, TextAlignment.RIGHT);
y += rowH;
}
}
private void DrawAssemblersPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
float y    = top;
float lx   = ox + 14f;
float vx   = ox + W - 14f;
float rowH = 18f;
float fs   = 0.34f;
DrawText(frame, "ASSEMBLER DETAILS", lx, y, 0.46f, COL_ACCENT, TextAlignment.LEFT);
DrawText(frame, _assemblerIds.Count.ToString(), vx, y, 0.46f,
_assemblerIds.Count > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
y += 23f;
DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;
if (_assemblerIds.Count == 0)
{ DrawText(frame, "No [RNBAssembler] tagged", ox + W/2f, top + H/2f, 0.45f, COL_DIM, TextAlignment.CENTER); return; }
float nameX = lx;
float stateX = ox + W * 0.48f;
float modeX = ox + W * 0.65f;
float outX = vx;
float nameW = stateX - nameX - 8f;
float stateW = modeX - stateX - 8f;
float modeW = outX - modeX - 95f;
DrawText(frame, "NAME", nameX, y, 0.26f, COL_DIM, TextAlignment.LEFT);
DrawText(frame, "STATE", stateX, y, 0.26f, COL_DIM, TextAlignment.LEFT);
DrawText(frame, "MODE", modeX, y, 0.26f, COL_DIM, TextAlignment.LEFT);
DrawText(frame, "OUTPUT", outX, y, 0.26f, COL_DIM, TextAlignment.RIGHT);
y += 15f;
int shown = 0;
for (int i = 0; i < _assemblers.Count; i++)
{
var asm = _assemblers[i];
if (y + rowH > top + H)
{
DrawText(frame, "+" + (_assemblers.Count - i) + " more", lx, y, 0.30f, COL_DIM, TextAlignment.LEFT);
break;
}
var tb = asm as IMyTerminalBlock;
string asmName = tb != null
? CleanBlockName(tb.CustomName)
: "Assembler";
Color nameCol;
string stStr;
if (!asm.IsFunctional)  { nameCol = COL_RED;    stStr = "DAMAGED"; }
else if (!asm.Enabled)  { nameCol = COL_AMBER;  stStr = "OFFLINE"; }
else if (asm.IsWorking) { nameCol = COL_GREEN; stStr = "WORKING"; }
else                    { nameCol = COL_ACCENT; stStr = "IDLE";    }
string modeStr = asm.Mode == MyAssemblerMode.Disassembly ? "DISASSEMBLY" : "ASSEMBLY";
if (asm.CooperativeMode) modeStr = modeStr + " COOP";
if (asm.Repeating) modeStr = modeStr + " RPT";
Color  modeCol = asm.Mode == MyAssemblerMode.Disassembly ? COL_AMBER : COL_WHITE;
int outItems = asm.OutputInventory != null ? asm.OutputInventory.ItemCount : 0;
if (!asm.IsFunctional)
DrawRect(frame, ox + W/2f, y + 8f, W - 24f, 16f, new Color(70, 12, 18));
else if (shown % 2 == 0)
DrawRect(frame, ox + W/2f, y + 8f, W - 24f, 16f, new Color(5, 20, 34));
DrawTextFit(frame, asmName, nameX, y, nameW, fs, 0.22f, nameCol, TextAlignment.LEFT);
DrawTextFit(frame, stStr, stateX, y, stateW, fs, 0.22f, nameCol, TextAlignment.LEFT);
DrawTextFit(frame, modeStr, modeX, y, modeW, fs, 0.22f, modeCol, TextAlignment.LEFT);
DrawText(frame, outItems.ToString(), outX, y, fs, outItems > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
y += rowH;
shown++;
}
if (shown == 0)
DrawText(frame, "No tagged assemblers on grid", ox + W/2f, top + H/2f, 0.45f, COL_DIM, TextAlignment.CENTER);
}
private void DrawProjectorsPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
float y = top;
float lx = ox + 14f;
float vx = ox + W - 14f;
float bw = W - 36f;
int building = 0;
int online = 0;
int offline = 0;
int complete = 0;
for (int i = 0; i < _projectors.Count; i++)
{
ProjectorInfo info = _projectors[i];
if (ProjectorActive(info)) building++;
if (info.Enabled && info.Functional && info.Working) online++;
else offline++;
if (info.Projecting && info.Total > 0 && info.Remaining <= 0) complete++;
}
DrawText(frame, "PROJECTOR CONTROL", lx, y, 0.46f, COL_ACCENT, TextAlignment.LEFT);
DrawText(frame, building + " BUILDING", vx, y, 0.36f,
building > 0 ? COL_GREEN : COL_DIM, TextAlignment.RIGHT);
y += 23f;
DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 7f;
DrawText(frame, "ONLINE " + online, lx, y, 0.28f, online > 0 ? COL_GREEN : COL_DIM, TextAlignment.LEFT);
DrawText(frame, "OFFLINE " + offline, ox + W * 0.5f, y, 0.28f, offline > 0 ? COL_AMBER : COL_DIM, TextAlignment.CENTER);
DrawText(frame, "COMPLETE " + complete, vx, y, 0.28f, complete > 0 ? COL_GREEN : COL_DIM, TextAlignment.RIGHT);
y += 20f;
if (_projectors.Count == 0)
{
DrawText(frame, "NO PROJECTORS REGISTERED", ox + W/2f, top + H/2f, 0.48f, COL_DIM, TextAlignment.CENTER);
return;
}
float slotH = (top + H - y) / _projectors.Count;
if (slotH > 64f) slotH = 64f;
if (slotH < 42f) slotH = 42f;
for (int i = 0; i < _projectors.Count; i++)
{
if (y + 36f > top + H)
{
DrawText(frame, "+" + (_projectors.Count - i) + " MORE PROJECTORS", lx, y, 0.28f, COL_DIM, TextAlignment.LEFT);
break;
}
ProjectorInfo info = _projectors[i];
string state = ProjectorState(info);
Color stateCol = ProjectorStateColor(info);
int built = info.Total - info.Remaining;
if (built < 0) built = 0;
DrawRect(frame, ox + W/2f, y + slotH * 0.46f, W - 24f, slotH - 4f,
i % 2 == 0 ? new Color(5, 20, 34) : new Color(2, 16, 29));
DrawRect(frame, lx + 2f, y + slotH * 0.46f, 3f, slotH - 10f, stateCol);
DrawTextFit(frame, info.Name, lx + 10f, y + 3f, W * 0.62f, 0.36f, 0.21f, COL_WHITE, TextAlignment.LEFT);
DrawTextFit(frame, state, vx - 2f, y + 3f, W * 0.30f, 0.34f, 0.20f, stateCol, TextAlignment.RIGHT);
string detail = info.Total > 0
? built + "/" + info.Total + " built  |  " + info.Remaining + " remaining"
: "No blueprint loaded";
DrawTextFit(frame, detail, lx + 10f, y + 21f, W * 0.72f, 0.28f, 0.18f, COL_DIM, TextAlignment.LEFT);
if (info.Total > 0)
DrawText(frame, (int)(info.Progress * 100f) + "%", vx - 2f, y + 21f, 0.28f, COL_ACCENT, TextAlignment.RIGHT);
if (info.Total > 0 && slotH >= 50f)
DrawProgressBar(frame, lx + 10f, y + 39f, bw, 7f, info.Progress,
info.Progress >= 1f ? COL_BAR_DONE : stateCol);
y += slotH;
}
}
private void DrawCornerLcds()
{
if (_cornerLcds.Count == 0) return;
Color  stateCol;
string stateStr;
string subLine;
int damaged = 0;
for (int i = 0; i < _welders.Welders.Count; i++)
if (!_welders.Welders[i].IsFunctional) damaged++;
if (damaged > 0)
{
stateCol = COL_RED;
stateStr = "DAMAGED";
subLine = damaged + " BaR need" + (damaged == 1 ? "s" : "") + " repair";
}
else switch (_state)
{
case RNBState.Working:
stateCol = COL_GREEN;
stateStr = "WORKING";
subLine  = _welders.CountWorking + "/" + _welders.Count + " BaRs working";
break;
case RNBState.Missing:
stateCol = COL_RED;
stateStr = "MISSING";
subLine  = _missing.Count + " part type" + (_missing.Count != 1 ? "s" : "") + " needed";
break;
case RNBState.Offline:
stateCol = COL_AMBER;
stateStr = "OFFLINE";
subLine  = _isOffline ? "Idle timeout - welders off" : "BaR blocks disabled";
break;
default:
stateCol = COL_ACCENT;
stateStr = "IDLE";
subLine  = _welders.CountEnabled + "/" + _welders.Count + " BaRs online";
break;
}
for (int i = 0; i < _cornerLcds.Count; i++)
{
var s = _cornerLcds[i];
if (s == null) continue;
PrepSurface(s);
_drawSurface = s;
RectangleF vp    = Viewport(s);
float inset = vp.Height < 160f ? 5f : 10f;
RectangleF panel = Inset(vp, inset);
float cx = panel.X + panel.Width * 0.5f;
bool banner = panel.Width / panel.Height >= 2.2f;
double idleSec = _elapsed - _lastActivityTime;
string timer = FormatTime(idleSec);
using (var frame = s.DrawFrame())
{
Fill(frame, vp,    COL_BG);
Fill(frame, panel, COL_PANEL);
DrawBorder(frame, panel, stateCol, 3f);
if (banner)
{
float headerY = panel.Y + panel.Height * 0.10f;
float dividerY = panel.Y + panel.Height * 0.34f;
float mainY = panel.Y + panel.Height * 0.52f;
DrawTextFit(frame, "RNB v" + RNB_VERSION, panel.X + 12f, headerY,
panel.Width * 0.35f, 0.27f, 0.16f, COL_ACCENT, TextAlignment.LEFT);
DrawTextFit(frame, timer, panel.Right - 12f, headerY,
panel.Width * 0.28f, 0.27f, 0.16f, COL_DIM, TextAlignment.RIGHT);
DrawRect(frame, cx, dividerY, panel.Width - 24f, 1f, stateCol);
DrawTextFit(frame, stateStr, panel.X + 14f, mainY,
panel.Width * 0.40f, 0.52f, 0.22f, stateCol, TextAlignment.LEFT);
DrawTextFit(frame, subLine, panel.Right - 14f, mainY,
panel.Width * 0.52f, 0.34f, 0.18f, COL_WHITE, TextAlignment.RIGHT);
}
else
{
float headerY = panel.Y + 12f;
float dividerY = panel.Y + 36f;
float stateY = panel.Y + panel.Height * 0.38f;
float subY = panel.Y + panel.Height * 0.68f;
DrawTextFit(frame, "RNB v" + RNB_VERSION, panel.X + 14f, headerY,
panel.Width * 0.45f, 0.34f, 0.18f, COL_ACCENT, TextAlignment.LEFT);
DrawTextFit(frame, timer, panel.Right - 14f, headerY,
panel.Width * 0.35f, 0.34f, 0.18f, COL_DIM, TextAlignment.RIGHT);
DrawRect(frame, cx, dividerY, panel.Width - 28f, 1f, stateCol);
DrawTextFit(frame, stateStr, cx, stateY,
panel.Width - 36f, 0.90f, 0.30f, stateCol, TextAlignment.CENTER);
DrawTextFit(frame, subLine, cx, subY,
panel.Width - 36f, 0.42f, 0.20f, COL_WHITE, TextAlignment.CENTER);
}
}
_drawSurface = null;
}
}
private void DrawBootScreen(float progress)
{
if (_pbSurface == null) return;
DrawBootSurfaceClean(_pbSurface, progress, true);
}
private void DrawBootDisplays(float progress)
{
for (int i = 0; i < _displays.Count; i++)
DrawBootSurfaceClean(_displays[i].Surface, progress, false);
}
private void DrawBootSurfaceClean(IMyTextSurface s, float progress, bool compact)
{
if (s == null) return;
PrepSurface(s);
if (progress < 0f) progress = 0f;
if (progress > 1f) progress = 1f;
RectangleF vp = Viewport(s);
RectangleF panel = Inset(vp, compact ? 10f : 12f);
Vector2 center = panel.Position + panel.Size * 0.5f;
using (var frame = s.DrawFrame())
{
Fill(frame, vp, COL_BG);
Fill(frame, panel, COL_PANEL);
DrawBorder(frame, panel, COL_ACCENT, compact ? 2f : 3f);
float titleY = panel.Y + panel.Height * (compact ? 0.30f : 0.28f);
DrawText(frame, "RNB", center.X, titleY, compact ? 0.88f : 1.65f, COL_ACCENT, TextAlignment.CENTER);
DrawText(frame, "Rev NanoBot Manager", center.X, titleY + (compact ? 24f : 52f),
compact ? 0.28f : 0.52f, COL_WHITE, TextAlignment.CENTER);
DrawText(frame, "v" + RNB_VERSION + "  |  RevGamer", center.X, titleY + (compact ? 42f : 84f),
compact ? 0.24f : 0.40f, COL_ACCENT, TextAlignment.CENTER);
float barW = Math.Min(panel.Width * (compact ? 0.54f : 0.66f), compact ? 132f : 430f);
float barH = compact ? 8f : 14f;
RectangleF bar = new RectangleF(center.X - barW * 0.5f, center.Y + (compact ? 24f : 58f), barW, barH);
Fill(frame, bar, COL_BAR_BG);
Fill(frame, new RectangleF(bar.X, bar.Y, bar.Width * progress, bar.Height),
progress >= 1f ? COL_GREEN : COL_BAR_FILL);
string dots = new string('.', _bootDotCount);
string bootMsg = progress >= 1f ? "READY" : ("INITIALISING" + dots);
Color bootCol = progress >= 1f ? COL_GREEN : COL_WHITE;
DrawText(frame, bootMsg, center.X, bar.Bottom + (compact ? 12f : 28f),
compact ? 0.28f : 0.52f, bootCol, TextAlignment.CENTER);
DrawText(frame, (int)(progress * 100f) + "%", center.X, bar.Bottom + (compact ? 28f : 58f),
compact ? 0.24f : 0.44f, COL_ACCENT, TextAlignment.CENTER);
}
}
private void DrawPBScreen()
{
if (_pbSurface == null) return;
var s = _pbSurface;
PrepSurface(s);
_drawSurface = s;
RectangleF vp = Viewport(s);
RectangleF panel = Inset(vp, 8f);
float cx = panel.X + panel.Width * 0.5f;
using (var frame = s.DrawFrame())
{
Fill(frame, vp, COL_BG);
Fill(frame, panel, COL_PANEL);
DrawBorder(frame, panel, COL_ACCENT, 3f);
DrawTextFit(frame, "RNB", cx, panel.Y + panel.Height * 0.12f,
panel.Width - 30f, 1.20f, 0.50f, COL_ACCENT, TextAlignment.CENTER);
DrawTextFit(frame, "Rev Nanobot", cx, panel.Y + panel.Height * 0.40f,
panel.Width - 30f, 0.58f, 0.28f, COL_WHITE, TextAlignment.CENTER);
DrawTextFit(frame, "Manager", cx, panel.Y + panel.Height * 0.56f,
panel.Width - 30f, 0.58f, 0.28f, COL_WHITE, TextAlignment.CENTER);
DrawTextFit(frame, "v" + RNB_VERSION, cx, panel.Y + panel.Height * 0.76f,
panel.Width - 30f, 0.44f, 0.24f, COL_DIM, TextAlignment.CENTER);
}
_drawSurface = null;
}
private void DrawProgressBar(MySpriteDrawFrame f, float x, float y, float w, float h, float pct, Color fillCol)
{
DrawRect(f, x + w/2f, y + h/2f, w, h,  COL_BAR_BG);
DrawRect(f, x + w/2f, y,        w, 1f, COL_DIM);
DrawRect(f, x + w/2f, y + h,    w, 1f, COL_DIM);
DrawRect(f, x,        y + h/2f, 1f, h, COL_DIM);
DrawRect(f, x + w,    y + h/2f, 1f, h, COL_DIM);
if (pct <= 0f) return;
if (pct > 1f)  pct = 1f;
float fw = (w - 2f) * pct;
DrawRect(f, x + 1f + fw/2f, y + h/2f, fw, h - 2f, fillCol);
}
private void DrawBootProgressBar(MySpriteDrawFrame f, float x, float y, float w, float h, float pct, Color fillCol)
{
if (pct < 0f) pct = 0f;
if (pct > 1f) pct = 1f;
DrawRect(f, x + w/2f, y + h/2f, w, h, COL_BAR_BG);
if (pct <= 0f) return;
float fw = w * pct;
DrawRect(f, x + fw/2f, y + h/2f, fw, h, fillCol);
}
private void DrawPanelFrame(MySpriteDrawFrame f, float x, float y, float w, float h, Color col)
{
float cut = 22f;
DrawRect(f, x + w/2f,     y,            w - cut * 2f, 2f, col);
DrawRect(f, x + w/2f,     y + h,        w - cut * 2f, 2f, col);
DrawRect(f, x,            y + h/2f,     2f, h - cut * 2f, col);
DrawRect(f, x + w,        y + h/2f,     2f, h - cut * 2f, col);
DrawRect(f, x + cut/2f,   y + cut/2f,   cut, 2f, col);
DrawRect(f, x + w-cut/2f, y + cut/2f,   cut, 2f, col);
DrawRect(f, x + cut/2f,   y + h-cut/2f, cut, 2f, col);
DrawRect(f, x + w-cut/2f, y + h-cut/2f, cut, 2f, col);
}
private RectangleF Viewport(IMyTextSurface s)
{
return new RectangleF((s.TextureSize - s.SurfaceSize) * 0.5f, s.SurfaceSize);
}
private RectangleF Inset(RectangleF r, float amount)
{
return new RectangleF(r.X + amount, r.Y + amount, r.Width - amount * 2f, r.Height - amount * 2f);
}
private static float FitTextScale(string text, float maxWidth, float maxHeight, float minScale, float maxScale)
{
if (string.IsNullOrEmpty(text)) return minScale;
float byWidth = maxWidth / (text.Length * 24f);
float byHeight = maxHeight / 28f;
float scale = byWidth < byHeight ? byWidth : byHeight;
if (scale < minScale) return minScale;
if (scale > maxScale) return maxScale;
return scale;
}
private void Fill(MySpriteDrawFrame f, RectangleF r, Color col)
{
DrawRect(f, r.X + r.Width * 0.5f, r.Y + r.Height * 0.5f, r.Width, r.Height, col);
}
private void DrawBorder(MySpriteDrawFrame f, RectangleF r, Color col, float t)
{
Fill(f, new RectangleF(r.X, r.Y, r.Width, t), col);
Fill(f, new RectangleF(r.X, r.Bottom - t, r.Width, t), col);
Fill(f, new RectangleF(r.X, r.Y, t, r.Height), col);
Fill(f, new RectangleF(r.Right - t, r.Y, t, r.Height), col);
}
private void DrawRect(MySpriteDrawFrame f, float cx, float cy, float w, float h, Color col)
{
f.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cx, cy), new Vector2(w, h), col));
}
private void DrawText(MySpriteDrawFrame f, string text, float x, float y, float scale, Color col, TextAlignment align)
{
var sp = new MySprite();
sp.Type            = SpriteType.TEXT;
sp.Data            = text;
sp.Position        = new Vector2(x, y);
sp.RotationOrScale = scale;
sp.Color           = col;
sp.Alignment       = align;
sp.FontId          = "Monospace";
f.Add(sp);
}
private void DrawTextFit(MySpriteDrawFrame f, string text, float x, float y, float maxWidth, float scale, float minScale, Color col, TextAlignment align)
{
DrawText(f, text, x, y, TextScaleForWidth(text, maxWidth, scale, minScale), col, align);
}
private float TextScaleForWidth(string text, float maxWidth, float scale, float minScale)
{
if (string.IsNullOrEmpty(text) || maxWidth <= 1f) return minScale;
if (_drawSurface != null)
{
_measureText.Clear();
_measureText.Append(text);
Vector2 px = _drawSurface.MeasureStringInPixels(_measureText, "Monospace", scale);
if (px.X > maxWidth && px.X > 0f)
{
scale = scale * maxWidth / px.X;
if (scale < minScale) scale = minScale;
}
return scale;
}
return FitTextScale(text, maxWidth, 999f, minScale, scale);
}
private void DrawRow(MySpriteDrawFrame f, float lx, float vx, float y, float fs, string label, string value, Color valCol)
{
DrawTextFit(f, label, lx, y, vx - lx - 80f, fs, 0.22f, COL_WHITE, TextAlignment.LEFT);
DrawTextFit(f, value, vx, y, 190f, fs, 0.22f, valCol, TextAlignment.RIGHT);
}
private void DrawStatusMetric(MySpriteDrawFrame f, float lx, float vx, float y, float fs, string label, string value, Color valCol)
{
DrawTextFit(f, label, lx, y, vx - lx - 54f, fs, 0.20f, COL_WHITE, TextAlignment.LEFT);
DrawTextFit(f, value, vx, y, 96f, fs, 0.20f, valCol, TextAlignment.RIGHT);
}
private void DrawOverviewRow(MySpriteDrawFrame f, float lx, float vx, float y, float fs, string label, string value, Color valCol)
{
DrawText(f, label, lx, y, fs, COL_WHITE, TextAlignment.LEFT);
DrawText(f, value, vx, y, fs, valCol, TextAlignment.RIGHT);
DrawRect(f, vx + 24f, y + 10f, 8f, 8f, valCol);
}
private static string SlimName(IMySlimBlock b)
{
if (b == null) return "-";
if (b.FatBlock != null)
{
var tb = b.FatBlock as IMyTerminalBlock;
return tb != null ? tb.CustomName : b.FatBlock.BlockDefinition.SubtypeName;
}
return b.BlockDefinition.SubtypeName;
}
private static string DefinitionName(MyDefinitionId def)
{
string s = def.SubtypeName;
if (!string.IsNullOrEmpty(s)) return s;
s = def.ToString();
int slash = s.LastIndexOf('/');
if (slash >= 0 && slash < s.Length - 1) return s.Substring(slash + 1);
return s;
}
private static string CleanBlockName(string name)
{
if (string.IsNullOrEmpty(name)) return "";
return name
.Replace(TAG_BASIC_ASSEMBLER, "")
.Replace(TAG_ASSEMBLER, "")
.Replace(TAG_NANOBOT, "")
.Replace(TAG_ALERT, "")
.Replace(TAG_CORNER_LCD, "")
.Replace(TAG_PROJECTOR, "")
.Trim();
}
private static IMySlimBlock WelderSlimValue(IMyShipWelder w, string prop)
{
try { return w.GetValue<IMySlimBlock>(prop); } catch { return null; }
}
private static long WelderLongValue(IMyShipWelder w, string prop)
{
try { return w.GetValue<long>(prop); } catch { return -1; }
}
private static long WelderLongValueAny(IMyShipWelder w, string a, string b, string c, string d)
{
long v;
if (!string.IsNullOrEmpty(a)) { v = WelderLongValue(w, a); if (v >= 0) return v; }
if (!string.IsNullOrEmpty(b)) { v = WelderLongValue(w, b); if (v >= 0) return v; }
if (!string.IsNullOrEmpty(c)) { v = WelderLongValue(w, c); if (v >= 0) return v; }
if (!string.IsNullOrEmpty(d)) { v = WelderLongValue(w, d); if (v >= 0) return v; }
return -1;
}
private string WelderModeSummary(IMyShipWelder w)
{
if (!w.IsFunctional) return "DAMAGED";
if (!w.Enabled)      return "OFFLINE";
string move = WelderMoveMode(w);
string work = WelderWorkMode(w);
string weld = WelderWeldMode(w);
string s = move;
if (!string.IsNullOrEmpty(work)) s = s + " " + work;
if (!string.IsNullOrEmpty(weld)) s = s + " " + weld;
return string.IsNullOrEmpty(s) ? "READY" : s;
}
private string WelderMoveMode(IMyShipWelder w)
{
long mode = WelderLongValue(w, "BuildAndRepair.Mode");
if (mode == 2) return "FLY";
if (mode == 1 || mode == 0) return "WALK";
if (mode >= 0) return "M" + mode;
return "?";
}
private string WelderWorkMode(IMyShipWelder w)
{
long mode = WelderLongValueAny(w,
"BuildAndRepair.WorkMode",
"BuildAndRepair.WeldGrindMode",
"BuildAndRepair.PriorityMode",
"BuildAndRepair.WeldingGrindingMode");
if (mode < 0) return "";
if (mode == 0) return "W>G";
if (mode == 1) return "G>W";
if (mode == 2) return "WELD";
if (mode == 3) return "GRIND";
return "W" + mode;
}
private string WelderWeldMode(IMyShipWelder w)
{
long mode = WelderLongValueAny(w,
"BuildAndRepair.WeldMode",
"BuildAndRepair.WeldingMode",
"BuildAndRepair.BuildMode",
"BuildAndRepair.WeldToMode");
if (mode < 0) return "";
if (mode == 0) return "FULL";
if (mode == 1) return "FUNC";
if (mode == 2) return "SKEL";
return "B" + mode;
}
private string WelderReason(IMyShipWelder w)
{
if (!w.IsFunctional) return "Needs repair";
if (!w.Enabled)      return "Block disabled";
if (_missing.Count > 0) return "Waiting parts";
if (_grindTargets  != null && _grindTargets.Count  > 0) return "Grind queue";
if (_weldTargets   != null && _weldTargets.Count   > 0) return "Weld queue";
if (_collectTargets != null && _collectTargets.Count > 0) return "Collecting";
return "No target";
}
private static string FormatTime(double sec)
{
int m = (int)(sec / 60);
int s = (int)(sec % 60);
return m + "m" + s.ToString().PadLeft(2, '0') + "s";
}
private static string TruncStr(string s, int max)
{
if (s == null) return "";
return s.Length <= max ? s : s.Substring(0, max - 1) + "~";
}
private static string PageLabel(PageKind p)
{
switch (p)
{
case PageKind.Status:     return "STATUS";
case PageKind.Missing:    return "MISSING";
case PageKind.Weld:       return "WELD";
case PageKind.Grind:      return "GRIND";
case PageKind.Welders:    return "WELDERS";
case PageKind.Assemblers: return "ASSEMBLERS";
case PageKind.Projectors: return "PROJECTORS";
default: return "";
}
}
