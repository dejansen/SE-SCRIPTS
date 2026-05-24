// =============================================================================
// RNB — Rev's Nanobot Bridge  v1.0.0
// Companion script for SKO Nanobot Build and Repair System (Maintained) v2.5.0+
// Author : Simba 'Davy' Jones — 505th Expeditionary Force
//
// BLOCK TAGS — rename blocks in-game, no config needed:
//
//   [RNBAssembler]          Advanced assembler — auto-queues all components
//   [RNBBasicAssembler]     Basic assembler    — auto-queues basic components only
//   [RNBAlert]              Light              — colour/blink reflects state
//   [RNBProjector]          Projector          — tracked on Projector page
//
//   LCD tags — each LCD shows one fixed page (no cycling):
//   [RNBStatus]             Status overview
//   [RNBMissing]            Missing components list
//   [RNBWeld]               Weld queue + progress bar
//   [RNBGrind]              Grind queue list
//   [RNBWelders]            Per-welder status detail
//   [RNBAssemblers]         Per-assembler status detail
//   [RNBProjectors]         Projector build progress
//
//   PB surface 0 shows boot sequence then live status — no tag needed.
//
// TOOLBAR ARGUMENTS:
//   online      Re-enable BaR welders, reset idle clock
//   offline     Force-disable BaR welders immediately
//   info-only   Skip assembler queuing this cycle
// =============================================================================

// ---------------------------------------------------------------------------
// USER SETTINGS
// ---------------------------------------------------------------------------
private const double IDLE_TIMEOUT_SECONDS  = 600.0;  // 10 min idle → auto-offline
private const bool   AUTO_PRODUCE_FIX_MODE = true;   // auto-fix assembler mode
private const double REINIT_INTERVAL       = 10.0;   // seconds between block rescans
private const double ASSEMBLER_QUEUE_INTERVAL = 0.5; // seconds between production queue checks

// ---------------------------------------------------------------------------
// COLOUR PALETTE
// ---------------------------------------------------------------------------
private readonly Color COL_BG        = new Color(  4,  8, 16);
private readonly Color COL_ACCENT    = new Color(  0,180,255);
private readonly Color COL_DIM       = new Color( 20, 60, 90);
private readonly Color COL_WHITE     = new Color(220,235,255);
private readonly Color COL_GREEN     = new Color(  0,220,100);
private readonly Color COL_AMBER     = new Color(255,160,  0);
private readonly Color COL_RED       = new Color(220, 30, 30);
private readonly Color COL_HEADER_BG = new Color(  0, 40, 80);
private readonly Color COL_BAR_BG    = new Color( 10, 25, 45);
private readonly Color COL_BAR_FILL  = new Color(  0,160,220);
private readonly Color COL_BAR_DONE  = new Color(  0,210, 90);

// ---------------------------------------------------------------------------
// LCD TAGS — one per page kind
// ---------------------------------------------------------------------------
private const string TAG_ASSEMBLER        = "[RNBAssembler]";
private const string TAG_BASIC_ASSEMBLER  = "[RNBBasicAssembler]";
private const string TAG_ALERT            = "[RNBAlert]";
private const string TAG_PROJECTOR        = "[RNBProjector]";
private const string TAG_LCD_STATUS     = "[RNBStatus]";
private const string TAG_LCD_MISSING    = "[RNBMissing]";
private const string TAG_LCD_WELD       = "[RNBWeld]";
private const string TAG_LCD_GRIND      = "[RNBGrind]";
private const string TAG_LCD_WELDERS    = "[RNBWelders]";
private const string TAG_LCD_ASSEMBLERS = "[RNBAssemblers]";
private const string TAG_LCD_PROJECTORS = "[RNBProjectors]";

// ---------------------------------------------------------------------------
// ENUMS / DATA
// ---------------------------------------------------------------------------
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
    public int          Total     = 0;
    public int          Remaining = 0;
    public float        Progress  = 0f;
}

// ---------------------------------------------------------------------------
// BaR HANDLER
// ---------------------------------------------------------------------------
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
    { get { return Welders.Count > 0 && Welders[0].GetValueBool("BuildAndRepair.AllowBuild"); } }

    public IMySlimBlock CurrentTarget
    { get { return GetValue<IMySlimBlock>("BuildAndRepair.CurrentTarget"); } }

    public IMySlimBlock CurrentGrindTarget
    { get { return GetValue<IMySlimBlock>("BuildAndRepair.CurrentGrindTarget"); } }

    public List<IMySlimBlock> PossibleTargets()
    { return GetValue<List<IMySlimBlock>>("BuildAndRepair.PossibleTargets"); }

    public List<IMySlimBlock> PossibleGrindTargets()
    { return GetValue<List<IMySlimBlock>>("BuildAndRepair.PossibleGrindTargets"); }

    public List<IMyEntity> PossibleCollectTargets()
    { return GetValue<List<IMyEntity>>("BuildAndRepair.PossibleCollectTargets"); }

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
        return EnsureQueuedFn(ids, def, amt);
    }
}

// ---------------------------------------------------------------------------
// SCRIPT FIELDS
// ---------------------------------------------------------------------------
private BaRHandler             _welders      = new BaRHandler();
private List<long>             _assemblerIds = new List<long>();
private List<IMyAssembler>     _assemblers   = new List<IMyAssembler>();
private List<DisplayEntry>     _displays     = new List<DisplayEntry>();
private List<IMyLightingBlock> _alertLights  = new List<IMyLightingBlock>();
private List<ProjectorInfo>    _projectors   = new List<ProjectorInfo>();

