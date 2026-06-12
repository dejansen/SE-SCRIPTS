// =====================================================================
// HERMES — Intergrid Messaging Service  v1.9
// =====================================================================
// Single script for sender, receiver, both, or local roles.
// Configure via Custom Data on the Programmable Block.
//
// Quick-start Custom Data:
//   mode    = receiver        ; sender | receiver | both | local
//   channel = HERMES
//   lcd_tag = [HERMES]
// =====================================================================

// -------------------------------------------------------------------------
// Constants
// -------------------------------------------------------------------------
private const string VERSION         = "1.9";
private const string DEFAULT_CHANNEL = "HERMES";
private const string DEFAULT_LCD_TAG = "[HERMES]";
private const string ACK_TAG         = "HERMES_ACK";
private const int    DEFAULT_MAX_MESSAGES      = 20;
private const int    DEFAULT_RETRY_SECONDS     = 30;
private const int    DEFAULT_MAX_RETRIES       = 0;
private const int    DEFAULT_CAROUSEL_SECONDS  = 5;
private const string DEFAULT_BC_TAG     = "[HERMES_BC]";
private const bool   DEFAULT_BC_ENABLED = true;
private const int    DEFAULT_BC_INDEX   = 8;

// -------------------------------------------------------------------------
// Alert shortcode table
// -------------------------------------------------------------------------
private readonly string[][] SHORTCODES = new string[][]
{
    new string[] { "HYDROGEN_LOW",  "Hydrogen tanks critically low" },
    new string[] { "OXYGEN_LOW",    "Oxygen supply critically low" },
    new string[] { "POWER_LOW",     "Battery power critically low" },
    new string[] { "GAS_LOW",       "Gas supply critically low" },
    new string[] { "AMMO_LOW",      "Ammunition stockpile low" },
    new string[] { "ICE_LOW",       "Ice supply low" },
    new string[] { "ORE_LOW",       "Ore stockpile low" },
    new string[] { "COMPONENT_LOW", "Component stockpile low" },
    new string[] { "OFFLINE",       "System offline or unresponsive" },
    new string[] { "MAINTENANCE",   "Maintenance required" },
    new string[] { "FUEL_LOW",      "Fuel reserves low" },
    new string[] { "WATER_LOW",     "Water supply low" },
};

// -------------------------------------------------------------------------
// Types
// -------------------------------------------------------------------------
private enum ScriptMode { Sender, Receiver, Both, Local }

private struct ReceivedMessage
{
    public string GridName;
    public string Text;
    public string Timestamp;
}

private struct QueuedMessage
{
    public string Channel;
    public string Payload;
    public int    Retries;
    public long   LastSentTicks;
}

// -------------------------------------------------------------------------
// Configuration
// -------------------------------------------------------------------------
private ScriptMode _mode             = ScriptMode.Receiver;
private string     _channel          = DEFAULT_CHANNEL;
private string     _lcdTag           = DEFAULT_LCD_TAG;
private int        _lcdSurface       = 0;
private int        _maxMessages      = DEFAULT_MAX_MESSAGES;
private bool       _ackEnabled       = false;
private int        _retrySeconds     = DEFAULT_RETRY_SECONDS;
private int        _maxRetries       = DEFAULT_MAX_RETRIES;
private bool       _carouselMode     = false;
private int        _carouselSeconds  = DEFAULT_CAROUSEL_SECONDS;
private string     _bcTag            = DEFAULT_BC_TAG;
private bool       _bcEnabled        = DEFAULT_BC_ENABLED;
private int        _bcIndex          = DEFAULT_BC_INDEX;

// -------------------------------------------------------------------------
// Runtime state
// -------------------------------------------------------------------------
private IMyRadioAntenna      _antenna;
private IMyBroadcastListener _broadcastListener;
private bool                 _pendingAntennaOff = false;

private readonly List<IMyTerminalBlock>  _lcdBuffer     = new List<IMyTerminalBlock>();
private readonly List<IMyLightingBlock>  _alertLights   = new List<IMyLightingBlock>();
private readonly List<IMySoundBlock>     _alertSounds   = new List<IMySoundBlock>();
private readonly List<IMyRadioAntenna>          _antennaBuffer = new List<IMyRadioAntenna>();
private IMyBroadcastController                  _broadcastController;
private readonly List<IMyBroadcastController>   _bcBuffer = new List<IMyBroadcastController>();
private readonly List<ReceivedMessage>   _messages      = new List<ReceivedMessage>();
private readonly List<QueuedMessage>     _queue         = new List<QueuedMessage>();
private readonly StringBuilder           _sb            = new StringBuilder();

