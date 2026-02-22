using Godot;

namespace Wild;

/// <summary>
/// Escena de partida (mundo) con movimiento básico y UI de coordenadas
/// </summary>
public partial class GameWorld : Node3D
{
    private Camera3D _camera = null!;
    private Label _labelCoords = null!;
    private bool _isFrozen = false;
    private bool _isCameraLocked = false;
    
    private const float MoveSpeed = 6f;
    private const float MouseSensitivity = 0.15f;
    private const float CameraHeight = 2f;
    private float _cameraYaw = 0f;   // grados, rotación horizontal
    private float _cameraPitch = 0f; // grados, rotación vertical (-90 a 90)

    public override void _Ready()
    {
        Logger.Log("GameWorld: _Ready() iniciado - ESCENA PRINCIPAL");
        
        _camera = GetNode<Camera3D>("Camera3D");
        _labelCoords = GetNode<Label>("UI/LabelCoords");
        
        // Posición inicial de la cámara
        _camera.GlobalPosition = new Vector3(0, CameraHeight, 5);
        
        // Ángulos iniciales desde la rotación actual
        var rot = _camera.GlobalRotation;
        _cameraYaw = Mathf.RadToDeg(rot.Y);
        _cameraPitch = Mathf.RadToDeg(rot.X);
        
        // Capturar el mouse para movimiento FPS
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        Logger.Log("GameWorld: ✅ Escena del mundo cargada correctamente");
        Logger.Log("GameWorld: Movimiento WASD + Mouse habilitado");
        Logger.Log("GameWorld: UI de coordenadas activada");
        Logger.Log("GameWorld: _Ready() completado");
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        
        // Movimiento WASD en el plano XZ - SOLO si no está congelado
        if (!_isFrozen)
        {
            var move = Vector3.Zero;
            if (Input.IsActionPressed("move_forward")) move += GetCameraForwardXZ();
            if (Input.IsActionPressed("move_back")) move -= GetCameraForwardXZ();
            if (Input.IsActionPressed("move_left")) move -= GetCameraRightXZ();
            if (Input.IsActionPressed("move_right")) move += GetCameraRightXZ();
            
            if (move.LengthSquared() > 0.001f)
            {
                move = move.Normalized() * MoveSpeed * dt;
                var pos = _camera.GlobalPosition;
                pos += move;
                pos.Y = CameraHeight; // Mantener altura constante
                _camera.GlobalPosition = pos;
            }
        }
        
        ApplyCameraRotation();
        UpdateCoordsLabel();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventMouseMotion motion && !_isCameraLocked)
        {
            _cameraYaw -= motion.Relative.X * MouseSensitivity;
            _cameraPitch -= motion.Relative.Y * MouseSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -89f, 89f);
        }
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        // ESC ahora lo maneja el PauseMenu
        // Este método queda vacío para evitar conflictos
    }

    private Vector3 GetCameraForwardXZ()
    {
        var f = -_camera.GlobalTransform.Basis.Z;
        f.Y = 0;
        return f.Normalized();
    }

    private Vector3 GetCameraRightXZ()
    {
        var r = _camera.GlobalTransform.Basis.X;
        r.Y = 0;
        return r.Normalized();
    }

    private void ApplyCameraRotation()
    {
        _camera.GlobalRotation = new Vector3(
            Mathf.DegToRad(_cameraPitch),
            Mathf.DegToRad(_cameraYaw),
            0f
        );
    }

    private void UpdateCoordsLabel()
    {
        var pos = _camera.GlobalPosition;
        _labelCoords.Text = $"Pos: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1}) | Cámara: pitch {_cameraPitch:F0}° yaw {_cameraYaw:F0}°";
    }

    /// <summary>Congela el movimiento del jugador y cámara.</summary>
    public void FreezePlayer()
    {
        _isFrozen = true;
        _isCameraLocked = true;
        Logger.Log("GameWorld: Jugador y cámara congelados");
    }

    /// <summary>Descongela el movimiento del jugador y cámara.</summary>
    public void UnfreezePlayer()
    {
        _isFrozen = false;
        _isCameraLocked = false;
        Logger.Log("GameWorld: Jugador y cámara descongelados");
    }
}