private double   _elapsed          = 0.0;
private double   _nextReinit       = 0.0;
private double   _nextAssembler    = 0.0;
private double   _lastActivityTime = 0.0;
private bool     _isOffline        = false;
private bool     _forcedOffline    = false;
private RNBState _state            = RNBState.Idle;
private int      _drawTick         = 0;

// BaR data snapshot. Mod API reads are kept to one place per tick.
private List<IMySlimBlock> _weldTargets = null;
private List<IMySlimBlock> _grindTargets = null;
private List<IMyEntity> _collectTargets = null;
private Dictionary<MyDefinitionId, int> _missing = new Dictionary<MyDefinitionId, int>();
private IMySlimBlock _currentTarget = null;
private IMySlimBlock _currentGrindTarget = null;

// Weld progress latch
private int _weldPeak = 0;
private int _weldPrev = 0;

// Boot sequence
private enum BootStage { Booting, Ready }
private BootStage _bootStage    = BootStage.Booting;
private double    _bootElapsed  = 0.0;
private const double BOOT_DURATION = 1.0;   // seconds for boot animation
private float     _bootProgress = 0f;
private int       _bootDotCount = 0;
private double    _bootDotTimer = 0.0;

// PB own LCD (surface 0)
private IMyTextSurface _pbSurface = null;

// Scan buffers
private readonly List<IMyShipWelder>    _wBuf = new List<IMyShipWelder>();
private readonly List<IMyTerminalBlock> _tBuf = new List<IMyTerminalBlock>();

// Assembler ID lists split by capability
// Basic assemblers only handle a limited blueprint set.
// Advanced assemblers handle everything.
private List<long> _basicAssemblerIds    = new List<long>();
private List<long> _advancedAssemblerIds = new List<long>();

// Components a basic assembler CAN produce (vanilla SE subtypes)
private readonly string[] BASIC_COMPONENTS = new string[] {
    "SteelPlate", "InteriorPlate", "Construction", "SmallTube",
    "LargeTube", "Motor", "Display", "BulletproofGlass", "Girder"
};

// ---------------------------------------------------------------------------
// ENTRY POINTS
// ---------------------------------------------------------------------------
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    // Grab the PB's own LCD surface (surface 0 on large PB)
    var pb = Me as IMyTextSurfaceProvider;
    if (pb != null && pb.SurfaceCount > 0)
    {
        _pbSurface = pb.GetSurface(0);
        PrepSurface(_pbSurface);
    }

    Initialise();
    DrawBootScreen(0f); // show boot immediately on first compile
    DrawBootDisplays(0f);
}

public void Save() { }

public void Main(string argument, UpdateType updateSource)
{
    _elapsed     += Runtime.TimeSinceLastRun.TotalSeconds;
    _bootElapsed += Runtime.TimeSinceLastRun.TotalSeconds;
    bool pbBooting = _bootStage == BootStage.Booting;

    // Boot sequence — run for BOOT_DURATION seconds, then switch to live
    if (pbBooting)
    {
        _bootProgress = (float)(_bootElapsed / BOOT_DURATION);
        if (_bootProgress > 1f) _bootProgress = 1f;

        // Animate dots every 0.4 s
        _bootDotTimer += Runtime.TimeSinceLastRun.TotalSeconds;
        if (_bootDotTimer >= 0.4) { _bootDotTimer = 0; _bootDotCount = (_bootDotCount + 1) % 4; }

        DrawBootScreen(_bootProgress);
        DrawBootDisplays(_bootProgress);

        if (_bootElapsed >= BOOT_DURATION)
        {
            _bootStage = BootStage.Ready;
            pbBooting = false;
        }
        else
        {
            return;
        }
    }

    if (!string.IsNullOrEmpty(argument))
    {
        string arg = argument.Trim().ToLower();
        if (arg == "online")  { BringOnline(); }
        if (arg == "offline") { _forcedOffline = true; BringOffline("Forced offline."); }
        if (arg == "reinit")  { Initialise(); _nextReinit = _elapsed + REINIT_INTERVAL; Echo("REINIT complete."); }
    }

    if (_elapsed >= _nextReinit)
    {
        Initialise();
        _nextReinit = _elapsed + REINIT_INTERVAL;
    }

    bool infoOnly = argument != null && argument.Trim().ToLower() == "info-only";
    RefreshBaRData();

    // ── State update ────────────────────────────────────────────────────────
    if (!_isOffline)
    {
        int wtc      = _weldTargets != null ? _weldTargets.Count : 0;
        bool anyWork = wtc > 0
            || (_grindTargets != null && _grindTargets.Count > 0)
            || (_collectTargets != null && _collectTargets.Count > 0);

        if (anyWork) _lastActivityTime = _elapsed;

        // Weld peak latch
        if (_weldPrev == 0 && wtc > 0) _weldPeak = wtc;
        if (wtc > _weldPeak)           _weldPeak = wtc;
        _weldPrev = wtc;

        if (_missing.Count > 0)
            _state = RNBState.Missing;
        else if (anyWork)
            _state = RNBState.Working;
        else if (!_forcedOffline && (_elapsed - _lastActivityTime) >= IDLE_TIMEOUT_SECONDS)
            BringOffline("Idle timeout.");
        else
            _state = RNBState.Idle;
    }

    RefreshProjectors();

    if (!infoOnly && _elapsed >= _nextAssembler)
    {
        _nextAssembler = _elapsed + ASSEMBLER_QUEUE_INTERVAL;
        CheckAssemblerQueues();
    }

    UpdateAlertLights();
    DrawDisplays();
    if (!pbBooting) DrawPBScreen();
    _drawTick = (_drawTick + 1) % 1000;
}