private readonly Color H_BG     = new Color(  3,  5, 10);
private readonly Color H_PANEL  = new Color(  6, 10, 18);
private readonly Color H_BORDER = new Color( 25, 60, 90);
private readonly Color H_HEAD   = new Color(120, 200, 255);
private readonly Color H_TEXT   = new Color(200, 230, 255);
private readonly Color H_DIM    = new Color( 70, 110, 145);
private readonly Color H_ACCENT = new Color( 40, 100, 160);

private int  _carouselIndex      = 0;
private long _carouselLastChange = 0;

// =========================================================================
// Constructor
// =========================================================================
public Program()
{
    LoadConfig();
    LoadFromStorage();

    if (_mode != ScriptMode.Local)
        InitAntenna();

    InitBroadcastController();

    bool needsTick = (_mode != ScriptMode.Sender) || _ackEnabled;
    if (needsTick)
        Runtime.UpdateFrequency = UpdateFrequency.Update100;

    if (_mode != ScriptMode.Sender && _mode != ScriptMode.Local)
    {
        _broadcastListener = IGC.RegisterBroadcastListener(_channel);
        _broadcastListener.SetMessageCallback();
    }

    UpdatePbSurface();
    Echo("HERMES v" + VERSION + " — " + ModeLabel() + " online.");
    Echo("Channel: " + _channel);
}

// =========================================================================
// Save
// =========================================================================
public void Save()
{
    _sb.Clear();

    foreach (var m in _messages)
    {
        _sb.Append("R|");
        _sb.Append(m.Timestamp); _sb.Append('|');
        _sb.Append(m.GridName);  _sb.Append('|');
        _sb.AppendLine(m.Text);
    }

    foreach (var q in _queue)
    {
        _sb.Append("Q|");
        _sb.Append(q.Retries);       _sb.Append('|');
        _sb.Append(q.LastSentTicks); _sb.Append('|');
        _sb.Append(q.Channel);       _sb.Append('|');
        _sb.AppendLine(q.Payload);
    }

    Storage = _sb.ToString();
}

// =========================================================================
// Main
// =========================================================================
public void Main(string argument, UpdateType updateSource)
{
    // Turn antenna off one tick after transmitting (sender mode only)
    if (_pendingAntennaOff)
    {
        if (_antenna != null)
            _antenna.Enabled = false;
        _pendingAntennaOff = false;

        if (_mode == ScriptMode.Sender && !_ackEnabled)
            Runtime.UpdateFrequency = UpdateFrequency.None;
    }

    if ((updateSource & UpdateType.Update100) != 0)
    {
        OnTick();
        return;
    }

    // Update10 only fires for the deferred antenna shutdown — nothing else to do
    if ((updateSource & UpdateType.Update10) != 0)
        return;

    // Manual trigger (terminal, toolbar, Event Controller → Timer Block)
    string arg = argument == null ? "" : argument.Trim();

    if (arg.Length == 0)
    {
        Echo("HERMES v" + VERSION + " — " + ModeLabel());
        Echo("Channel: " + _channel);
        if (_mode != ScriptMode.Sender)
            Echo("Messages: " + _messages.Count + " / " + _maxMessages);
        return;
    }

    if (_mode != ScriptMode.Sender && TryHandleClear(arg))
        return;

    if (_mode == ScriptMode.Local)
        InjectLocal(arg);
    else if (_mode != ScriptMode.Receiver)
        Send(arg);
    else
        Echo("WARNING: mode = receiver. Cannot send. Set mode = sender or both.");
}

// =========================================================================
// Tick
// =========================================================================
private void OnTick()
{
    if (_mode != ScriptMode.Sender)
    {
        int before = _messages.Count;
        if (_mode != ScriptMode.Local)
        {
            PollBroadcast();
            EnsureAntennaOn();
        }
        bool newArrived = _messages.Count > before;
        if (_carouselMode) AdvanceCarousel(newArrived);
        RefreshLcds();
        RefreshAlertBlocks(newArrived);
    }

    if (_ackEnabled && _mode != ScriptMode.Local)
    {
        PollAckListener();
        if (_mode != ScriptMode.Receiver)
            RetryQueue();
    }

    UpdatePbSurface();
}

