using Godot;
using Wild.Network;
using Wild.Scripts.Terrain;
using Wild.Scripts.Player;
using Wild.Scripts.UI;
using Wild.Systems;
using Wild.Scripts.Character;
using Wild.WorldEnvironment;
using FileAccess = Godot.FileAccess;

namespace Wild;

/// <summary>
/// Escena de partida (mundo) con movimiento básico y UI de coordenadas
/// </summary>
public partial class GameWorld : Node3D
{
    private CharacterBody3D _player;
    private Camera3D _camera;
    // Sistema de HUD
    private GameHUD _gameHUD;
    private bool _isFrozen = false;
    private bool _isCameraLocked = false;
    
    // Sistema de terreno
    private TerrainManager _terrainManager;
    
    // Gestor de red
    private NetworkManager _networkManager;
    
    // Controlador del jugador
    private PlayerController _playerController;
    
    // Sistema de persistencia del jugador
    private PlayerPersistence _playerPersistence;
    
    // Sistema de colisiones
    private CollisionHandler _collisionHandler;
    
    // Sistema de spawn de modelos
    private ModelSpawner? _modelSpawner;
    
    // Sistema de maniquíes
    private MannequinSpawner _mannequinSpawner;
    
    // Sistema de carga dinámica de chunks
    private DynamicChunkLoader _dynamicChunkLoader;
    
    // Referencia directa al singleton para facilitar acceso
    private DynamicChunkLoader ChunkLoader => DynamicChunkLoader.Instance;
    
    // Inicializador del mundo
    private GameWorldInitializer _initializer;
    
    // Sistema de entorno del mundo (enfoque oficial Godot 4)
    private WorldEnvironmentManager? _worldEnvironmentManager;

    /// <summary>
    /// Establece las referencias a los sistemas del juego
    /// </summary>
    public void SetSystemsReference(TerrainManager terrain, NetworkManager network, PlayerController player, PlayerPersistence persistence, 
        CollisionHandler collision, MannequinSpawner mannequin)
    {
        _terrainManager = terrain;
        _networkManager = network;
        _playerController = player;
        _playerPersistence = persistence;
        _collisionHandler = collision;
        _mannequinSpawner = mannequin;
        
        // DynamicChunkLoader usa patrón singleton, no necesita referencia
        _dynamicChunkLoader = DynamicChunkLoader.Instance;
        
        // Obtener referencias a UI (ya se obtuvieron en _Ready)
        var labelCoords = GetNode<Label>("UI/LabelCoords");
        
        // Inicializar el sistema de HUD (requiere PlayerController)
        _gameHUD = new GameHUD();
        _gameHUD.Initialize(labelCoords, _playerController, _terrainManager);
        AddChild(_gameHUD);
        
        // Conectar eventos del PlayerController para actualizar UI
        _playerController.PlayerMoved += OnPlayerMoved;
        _playerController.CameraRotated += OnCameraRotated;
        
        // Activar controles siempre (forzar estado correcto)
        _playerController.SetProcess(true);
        _playerController.SetPhysicsProcess(true);
        _playerController.SetProcessInput(true);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Logger.Log("GameWorld: Controles activados en SetSystemsReference() - forzando estado correcto");
        
        // Conectar señales del PlayerController para actualización de terrain
        _playerController.PlayerPositionUpdated += OnPlayerPositionUpdated;
        Logger.Log($"🎮 GameWorld: Evento PlayerPositionUpdated conectado correctamente");
        
        // Forzar actualización inicial de visibilidad de sub-chunks
        if (_terrainManager != null)
        {
            var initialPosition = _playerController.GetPlayerPosition();
            _terrainManager.UpdateChunksForPlayer(initialPosition);
            Logger.Log($"🎮 GameWorld: Actualización inicial de sub-chunks en posición {initialPosition}");
        }
        
        Logger.Log("🎮 GameWorld: Sistemas referenciados correctamente");
    }
    
    /// <summary>
    /// Establece las referencias a los sistemas del juego desde una tupla
    /// </summary>
    public void SetSystemsReferenceFromTuple((Wild.Scripts.Terrain.TerrainManager terrain, Wild.Network.NetworkManager network, Wild.Scripts.Player.PlayerController player, Wild.Scripts.Player.PlayerPersistence persistence, 
        Wild.Systems.CollisionHandler collision, Wild.Scripts.Character.MannequinSpawner mannequin) systems)
    {
        SetSystemsReference(systems.terrain, systems.network, systems.player, systems.persistence, 
            systems.collision, systems.mannequin);
    }

