using Godot;
using Wild.Network;
using Wild.Scripts.Player;
using Wild.Systems;
using Wild.Scripts.Terrain;
using Wild.Scripts.Character;

namespace Wild.Systems;

/// <summary>
/// Inicializador del mundo del juego - Encargado de configurar todos los sistemas
/// Patrón Singleton para evitar doble inicialización
/// </summary>
public partial class GameWorldInitializer : Node
{
    private static GameWorldInitializer _instance;
    public static GameWorldInitializer Instance => _instance;
    
    private bool _isInitialized = false;
    private GameWorld _gameWorld;
    private CharacterBody3D _player;
    private Camera3D _camera;
    private Label _labelCoords;
    
    // Sistemas a inicializar
    private TerrainManager _terrainManager;
    private NetworkManager _networkManager;
    private PlayerController _playerController;
    private PlayerPersistence _playerPersistence;
    private CollisionHandler _collisionHandler;
    private MannequinSpawner _mannequinSpawner;
    private DynamicChunkLoader _dynamicChunkLoader;
    
    public GameWorldInitializer(GameWorld gameWorld)
    {
        // Patrón singleton: evitar múltiples instancias
        if (_instance != null)
        {
            Logger.LogWarning("GameWorldInitializer: Ya existe una instancia, ignorando creación duplicada");
            QueueFree();
            return;
        }
        
        _instance = this;
        _gameWorld = gameWorld;
    }
    
    public override void _Ready()
    {
        // Asegurar singleton - manejar instancias inválidas
        if (_instance == null)
        {
            _instance = this;
            Logger.Log("GameWorldInitializer: Nueva instancia singleton establecida");
        }
        else if (_instance != this)
        {
            // Verificar si la instancia existente es válida
            if (IsInstanceValid(_instance))
            {
                Logger.LogWarning("GameWorldInitializer: Instancia duplicada detectada, eliminando esta nueva");
                QueueFree();
                return;
            }
            else
            {
                Logger.LogWarning("GameWorldInitializer: Instancia anterior inválida, reemplazando");
                _instance = this;
                
                // CRÍTICO: Establecer _gameWorld desde el nodo padre
                if (_gameWorld == null && GetParent() is GameWorld parentGameWorld)
                {
                    _gameWorld = parentGameWorld;
                    Logger.Log("GameWorldInitializer: _gameWorld establecido desde nodo padre");
                }
            }
        }
        else
        {
            Logger.Log("GameWorldInitializer: Reutilizando instancia existente");
        }
        
        // Verificación final
        if (_gameWorld == null)
        {
            Logger.LogError("GameWorldInitializer: ❌ _gameWorld sigue siendo null después de _Ready()");
        }
        else
        {
            Logger.Log($"GameWorldInitializer: ✅ _gameWorld configurado: {_gameWorld.Name}");
        }
    }
    