// =========================================================================
// Carousel
// =========================================================================
private void AdvanceCarousel(bool newArrived)
{
    if (_messages.Count == 0) { _carouselIndex = 0; return; }

    if (newArrived)
    {
        _carouselIndex      = 0;
        _carouselLastChange = DateTime.Now.Ticks;
        return;
    }

    // Clamp in case messages were cleared outside OnTick
    if (_carouselIndex >= _messages.Count)
        _carouselIndex = 0;

    if (_messages.Count == 1) return;

    // Initialise timer on first tick so we don't advance immediately
    if (_carouselLastChange == 0) { _carouselLastChange = DateTime.Now.Ticks; return; }

    long interval = (long)_carouselSeconds * TimeSpan.TicksPerSecond;
    if (DateTime.Now.Ticks - _carouselLastChange >= interval)
    {
        _carouselIndex      = (_carouselIndex + 1) % _messages.Count;
        _carouselLastChange = DateTime.Now.Ticks;
    }
}

// =========================================================================
// Sender
// =========================================================================
private void Send(string text)
{
    // Parse optional @channel:message prefix
    string targetChannel = _channel;
    string messageText   = text;

    if (text.StartsWith("@"))
    {
        int colonIdx = text.IndexOf(':');
        if (colonIdx > 1)
        {
            targetChannel = text.Substring(1, colonIdx - 1).Trim();
            messageText   = text.Substring(colonIdx + 1).Trim();
        }
    }

    string message  = ExpandShortcode(messageText);
    string gridName = Me.CubeGrid.CustomName;
    string payload  = _ackEnabled
        ? gridName + "|" + message + "|" + IGC.Me
        : gridName + "|" + message;

    Transmit(targetChannel, payload);

    if (_ackEnabled)
        Enqueue(targetChannel, payload);

    Echo("Sent on " + targetChannel + ":");
    Echo("  " + gridName + " — " + message);
}

// =========================================================================
// Local mode — inject message directly (no network)
// =========================================================================
private void InjectLocal(string text)
{
    string message  = ExpandShortcode(text);
    string gridName = Me.CubeGrid.CustomName;

    _messages.Insert(0, new ReceivedMessage
    {
        GridName  = gridName,
        Text      = message,
        Timestamp = DateTime.Now.ToString("HH:mm"),
    });

    while (_messages.Count > _maxMessages)
        _messages.RemoveAt(_messages.Count - 1);

    Broadcast(message);

    if (_carouselMode) { _carouselIndex = 0; _carouselLastChange = DateTime.Now.Ticks; }
    RefreshLcds();
    RefreshAlertBlocks(true);
    UpdatePbSurface();

    Echo("Posted: " + gridName + " — " + message);
}

private string ExpandShortcode(string input)
{
    string upper = input.ToUpperInvariant().Trim();
    foreach (var pair in SHORTCODES)
    {
        if (upper == pair[0])
            return pair[1];
    }
    return input;
}

private void Transmit(string channel, string payload)
{
    bool antennaWasOff = _antenna != null && !_antenna.Enabled;
    if (antennaWasOff)
    {
        _antenna.Enabled   = true;
        _pendingAntennaOff = true;
        // Keep antenna on for ~160ms (10 ticks); on multiplayer servers the broadcast
        // is queued and dispatched after Main() returns, so one tick is not enough.
        if (Runtime.UpdateFrequency == UpdateFrequency.None)
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }

    IGC.SendBroadcastMessage(channel, payload, TransmissionDistance.TransmissionDistanceMax);
}

// =========================================================================
// Outgoing queue (ack mode)
// =========================================================================
private void Enqueue(string channel, string payload)
{
    _queue.Add(new QueuedMessage
    {
        Channel       = channel,
        Payload       = payload,
        Retries       = 0,
        LastSentTicks = DateTime.Now.Ticks,
    });
}

