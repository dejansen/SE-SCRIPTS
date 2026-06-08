// -------------------------------------------------------------------------
// Horizon Stabilizer — proof of concept
// Commands: ENABLE, DISABLE (put on hotbar)
// Keeps the ship level with the planetary horizon using gyro override.
// Override stays ON while correcting (full tick duration), released when aligned.
// -------------------------------------------------------------------------

private const string SECTION            = "stabilizer";
private const float  DEFAULT_CORRECTION = 0.5f;
private const float  DEFAULT_DAMPING    = 0.2f;
private const float  DEFAULT_THRESHOLD  = 0.02f;

private float _correctionStrength = DEFAULT_CORRECTION;
private float _dampingStrength    = DEFAULT_DAMPING;
private float _threshold          = DEFAULT_THRESHOLD;

private IMyShipController _controller;
private readonly List<IMyGyro> _gyros = new List<IMyGyro>();
private bool _active = false;

// -------------------------------------------------------------------------
// Lifecycle
// -------------------------------------------------------------------------

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.None;
    ParseConfig();
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument == "ENABLE")
    {
        ParseConfig();
        InitBlocks();
        _active = true;
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
        Echo("Horizon stabilizer: ENABLED\nCorrection=" + _correctionStrength + "  Damping=" + _dampingStrength);
        return;
    }

    if (argument == "DISABLE")
    {
        _active = false;
        Runtime.UpdateFrequency = UpdateFrequency.None;
        ReleaseGyros();
        Echo("Horizon stabilizer: DISABLED");
        return;
    }

    if (!_active) return;

    AlignToHorizon();
}

// -------------------------------------------------------------------------
// Alignment
// -------------------------------------------------------------------------

private void AlignToHorizon()
{
    if (_controller == null || _gyros.Count == 0) return;

    Vector3D gravity = _controller.GetNaturalGravity();
    if (gravity.LengthSquared() < 0.001)
    {
        Echo("Horizon stabilizer: ACTIVE\nNo gravity — idle");
        return;
    }

    Vector3D desiredUp       = -Vector3D.Normalize(gravity);
    Vector3D currentUp       = _controller.WorldMatrix.Up;
    Vector3D tiltCorrection  = Vector3D.Cross(currentUp, desiredUp);
    Vector3D angularVelocity = _controller.GetShipVelocities().AngularVelocity;

    double tiltError = tiltCorrection.Length();
    if (tiltError < _threshold)
    {
        ReleaseGyros();
        Echo("Horizon stabilizer: ACTIVE\nAligned");
        return;
    }

    // Leave override ON so correction runs for the full tick (~1.67s at Update100)
    Vector3D gyroCommand = tiltCorrection * _correctionStrength - angularVelocity * _dampingStrength;

    foreach (var gyro in _gyros)
    {
        Vector3D localCommand = Vector3D.TransformNormal(gyroCommand, MatrixD.Transpose(gyro.WorldMatrix));
        gyro.GyroOverride = true;
        gyro.Pitch = -(float)localCommand.X;
        gyro.Yaw   = -(float)localCommand.Y;
        gyro.Roll  = -(float)localCommand.Z;
    }

    Echo("Horizon stabilizer: ACTIVE\nCorrecting — tilt: " + tiltError.ToString("F3"));
}

// -------------------------------------------------------------------------
// Config
// -------------------------------------------------------------------------

private void ParseConfig()
{
    if (string.IsNullOrWhiteSpace(Me.CustomData))
        Me.CustomData = "[" + SECTION + "]\ncorrection = " + DEFAULT_CORRECTION + "\ndamping = " + DEFAULT_DAMPING + "\nthreshold = " + DEFAULT_THRESHOLD + "\n";

    var ini = new MyIni();
    MyIniParseResult parseResult;
    if (!ini.TryParse(Me.CustomData, out parseResult))
        throw new Exception("Custom Data parse error at line " + parseResult.LineNo);

    _correctionStrength = (float)ini.Get(SECTION, "correction").ToDouble(DEFAULT_CORRECTION);
    _dampingStrength    = (float)ini.Get(SECTION, "damping").ToDouble(DEFAULT_DAMPING);
    _threshold          = (float)ini.Get(SECTION, "threshold").ToDouble(DEFAULT_THRESHOLD);
}

// -------------------------------------------------------------------------
// Block init
// -------------------------------------------------------------------------

private void InitBlocks()
{
    _controller = null;
    _gyros.Clear();

    var controllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(controllers, b => b.IsSameConstructAs(Me));

    foreach (var c in controllers)
    {
        if (c.IsMainCockpit) { _controller = c; break; }
    }
    if (_controller == null && controllers.Count > 0)
        _controller = controllers[0];

    GridTerminalSystem.GetBlocksOfType(_gyros, b => b.IsSameConstructAs(Me));

    if (_controller == null)
        throw new Exception("No ship controller found on grid");
    if (_gyros.Count == 0)
        throw new Exception("No gyroscopes found on grid");
}

// -------------------------------------------------------------------------
// Helpers
// -------------------------------------------------------------------------

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
