using Godot;
using System.Globalization;
using Wild.Network;

namespace Wild;

/// <summary>
/// Escena de partida (mundo) con movimiento básico y UI de coordenadas
/// </summary>
public partial class GameWorld : Node3D
{
    private Camera3D _camera = null!;
    private Label _labelCoords = null!;
    // Lista para almacenar todos los árboles spawnados
    private List<Node3D> _trees = new List<Node3D>(); 
    private bool _isFrozen = false;
    private bool _isCameraLocked = false;
    
    // Componente de red
    private GameClient _gameClient = null!;
    
    // Sincronización con servidor
    private bool _isNetworkMode = false;
    private Vector3 _serverPosition = Vector3.Zero;
    private Vector3 _serverRotation = Vector3.Zero;
    private string _localPlayerId = string.Empty; // ID de nuestro jugador
    
    // Control de envío de inputs al servidor
    private float _lastSentYaw = 0f;
    private float _lastSentPitch = 0f;
    
    // Control de envío de posición al servidor
    private ulong _lastPositionSendTime = 0;
    private const ulong PositionSendIntervalMs = 50; // 20 actualizaciones por segundo (1000/20 = 50ms)
    
    private const float MoveSpeed = 1.11f; // 4 km/h = 1.11 m/s exactos
    private const float MouseSensitivity = 0.15f; // Aumentada para mejor control
    private const float CameraHeight = 2f;
    private float _cameraYaw = 0f;   // grados, rotación horizontal
    private float _cameraPitch = 0f; // grados, rotación vertical (-90 a 90)