private void RetryQueue()
{
    long now        = DateTime.Now.Ticks;
    long retryTicks = (long)_retrySeconds * TimeSpan.TicksPerSecond;

    for (int i = _queue.Count - 1; i >= 0; i--)
    {
        var entry = _queue[i];
        if (now - entry.LastSentTicks < retryTicks) continue;

        if (_maxRetries > 0 && entry.Retries >= _maxRetries)
        {
            string preview = entry.Payload.Split('|')[0];
            Echo("WARNING: Dropped after " + entry.Retries + " retries: " + preview);
            _queue.RemoveAt(i);
            continue;
        }

        Transmit(entry.Channel, entry.Payload);
        entry.Retries++;
        entry.LastSentTicks = now;
        _queue[i] = entry;
    }
}

private void PollAckListener()
{
    var listener = IGC.UnicastListener;
    while (listener.HasPendingMessage)
    {
        var msg = listener.AcceptMessage();
        if (msg.Tag != ACK_TAG) continue;

        string acked = msg.Data.ToString();
        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            if (_queue[i].Payload == acked)
            {
                _queue.RemoveAt(i);
                break;
            }
        }
    }
}

// =========================================================================
// Receiver
// =========================================================================
private void PollBroadcast()
{
    if (_broadcastListener == null) return;

    while (_broadcastListener.HasPendingMessage)
    {
        var    msg = _broadcastListener.AcceptMessage();
        if (msg.Source == IGC.Me) continue;   // ignore own broadcasts (mode = both)

        string raw  = msg.Data.ToString();
        string[] p  = raw.Split(new char[] { '|' }, 3);
        if (p.Length < 2) continue;

        string gridName   = p[0].Trim();
        string text       = p[1].Trim();
        long   senderAddr = 0;
        bool   hasAddr    = p.Length >= 3 && long.TryParse(p[2].Trim(), out senderAddr);

        _messages.Insert(0, new ReceivedMessage
        {
            GridName  = gridName,
            Text      = text,
            Timestamp = DateTime.Now.ToString("HH:mm"),
        });

        while (_messages.Count > _maxMessages)
            _messages.RemoveAt(_messages.Count - 1);

        Broadcast(text);

        if (_ackEnabled && hasAddr)
            IGC.SendUnicastMessage(senderAddr, ACK_TAG, raw);
    }
}

private bool TryHandleClear(string argument)
{
    string upper = argument.ToUpperInvariant();

    if (upper == "CLEAR" || upper == "CLEAR ALL")
    {
        _messages.Clear();
        Echo("All messages cleared.");
        RefreshAlertBlocks(false);
        RefreshLcds();
        UpdatePbSurface();
        return true;
    }

    if (upper.StartsWith("CLEAR "))
    {
        string numStr = argument.Substring(6).Trim();
        int    index;
        if (int.TryParse(numStr, out index) && index >= 1 && index <= _messages.Count)
        {
            var m = _messages[index - 1];
            _messages.RemoveAt(index - 1);
            Echo("Cleared #" + index + ": " + m.GridName + " — " + m.Text);
            RefreshAlertBlocks(false);
            RefreshLcds();
            UpdatePbSurface();
            return true;
        }

        if (_messages.Count == 0)
            Echo("No messages to clear.");
        else
            Echo("Invalid index. Use CLEAR 1 to CLEAR " + _messages.Count + ".");
        return true;
    }

    // Relay: re-broadcast all stored messages then clear the log
    if (upper == "FORWARD")
    {
        if (_mode == ScriptMode.Receiver || _mode == ScriptMode.Local)
        {
            Echo("WARNING: FORWARD not available in " + ModeLabel() + " mode.");
            return true;
        }

        int count = _messages.Count;
        if (count == 0)
        {
            Echo("Nothing to forward.");
            return true;
        }

        foreach (var m in _messages)
        {
            // Rebuild payload with this relay's IGC address so ACKs return here
            string payload = _ackEnabled
                ? m.GridName + "|" + m.Text + "|" + IGC.Me
                : m.GridName + "|" + m.Text;

            Transmit(_channel, payload);

            if (_ackEnabled)
                Enqueue(_channel, payload);
        }

        _messages.Clear();
        Echo("Forwarded " + count + " message(s) on " + _channel + ".");
        RefreshAlertBlocks(false);
        RefreshLcds();
        UpdatePbSurface();
        return true;
    }

    return false;
}

// =========================================================================
// Display — sprite helpers
// =========================================================================
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

private void ConfigureSurface(IMyTextSurface s)
{
    s.ContentType           = ContentType.SCRIPT;
    s.Script                = "";
    s.BackgroundColor       = H_BG;
    s.ScriptBackgroundColor = H_BG;
}