    public override void _Ready()
    {
        Logger.Log("🎮 GameWorld: _Ready() INICIADO - ESCENA DEL MUNDO CARGÁNDOSE");
        
        try
        {
            // Obtener referencias a los sistemas singleton
            _terrainManager = TerrainManager.Instance;
            _networkManager = NetworkManager.Instance;
            
            // Obtener referencias a UI
            _player = GetNode<CharacterBody3D>("Player");
            _camera = GetNode<Camera3D>("Player/Camera3D");
            var labelCoords = GetNode<Label>("UI/LabelCoords");
            
            // El PlayerController se inicializará a través de GameWorldInitializer
            // Solo obtener referencia si ya existe
            _playerController = PlayerController.Instance;
            
            // Verificar sistemas esenciales (PlayerController se verificará después de la inicialización)
            if (_terrainManager != null && _networkManager != null)
            {
                Logger.Log("GameWorld: Sistemas singleton esenciales obtenidos correctamente");
                
                // El resto de la inicialización se manejará cuando GameWorldInitializer 
                // llame a SetSystemsReference() después de crear el PlayerController
                
                // Inicializar el sistema de spawn de modelos
                _modelSpawner = new ModelSpawner(this);
                AddChild(_modelSpawner);
                
                // Inicializar el sistema de entorno del mundo (enfoque oficial Godot 4)
                _worldEnvironmentManager = new WorldEnvironmentManager();
                AddChild(_worldEnvironmentManager);
                
                // Inicializar el sistema de carga dinámica de chunks
                _initializer = new GameWorldInitializer(this);
                AddChild(_initializer);
                _initializer.InitializeGameWorld();
                
                Logger.Log("GameWorld: ✅ Mundo del juego inicializado correctamente");
            
                // CONECTAR EVENTO PlayerPositionUpdated AQUÍ POR SEGURIDAD
                if (_playerController != null)
                {
                    _playerController.PlayerPositionUpdated += OnPlayerPositionUpdated;
                    Logger.Log($"🎮 GameWorld: Evento PlayerPositionUpdated conectado en _Ready()");
                }
                else
                {
                    Logger.LogError("🎮 GameWorld: ERROR - PlayerController es null en _Ready()");
                }
            }
            else
            {
                Logger.LogError("GameWorld: ❌ ERROR - Sistemas singleton no disponibles");
                Logger.LogError($"GameWorld: TerrainManager: {_terrainManager != null}");
                Logger.LogError($"GameWorld: NetworkManager: {_networkManager != null}");
                Logger.LogError($"GameWorld: PlayerController: {_playerController != null}");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"🎮 GameWorld: ❌ ERROR CRÍTICO en _Ready(): {ex.Message}");
            Logger.LogError($"🎮 GameWorld: Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Se llama cuando el jugador se mueve (evento del PlayerController)
    /// </summary>
    private void OnPlayerMoved(Vector3 newPosition)
    {
        // Actualizar HUD de coordenadas
        _gameHUD.UpdateCoordinatesDisplay();
        
        // Ajustar altura al terreno si está disponible y no está en modo red
        if (!_networkManager.IsNetworkMode && _terrainManager != null)
        {
            float terrainHeight = _terrainManager.GetTerrainHeightAt(newPosition.X, newPosition.Z);
            _playerController.AdjustToTerrain(terrainHeight);
        }
    }
    
    /// <summary>
    /// Se llama cuando la cámara rota (evento del PlayerController)
    /// </summary>
    private void OnCameraRotated(Vector3 rotation)
    {
        // Actualizar HUD de coordenadas
        _gameHUD.UpdateCoordinatesDisplay();
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
                HandleExitTree();
                break;
        }
    }
    
    /// <summary>Maneja la salida del árbol de escenas.</summary>
    private void HandleExitTree()
    {
        Logger.Log("GameWorld: Saliendo del árbol de escenas");
        // El guardado automático lo maneja PlayerPersistence en su _ExitTree
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        
        // Debug: Loggear cada 120 frames (2 segundos) para verificar que _Process se ejecuta
        // if (Engine.GetFramesDrawn() % 120 == 0)
        // {
            // Logger.Log($"GameWorld._Process: Ejecutando - IsNetworkMode: {_networkManager.IsNetworkMode}, PlayerController null: {_playerController == null}");
        // }
        
        // Procesar auto-save del jugador
        _playerPersistence.ProcessAutoSave(delta);
        
        // DEBUG: Logging de posición cada 10 segundos (comentado para producción)
        // if (Engine.GetFramesDrawn() % 600 == 0) // Cada ~600 frames (10 segundos a 60 FPS)
        // {
        //     Logger.Log($"GameWorld: DEBUG - Posición actual: Jugador Local: {_player.Position}, Jugador Global: {_player.GlobalPosition}");
        // }
        
        // Movimiento WASD en el plano XZ - diferente según modo
        if (!_networkManager.IsNetworkMode)
        {
            if (_playerController != null)
            {
                _playerController.ProcessMovement(delta);
            }
            else
            {
                if (Engine.GetFramesDrawn() % 60 == 0)
                {
                    Logger.LogError("GameWorld._Process: PlayerController es nulo en modo local");
                }
            }
        }
        
        // En modo red, procesar inputs locales y enviar inputs al servidor
        if (_networkManager.IsNetworkMode)
        {
            if (_playerController != null)
            {
                _playerController.ProcessMovement(delta);
                
                // Ajustar altura al terreno periódicamente
                if (_terrainManager != null)
                {
                    float terrainHeight = _terrainManager.GetTerrainHeightAt(_playerController.GetPlayerPosition().X, _playerController.GetPlayerPosition().Z);
                    _playerController.AdjustToTerrain(terrainHeight);
                }
                
                // Delegar procesamiento de red al NetworkManager
                _networkManager.ProcessNetworkMovement(_playerController);
            }
            else
            {
                if (Engine.GetFramesDrawn() % 60 == 0)
                {
                    Logger.LogError("GameWorld._Process: PlayerController es nulo en modo red");
                }
            }
        }
        
        _gameHUD.UpdateCoordinatesDisplay();
    }

    public override void _Input(InputEvent ev)
    {
        // Delegar el procesamiento de input al PlayerController
        _playerController.ProcessInput(ev);
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


    
    /// <summary>
    /// Maneja actualización de posición del jugador para optimizar renderizado
    /// </summary>
    private void OnPlayerPositionUpdated(Vector3 position)
    {
        // Log cada 60 actualizaciones (aproximadamente cada segundo) para reducir spam
        if (Engine.GetFramesDrawn() % 60 == 0)
        {
            // Logger.Log($"🎮 GameWorld: Actualizando chunks para jugador en posición: {position}");
        }
        
        // Actualizar visibilidad de sub-chunks según posición del jugador con seguridad
        try
        {
            if (_terrainManager != null)
            {
                _terrainManager.UpdateChunksForPlayer(position);
            }
            else
            {
                Logger.LogError("🎮 GameWorld: TerrainManager es null en OnPlayerPositionUpdated");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"🎮 GameWorld: Error en OnPlayerPositionUpdated: {ex.Message}");
            // No lanzar la excepción para permitir que el juego continúe
        }
    }
    
    /// <summary>Congela el movimiento del jugador y cámara.</summary>
    public void FreezePlayer()
    {
        _playerController.FreezePlayer();
    }

    /// <summary>Descongela el movimiento del jugador y cámara.</summary>
    public void UnfreezePlayer()
    {
        _playerController.UnfreezePlayer();
    }
    
    
    
    /// <summary>Muestra un modelo 3D en coordenadas específicas.</summary>
    /// <param name="modelPath">Ruta al archivo del modelo (ej: "res://assets/models/realistic_tree.glb")</param>
    /// <param name="position">Posición donde mostrar el modelo</param>
    /// <param name="name">Nombre del objeto (opcional)</param>
    /// <returns>El nodo 3D del modelo instanciado, o null si falla</returns>
    public Node3D SpawnModel(string modelPath, Vector3 position, string name = "Model")
    {
        return _modelSpawner?.SpawnModel(modelPath, position, name);
    }
    
    /// <summary>Crea un maniquí en coordenadas específicas usando assets externos.</summary>
    /// <param name="x">Coordenada X</param>
    /// <param name="z">Coordenada Z</param>
    /// <param name="name">Nombre del maniquí (opcional)</param>
    /// <returns>El nodo contenedor del maniquí o null si falla</returns>
    public async System.Threading.Tasks.Task<Node3D> SpawnMannequinAsync(float x, float z, string name = "Mannequin")
    {
        if (_mannequinSpawner == null)
        {
            Logger.LogError("🎮 GameWorld: MannequinSpawner no disponible");
            return null;
        }
        
        return await _mannequinSpawner.SpawnMannequinAsync(x, z, name);
    }
    
    /// <summary>Crea un maniquí de prueba en coordenadas 50,50.</summary>
    /// <returns>El nodo contenedor del maniquí o null si falla</returns>
    public async System.Threading.Tasks.Task<Node3D> SpawnTestMannequinAsync()
    {
        if (_mannequinSpawner == null)
        {
            Logger.LogError("🎮 GameWorld: MannequinSpawner no disponible");
            return null;
        }
        
        return await _mannequinSpawner.SpawnTestMannequinAsync();
    }
}