    /// <summary>
    /// Inicializa el mundo del juego de forma asíncrona
    /// </summary>
    public async void InitializeGameWorld()
    {
        // Evitar doble inicialización
        if (_isInitialized)
        {
            Logger.LogWarning("GameWorldInitializer: Sistema ya inicializado, ignorando llamada duplicada");
            return;
        }
        
        try
        {
            Logger.Log("🎮 GameWorldInitializer: Iniciando configuración de sistemas");
            _isInitialized = true;
            
            // Obtener referencias a componentes importantes
            GetWorldReferences();
            
            // Configurar sistema de colisiones
            SetupCollisionSystem();
            
            // Inicializar sistema de terreno
            await SetupTerrainSystem();
            
            // Configurar sistema de jugador
            SetupPlayerSystem();
            
            // Configurar sistema de red
            SetupNetworkSystem();
            
            // Configurar persistencia del jugador
            SetupPlayerPersistence();
            
            // Conectar sistemas entre sí
            ConnectSystems();
            
            // Configurar posición inicial del jugador
            SetupInitialPlayerPosition();
            
            // Configurar controles
            SetupControls();
            
            // Iniciar sistemas automáticos
            StartAutomaticSystems();
            
            // Configurar sistema de maniquíes
            SetupMannequinSystem();
            
            // Configurar sistema de carga dinámica de chunks
            SetupDynamicChunkLoader();
            
            // Conectar sistemas con GameWorld
            ConnectSystemsToGameWorld();
            
            // Notificar completado
            NotifyCompletion();
            
            Logger.Log("🎮 GameWorldInitializer: ✅ Sistemas configurados correctamente");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameWorldInitializer: ❌ ERROR CRÍTICO en InitializeGameWorld(): {ex.Message}");
            Logger.LogError($"GameWorldInitializer: Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Obtiene las referencias a los componentes del mundo
    /// </summary>
    private void GetWorldReferences()
    {
        try
        {
            Logger.Log("GameWorldInitializer: Obteniendo referencias del GameWorld...");
            
            // Verificar que _gameWorld no sea null
            if (_gameWorld == null)
            {
                Logger.LogError("GameWorldInitializer: ❌ _gameWorld es null en GetWorldReferences()");
                throw new System.Exception("_gameWorld es null - no se pueden obtener referencias");
            }
            
            // Verificar que _gameWorld esté en el árbol
            if (!_gameWorld.IsInsideTree())
            {
                Logger.LogError("GameWorldInitializer: ❌ _gameWorld no está en el árbol de escenas");
                throw new System.Exception("_gameWorld no está en el árbol - no se pueden obtener nodos");
            }
            
            Logger.Log("GameWorldInitializer: Buscando nodos Player, Camera3D y LabelCoords...");
            
            // Obtener nodos con verificación
            _player = _gameWorld.GetNode<CharacterBody3D>("Player");
            if (_player == null)
            {
                Logger.LogError("GameWorldInitializer: ❌ No se encontró el nodo Player");
                throw new System.Exception("No se encontró el nodo Player en GameWorld");
            }
            
            _camera = _gameWorld.GetNode<Camera3D>("Player/Camera3D");
            if (_camera == null)
            {
                Logger.LogError("GameWorldInitializer: ❌ No se encontró el nodo Player/Camera3D");
                throw new System.Exception("No se encontró el nodo Player/Camera3D en GameWorld");
            }
            
            _labelCoords = _gameWorld.GetNode<Label>("UI/LabelCoords");
            if (_labelCoords == null)
            {
                Logger.LogWarning("GameWorldInitializer: ⚠️ No se encontró el nodo UI/LabelCoords (opcional)");
            }
            
            Logger.Log($"GameWorldInitializer: ✅ Referencias obtenidas - Player: {_player != null}, Camera: {_camera != null}, LabelCoords: {_labelCoords != null}");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameWorldInitializer: ❌ Error en GetWorldReferences(): {ex.Message}");
            throw; // Re-lanzar para que se maneje en InitializeGameWorld
        }
    }
    
    /// <summary>
    /// Configura el sistema de colisiones
    /// </summary>
    private void SetupCollisionSystem()
    {
        _collisionHandler = new CollisionHandler();
        _collisionHandler.Name = "CollisionHandler";
        _gameWorld.AddChild(_collisionHandler);
    }
    
    /// <summary>
    /// Inicializa el sistema de terreno
    /// </summary>
    private async Task SetupTerrainSystem()
    {
        // Usar TerrainManager existente creado por LoadingScene
        _terrainManager = TerrainManager.Instance;
        if (_terrainManager == null)
        {
            Logger.LogError("GameWorldInitializer: ❌ ERROR - TerrainManager.Instance es null");
            throw new System.Exception("TerrainManager no disponible - LoadingScene debería haberlo creado");
        }
        
        // Obtener el nombre del mundo actual desde WorldManager
        string worldName = WorldManager.Instance.CurrentWorld ?? "test_world";
        
        // Restaurar el estado del personaje actual al iniciar la escena
        CharacterManager.RestoreCurrentCharacterState();
        
        // Obtener la semilla desde WorldManager
        int seed = WorldManager.Instance.GetCurrentWorldSeed();
        
        // El TerrainManager ya debería estar inicializado desde LoadingScene
        // Solo verificar que tenga el mundo correcto
        if (!_terrainManager.IsInitialized())
        {
            _terrainManager.InitializeWorld(worldName, seed);
        }
        
        // Determinar el chunk inicial basado en la posición del jugador
        Vector2I initialChunkPos = GetPlayerChunkPosition();
        
        // Cargar o generar el chunk inicial
        await LoadOrGenerateInitialChunk(initialChunkPos);
    }
    
    /// <summary>
    /// Determina el chunk donde debería estar el jugador basado en su posición guardada
    /// </summary>
    private Vector2I GetPlayerChunkPosition()
    {
        try
        {
            // Crear PlayerPersistence temporal para verificar si hay datos guardados
            var tempPersistence = new PlayerPersistence();
            if (tempPersistence.HasSavedData())
            {
                // Si hay datos guardados, cargarlos para obtener la posición
                var tempController = new PlayerController();
                if (tempPersistence.LoadPlayerData(tempController))
                {
                    var position = tempController.GetPlayerGlobalPosition();
                    
                    // Convertir coordenadas de mundo a coordenadas de chunk
                    int chunkX = (int)Math.Floor(position.X / 100.0f);
                    int chunkZ = (int)Math.Floor(position.Z / 100.0f);
                    
                    Logger.Log($"GameWorldInitializer: Posición guardada encontrada: ({position.X}, {position.Z}) → Chunk: ({chunkX}, {chunkZ})");
                    return new Vector2I(chunkX, chunkZ);
                }
            }
            
            // Si no hay posición guardada, usar chunk (0,0) como fallback
            Logger.Log("GameWorldInitializer: No hay posición guardada, usando chunk (0,0) como fallback");
            return new Vector2I(0, 0);
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameWorldInitializer: Error al determinar chunk del jugador: {ex.Message}");
            return new Vector2I(0, 0);
        }
    }
    
    /// <summary>
    /// Carga o genera el chunk inicial
    /// </summary>
    private async Task LoadOrGenerateInitialChunk(Vector2I initialChunkPos)
    {
        // Verificar si el chunk inicial existe antes de generarlo
        string initialChunkPath = System.IO.Path.Combine(_terrainManager.ChunksDirectory, $"chunk_{initialChunkPos.X}_{initialChunkPos.Y}.dat");
        
        if (Godot.FileAccess.FileExists(initialChunkPath))
        {
            Logger.Log($"GameWorldInitializer: Chunk inicial ({initialChunkPos.X}, {initialChunkPos.Y}) ya existe, cargando desde disco...");
            Chunk existingChunk = await _terrainManager.LoadChunk(initialChunkPos);
            
            if (existingChunk != null)
            {
                Logger.Log($"GameWorldInitializer: ✅ Chunk inicial ({initialChunkPos.X}, {initialChunkPos.Y}) cargado correctamente desde disco");
            }
            else
            {
                Logger.LogError($"GameWorldInitializer: ❌ Error al cargar chunk inicial ({initialChunkPos.X}, {initialChunkPos.Y})");
            }
        }
        else
        {
            Logger.Log($"GameWorldInitializer: Chunk inicial ({initialChunkPos.X}, {initialChunkPos.Y}) no existe, generando nuevo...");
            Chunk initialChunk = await _terrainManager.LoadChunk(initialChunkPos);
            
            if (initialChunk != null)
            {
                Logger.Log($"GameWorldInitializer: ✅ Chunk inicial ({initialChunkPos.X}, {initialChunkPos.Y}) generado y cargado correctamente");
            }
            else
            {
                Logger.LogError($"GameWorldInitializer: ❌ Error al generar chunk inicial ({initialChunkPos.X}, {initialChunkPos.Y})");
            }
        }
    }
    
    /// <summary>
    /// Configura el sistema de red
    /// </summary>
    private void SetupNetworkSystem()
    {
        // Usar NetworkManager existente creado por LoadingScene
        _networkManager = NetworkManager.Instance;
        if (_networkManager == null)
        {
            Logger.LogError("GameWorldInitializer: ❌ ERROR - NetworkManager.Instance es null");
            throw new System.Exception("NetworkManager no disponible - LoadingScene debería haberlo creado");
        }
        
        // El NetworkManager ya debería estar inicializado desde LoadingScene
        // Solo asegurarse de que tenga el PlayerController configurado
        if (_playerController != null)
        {
            _networkManager.SetPlayerController(_playerController);
        }
    }
    
    /// <summary>
    /// Configura el sistema de control del jugador
    /// </summary>
    private void SetupPlayerSystem()
    {
        _playerController = new PlayerController();
        _playerController.Name = "PlayerController";
        
        // Verificar si _gameWorld está en el árbol antes de añadir
        if (_gameWorld.IsInsideTree())
        {
            _gameWorld.AddChild(_playerController);
        }
        else
        {
            Logger.LogError("GameWorldInitializer: GameWorld no está en el árbol, no se puede añadir PlayerController");
        }
    }
    
    /// <summary>
    /// Configura el sistema de persistencia del jugador
    /// </summary>
    private void SetupPlayerPersistence()
    {
        _playerPersistence = new PlayerPersistence();
        _playerPersistence.Name = "PlayerPersistence";
        
        // Verificar si _gameWorld está en el árbol antes de añadir
        if (_gameWorld.IsInsideTree())
        {
            _gameWorld.AddChild(_playerPersistence);
            Logger.Log("GameWorldInitializer: PlayerPersistence añadido al GameWorld en árbol");
        }
        else
        {
            Logger.LogError("GameWorldInitializer: GameWorld no está en el árbol, no se puede añadir PlayerPersistence");
        }
        
        Logger.Log("GameWorldInitializer: Sistema de persistencia del jugador configurado");
    }
    
    /// <summary>
    /// Conecta los sistemas entre sí
    /// </summary>
    private void ConnectSystems()
    {
        // Obtener referencias del GameWorld ANTES de inicializar
        var playerNode = _gameWorld.GetNode<CharacterBody3D>("Player");
        var cameraNode = _gameWorld.GetNode<Camera3D>("Player/Camera3D");
        
        Logger.Log($"GameWorldInitializer: Referencias obtenidas - Player: {playerNode != null}, Camera: {cameraNode != null}");
        
        // Inicializar el controlador con las referencias - siempre modo red
        _playerController.Initialize(playerNode, cameraNode, true);
        
        // Establecer referencia del PlayerController en NetworkManager
        _networkManager.SetPlayerController(_playerController);
        
        // Inicializar PlayerPersistence con el PlayerController
        _playerPersistence.Initialize(_playerController);
        
        // Inicializar CollisionHandler con las referencias
        _collisionHandler.Initialize(_player, _playerController, _networkManager);
        
        // Conectar señales del PlayerController
        _playerController.PlayerMoved += OnPlayerMoved;
        _playerController.CameraRotated += OnCameraRotated;
        
        Logger.Log("GameWorldInitializer: Sistemas conectados entre sí");
    }
    
    /// <summary>
    /// Configura la posición inicial del jugador
    /// </summary>
    private void SetupInitialPlayerPosition()
    {
        // Cargar posición guardada del jugador
        _playerPersistence.LoadPlayerPosition();
        
        // Si no hay posición guardada, usar posición inicial
        bool hasLoadedPosition = _playerPersistence.HasSavedData();
        
        Logger.Log($"GameWorldInitializer: DEBUG - Posición después de cargar: {_player.Position}, HasLoaded: {hasLoadedPosition}");
        
        if (!hasLoadedPosition)
        {
            Logger.Log("GameWorldInitializer: Configurando posición inicial de cámara");
            
            // Posición inicial en el centro del chunk (50, 50) y altura sobre el terreno
            Vector3 initialPosition = new Vector3(50f, 2f, 50f);
            
            // Si el terreno está inicializado, obtener altura real del terreno
            if (_terrainManager != null)
            {
                // Obtener altura del terreno en la posición inicial
                float terrainHeight = GetTerrainHeightAt(50f, 50f);
                initialPosition.Y = terrainHeight + 2f; // 2 metros sobre el terreno
                
                Logger.Log($"GameWorldInitializer: Altura del terreno en (50,50): {terrainHeight}");
            }
            else
            {
                Logger.Log($"GameWorldInitializer: Terreno no inicializado, usando altura por defecto: 2");
            }
            
            _playerController.SetPlayerGlobalPosition(initialPosition);
            
            Logger.Log("GameWorldInitializer: Configurando ángulos iniciales de cámara");
            // Los ángulos iniciales se establecen en la inicialización del PlayerController
            
            Logger.Log($"GameWorldInitializer: Posición inicial establecida: {initialPosition}");
            var angles = _playerController.GetCameraAngles();
            Logger.Log($"GameWorldInitializer: Ángulos iniciales - Yaw: {angles.X:F1}°, Pitch: {angles.Y:F1}°");
        }
        else
        {
            Logger.Log("GameWorldInitializer: ✅ Usando posición guardada del jugador");
        }
    }
    
    /// <summary>
    /// Obtiene la altura del terreno en una posición específica
    /// </summary>
    private float GetTerrainHeightAt(float worldX, float worldZ)
    {
        try
        {
            if (_terrainManager == null)
                return 0f;
            
            // Obtener el generador de chunks del terrain manager
            var chunkGenerator = _terrainManager.GetChunkGenerator();
            if (chunkGenerator == null)
                return 0f;
            
            // Obtener altura usando el generador de ruido
            return chunkGenerator.GetHeightAt(worldX, worldZ);
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameWorldInitializer: Error al obtener altura del terreno: {ex.Message}");
            return 0f;
        }
    }
    
    /// <summary>
    /// Configura los controles del juego
    /// </summary>
    private void SetupControls()
    {
        Logger.Log("GameWorldInitializer: Capturando mouse para movimiento FPS");
        // Capturar el mouse para movimiento FPS
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        // Asegurar que el mouse esté capturado
        if (Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Logger.Log("GameWorldInitializer: ⚠️ El mouse no está capturado - intentando de nuevo");
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        
        Logger.Log("GameWorldInitializer: Controles configurados");
    }
    
    /// <summary>
    /// Inicia los sistemas automáticos
    /// </summary>
    private void StartAutomaticSystems()
    {
        // Iniciar auto-save del jugador
        _playerPersistence.StartAutoSave();
        
        // Liberar el botón de nueva partida en GameFlow
        var gameFlowNode = _gameWorld.GetNode<GameFlow>("/root/GameFlow");
        gameFlowNode?.Call("UpdateNewGameButton", "Nueva Partida");
        
        Logger.Log("GameWorldInitializer: Sistemas automáticos iniciados");
    }
    
    /// <summary>
    /// Configura el sistema de maniquíes
    /// </summary>
    private void SetupMannequinSystem()
    {
        try
        {
            _mannequinSpawner = new MannequinSpawner();
            _mannequinSpawner.Name = "MannequinSpawner";
            _gameWorld.AddChild(_mannequinSpawner);
            
            Logger.Log("🎮 GameWorldInitializer: ✅ Sistema de maniquíes configurado");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"🎮 GameWorldInitializer: ❌ Error configurando maniquíes: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Configura el sistema de carga dinámica de chunks
    /// </summary>
    private void SetupDynamicChunkLoader()
    {
        try
        {
            Logger.Log("🎮 GameWorldInitializer: Configurando DynamicChunkLoader");
            
            // Verificar que las dependencias estén disponibles
            if (_terrainManager == null)
            {
                Logger.LogError("🎮 GameWorldInitializer: ❌ TerrainManager es null, no se puede configurar DynamicChunkLoader");
                return;
            }
            
            if (_playerController == null)
            {
                Logger.LogError("🎮 GameWorldInitializer: ❌ PlayerController es null, no se puede configurar DynamicChunkLoader");
                return;
            }
            
            // Verificar si ya existe una instancia
            if (DynamicChunkLoader.Instance == null)
            {
                _dynamicChunkLoader = new DynamicChunkLoader();
                _dynamicChunkLoader.Name = "DynamicChunkLoader";
                _gameWorld.AddChild(_dynamicChunkLoader);
                
                Logger.Log("🎮 GameWorldInitializer: DynamicChunkLoader creado");
            }
            else
            {
                _dynamicChunkLoader = DynamicChunkLoader.Instance;
                Logger.Log("🎮 GameWorldInitializer: DynamicChunkLoader existente obtenido");
            }
            
            // Inicializar con las dependencias necesarias (con verificación)
            try
            {
                _dynamicChunkLoader.Initialize(_terrainManager, _playerController);
                
                // Inicializar con posición actual del jugador
                _ = _dynamicChunkLoader.InitializeWithPlayerPosition();
                
                Logger.Log("🎮 GameWorldInitializer: ✅ Sistema de carga dinámica de chunks configurado");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"🎮 GameWorldInitializer: ❌ Error en inicialización de DynamicChunkLoader: {ex.Message}");
                // Continuar sin DynamicChunkLoader - el juego puede funcionar sin él
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"🎮 GameWorldInitializer: ❌ Error configurando carga dinámica de chunks: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Notifica la completitud de la inicialización
    /// </summary>
    private void NotifyCompletion()
    {
        Logger.Log("🎮 GameWorldInitializer: ✅ ESCENA DEL MUNDO CARGADA CORRECTAMENTE");
        Logger.Log("GameWorldInitializer: Movimiento WASD + Mouse habilitado");
        Logger.Log("GameWorldInitializer: UI de coordenadas activada");
        Logger.Log("GameWorldInitializer: Árbol con colisiones listo");
        Logger.Log("GameWorldInitializer: INICIALIZACIÓN COMPLETADA - ESCENA LISTA PARA JUGAR");
    }
    
    /// <summary>
    /// Se llama cuando el jugador se mueve (evento del PlayerController)
    /// </summary>
    private void OnPlayerMoved(Vector3 newPosition)
    {
        // Ajustar altura al terreno si está disponible y no está en modo red
        if (!_networkManager.IsNetworkMode && _terrainManager != null)
        {
            float terrainHeight = GetTerrainHeightAt(newPosition.X, newPosition.Z);
            _playerController.AdjustToTerrain(terrainHeight);
        }
    }
    
    /// <summary>
    /// Se llama cuando la cámara rota (evento del PlayerController)
    /// </summary>
    private void OnCameraRotated(Vector3 rotation)
    {
        // En modo local, la rotación ya se aplica en el PlayerController
        // En modo red, la rotación ya se aplica para respuesta inmediata
        // Este evento puede usarse para sincronización con servidor si es necesario
    }
    
    /// <summary>
    /// Conecta los sistemas inicializados con el GameWorld
    /// </summary>
    private void ConnectSystemsToGameWorld()
    {
        try
        {
            // Usar el método existente de GameWorld para establecer referencias
            // DynamicChunkLoader usa patrón singleton, no se pasa como parámetro
            _gameWorld.SetSystemsReference(_terrainManager, _networkManager, _playerController, 
                _playerPersistence, _collisionHandler, _mannequinSpawner);
            
            Logger.Log("🎮 GameWorldInitializer: ✅ Sistemas conectados con GameWorld");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"🎮 GameWorldInitializer: ❌ Error conectando sistemas con GameWorld: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Obtiene referencias a los sistemas inicializados para uso externo
    /// </summary>
    public (Wild.Scripts.Terrain.TerrainManager terrain, Wild.Network.NetworkManager network, Wild.Scripts.Player.PlayerController player, Wild.Scripts.Player.PlayerPersistence persistence, Wild.Systems.CollisionHandler collision, Wild.Scripts.Character.MannequinSpawner mannequin) GetSystems()
    {
        return (_terrainManager, _networkManager, _playerController, _playerPersistence, _collisionHandler, _mannequinSpawner);
    }
    
    /// <summary>
    /// Limpia todos los recursos y sistemas inicializados
    /// </summary>
    public void Cleanup()
    {
        Logger.Log("GameWorldInitializer: Iniciando limpieza de recursos...");
        
        try
        {
            // Limpiar sistemas de jugador
            if (_playerPersistence != null && IsInstanceValid(_playerPersistence))
            {
                _playerPersistence.StopAutoSave();
                _playerPersistence.QueueFree();
                _playerPersistence = null;
            }
            
            if (_playerController != null && IsInstanceValid(_playerController))
            {
                _playerController.QueueFree();
                _playerController = null;
            }
            
            // Limpiar sistema de colisiones
            if (_collisionHandler != null && IsInstanceValid(_collisionHandler))
            {
                _collisionHandler.QueueFree();
                _collisionHandler = null;
            }
            
            // Limpiar sistema de maniquíes
            if (_mannequinSpawner != null && IsInstanceValid(_mannequinSpawner))
            {
                _mannequinSpawner.QueueFree();
                _mannequinSpawner = null;
            }
            
            // Limpiar DynamicChunkLoader
            if (_dynamicChunkLoader != null)
            {
                _dynamicChunkLoader.Cleanup();
                _dynamicChunkLoader = null;
            }
            
            // Resetear estado
            _isInitialized = false;
            
            // NOTA: No resetear el singleton aquí para evitar null references
            // El singleton se reseteará cuando se cree una nueva instancia
            
            Logger.Log("GameWorldInitializer: ✅ Limpieza completada");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameWorldInitializer: Error durante limpieza: {ex.Message}");
        }
    }
}