// =========================================================================
// Display — dispatch LCD
// =========================================================================
private void RefreshLcds()
{
    _lcdBuffer.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(_lcdBuffer, b =>
        b.IsSameConstructAs(Me)
        && b.CustomName.Contains(_lcdTag)
        && b is IMyTextSurfaceProvider);

    if (_lcdBuffer.Count == 0) return;

    foreach (var block in _lcdBuffer)
    {
        var provider = (IMyTextSurfaceProvider)block;
        int idx = (block is IMyTextPanel) ? 0 : _lcdSurface;
        if (idx >= provider.SurfaceCount) continue;

        var surface = provider.GetSurface(idx);
        ConfigureSurface(surface);

        if (_carouselMode)
            DrawCarousel(surface);
        else
            DrawDispatch(surface);
    }
}

private void DrawDispatch(IMyTextSurface s)
{
    var   vp  = VP(s);
    var   pan = Inset(vp, 8f);
    float cx  = vp.X + vp.Width * 0.5f;
    float pw  = pan.Width;
    float ph  = pan.Height;

    // Measure base char at scale 1 to derive working scales from actual surface size
    _sb.Clear(); _sb.Append('W');
    var   base1  = s.MeasureStringInPixels(_sb, "Monospace", 1.0f);
    float baseH  = base1.Y > 0 ? base1.Y : 28f;
    float baseW  = base1.X > 0 ? base1.X : 15f;

    float headScale = Math.Max(0.25f, Math.Min(0.9f,  ph * 0.13f / baseH));
    float bodyScale = Math.Max(0.20f, Math.Min(0.65f, ph * 0.085f / baseH));

    float cw       = baseW  * bodyScale;
    float rowH     = baseH  * bodyScale + 3f;
    int   maxChars = Math.Max(8, (int)((pw - 20f) / cw));

    float headH = baseH * headScale + 18f;
    float footH = baseH * bodyScale * 0.7f + 12f;
    float divY1 = pan.Y    + headH;
    float divY2 = pan.Bottom - footH;

    using (var fr = s.DrawFrame())
    {
        Fill(fr, vp, H_BG);
        Fill(fr, pan, H_PANEL);
        DrawBorder(fr, pan, H_BORDER, 2f);

        Txt(fr, "HERMES DISPATCH", pan.X + 12f, pan.Y + 6f,  H_HEAD, headScale, TextAlignment.LEFT);
        Txt(fr, DateTime.Now.ToString("HH:mm"), pan.Right - 12f, pan.Y + 8f, H_DIM, bodyScale, TextAlignment.RIGHT);

        Fill(fr, new RectangleF(pan.X + 6f, divY1, pw - 12f, 2f), H_TEXT);
        Fill(fr, new RectangleF(pan.X + 6f, divY2, pw - 12f, 2f), H_TEXT);

        Txt(fr, "Intergrid Messaging  v" + VERSION, cx, divY2 + 3f, H_DIM, bodyScale * 0.6f, TextAlignment.CENTER);

        float dy = divY1 + 6f;

        if (_messages.Count == 0)
        {
            Txt(fr, "No messages — all clear.", cx, dy + rowH, H_DIM, bodyScale, TextAlignment.CENTER);
        }
        else
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                if (dy + rowH * 2f + 2f > divY2 - 4f) break;

                var    m   = _messages[i];
                string hdr = "#" + (i + 1) + "  [" + m.Timestamp + "]  " + m.GridName;
                string txt = m.Text;
                if (hdr.Length > maxChars) hdr = hdr.Substring(0, maxChars - 1) + "~";
                if (txt.Length > maxChars) txt = txt.Substring(0, maxChars - 1) + "~";

                Txt(fr, hdr,         pan.X + 12f, dy,        H_DIM,  bodyScale, TextAlignment.LEFT);
                Txt(fr, "  " + txt,  pan.X + 12f, dy + rowH, H_TEXT, bodyScale, TextAlignment.LEFT);
                dy += rowH * 2f + 4f;
            }
        }
    }
}