// ---------------------------------------------------------------------------
// INITIALISE — tag-based block discovery
// ---------------------------------------------------------------------------
private void Initialise()
{
    _welders.Welders.Clear();
    _welders.ResetProductionCache();
    _assemblerIds.Clear();
    _assemblers.Clear();
    _basicAssemblerIds.Clear();
    _advancedAssemblerIds.Clear();
    _displays.Clear();
    _alertLights.Clear();
    _projectors.Clear();

    _tBuf.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(_tBuf);

    for (int i = 0; i < _tBuf.Count; i++)
    {
        var tb   = _tBuf[i];
        if (!tb.IsSameConstructAs(Me)) continue;
        string n = tb.CustomName;

        // Assemblers — explicit basic tag overrides subtype detection
        bool hasBasicTag    = n.Contains(TAG_BASIC_ASSEMBLER);
        bool hasAdvancedTag = !hasBasicTag && n.Contains(TAG_ASSEMBLER);

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
                    // Explicitly tagged as basic
                    isBasic = true;
                }
                else
                {
                    // Infer from block definition subtype
                    string defSubtype = asm.BlockDefinition.SubtypeName;
                    isBasic = defSubtype.IndexOf("Basic", System.StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (isBasic)
                {
                    _basicAssemblerIds.Add(asm.EntityId);
                }
                else
                {
                    _advancedAssemblerIds.Add(asm.EntityId);
                }
            }
        }

        // Alert lights
        if (n.Contains(TAG_ALERT))
        {
            var lt = tb as IMyLightingBlock;
            if (lt != null) _alertLights.Add(lt);
        }

        // Projectors
        if (n.Contains(TAG_PROJECTOR))
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

        // LCDs — each tag maps directly to one PageKind
        PageKind lcdPage;
        if (TagToPage(n, out lcdPage))
        {
            var surf = tb as IMyTextSurface;
            if (surf != null)
            {
                bool dup = false;
                for (int d = 0; d < _displays.Count; d++)
                    if (_displays[d].Surface == surf) { dup = true; break; }
                if (!dup)
                {
                    PrepSurface(surf);
                    _displays.Add(new DisplayEntry { Surface = surf, Page = lcdPage });
                }
            }
        }
    }

    // BaR welder auto-detect
    _wBuf.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(_wBuf);
    for (int i = 0; i < _wBuf.Count; i++)
    {
        if (!_wBuf[i].IsSameConstructAs(Me)) continue;
        if (BaRHandler.IsBaRWelder(_wBuf[i])) _welders.Welders.Add(_wBuf[i]);
    }

    string msg = "RNB v1.0 | Welders:" + _welders.Count
        + " Asm:" + _assemblerIds.Count
        + " (B:" + _basicAssemblerIds.Count + " A:" + _advancedAssemblerIds.Count + ")"
        + " LCD:" + _displays.Count
        + " Proj:" + _projectors.Count;
    Echo(msg);

    // Extra: confirm each assembler found by name
    if (_assemblerIds.Count > 0)
    {
        for (int i = 0; i < _assemblers.Count; i++)
        {
            var atb = _assemblers[i] as IMyTerminalBlock;
            string aname = atb != null ? atb.CustomName : "?";
            bool enabled = _assemblers[i].Enabled;
            bool working = _assemblers[i].IsWorking;
            string amode = _assemblers[i].Mode == MyAssemblerMode.Disassembly ? "DISASM" : "ASSEM";
            Echo("  ASM: " + aname + " | " + amode + " | en=" + enabled + " wrk=" + working);
        }
    }
}

// Maps a block name to a PageKind via LCD tags.
// Returns false if the block has no LCD tag.
private static bool TagToPage(string name, out PageKind page)
{
    // Check longest/most-specific tags first to avoid substring false-matches
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

private void PrepSurface(IMyTextSurface s)
{
    s.ContentType           = ContentType.SCRIPT;
    s.ScriptBackgroundColor = COL_BG;
    s.BackgroundColor       = COL_BG;
    s.Script                = "";
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

// ---------------------------------------------------------------------------
// PROJECTOR REFRESH
// ---------------------------------------------------------------------------
private void RefreshProjectors()
{
    for (int i = 0; i < _projectors.Count; i++)
    {
        var info = _projectors[i];
        var p    = info.Block;
        if (p == null || !p.IsFunctional)
        { info.Total = 0; info.Remaining = 0; info.Progress = 0f; continue; }
        info.Total     = p.TotalBlocks;
        info.Remaining = p.RemainingBlocks;
        info.Progress  = info.Total > 0
            ? 1f - (float)info.Remaining / (float)info.Total
            : 0f;
    }
}

// ---------------------------------------------------------------------------
// ONLINE / OFFLINE
// ---------------------------------------------------------------------------
private void BringOffline(string reason)
{
    _isOffline = true;
    _state     = RNBState.Offline;
    _welders.SetEnabled(false);
    Echo("OFFLINE: " + reason);
}

private void BringOnline()
{
    _isOffline        = false;
    _forcedOffline    = false;
    _lastActivityTime = _elapsed;
    _state            = RNBState.Idle;
    _welders.SetEnabled(true);
    Echo("ONLINE.");
}

// ---------------------------------------------------------------------------
// ASSEMBLER QUEUE
// ---------------------------------------------------------------------------
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

    if (_missing.Count == 0) return;  // nothing needed - silent

    if (AUTO_PRODUCE_FIX_MODE) EnsureAssemblyMode();

    foreach (var kv in _missing)
    {
        if (kv.Value <= 0) continue;

        string subtype = kv.Key.SubtypeName;
        bool basicCanMake = IsBasicComponent(subtype);

        // Route to the right assembler pool:
        //   basic-craftable items  → try basic first, fall back to advanced
        //   advanced-only items    → advanced assemblers only
        List<long> targets;
        if (basicCanMake && _basicAssemblerIds.Count > 0)
            targets = _basicAssemblerIds;
        else if (_advancedAssemblerIds.Count > 0)
            targets = _advancedAssemblerIds;
        else
            targets = _assemblerIds; // fallback: send to all

        int result = _welders.EnsureQueued(targets, kv.Key, kv.Value);
        if (result < 0)
            Echo("QUEUE FAIL: " + subtype + " code=" + result);
    }
}

// Returns true if a basic assembler can produce this component subtype.
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
            Echo("Auto-mode: '" + (tb != null ? tb.CustomName : "asm") + "' → Assembly");
        }
    }
}

