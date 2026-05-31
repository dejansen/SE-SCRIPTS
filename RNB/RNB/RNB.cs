using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using VRage.Game.GUI.TextPanel;
using VRage.Collections;

namespace Script
{
    // =============================================================================
    // RNB — Rev NanoBot Manager  v1.0.0
    // Companion script for SKO Nanobot Build and Repair System (Maintained) v2.5.0+
    // Author : RevGamer
    //
    // BLOCK TAGS — rename blocks in-game, no config needed:
    //
    //   [RNBAssembler]          Advanced assembler — auto-queues all components
    //   [RNBBasicAssembler]     Basic assembler    — auto-queues basic components only
    //   [NanoBot]               BaR welder         — preferred explicit BaR detection tag
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
    //   PB surface 0 shows boot sequence then live status — no tag needed.
    //
    // Fully automated: no toolbar input required.
    // =============================================================================

    public sealed class Program : MyGridProgram
    {
        // ---------------------------------------------------------------------------
        // USER SETTINGS
        // ---------------------------------------------------------------------------
        private const double DEFAULT_IDLE_TIMEOUT_SECONDS     = 600.0; // 10 min idle auto-offline
        private const double DEFAULT_REINIT_INTERVAL          = 10.0;  // seconds between block rescans
        private const double DEFAULT_ASSEMBLER_QUEUE_INTERVAL = 0.5;   // seconds between production queue checks
        private const double DEFAULT_BOOT_DURATION            = 6.0;   // seconds for boot animation
        private const bool   AUTO_PRODUCE_FIX_MODE            = true;  // auto-fix assembler mode

        // ---------------------------------------------------------------------------
        // COLOUR PALETTE
        // ---------------------------------------------------------------------------
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

        // ---------------------------------------------------------------------------
        // LCD TAGS — one per page kind
        // ---------------------------------------------------------------------------
        private const string TAG_ASSEMBLER        = "[RNBAssembler]";
        private const string TAG_BASIC_ASSEMBLER  = "[RNBBasicAssembler]";
        private const string TAG_NANOBOT          = "[NanoBot]";
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

        // ---------------------------------------------------------------------------
        // SCRIPT FIELDS
        // ---------------------------------------------------------------------------
        private BaRHandler             _welders      = new BaRHandler();
        private List<long>             _assemblerIds = new List<long>();
        private List<IMyAssembler>     _assemblers   = new List<IMyAssembler>();
        private List<DisplayEntry>     _displays     = new List<DisplayEntry>();
        private List<IMyLightingBlock> _alertLights  = new List<IMyLightingBlock>();
        private List<ProjectorInfo>    _projectors   = new List<ProjectorInfo>();
        private bool                   _usingNanoBotTags = false;

        private double   _elapsed          = 0.0;
        private double   _nextReinit       = 0.0;
        private double   _nextAssembler    = 0.0;
        private double   _nextEcho         = 0.0;
        private double   _lastActivityTime = 0.0;
        private double   _idleTimeoutSeconds     = DEFAULT_IDLE_TIMEOUT_SECONDS;
        private double   _reinitInterval         = DEFAULT_REINIT_INTERVAL;
        private double   _assemblerQueueInterval = DEFAULT_ASSEMBLER_QUEUE_INTERVAL;
        private double   _bootDuration           = DEFAULT_BOOT_DURATION;
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

        private List<long> _basicAssemblerIds    = new List<long>();
        private List<long> _advancedAssemblerIds = new List<long>();

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

            if (_isOffline && _welders.CountEnabled > 0)
            {
                _isOffline        = false;
                _lastActivityTime = _elapsed;
                _state            = RNBState.Idle;
                Echo("ONLINE: BaR welder enabled manually.");
            }