private void DrawCarousel(IMyTextSurface s)
{
    if (_messages.Count == 0) { DrawDispatch(s); return; }
    if (_carouselIndex >= _messages.Count) _carouselIndex = 0;

    var   vp  = VP(s);
    var   pan = Inset(vp, 8f);
    float cx  = vp.X + vp.Width  * 0.5f;
    float ph  = pan.Height;
    float mid = pan.Y + ph * 0.5f;

    _sb.Clear(); _sb.Append('W');
    var   base1    = s.MeasureStringInPixels(_sb, "Monospace", 1.0f);
    float baseH    = base1.Y > 0 ? base1.Y : 28f;
    float msgScale = Math.Max(0.3f, Math.Min(0.9f,  ph * 0.15f / baseH));
    float metScale = Math.Max(0.2f, Math.Min(0.55f, ph * 0.075f / baseH));

    float msgH  = baseH * msgScale;
    float divGap = msgH * 0.6f;

    var    m       = _messages[_carouselIndex];
    string counter = (_carouselIndex + 1) + " / " + _messages.Count;

    using (var fr = s.DrawFrame())
    {
        Fill(fr, vp, H_BG);
        Fill(fr, pan, H_PANEL);
        DrawBorder(fr, pan, H_BORDER, 2f);

        Fill(fr, new RectangleF(pan.X + 6f, mid - divGap - msgH * 0.1f, pan.Width - 12f, 2f), H_TEXT);
        Txt(fr, m.Text, cx, mid - divGap + 2f, H_TEXT, msgScale, TextAlignment.CENTER);
        Fill(fr, new RectangleF(pan.X + 6f, mid + divGap + msgH * 0.8f, pan.Width - 12f, 2f), H_TEXT);

        Txt(fr, m.GridName + "  •  " + m.Timestamp + "  •  " + counter,
            cx, mid + divGap + msgH * 0.9f + 4f, H_DIM, metScale, TextAlignment.CENTER);
    }
}

private void RefreshAlertBlocks(bool newMessageArrived)
{
    _alertLights.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(_alertLights, b =>
        b.IsSameConstructAs(Me) && b.CustomName.Contains(_lcdTag));

    _alertSounds.Clear();
    GridTerminalSystem.GetBlocksOfType<IMySoundBlock>(_alertSounds, b =>
        b.IsSameConstructAs(Me) && b.CustomName.Contains(_lcdTag));

    bool hasMessages = _messages.Count > 0;
    foreach (var light in _alertLights)
        light.Enabled = hasMessages;

    if (newMessageArrived)
        foreach (var sound in _alertSounds)
            sound.Play();
}

// =========================================================================
// Display — PB surface (small LCD on block face)
// =========================================================================
private void UpdatePbSurface()
{
    var provider = Me as IMyTextSurfaceProvider;
    if (provider == null) return;
    var surface = provider.GetSurface(0);
    if (surface == null) return;

    surface.ContentType = ContentType.TEXT_AND_IMAGE;
    surface.Font        = "Monospace";

    string bar = new string('═', 18);   // ══════════════════

    _sb.Clear();
    _sb.AppendLine(bar);
    _sb.AppendLine("   * H E R M E S *");
    _sb.AppendLine(bar);
    _sb.AppendLine(" Intergrid Comms");
    _sb.AppendLine("      v" + VERSION);
    _sb.AppendLine(bar);
    _sb.AppendLine("Mode: " + ModeLabel());
    if (_mode != ScriptMode.Local)
        _sb.AppendLine("Chan: " + _channel);

    if (_mode != ScriptMode.Sender)
        _sb.AppendLine("Msgs: " + _messages.Count + " / " + _maxMessages);

    if (_ackEnabled && _mode != ScriptMode.Local)
    {
        _sb.AppendLine(" ACK: ON");
        if (_queue.Count > 0)
            _sb.AppendLine("Que:  " + _queue.Count + " pending");
    }

    if (_mode != ScriptMode.Local)
    {
        if (_antenna != null)
            _sb.Append(" Ant: " + (_antenna.Enabled ? "ON" : "OFF"));
        else
            _sb.Append(" Ant: none");
    }

    surface.WriteText(_sb.ToString());
}

private string ModeLabel()
{
    switch (_mode)
    {
        case ScriptMode.Sender:   return "SENDER";
        case ScriptMode.Receiver: return "RECEIVER";
        case ScriptMode.Local:    return "LOCAL";
        default:                  return "BOTH";
    }
}

// =========================================================================
// Broadcast controller
// =========================================================================
private void InitBroadcastController()
{
    _bcBuffer.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyBroadcastController>(_bcBuffer,
        b => b.IsSameConstructAs(Me) && b.CustomName.Contains(_bcTag));
    _broadcastController = _bcBuffer.Count > 0 ? _bcBuffer[0] : null;
}