// ---------------------------------------------------------------------------
// ALERT LIGHTS
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// DISPLAY DRIVER
// ---------------------------------------------------------------------------
private void DrawDisplays()
{
    for (int i = 0; i < _displays.Count; i++)
        DrawPage(_displays[i]);
}

// ---------------------------------------------------------------------------
// PAGE FRAME — header + footer, content delegated
// ---------------------------------------------------------------------------
private void DrawPage(DisplayEntry entry)
{
    var s  = entry.Surface;
    var vp = new RectangleF((s.TextureSize - s.SurfaceSize) / 2f, s.SurfaceSize);
    float W  = vp.Width;
    float H  = vp.Height;
    float ox = vp.X;
    float oy = vp.Y;

    using (var frame = s.DrawFrame())
    {
        // Background
        DrawRect(frame, ox + W/2f, oy + H/2f, W, H, COL_BG);

        // Side rails
        DrawRect(frame, ox + 3f,     oy + H/2f, 3f, H, COL_DIM);
        DrawRect(frame, ox + W - 3f, oy + H/2f, 3f, H, COL_DIM);

        // Header — row1: title | row2: state + welder count
        float row1H   = 28f;
        float row2H   = 22f;
        float headerH = row1H + row2H + 4f;
        DrawRect(frame, ox + W/2f, oy + headerH/2f, W, headerH, COL_HEADER_BG);

        DrawText(frame, "RNB v1.0  |  Rev's Nanobot Bridge",
            ox + 12f, oy + 4f, 0.52f, COL_ACCENT, TextAlignment.LEFT);

        string stateStr; Color stateCol;
        switch (_state)
        {
            case RNBState.Working: stateStr = "WORKING"; stateCol = COL_GREEN; break;
            case RNBState.Missing: stateStr = "MISSING"; stateCol = COL_RED;   break;
            case RNBState.Offline: stateStr = "OFFLINE"; stateCol = COL_AMBER; break;
            default:               stateStr = "IDLE";    stateCol = COL_WHITE; break;
        }
        float row2Y = oy + row1H + 4f;
        DrawText(frame, "Welders: " + _welders.CountWorking + "/" + _welders.Count,
            ox + 12f, row2Y, 0.45f, COL_DIM, TextAlignment.LEFT);
        DrawText(frame, "[ " + stateStr + " ]",
            ox + W - 12f, row2Y, 0.45f, stateCol, TextAlignment.RIGHT);
        DrawText(frame, "LIVE " + _drawTick.ToString().PadLeft(3, '0'),
            ox + W/2f, row2Y, 0.38f, COL_ACCENT, TextAlignment.CENTER);

        DrawRect(frame, ox + W/2f, oy + headerH + 1f, W, 2f, COL_ACCENT);

        // Content
        float cTop = oy + headerH + 8f;
        float cH   = H - headerH - 28f;

        switch (entry.Page)
        {
            case PageKind.Status:     DrawStatusPage    (frame, ox, cTop, W, cH); break;
            case PageKind.Missing:    DrawMissingPage   (frame, ox, cTop, W, cH); break;
            case PageKind.Weld:       DrawListPage      (frame, ox, cTop, W, cH, "WELD QUEUE",  _weldTargets);      break;
            case PageKind.Grind:      DrawListPage      (frame, ox, cTop, W, cH, "GRIND QUEUE", _grindTargets);     break;
            case PageKind.Welders:    DrawWeldersPage   (frame, ox, cTop, W, cH); break;
            case PageKind.Assemblers: DrawAssemblersPage(frame, ox, cTop, W, cH); break;
            case PageKind.Projectors: DrawProjectorsPage(frame, ox, cTop, W, cH); break;
        }

        // Footer
        float footerY = oy + H - 20f;
        DrawRect(frame, ox + W/2f, footerY - 4f, W, 1f, COL_DIM);
        DrawText(frame, PageLabel(entry.Page), ox + 12f, footerY, 0.4f, COL_DIM, TextAlignment.LEFT);
        double idleSec = _elapsed - _lastActivityTime;
        string idleStr = _isOffline ? "OFFLINE" : ("IDLE " + FormatTime(idleSec));
        DrawText(frame, idleStr, ox + W - 12f, footerY, 0.4f,
            _isOffline ? COL_AMBER : COL_DIM, TextAlignment.RIGHT);
    }
}