    public override void _Ready()
    {
        Logger.Log("🎮 GameWorld: _Ready() INICIADO - ESCENA DEL MUNDO CARGÁNDOSE");
        
        try
        {
            // Logger.Log("GameWorld: Obteniendo referencias de componentes");
            _camera = GetNode<Camera3D>("Camera3D");
            _labelCoords = GetNode<Label>("UI/LabelCoords");
            
            // Cargar múltiples árboles en posiciones aleatorias
            SpawnRandomTrees();
            
            // Logger.Log($"GameWorld: ✅ Cámara obtenida: {_camera.Name}");
            // Logger.Log($"GameWorld: ✅ Label coordenadas obtenido: {_labelCoords.Name}");
            
            // Procesar árboles si se spawnearon
            if (_trees.Count > 0)
            {
                // Logger.Log($"GameWorld: ✅ Árboles cargados: {_trees.Count} árboles");
                
                // Mostrar información del primer árbol como ejemplo
                var firstTree = _trees[0];
                // Logger.Log($"GameWorld: Primer árbol: {firstTree.Name} (tipo: {firstTree.GetType().Name})");
                
                // Verificar si el primer árbol es una instancia de PackedScene
                var treeMesh = firstTree.GetChildren().FirstOrDefault() as MeshInstance3D;
                if (treeMesh != null)
                {
                    // Logger.Log($"GameWorld: Mesh encontrado en el árbol: {treeMesh.Name}");
                    // Logger.Log($"GameWorld: Mesh del árbol: {treeMesh.Mesh?.GetType().Name ?? "null"}");
                    // Logger.Log($"GameWorld: Árbol visible: {treeMesh.Visible}");
                    // Logger.Log($"GameWorld: Posición del árbol: {firstTree.GlobalPosition}");
                    // Logger.Log($"GameWorld: Scale del árbol: {firstTree.Scale}");
                }
                else
                {
                    Logger.LogError("GameWorld: ❌ No se encontró MeshInstance3D dentro del árbol");
                }
                
                // Añadir material temporal si no tiene
                if (treeMesh != null && treeMesh.GetActiveMaterial(0) == null)
                {
                    // Logger.Log("GameWorld: Añadiendo material temporal al árbol");
                    var material = new StandardMaterial3D();
                    material.AlbedoColor = Colors.Brown;
                    material.Metallic = 0.0f;
                    material.Roughness = 1.0f;
                    treeMesh.SetSurfaceOverrideMaterial(0, material);
                }
            }
            
            // Obtener referencia del cliente de red desde GameFlow
            Logger.Log("GameWorld: Obteniendo cliente de red desde GameFlow");
            var gameFlow = GetNode<GameFlow>("/root/GameFlow");
            _gameClient = gameFlow.GetPrivateField<GameClient>("_gameClient");
            
            Logger.Log($"GameWorld: Cliente de red obtenido: {_gameClient != null}");
            
            if (_gameClient != null && _gameClient.IsConnected)
            {
                _isNetworkMode = true;
                Logger.Log("GameWorld: 🌐 MODO RED ACTIVADO - conectado al servidor");
                
                // Conectar señales del cliente
                Logger.Log("GameWorld: Conectando señales del cliente de red");
                _gameClient.OnPositionUpdated += OnServerPositionUpdated;
                _gameClient.OnRotationUpdated += OnServerRotationUpdated;
                _gameClient.OnLocalPlayerIdAssigned += OnLocalPlayerIdAssigned;
                _gameClient.OnRemotePlayerJoined += OnRemotePlayerJoined;
                _gameClient.OnRemotePlayerLeft += OnRemotePlayerLeft;
                _gameClient.OnRemotePlayerUpdated += OnRemotePlayerUpdated;
                Logger.Log("GameWorld: ✅ Señales del cliente conectadas");
            }
            else
            {
                Logger.Log("GameWorld: 🏠 MODO LOCAL - sin conexión de red");
            }
            
            // Logger.Log("GameWorld: Configurando posición inicial de cámara");
            // Posición inicial de la cámara
            _camera.GlobalPosition = new Vector3(0, CameraHeight, 5);
            
            // Logger.Log("GameWorld: Configurando ángulos iniciales de cámara");
            // Ángulos iniciales desde la rotación actual
            var rot = _camera.GlobalRotation;
            _cameraYaw = Mathf.RadToDeg(rot.Y);
            _cameraPitch = Mathf.RadToDeg(rot.X);
            
            Logger.Log($"GameWorld: Ángulos iniciales - Yaw: {_cameraYaw:F1}°, Pitch: {_cameraPitch:F1}°");
            
            Logger.Log("GameWorld: Capturando mouse para movimiento FPS");
            // Capturar el mouse para movimiento FPS
            Input.MouseMode = Input.MouseModeEnum.Captured;
            
            // Asegurar que el mouse esté capturado
            if (Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                Logger.Log("GameWorld: ⚠️ El mouse no está capturado - intentando de nuevo");
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
            
            Logger.Log("🎮 GameWorld: ✅ ESCENA DEL MUNDO CARGADA CORRECTAMENTE");
            Logger.Log("GameWorld: Movimiento WASD + Mouse habilitado");
            Logger.Log("GameWorld: UI de coordenadas activada");
            Logger.Log("GameWorld: Árbol con colisiones listo");
            Logger.Log("GameWorld: _Ready() COMPLETADO - ESCENA LISTA PARA JUGAR");
            
            // Logger.Log("GameWorld: Iniciando bucle _Process - el juego debería responder ahora");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameWorld: ❌ ERROR CRÍTICO en _Ready(): {ex.Message}");
            Logger.LogError($"GameWorld: Stack trace: {ex.StackTrace}");
        }
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        
        switch ((int)what)
        {
            case (int)NotificationWMCloseRequest:
                Logger.Log("GameWorld: Notificación de cierre de ventana recibida");
                break;
            case (int)NotificationReady:
                Logger.Log("GameWorld: Notificación Ready recibida");
                break;
            case (int)NotificationPaused:
                Logger.Log("GameWorld: ⚠️ ESCENA PAUSADA - esto podría causar el problema");
                break;
            case (int)NotificationUnpaused:
                Logger.Log("GameWorld: Escena reanudada");
                break;
            case (int)NotificationExitTree:
                Logger.Log("GameWorld: Saliendo del árbol de escenas");
                break;
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        
        // Logging cada segundo para verificar que _Process se ejecuta
        if (Engine.GetFramesDrawn() % 60 == 0) // Cada ~60 frames (1 segundo a 60 FPS)
        {
            Logger.Log($"GameWorld: _Process ejecutándose - Frame: {Engine.GetFramesDrawn()}, Delta: {dt:F3}");
        }
        
        // Movimiento WASD en el plano XZ - SOLO si no está congelado y NO está en modo red
        if (!_isFrozen && !_isNetworkMode)
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
        
        // En modo red, procesar inputs locales y enviar inputs al servidor
        if (_isNetworkMode)
        {
            // En modo red, procesamos movimiento localmente PERO solo enviamos inputs
            var move = Vector3.Zero;
            if (Input.IsActionPressed("move_forward")) move += GetCameraForwardXZ();
            if (Input.IsActionPressed("move_back")) move -= GetCameraForwardXZ();
            if (Input.IsActionPressed("move_left")) move -= GetCameraRightXZ();
            if (Input.IsActionPressed("move_right")) move += GetCameraRightXZ();
            
            // En modo red, SÍ aplicamos movimiento localmente para respuesta inmediata
            // pero el servidor es la autoridad final
            if (move.LengthSquared() > 0.001f)
            {
                move = move.Normalized() * MoveSpeed * dt;
                var pos = _camera.GlobalPosition;
                pos += move;
                pos.Y = CameraHeight; // Mantener altura constante
                _camera.GlobalPosition = pos;
            }
            
            // Enviar inputs al servidor en lugar de posición
            if (move.LengthSquared() > 0.001f || _cameraYaw != _lastSentYaw || _cameraPitch != _lastSentPitch)
            {
                SendInputToServer(move, new Vector3(_cameraPitch, _cameraYaw, 0));
                _lastSentYaw = _cameraYaw;
                _lastSentPitch = _cameraPitch;
            }
        }
        
        ApplyCameraRotation();
        UpdateCoordsLabel();
    }

    public override void _Input(InputEvent ev)
    {
        // El input del mouse se procesa siempre, pero la aplicación depende del modo
        if (ev is InputEventMouseMotion motion && !_isCameraLocked)
        {
            _cameraYaw -= motion.Relative.X * MouseSensitivity;
            _cameraPitch -= motion.Relative.Y * MouseSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -89f, 89f);
            
            // Normalizar yaw a 0-360 grados
            _cameraYaw = Mathf.Wrap(_cameraYaw, 0f, 360f);
            
            // En modo red, aplicar rotación inmediatamente para respuesta
            if (_isNetworkMode)
            {
                _camera.GlobalRotation = new Vector3(
                    Mathf.DegToRad(_cameraPitch),
                    Mathf.DegToRad(_cameraYaw),
                    0f
                );
            }
        }
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        // Detectar tecla ESC para debug
        if (ev is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            Logger.Log("GameWorld: Tecla ESC presionada - verificando si hay pausa");
        }
        
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
        // En modo local, aplicar rotación directamente
        // En modo red, la rotación ya se aplica en _Input para respuesta inmediata
        if (!_isNetworkMode)
        {
            _camera.GlobalRotation = new Vector3(
                Mathf.DegToRad(_cameraPitch),
                Mathf.DegToRad(_cameraYaw),
                0f
            );
        }
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
    
    /// <summary>Maneja asignación de ID del jugador local.</summary>
    private void OnLocalPlayerIdAssigned(string playerId)
    {
        _localPlayerId = playerId;
        Logger.Log($"GameWorld: ID de jugador local asignado: {playerId}");
    }
    
    /// <summary>Maneja actualización de posición desde el servidor.</summary>
    private void OnServerPositionUpdated(Vector3 position)
    {
        _serverPosition = position;
        
        // En modo red, sincronizar posición local con servidor
        if (_isNetworkMode)
        {
            // Aplicar posición completa del servidor (incluyendo Y)
            _camera.GlobalPosition = position;
            // Logger.Log($"GameWorld: Posición sincronizada con servidor: {position}");
        }
    }
    
    /// <summary>Maneja actualización de rotación desde el servidor.</summary>
    private void OnServerRotationUpdated(Vector3 rotation)
    {
        _serverRotation = rotation;
        
        // En modo red, sincronizar rotación local con servidor
        if (_isNetworkMode)
        {
            // Actualizar valores locales
            _cameraPitch = rotation.X;
            _cameraYaw = rotation.Y;
            
            // Aplicar rotación inmediatamente a la cámara
            _camera.GlobalRotation = new Vector3(
                Mathf.DegToRad(_cameraPitch),
                Mathf.DegToRad(_cameraYaw),
                0f
            );
            
            // Logger.Log($"GameWorld: Rotación sincronizada con servidor: {rotation}");
        }
    }
    
    /// <summary>Maneja conexión de un jugador remoto.</summary>
    private void OnRemotePlayerJoined(string playerId, Vector3 position, Vector3 rotation)
    {
        // Logger.Log($"GameWorld: Jugador remoto {playerId} se unió en pos={position}, rot={rotation}");
        // TODO: Crear visualización del jugador remoto
    }
    
    /// <summary>Maneja desconexión de un jugador remoto.</summary>
    private void OnRemotePlayerLeft(string playerId)
    {
        Logger.Log($"GameWorld: Jugador remoto {playerId} se desconectó");
        // TODO: Eliminar visualización del jugador remoto
    }
    
    /// <summary>Maneja actualización de un jugador remoto.</summary>
    private void OnRemotePlayerUpdated(string playerId, Vector3 position, Vector3 rotation)
    {
        // Logger.Log($"GameWorld: Jugador remoto {playerId} actualizado: pos={position}, rot={rotation}");
        // TODO: Actualizar visualización del jugador remoto
    }
    
    /// <summary>Envía inputs al servidor en lugar de posición.</summary>
    private async void SendInputToServer(Vector3 direction, Vector3 rotation)
    {
        // Formato: "INPUT:movimiento:x,y,z|rotacion:pitch,yaw"
        var message = $"INPUT:movimiento:{direction.X.ToString(CultureInfo.InvariantCulture)},{direction.Y.ToString(CultureInfo.InvariantCulture)},{direction.Z.ToString(CultureInfo.InvariantCulture)}|rotacion:{rotation.X.ToString(CultureInfo.InvariantCulture)},{rotation.Y.ToString(CultureInfo.InvariantCulture)}";
        
        await _gameClient.SendPlayerInput(direction, rotation);
    }
    
    /// <summary>Spawnea árboles en posiciones aleatorias dentro del área del suelo.</summary>
    private void SpawnRandomTrees()
    {
        Logger.Log("GameWorld: SpawnRandomTrees() - INICIADO");
        
        // Definir el área del suelo (10x10 metros, centrado en 0,0)
        const float floorSize = 10f;
        const float halfFloorSize = floorSize / 2f;
        const int treeCount = 10;
        
        // Generar posiciones aleatorias
        var random = new Random();
        var positions = new List<Vector3>();
        
        for (int i = 0; i < treeCount; i++)
        {
            // Coordenadas aleatorias dentro del cuadrado del suelo
            float x = (float)(random.NextDouble() * floorSize - halfFloorSize);
            float z = (float)(random.NextDouble() * floorSize - halfFloorSize);
            float y = 0f; // Altura del suelo
            
            var position = new Vector3(x, y, z);
            positions.Add(position);
            
            // Logger.Log($"GameWorld: Posición árbol {i + 1}: ({x:F2}, {y:F2}, {z:F2})");
        }
        
        // Spawnear cada árbol
        int spawnedCount = 0;
        foreach (var position in positions)
        {
            var treeName = $"Tree_{spawnedCount + 1}";
            var tree = SpawnModel("res://assets/models/tree_default.glb", position, treeName);
            
            if (tree != null)
            {
                _trees.Add(tree);
                spawnedCount++;
                Logger.Log($"GameWorld: ✅ Árbol {treeName} spawnado en {position}");
            }
            else
            {
                Logger.LogError($"GameWorld: ❌ No se pudo spawnear árbol en {position}");
            }
        }
        
        Logger.Log($"GameWorld: ✅ SpawnRandomTrees completado: {spawnedCount}/{treeCount} árboles spawnados");
    }
    
    /// <summary>Muestra un modelo 3D en coordenadas específicas.</summary>
    /// <param name="modelPath">Ruta al archivo del modelo (ej: "res://assets/models/tree_default.glb")</param>
    /// <param name="position">Posición donde mostrar el modelo</param>
    /// <param name="name">Nombre del objeto (opcional)</param>
    /// <returns>El nodo 3D del modelo instanciado, o null si falla</returns>
    public Node3D SpawnModel(string modelPath, Vector3 position, string name = "Model")
    {
        try
        {
            Logger.Log($"GameWorld: SpawnModel() - INICIADO para: {modelPath}");
            // Logger.Log($"GameWorld: Posición: {position}, Nombre: {name}");
            
            // Verificar si el archivo existe
            if (!Godot.FileAccess.FileExists(modelPath))
            {
                Logger.LogError($"GameWorld: ❌ El modelo no existe: {modelPath}");
                return null;
            }
            
            Logger.Log($"GameWorld: ✅ Archivo de modelo encontrado: {modelPath}");
            
            // Cargar el modelo
            var modelResource = GD.Load(modelPath);
            
            if (modelResource == null)
            {
                Logger.LogError($"GameWorld: ❌ No se pudo cargar el modelo: {modelPath}");
                return null;
            }
            
            Logger.Log($"GameWorld: ✅ Modelo cargado - Tipo: {modelResource.GetType().Name}");
            
            Node3D modelNode = null;
            
            // Si es una PackedScene, instanciarla
            if (modelResource is PackedScene packedScene)
            {
                Logger.Log("GameWorld: Modelo es PackedScene, instanciando");
                modelNode = packedScene.Instantiate<Node3D>();
                modelNode.Name = name;
                modelNode.Position = position;
                
                AddChild(modelNode);
                Logger.Log($"GameWorld: ✅ Modelo instanciado: {modelNode.Name}");
                // Logger.Log($"GameWorld: Hijos del modelo: {modelNode.GetChildCount()}");
                
                // Listar hijos para depuración
                // for (int i = 0; i < modelNode.GetChildCount(); i++)
                // {
                //     var child = modelNode.GetChild(i);
                //     Logger.Log($"GameWorld:  Hijo {i}: {child.Name} (tipo: {child.GetType().Name})");
                // }
            }
            // Si es un mesh directo, crear MeshInstance3D
            else if (modelResource is Mesh mesh)
            {
                Logger.Log("GameWorld: Modelo es Mesh, creando MeshInstance3D");
                var meshInstance = new MeshInstance3D();
                meshInstance.Mesh = mesh;
                meshInstance.Name = name;
                meshInstance.Position = position;
                
                AddChild(meshInstance);
                modelNode = meshInstance;
                Logger.Log($"GameWorld: ✅ MeshInstance3D creado: {modelNode.Name}");
            }
            else
            {
                Logger.LogError($"GameWorld: ❌ Tipo de modelo no soportado: {modelResource.GetType().Name}");
            }
            
            return modelNode;
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameWorld: ❌ Error instanciando modelo: {ex.Message}");
            return null;
        }
    }
}