private void Broadcast(string message)
{
    if (!_bcEnabled || _broadcastController == null || string.IsNullOrEmpty(message)) return;
    _sb.Clear();
    _sb.Append(message);
    _broadcastController.SetValue<StringBuilder>("Message" + (_bcIndex - 1), _sb);
    _broadcastController.ApplyAction("Transmit Message " + _bcIndex);
}

// =========================================================================
// Antenna management
// =========================================================================
private void InitAntenna()
{
    _antennaBuffer.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(_antennaBuffer,
        a => a.IsSameConstructAs(Me));
    _antenna = _antennaBuffer.Count > 0 ? _antennaBuffer[0] : null;

    if (_antenna == null)
    {
        Echo("WARNING: No antenna found. IGC limited to physics range.");
        return;
    }

    // Receiver needs antenna on at all times to receive broadcasts
    if (_mode != ScriptMode.Sender && !_antenna.Enabled)
    {
        _antenna.Enabled = true;
        Echo("Antenna enabled for receiving.");
    }
}

private void EnsureAntennaOn()
{
    if (_antenna == null || _antenna.Enabled) return;
    _antenna.Enabled = true;
    Echo("WARNING: Antenna was off — re-enabled for receiving.");
}

// =========================================================================
// Configuration parsing
// =========================================================================
private void LoadConfig()
{
    string raw = Me.CustomData ?? "";

    string[] lines = raw.Split(new char[] { '\r', '\n' },
        StringSplitOptions.RemoveEmptyEntries);

    foreach (var line in lines)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0
            || trimmed.StartsWith(";")
            || trimmed.StartsWith("//"))
            continue;

        int commentIdx = trimmed.IndexOf(';');
        if (commentIdx > 0)
            trimmed = trimmed.Substring(0, commentIdx).Trim();

        string key, value;
        if (!TrySplitKeyValue(trimmed, '=', out key, out value)) continue;

        switch (key.ToLowerInvariant())
        {
            case "mode":
                switch (value.ToLowerInvariant())
                {
                    case "sender":   _mode = ScriptMode.Sender;   break;
                    case "receiver": _mode = ScriptMode.Receiver; break;
                    case "both":     _mode = ScriptMode.Both;     break;
                    case "local":    _mode = ScriptMode.Local;    break;
                    default:
                        Echo("WARNING: Unknown mode '" + value + "' — defaulting to receiver.");
                        break;
                }
                break;
            case "channel":
                if (!string.IsNullOrEmpty(value)) _channel = value;
                break;
            case "lcd_tag":
                if (!string.IsNullOrEmpty(value)) _lcdTag = value;
                break;
            case "lcd_surface":
                int surf;
                if (int.TryParse(value, out surf) && surf >= 0)
                    _lcdSurface = surf;
                break;
            case "max_messages":
                int maxMsgs;
                if (int.TryParse(value, out maxMsgs) && maxMsgs > 0)
                    _maxMessages = maxMsgs;
                break;
            case "ack":
            {
                string lv = value.ToLowerInvariant();
                _ackEnabled = lv == "true" || lv == "1" || lv == "yes";
                break;
            }
            case "retry_seconds":
                int secs;
                if (int.TryParse(value, out secs) && secs > 0)
                    _retrySeconds = secs;
                break;
            case "max_retries":
                int maxR;
                if (int.TryParse(value, out maxR) && maxR >= 0)
                    _maxRetries = maxR;
                break;
            case "lcd_mode":
            {
                string lm = value.ToLowerInvariant();
                _carouselMode = lm == "carousel";
                break;
            }
            case "lcd_carousel_seconds":
                int cs;
                if (int.TryParse(value, out cs) && cs > 0)
                    _carouselSeconds = cs;
                break;
            case "bc_tag":
                if (!string.IsNullOrEmpty(value)) _bcTag = value;
                break;
            case "bc_enabled":
            {
                string lv = value.ToLowerInvariant();
                _bcEnabled = lv == "true" || lv == "1" || lv == "yes";
                break;
            }
            case "bc_index":
                int bcIdx;
                if (int.TryParse(value, out bcIdx) && bcIdx >= 1 && bcIdx <= 8)
                    _bcIndex = bcIdx;
                break;
        }
    }

    WriteDefaults(raw);
}