// ---------------------------------------------------------------------------
// PAGE: STATUS
// ---------------------------------------------------------------------------
private void DrawStatusPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
    float rowH = H / 9f;
    float y    = top + 4f;
    float lx   = ox + 14f;
    float vx   = ox + W - 14f;
    float fs   = 0.52f;

    int wtc = _weldTargets != null ? _weldTargets.Count : 0;
    int gtc = _grindTargets != null ? _grindTargets.Count : 0;
    int ctc = _collectTargets != null ? _collectTargets.Count : 0;

    DrawRow(frame, lx, vx, y, fs, "BaR Welders",
        _welders.CountWorking + " / " + _welders.Count,
        _welders.CountWorking > 0 ? COL_GREEN : COL_AMBER); y += rowH;
    DrawRow(frame, lx, vx, y, fs, "Assemblers",
        _assemblerIds.Count.ToString(), COL_WHITE); y += rowH;
    DrawRow(frame, lx, vx, y, fs, "Welding now",
        SlimName(_currentTarget),
        _currentTarget != null ? COL_GREEN : COL_DIM); y += rowH;
    DrawRow(frame, lx, vx, y, fs, "Grinding now",
        SlimName(_currentGrindTarget),
        _currentGrindTarget != null ? COL_AMBER : COL_DIM); y += rowH;
    DrawRow(frame, lx, vx, y, fs, "Weld queue",  wtc.ToString(), wtc > 0 ? COL_WHITE : COL_DIM); y += rowH;
    DrawRow(frame, lx, vx, y, fs, "Grind queue", gtc.ToString(), gtc > 0 ? COL_WHITE : COL_DIM); y += rowH;
    DrawRow(frame, lx, vx, y, fs, "Floating",    ctc.ToString(), ctc > 0 ? COL_WHITE : COL_DIM); y += rowH;
    DrawRow(frame, lx, vx, y, fs, "Missing types",
        _missing.Count.ToString(), _missing.Count > 0 ? COL_RED : COL_GREEN); y += rowH;
    DrawRow(frame, lx, vx, y, fs, "Projectors",
        _projectors.Count.ToString(), _projectors.Count > 0 ? COL_ACCENT : COL_DIM);
}

// ---------------------------------------------------------------------------
// PAGE: MISSING ITEMS
// ---------------------------------------------------------------------------
private void DrawMissingPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
    float y   = top;
    float lx  = ox + 14f;
    float vx  = ox + W - 14f;
    float rowH = 34f;

    DrawText(frame, "MISSING PARTS", lx, y, 0.72f, COL_RED, TextAlignment.LEFT);
    DrawText(frame, _missing.Count + " TYPES", vx, y, 0.56f,
        _missing.Count > 0 ? COL_RED : COL_GREEN, TextAlignment.RIGHT);
    y += 32f;
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

    int shown   = 0;
    foreach (var kv in _missing)
    {
        if (shown >= maxRows - 1) { DrawText(frame, "...", lx, y, 0.42f, COL_DIM, TextAlignment.LEFT); break; }
        Color c = shown % 2 == 0 ? new Color(255,120,120) : new Color(220,80,80);
        DrawRect(frame, ox + W/2f, y + 13f, W - 24f, 24f, shown % 2 == 0 ? new Color(20,35,55) : new Color(12,26,44));
        DrawText(frame, "x" + kv.Value, lx + 4f, y + 1f, 0.62f, c, TextAlignment.LEFT);
        DrawText(frame, TruncStr(DefinitionName(kv.Key), 22), lx + 92f, y + 1f, 0.62f, c, TextAlignment.LEFT);
        y += rowH; shown++;
    }
}

// ---------------------------------------------------------------------------
// PAGE: WELD / GRIND LIST
// ---------------------------------------------------------------------------
private void DrawListPage(MySpriteDrawFrame frame, float ox, float top, float W, float H,
    string title, List<IMySlimBlock> list)
{
    float y    = top;
    float lx   = ox + 14f;
    float rowH = 22f;
    int   count = list != null ? list.Count : 0;

    DrawText(frame, title, lx, y, 0.6f, COL_ACCENT, TextAlignment.LEFT);
    DrawText(frame, count.ToString(), ox + W - 14f, y, 0.6f,
        count > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
    y += 28f;
    DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;

    // Weld queue progress bar
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
        DrawText(frame, TruncStr(SlimName(list[i]), 28), lx, y, 0.5f, c, TextAlignment.LEFT);
        y += rowH;
    }
    if (count > shown)
        DrawText(frame, "+ " + (count - shown) + " more", lx, y, 0.42f, COL_DIM, TextAlignment.LEFT);
}