            if (!_isOffline)
            {
                int wtc      = _weldTargets != null ? _weldTargets.Count : 0;
                bool projectorsActive = ProjectorsActive();
                bool anyWork = wtc > 0
                    || (_grindTargets != null && _grindTargets.Count > 0)
                    || (_collectTargets != null && _collectTargets.Count > 0)
                    || projectorsActive;

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
            if (!pbBooting) DrawPBScreen();
            _drawTick = (_drawTick + 1) % 1000;
        }

        // ---------------------------------------------------------------------------
        // INITIALISE
        // ---------------------------------------------------------------------------
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
            _projectors.Clear();
            _usingNanoBotTags = false;

            _tBuf.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(_tBuf);

            for (int i = 0; i < _tBuf.Count; i++)
            {
                var tb   = _tBuf[i];
                if (!tb.IsSameConstructAs(Me)) continue;
                string n = tb.CustomName;

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
                            isBasic = defSubtype.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0;
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

            string msg = "Rev NanoBot Manager v1.0 | Welders:" + _welders.Count
                + (_usingNanoBotTags ? " tagged" : " auto")
                + " Asm:" + _assemblerIds.Count
                + " (B:" + _basicAssemblerIds.Count + " A:" + _advancedAssemblerIds.Count + ")"
                + " LCD:" + _displays.Count
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

        private void LoadPbConfig()
        {
            _idleTimeoutSeconds     = ConfigDouble("AutoOfflineSeconds", DEFAULT_IDLE_TIMEOUT_SECONDS, 30.0, 86400.0);
            _reinitInterval         = ConfigDouble("RescanSeconds", DEFAULT_REINIT_INTERVAL, 1.0, 3600.0);
            _assemblerQueueInterval = ConfigDouble("AssemblerQueueSeconds", DEFAULT_ASSEMBLER_QUEUE_INTERVAL, 0.1, 60.0);
            _bootDuration           = ConfigDouble("BootSeconds", DEFAULT_BOOT_DURATION, 0.5, 60.0);
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
                if (r.Equals(role, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool TryParsePage(string value, out PageKind page)
        {
            page = PageKind.Status;
            if (string.IsNullOrEmpty(value)) return false;

            string v = value.Trim();
            if (v.Equals("Status",     StringComparison.OrdinalIgnoreCase)) { page = PageKind.Status;     return true; }
            if (v.Equals("Missing",    StringComparison.OrdinalIgnoreCase)) { page = PageKind.Missing;    return true; }
            if (v.Equals("Weld",       StringComparison.OrdinalIgnoreCase)) { page = PageKind.Weld;       return true; }
            if (v.Equals("Grind",      StringComparison.OrdinalIgnoreCase)) { page = PageKind.Grind;      return true; }
            if (v.Equals("Welders",    StringComparison.OrdinalIgnoreCase)) { page = PageKind.Welders;    return true; }
            if (v.Equals("Assemblers", StringComparison.OrdinalIgnoreCase)) { page = PageKind.Assemblers; return true; }
            if (v.Equals("Projectors", StringComparison.OrdinalIgnoreCase)) { page = PageKind.Projectors; return true; }
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
                    inSection = section.Equals("RNB", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inSection) continue;

                int eq = line.IndexOf('=');
                if (eq < 1) continue;

                string k = line.Substring(0, eq).Trim();
                if (!k.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;

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
                if (p == null || !p.IsFunctional)
                { info.Total = 0; info.Remaining = 0; info.Progress = 0f; continue; }
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
                if (_projectors[i].Total > 0 && _projectors[i].Remaining > 0)
                    return true;
            return false;
        }

        private void BringOffline(string reason)
        {
            _isOffline = true;
            _state     = RNBState.Offline;
            _welders.SetEnabled(false);
            Echo("OFFLINE: " + reason);
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
                    Echo("Auto-mode: '" + (tb != null ? tb.CustomName : "asm") + "' → Assembly");
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

            RectangleF vp = Viewport(s);
            RectangleF panel = Inset(vp, 10f);
            float pad = 18f;
            float ix = panel.X + pad;
            float right = panel.Right - pad;
            float iw = panel.Width - pad * 2f;

            using (var frame = s.DrawFrame())
            {
                Fill(frame, vp, COL_BG);
                Fill(frame, panel, COL_PANEL);
                DrawBorder(frame, panel, COL_ACCENT, 3f);

                DrawText(frame, "RNB v1.0  |  Rev NanoBot Manager",
                    ix, panel.Y + 18f, 0.42f, COL_ACCENT, TextAlignment.LEFT);

                string stateStr; Color stateCol;
                switch (_state)
                {
                    case RNBState.Working: stateStr = "WORKING"; stateCol = COL_GREEN; break;
                    case RNBState.Missing: stateStr = "MISSING"; stateCol = COL_RED;   break;
                    case RNBState.Offline: stateStr = "OFFLINE"; stateCol = COL_AMBER; break;
                    default:               stateStr = "IDLE";    stateCol = COL_WHITE; break;
                }

                float row2Y = panel.Y + 46f;
                DrawText(frame, "Welders: " + _welders.CountWorking + "/" + _welders.Count,
                    ix, row2Y, 0.36f, COL_DIM, TextAlignment.LEFT);
                DrawText(frame, "LIVE", panel.X + panel.Width * 0.54f, row2Y, 0.34f, COL_GREEN, TextAlignment.LEFT);
                DrawProgressBar(frame, panel.X + panel.Width * 0.63f, row2Y + 3f, panel.Width * 0.16f, 7f,
                    (_drawTick % 80) / 80f, COL_BAR_FILL);
                DrawText(frame, "[ " + stateStr + " ]", right, row2Y, 0.38f, stateCol, TextAlignment.RIGHT);
                DrawRect(frame, panel.X + panel.Width * 0.5f, panel.Y + 70f, iw, 1f, COL_ACCENT);

                float cTop = panel.Y + 84f;
                float cH = panel.Height - 122f;
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

                float footerY = panel.Bottom - 20f;
                DrawRect(frame, panel.X + panel.Width * 0.5f, footerY - 8f, iw, 1f, COL_DIM);
                DrawText(frame, PageLabel(entry.Page), ix, footerY, 0.36f, COL_DIM, TextAlignment.LEFT);
                double idleSec = _elapsed - _lastActivityTime;
                string idleStr = _isOffline ? "OFFLINE" : ("IDLE " + FormatTime(idleSec));
                DrawText(frame, idleStr, right, footerY, 0.36f,
                    _isOffline ? COL_AMBER : COL_DIM, TextAlignment.RIGHT);
            }
        }

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

            int shown = 0;
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

        private void DrawWeldersPage(MySpriteDrawFrame frame, float ox, float top, float W, float H)
        {
            float y    = top;
            float lx   = ox + 14f;
            float vx   = ox + W - 14f;
            float rowH = 20f;

            DrawText(frame, _usingNanoBotTags ? "NANOBOT DETAILS" : "WELDER DETAILS", lx, y, 0.6f, COL_ACCENT, TextAlignment.LEFT);
            DrawText(frame, _welders.Count.ToString(), vx, y, 0.6f,
                _welders.Count > 0 ? COL_WHITE : COL_DIM, TextAlignment.RIGHT);
            y += 28f;
            DrawRect(frame, ox + W/2f, y, W - 20f, 1f, COL_DIM); y += 8f;

            if (_welders.Count == 0)
            { DrawText(frame, "No BaR welders found", ox + W/2f, top + H/2f, 0.5f, COL_DIM, TextAlignment.CENTER); return; }

            for (int i = 0; i < _welders.Welders.Count; i++)
            {
                if (y + rowH * 3f > top + H) break;

                var w  = _welders.Welders[i];
                var tb = w as IMyTerminalBlock;
                string wName = tb != null ? TruncStr(tb.CustomName, 20) : "Welder";

                Color nameCol;
                string statusStr;
                if (!w.IsFunctional)       { nameCol = COL_RED;   statusStr = "DAMAGED";  }
                else if (!w.Enabled)       { nameCol = COL_DIM;   statusStr = "OFF";      }
                else if (w.IsWorking)      { nameCol = COL_GREEN; statusStr = "WORKING";  }
                else                       { nameCol = COL_AMBER; statusStr = "STANDBY";  }

                DrawText(frame, wName,      lx, y, 0.48f, nameCol,  TextAlignment.LEFT);
                DrawText(frame, statusStr,  vx, y, 0.45f, nameCol,  TextAlignment.RIGHT);
                y += rowH;

                bool isBar = BaRHandler.IsBaRWelder(w);
                string barStr  = isBar ? "BaR" : "STD";
                Color  barCol  = isBar ? COL_ACCENT : COL_DIM;

                DrawText(frame, barStr + "  " + (w.IsFunctional ? "OK" : "DAMAGED"), lx, y, 0.42f, barCol, TextAlignment.LEFT);
                bool hasTarget = WelderSlimValue(w, "BuildAndRepair.CurrentTarget") != null
                    || WelderSlimValue(w, "BuildAndRepair.CurrentGrindTarget") != null;
                DrawText(frame, hasTarget ? "ON TARGET" : "", vx, y, 0.42f, COL_GREEN, TextAlignment.RIGHT);
                y += rowH + 3f;

                string modeStr = WelderMode(w);
                string reasonStr = WelderReason(w);
                Color modeCol = modeStr == "GRINDING" ? COL_AMBER : (modeStr == "WELDING" ? COL_GREEN : (modeStr == "OFFLINE" ? COL_AMBER : COL_DIM));
                DrawText(frame, modeStr, lx, y, 0.42f, modeCol, TextAlignment.LEFT);
                DrawText(frame, TruncStr(reasonStr, 18), vx, y, 0.42f, COL_DIM, TextAlignment.RIGHT);
                y += rowH + 3f;
            }
        }

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
                if (y + rowH * 3f > top + H) break;

                var tb = asm as IMyTerminalBlock;
                string asmName = tb != null
                    ? TruncStr(tb.CustomName.Replace(TAG_BASIC_ASSEMBLER, "").Replace(TAG_ASSEMBLER, "").Trim(), 20)
                    : "Assembler";

                Color nameCol;
                string stStr;
                if (!asm.IsFunctional)  { nameCol = COL_RED;   stStr = "DAMAGED"; }
                else if (!asm.Enabled)  { nameCol = COL_DIM;   stStr = "OFF";     }
                else if (asm.IsWorking) { nameCol = COL_GREEN; stStr = "WORKING"; }
                else                    { nameCol = COL_AMBER; stStr = "STANDBY"; }

                DrawText(frame, asmName, lx, y, 0.48f, nameCol, TextAlignment.LEFT);
                DrawText(frame, stStr,   vx, y, 0.45f, nameCol, TextAlignment.RIGHT);
                y += rowH;

                string modeStr = asm.Mode == MyAssemblerMode.Disassembly ? "DISASSEMBLY" : "ASSEMBLY";
                Color  modeCol = asm.Mode == MyAssemblerMode.Disassembly ? COL_AMBER : COL_WHITE;
                DrawText(frame, modeStr, lx, y, 0.42f, modeCol, TextAlignment.LEFT);
                if (asm.CooperativeMode)
                    DrawText(frame, "COOP", vx, y, 0.42f, COL_ACCENT, TextAlignment.RIGHT);
                y += rowH;

                int outItems = asm.OutputInventory != null ? asm.OutputInventory.ItemCount : 0;
                DrawText(frame, "Output: " + outItems + " item" + (outItems != 1 ? "s" : ""), lx, y, 0.42f, COL_DIM, TextAlignment.LEFT);
                if (asm.Repeating)
                    DrawText(frame, "REPEAT", vx, y, 0.42f, COL_ACCENT, TextAlignment.RIGHT);
                y += rowH + 3f;
                shown++;
            }

            if (shown == 0)
                DrawText(frame, "No tagged assemblers on grid", ox + W/2f, top + H/2f, 0.45f, COL_DIM, TextAlignment.CENTER);
        }

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

                DrawText(frame, TruncStr(info.Name, 22), lx, y, 0.5f, COL_WHITE, TextAlignment.LEFT);
                DrawText(frame, active ? "BUILDING" : "IDLE", vx, y, 0.45f, active ? COL_GREEN : COL_DIM, TextAlignment.RIGHT);
                y += 22f;

                if (active)
                {
                    DrawProgressBar(frame, ox + 14f, y, bw, 14f, info.Progress,
                        info.Progress >= 1f ? COL_BAR_DONE : COL_BAR_FILL);
                    y += 20f;
                    int built = info.Total - info.Remaining;
                    DrawText(frame, built + " / " + info.Total + " blocks", lx, y, 0.42f, COL_DIM, TextAlignment.LEFT);
                    DrawText(frame, (int)(info.Progress * 100f) + "%", vx, y, 0.45f, COL_ACCENT, TextAlignment.RIGHT);
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
                DrawText(frame, "v1.0  |  RevGamer", center.X, titleY + (compact ? 42f : 84f),
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

            var s  = _pbSurface;
            var vp = new RectangleF((s.TextureSize - s.SurfaceSize) / 2f, s.SurfaceSize);
            float W  = vp.Width;
            float H  = vp.Height;
            float ox = vp.X;
            float oy = vp.Y;

            using (var frame = s.DrawFrame())
            {
                DrawRect(frame, ox + W/2f, oy + H/2f, W, H, COL_BG);
                DrawPanelFrame(frame, ox + 8f, oy + 8f, W - 16f, H - 16f, COL_DIM);

                DrawText(frame, "Rev NanoBot Manager", ox + 18f, oy + 18f, 0.58f, COL_ACCENT, TextAlignment.LEFT);

                string stStr; Color stCol;
                switch (_state)
                {
                    case RNBState.Working: stStr = "WORKING"; stCol = COL_GREEN;  break;
                    case RNBState.Missing: stStr = "MISSING"; stCol = COL_RED;    break;
                    case RNBState.Offline: stStr = "OFFLINE"; stCol = COL_AMBER;  break;
                    default:               stStr = "IDLE";    stCol = COL_WHITE;  break;
                }
                DrawText(frame, "[ " + stStr + " ]",
                    ox + W - 52f, oy + 18f, 0.55f, stCol, TextAlignment.RIGHT);
                DrawText(frame, _welders.CountWorking + "/" + _welders.Count,
                    ox + W - 16f, oy + 18f, 0.55f, _welders.CountWorking > 0 ? COL_GREEN : COL_AMBER, TextAlignment.RIGHT);

                DrawRect(frame, ox + W/2f, oy + 54f, W - 32f, 1f, COL_ACCENT);
                DrawText(frame, "Welders", ox + 22f, oy + 78f, 0.78f, COL_WHITE, TextAlignment.LEFT);
                DrawRect(frame, ox + W/2f, oy + 116f, W - 40f, 1f, COL_ACCENT);

                float y  = oy + 140f;
                float lx = ox + 28f;
                float vx = ox + W - 48f;
                float fs = 0.62f;
                float rh = 34f;

                int wtc = _weldTargets != null ? _weldTargets.Count : 0;
                int gtc = _grindTargets != null ? _grindTargets.Count : 0;

                DrawOverviewRow(frame, lx, vx, y, fs, "Assemblers",
                    "B:" + _basicAssemblerIds.Count + " A:" + _advancedAssemblerIds.Count, COL_WHITE); y += rh;
                DrawOverviewRow(frame, lx, vx, y, fs, "Weld Queue",  wtc.ToString(), wtc > 0 ? COL_WHITE : COL_DIM); y += rh;
                DrawOverviewRow(frame, lx, vx, y, fs, "Grind Queue", gtc.ToString(), gtc > 0 ? COL_WHITE : COL_DIM); y += rh;
                DrawOverviewRow(frame, lx, vx, y, fs, "Missing",     _missing.Count.ToString(), _missing.Count > 0 ? COL_RED : COL_GREEN); y += rh;
                DrawOverviewRow(frame, lx, vx, y, fs, "Projectors",  _projectors.Count.ToString(), _projectors.Count > 0 ? COL_ACCENT : COL_DIM); y += rh;

                if (_weldPeak > 0)
                {
                    int   built = _weldPeak - wtc;
                    if (built < 0) built = 0;
                    float pct   = (float)built / (float)_weldPeak;
                    DrawRect(frame, ox + W/2f, y + 6f, W - 40f, 1f, COL_DIM); y += 14f;
                    DrawProgressBar(frame, ox + 22f, y, W - 44f, 12f, pct, pct >= 1f ? COL_BAR_DONE : COL_BAR_FILL);
                }

                DrawRect(frame, ox + W/2f, oy + H - 34f, W - 40f, 1f, COL_DIM);
                double idleSec = _elapsed - _lastActivityTime;
                string idleStr = _isOffline ? "OFFLINE" : ("IDLE " + FormatTime(idleSec));
                DrawText(frame, idleStr, ox + W - 18f, oy + H - 26f, 0.36f,
                    _isOffline ? COL_AMBER : COL_DIM, TextAlignment.RIGHT);
                DrawText(frame, "REV Systems", ox + 18f, oy + H - 26f, 0.36f, COL_DIM, TextAlignment.LEFT);
            }
        }

        // ---------------------------------------------------------------------------
        // SPRITE HELPERS
        // ---------------------------------------------------------------------------
        private void DrawProgressBar(MySpriteDrawFrame f, float x, float y, float w, float h, float pct, Color fillCol)
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

        private void DrawRow(MySpriteDrawFrame f, float lx, float vx, float y, float fs, string label, string value, Color valCol)
        {
            DrawText(f, label, lx, y, fs, COL_WHITE, TextAlignment.LEFT);
            DrawText(f, value, vx, y, fs, valCol,    TextAlignment.RIGHT);
        }

        private void DrawOverviewRow(MySpriteDrawFrame f, float lx, float vx, float y, float fs, string label, string value, Color valCol)
        {
            DrawText(f, label, lx, y, fs, COL_WHITE, TextAlignment.LEFT);
            DrawText(f, value, vx, y, fs, valCol, TextAlignment.RIGHT);
            DrawRect(f, vx + 24f, y + 10f, 8f, 8f, valCol);
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

        private static IMySlimBlock WelderSlimValue(IMyShipWelder w, string prop)
        {
            try { return w.GetValue<IMySlimBlock>(prop); } catch { return null; }
        }

        private static long WelderLongValue(IMyShipWelder w, string prop)
        {
            try { return w.GetValue<long>(prop); } catch { return -1; }
        }

        private string WelderMode(IMyShipWelder w)
        {
            if (!w.IsFunctional) return "DAMAGED";
            if (!w.Enabled) return "OFFLINE";
            if (WelderSlimValue(w, "BuildAndRepair.CurrentGrindTarget") != null) return "GRINDING";
            if (WelderSlimValue(w, "BuildAndRepair.CurrentTarget") != null) return "WELDING";
            long mode = WelderLongValue(w, "BuildAndRepair.Mode");
            if (mode >= 0) return "MODE " + mode;
            return "READY";
        }

        private string WelderReason(IMyShipWelder w)
        {
            if (!w.IsFunctional) return "Needs repair";
            if (!w.Enabled) return "Block disabled";
            if (_missing.Count > 0) return "Waiting parts";
            if (_grindTargets != null && _grindTargets.Count > 0) return "Grind queue";
            if (_weldTargets != null && _weldTargets.Count > 0) return "Weld queue";
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
    }
}