private void WriteDefaults(string raw)
{
    var sb = new StringBuilder();
    bool dirty = false;

    if (!HasKey(raw, "mode"))                 { sb.AppendLine("mode = receiver");                             dirty = true; }
    if (!HasKey(raw, "channel"))              { sb.AppendLine("channel = " + DEFAULT_CHANNEL);                dirty = true; }
    if (!HasKey(raw, "lcd_tag"))              { sb.AppendLine("lcd_tag = " + DEFAULT_LCD_TAG);                dirty = true; }
    if (!HasKey(raw, "lcd_surface"))          { sb.AppendLine("lcd_surface = 0");                             dirty = true; }
    if (!HasKey(raw, "max_messages"))         { sb.AppendLine("max_messages = " + DEFAULT_MAX_MESSAGES);      dirty = true; }
    if (!HasKey(raw, "ack"))                  { sb.AppendLine("ack = false");                                 dirty = true; }
    if (!HasKey(raw, "retry_seconds"))        { sb.AppendLine("retry_seconds = " + DEFAULT_RETRY_SECONDS);    dirty = true; }
    if (!HasKey(raw, "max_retries"))          { sb.AppendLine("max_retries = " + DEFAULT_MAX_RETRIES);        dirty = true; }
    if (!HasKey(raw, "lcd_mode"))             { sb.AppendLine("lcd_mode = dispatch");                         dirty = true; }
    if (!HasKey(raw, "lcd_carousel_seconds")) { sb.AppendLine("lcd_carousel_seconds = " + DEFAULT_CAROUSEL_SECONDS); dirty = true; }
    if (!HasKey(raw, "bc_tag"))               { sb.AppendLine("bc_tag = " + DEFAULT_BC_TAG);                  dirty = true; }
    if (!HasKey(raw, "bc_enabled"))           { sb.AppendLine("bc_enabled = true");                           dirty = true; }
    if (!HasKey(raw, "bc_index"))             { sb.AppendLine("bc_index = " + DEFAULT_BC_INDEX);              dirty = true; }

    if (dirty)
        Me.CustomData = raw.TrimEnd() + (raw.Length > 0 ? "\n" : "") + sb.ToString();
}

private bool HasKey(string raw, string key)
{
    string keyLower = key.ToLowerInvariant();
    foreach (var line in raw.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
    {
        string t = line.Trim().ToLowerInvariant();
        if (t.StartsWith(";") || t.StartsWith("//")) continue;
        if (t.StartsWith(keyLower + "=") || t.StartsWith(keyLower + " ="))
            return true;
    }
    return false;
}

private bool TrySplitKeyValue(string line, char sep, out string key, out string value)
{
    key   = "";
    value = "";
    int idx = line.IndexOf(sep);
    if (idx < 1) return false;
    key   = line.Substring(0, idx).Trim();
    value = line.Substring(idx + 1).Trim();
    return key.Length > 0;
}

// =========================================================================
// Storage — persistence across reboots
// =========================================================================
private void LoadFromStorage()
{
    if (string.IsNullOrEmpty(Storage)) return;

    string[] rows = Storage.Split(new char[] { '\n' },
        StringSplitOptions.RemoveEmptyEntries);

    foreach (var row in rows)
    {
        if (row.StartsWith("R|"))
        {
            // Format: R|HH:mm|GridName|Text  (Text may contain |)
            string[] p = row.Substring(2).Split(new char[] { '|' }, 3);
            if (p.Length < 3) continue;
            _messages.Add(new ReceivedMessage
            {
                Timestamp = p[0].Trim(),
                GridName  = p[1].Trim(),
                Text      = p[2].Trim(),
            });
        }
        else if (row.StartsWith("Q|"))
        {
            // Format: Q|Retries|LastSentTicks|Channel|Payload  (Payload may contain |)
            string[] p = row.Substring(2).Split(new char[] { '|' }, 4);
            if (p.Length < 4) continue;
            int  retries  = 0;
            long lastSent = 0;
            int.TryParse(p[0], out retries);
            long.TryParse(p[1], out lastSent);
            _queue.Add(new QueuedMessage
            {
                Retries       = retries,
                LastSentTicks = lastSent,
                Channel       = p[2].Trim(),
                Payload       = p[3].Trim(),
            });
        }
    }
}