// ---------------------------------------------------------------------------
// PAGE: WELDERS DETAIL
// ---------------------------------------------------------------------------
private void DrawWeldersPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
    float y    = top;
    float lx   = ox + 14f;
    float vx   = ox + W - 14f;
    float rowH = 20f;

    DrawText(frame, "WELDER DETAILS", lx, y, 0.6f, COL_ACCENT, TextAlignment.LEFT);
    DrawText(frame, _welders.Count.ToString(), vx, y, 0.6f,
        _welders.Count > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
    y += 28f;
    DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;

    if (_welders.Count == 0)
    { DrawText(frame, "No BaR welders found", ox + W/2f, top + H/2f, 0.5f, COL_DIM, TextAlignment.CENTER); return; }

    for (int i = 0; i < _welders.Welders.Count; i++)
    {
        if (y + rowH * 2f > top + H) break;

        var w  = _welders.Welders[i];
        var tb = w as IMyTerminalBlock;
        string wName = tb != null ? TruncStr(tb.CustomName, 20) : "Welder";

        // Status colour
        Color nameCol;
        string statusStr;
        if (!w.IsFunctional)       { nameCol = COL_RED;   statusStr = "DAMAGED";  }
        else if (!w.Enabled)       { nameCol = COL_DIM;   statusStr = "OFF";      }
        else if (w.IsWorking)      { nameCol = COL_GREEN; statusStr = "WORKING";  }
        else                       { nameCol = COL_AMBER; statusStr = "STANDBY";  }

        // Row 1 — name + status
        DrawText(frame, wName,      lx, y, 0.48f, nameCol,  TextAlignment.LEFT);
        DrawText(frame, statusStr,  vx, y, 0.45f, nameCol,  TextAlignment.RIGHT);
        y += rowH;

        // Row 2 — BaR type + functional state
        bool isBar = BaRHandler.IsBaRWelder(w);
        string barStr  = isBar ? "BaR" : "STD";
        Color  barCol  = isBar ? COL_ACCENT : COL_DIM;
        string funcStr = w.IsFunctional ? "OK" : "DAMAGED";

        DrawText(frame, barStr + "  " + funcStr, lx, y, 0.42f, barCol, TextAlignment.LEFT);
        // Show if currently locked onto a target
        bool hasTarget = (_currentTarget != null);
        DrawText(frame, hasTarget ? "ON TARGET" : "", vx, y, 0.42f, COL_GREEN, TextAlignment.RIGHT);
        y += rowH + 3f;
    }
}

// ---------------------------------------------------------------------------
// PAGE: ASSEMBLERS DETAIL
// ---------------------------------------------------------------------------
private void DrawAssemblersPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
    float y    = top;
    float lx   = ox + 14f;
    float vx   = ox + W - 14f;
    float rowH = 20f;

    DrawText(frame, "ASSEMBLER DETAILS", lx, y, 0.6f, COL_ACCENT, TextAlignment.LEFT);
    DrawText(frame, _assemblerIds.Count.ToString(), vx, y, 0.6f,
        _assemblerIds.Count > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
    y += 28f;
    DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;

    if (_assemblerIds.Count == 0)
    { DrawText(frame, "No [RNBAssembler] tagged", ox + W/2f, top + H/2f, 0.45f, COL_DIM, TextAlignment.CENTER); return; }

    int shown = 0;
    for (int i = 0; i < _assemblers.Count; i++)
    {
        var asm = _assemblers[i];
        if (y + rowH * 3f > top + H)              break;

        var tb = asm as IMyTerminalBlock;
        string asmName = tb != null
            ? TruncStr(tb.CustomName.Replace(TAG_BASIC_ASSEMBLER, "").Replace(TAG_ASSEMBLER, "").Trim(), 20)
            : "Assembler";

        // Row 1 — name + enabled state
        Color nameCol;
        string stStr;
        if (!asm.IsFunctional)  { nameCol = COL_RED;   stStr = "DAMAGED"; }
        else if (!asm.Enabled)  { nameCol = COL_DIM;   stStr = "OFF";     }
        else if (asm.IsWorking) { nameCol = COL_GREEN; stStr = "WORKING"; }
        else                    { nameCol = COL_AMBER; stStr = "STANDBY"; }

        DrawText(frame, asmName, lx, y, 0.48f, nameCol, TextAlignment.LEFT);
        DrawText(frame, stStr,   vx, y, 0.45f, nameCol, TextAlignment.RIGHT);
        y += rowH;

        // Row 2 — mode + cooperative
        string modeStr = asm.Mode == MyAssemblerMode.Disassembly ? "DISASSEMBLY" : "ASSEMBLY";
        Color  modeCol = asm.Mode == MyAssemblerMode.Disassembly ? COL_AMBER : COL_WHITE;
        string coopStr = asm.CooperativeMode ? "COOP" : "";
        DrawText(frame, modeStr, lx, y, 0.42f, modeCol, TextAlignment.LEFT);
        if (asm.CooperativeMode)
            DrawText(frame, coopStr, vx, y, 0.42f, COL_ACCENT, TextAlignment.RIGHT);
        y += rowH;

        // Row 3 — output inventory item count + repeat flag
        int outItems = asm.OutputInventory != null ? asm.OutputInventory.ItemCount : 0;
        string outStr    = "Output: " + outItems + " item" + (outItems != 1 ? "s" : "");
        string repeatStr = asm.Repeating ? "REPEAT" : "";
        DrawText(frame, outStr,    lx, y, 0.42f, COL_DIM,   TextAlignment.LEFT);
        DrawText(frame, repeatStr, vx, y, 0.42f, COL_ACCENT, TextAlignment.RIGHT);
        y += rowH + 3f;
        shown++;
    }

    if (shown == 0)
        DrawText(frame, "No tagged assemblers on grid", ox + W/2f, top + H/2f,
            0.45f, COL_DIM, TextAlignment.CENTER);
}

// ---------------------------------------------------------------------------
// PAGE: PROJECTORS
// ---------------------------------------------------------------------------
private void DrawProjectorsPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
{
    float y   = top;
    float lx  = ox + 14f;
    float vx  = ox + W - 14f;
    float bw  = W - 28f;

    DrawText(frame, "PROJECTORS", lx, y, 0.6f, COL_ACCENT, TextAlignment.LEFT);
    DrawText(frame, _projectors.Count.ToString(), vx, y, 0.6f,
        _projectors.Count > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
    y += 28f;
    DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;

    if (_projectors.Count == 0)
    { DrawText(frame, "No [RNBProjector] tagged", ox + W/2f, top + H/2f, 0.5f, COL_DIM, TextAlignment.CENTER); return; }

    float slotH = (top + H - y) / _projectors.Count;
    slotH = slotH > 72f ? 72f : slotH;

    for (int i = 0; i < _projectors.Count; i++)
    {
        if (y + 42f > top + H)
        {
            DrawText(frame, "+ " + (_projectors.Count - i) + " more", lx, y, 0.42f, COL_DIM, TextAlignment.LEFT);
            break;
        }

        var info   = _projectors[i];
        bool active = info.Total > 0 && info.Remaining > 0;

        // Name + state
        DrawText(frame, TruncStr(info.Name, 22), lx, y, 0.5f, COL_WHITE, TextAlignment.LEFT);
        DrawText(frame, active ? "BUILDING" : "IDLE",
            vx, y, 0.45f, active ? COL_GREEN : COL_DIM, TextAlignment.RIGHT);
        y += 22f;

        if (active)
        {
            // Progress bar
            DrawProgressBar(frame, ox + 14f, y, bw, 14f, info.Progress,
                info.Progress >= 1f ? COL_BAR_DONE : COL_BAR_FILL);
            y += 20f;

            int built = info.Total - info.Remaining;
            DrawText(frame, built + " / " + info.Total + " blocks",
                lx, y, 0.42f, COL_DIM, TextAlignment.LEFT);
            DrawText(frame, (int)(info.Progress * 100f) + "%",
                vx, y, 0.45f, COL_ACCENT, TextAlignment.RIGHT);
            y += 20f;
        }
        else
        {
            DrawText(frame, "No blueprint / complete", lx, y, 0.42f, COL_DIM, TextAlignment.LEFT);
            y += 20f;
        }
        y += 6f;
    }
}


// ---------------------------------------------------------------------------
// BOOT SCREEN — drawn on PB surface during startup
// ---------------------------------------------------------------------------
private void DrawBootScreen(float progress)
{
    if (_pbSurface == null) return;

    DrawBootSurface(_pbSurface, progress, true);
}

private void DrawBootDisplays(float progress)
{
    for (int i = 0; i < _displays.Count; i++)
        DrawBootSurface(_displays[i].Surface, progress, false);
}

private void DrawBootSurface(IMyTextSurface s, float progress, bool compact)
{
    var vp = new RectangleF((s.TextureSize - s.SurfaceSize) / 2f, s.SurfaceSize);
    float W  = vp.Width;
    float H  = vp.Height;
    float ox = vp.X;
    float oy = vp.Y;

    using (var frame = s.DrawFrame())
    {
        // Background
        DrawRect(frame, ox + W/2f, oy + H/2f, W, H, COL_BG);

        // Top accent bar
        DrawRect(frame, ox + W/2f, oy + 4f, W, 3f, COL_ACCENT);

        // Logo / title block — centred
        float midY = compact ? oy + H * 0.28f : oy + H * 0.24f;
        DrawText(frame, "RNB", ox + W/2f, midY, compact ? 1.8f : 1.35f, COL_ACCENT, TextAlignment.CENTER);
        DrawText(frame, "Rev's Nanobot Bridge",
            ox + W/2f, midY + (compact ? 52f : 42f), compact ? 0.48f : 0.52f, COL_WHITE, TextAlignment.CENTER);
        DrawText(frame, "v1.0  |  505th Expeditionary Force",
            ox + W/2f, midY + (compact ? 76f : 68f), 0.38f, COL_DIM, TextAlignment.CENTER);

        // Divider
        DrawRect(frame, ox + W/2f, oy + H * 0.55f, W * 0.7f, 1f, COL_DIM);

        // Boot progress bar
        float barW  = W * 0.7f;
        float barX  = ox + (W - barW) / 2f;
        float barY  = oy + H * 0.6f;
        DrawProgressBar(frame, barX, barY, barW, 16f, progress,
            progress >= 1f ? COL_GREEN : COL_BAR_FILL);

        // Status text with animated dots
        string dots = new string('.', _bootDotCount);
        string bootMsg = progress >= 1f ? "READY" : ("INITIALISING" + dots);
        Color  bootCol = progress >= 1f ? COL_GREEN : COL_WHITE;
        DrawText(frame, bootMsg, ox + W/2f, barY + 24f, 0.48f, bootCol, TextAlignment.CENTER);
        DrawText(frame, (int)(progress * 100f) + "%", ox + W/2f, barY + 48f, 0.42f, COL_ACCENT, TextAlignment.CENTER);

        // Bottom accent bar
        DrawRect(frame, ox + W/2f, oy + H - 4f, W, 3f, COL_ACCENT);
    }
}

// ---------------------------------------------------------------------------
// PB LIVE SCREEN — compact status on PB surface 0 after boot
// ---------------------------------------------------------------------------
private void DrawPBScreen()
{
    if (_pbSurface == null) return;

    var s  = _pbSurface;
    var vp = new RectangleF((s.TextureSize - s.SurfaceSize) / 2f, s.SurfaceSize);
    float W  = vp.Width;
    float H  = vp.Height;
    float ox = vp.X;
    float oy = vp.Y;

    using (var frame = s.DrawFrame())
    {
        // Background
        DrawRect(frame, ox + W/2f, oy + H/2f, W, H, COL_BG);

        // Header bar
        DrawRect(frame, ox + W/2f, oy + 18f, W, 32f, COL_HEADER_BG);
        DrawText(frame, "RNB v1.0", ox + 10f, oy + 4f, 0.55f, COL_ACCENT, TextAlignment.LEFT);

        // State badge
        string stStr; Color stCol;
        switch (_state)
        {
            case RNBState.Working: stStr = "WORKING"; stCol = COL_GREEN;  break;
            case RNBState.Missing: stStr = "MISSING"; stCol = COL_RED;    break;
            case RNBState.Offline: stStr = "OFFLINE"; stCol = COL_AMBER;  break;
            default:               stStr = "IDLE";    stCol = COL_WHITE;  break;
        }
        DrawText(frame, "[ " + stStr + " ]",
            ox + W - 10f, oy + 4f, 0.55f, stCol, TextAlignment.RIGHT);

        // Accent line
        DrawRect(frame, ox + W/2f, oy + 34f, W, 2f, COL_ACCENT);

        // Stats grid — compact two-column
        float y  = oy + 42f;
        float lx = ox + 10f;
        float vx = ox + W - 10f;
        float fs = 0.45f;
        float rh = 18f;

        int wtc = _weldTargets != null ? _weldTargets.Count : 0;
        int gtc = _grindTargets != null ? _grindTargets.Count : 0;

        DrawRow(frame, lx, vx, y, fs, "Welders",
            _welders.CountWorking + "/" + _welders.Count,
            _welders.CountWorking > 0 ? COL_GREEN : COL_AMBER); y += rh;
        DrawRow(frame, lx, vx, y, fs, "Assemblers",
            "B:" + _basicAssemblerIds.Count + " A:" + _advancedAssemblerIds.Count,
            COL_WHITE); y += rh;
        DrawRow(frame, lx, vx, y, fs, "Weld Q",
            wtc.ToString(), wtc > 0 ? COL_WHITE : COL_DIM); y += rh;
        DrawRow(frame, lx, vx, y, fs, "Grind Q",
            gtc.ToString(), gtc > 0 ? COL_WHITE : COL_DIM); y += rh;
        DrawRow(frame, lx, vx, y, fs, "Missing",
            _missing.Count.ToString(), _missing.Count > 0 ? COL_RED : COL_GREEN); y += rh;
        DrawRow(frame, lx, vx, y, fs, "Projectors",
            _projectors.Count.ToString(),
            _projectors.Count > 0 ? COL_ACCENT : COL_DIM); y += rh;

        // Weld progress bar if active
        if (_weldPeak > 0)
        {
            int   built = _weldPeak - wtc;
            if (built < 0) built = 0;
            float pct   = (float)built / (float)_weldPeak;
            DrawRect(frame, ox + W/2f, y, W, 1f, COL_DIM); y += 4f;
            DrawProgressBar(frame, ox + 10f, y, W - 20f, 10f, pct,
                pct >= 1f ? COL_BAR_DONE : COL_BAR_FILL);
            y += 14f;
        }

        // Footer — idle timer
        DrawRect(frame, ox + W/2f, oy + H - 18f, W, 1f, COL_DIM);
        double idleSec = _elapsed - _lastActivityTime;
        string idleStr = _isOffline ? "OFFLINE" : ("IDLE " + FormatTime(idleSec));
        DrawText(frame, idleStr, ox + W - 10f, oy + H - 16f, 0.38f,
            _isOffline ? COL_AMBER : COL_DIM, TextAlignment.RIGHT);
        DrawText(frame, "505th RNB", ox + 10f, oy + H - 16f, 0.38f,
            COL_DIM, TextAlignment.LEFT);
    }
}

// ---------------------------------------------------------------------------
// SPRITE HELPERS
// ---------------------------------------------------------------------------
private void DrawProgressBar(MySpriteDrawFrame f,
    float x, float y, float w, float h, float pct, Color fillCol)
{
    DrawRect(f, x + w/2f, y + h/2f, w,    h,    COL_BAR_BG);
    DrawRect(f, x + w/2f, y,        w,    1f,   COL_DIM);
    DrawRect(f, x + w/2f, y + h,    w,    1f,   COL_DIM);
    DrawRect(f, x,        y + h/2f, 1f,   h,    COL_DIM);
    DrawRect(f, x + w,    y + h/2f, 1f,   h,    COL_DIM);
    if (pct <= 0f) return;
    if (pct > 1f)  pct = 1f;
    float fw = (w - 2f) * pct;
    DrawRect(f, x + 1f + fw/2f, y + h/2f, fw, h - 2f, fillCol);
}

private void DrawRect(MySpriteDrawFrame f,
    float cx, float cy, float w, float h, Color col)
{
    var sp = new MySprite();
    sp.Type            = SpriteType.TEXTURE;
    sp.Data            = "SquareSimple";
    sp.Position        = new Vector2(cx, cy);
    sp.Size            = new Vector2(w,  h);
    sp.Color           = col;
    sp.RotationOrScale = 0f;
    f.Add(sp);
}

private void DrawText(MySpriteDrawFrame f, string text,
    float x, float y, float scale, Color col, TextAlignment align)
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

private void DrawRow(MySpriteDrawFrame f, float lx, float vx, float y,
    float fs, string label, string value, Color valCol)
{
    DrawText(f, label, lx, y, fs, COL_WHITE, TextAlignment.LEFT);
    DrawText(f, value, vx, y, fs, valCol,    TextAlignment.RIGHT);
}

// ---------------------------------------------------------------------------
// HELPERS
// ---------------------------------------------------------------------------
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
