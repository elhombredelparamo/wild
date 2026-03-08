using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Wild.Scripts.Terrain;
using Wild.Scripts.Player;

namespace Wild.Systems
{
    /// <summary>
    /// Gestiona la carga dinámica de chunks en una cuadrícula 3x3 alrededor del jugador
    /// </summary>
    public partial class DynamicChunkLoader : Node
    {
        private static DynamicChunkLoader _instance;
        public static DynamicChunkLoader Instance => _instance;
        
        private TerrainManager? _terrainManager;
        private PlayerController? _playerController;
        
        // Estado actual del jugador
        private Vector2I _currentPlayerChunk;
        private HashSet<Vector2I> _loadedChunks = new HashSet<Vector2I>();
        
        // Configuración
        private const int GRID_RADIUS = 1; // 3x3 grid (1 chunk a cada lado + centro)
        private const int CHUNK_SIZE = 100; // metros
        
        // Control de actualización
        private bool _isUpdating = false;
        private const float UPDATE_INTERVAL = 1.0f; // segundos entre actualizaciones (aumentado para reducir carga)
        private float _timeSinceLastUpdate = 0f;
        
        // Eventos
        [Signal]
        public delegate void ChunkLoadedEventHandler(Vector2I chunkPosition);
        
        [Signal]
        public delegate void ChunkUnloadedEventHandler(Vector2I chunkPosition);
        
        public override void _Ready()
        {
            // Patrón singleton
            if (_instance == null)
            {
                _instance = this;
                Name = "DynamicChunkLoader";
            }
            else if (_instance != this)
            {
                Logger.LogWarning("DynamicChunkLoader: Instancia duplicada detectada, eliminando");
                QueueFree();
                return;
            }
        }
        
        /// <summary>
        /// Inicializa el sistema con las dependencias necesarias
        /// </summary>
        public void Initialize(TerrainManager terrainManager, PlayerController playerController)
        {
            _terrainManager = terrainManager;
            _playerController = playerController;
            
            Logger.Log("DynamicChunkLoader: Sistema inicializado");
        }
        
        public override void _Process(double delta)
        {
            if (_terrainManager == null || _playerController == null)
                return;
                
            _timeSinceLastUpdate += (float)delta;
            
            // Actualizar posición del jugador periódicamente
            if (_timeSinceLastUpdate >= UPDATE_INTERVAL)
            {
                _timeSinceLastUpdate = 0f;
                UpdatePlayerChunkPosition();
            }
        }
        
        /// <summary>
        /// Actualiza la posición del chunk del jugador y gestiona carga/descarga
        /// </summary>
        private void UpdatePlayerChunkPosition()
        {
            if (_isUpdating)
                return;
            
            try
            {
                _isUpdating = true;
                
                // Obtener posición actual del jugador
                Vector3 playerWorldPos = _playerController.GetPlayerPosition();
                Vector2I newPlayerChunk = WorldToChunkCoordinates(playerWorldPos);
                
                // Si el jugador cambió de chunk, actualizar la cuadrícula
                if (newPlayerChunk != _currentPlayerChunk)
                {
                    Logger.Log($"DynamicChunkLoader: Jugador movido de chunk {_currentPlayerChunk} a {newPlayerChunk}");
                    _currentPlayerChunk = newPlayerChunk;
                    _ = UpdateChunkGridAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"DynamicChunkLoader: Error en UpdatePlayerChunkPosition: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }
        
        /// <summary>
        /// Convierte coordenadas del mundo a coordenadas de chunk
        /// </summary>
        private Vector2I WorldToChunkCoordinates(Vector3 worldPosition)
        {
            int chunkX = (int)Math.Floor(worldPosition.X / CHUNK_SIZE);
            int chunkZ = (int)Math.Floor(worldPosition.Z / CHUNK_SIZE);
            return new Vector2I(chunkX, chunkZ);
        }
        
        /// <summary>
        /// Actualiza la cuadrícula 3x3 de chunks alrededor del jugador
        /// </summary>
        private async Task UpdateChunkGridAsync()
        {
            try
            {
                Logger.Log($"DynamicChunkLoader: Actualizando cuadrícula 3x3 alrededor de chunk {_currentPlayerChunk}");
                
                // Determinar qué chunks deberían estar cargados
                var requiredChunks = GetRequiredChunks();
                
                // Descargar chunks que ya no se necesitan primero (libera recursos)
                await UnloadExtraChunks(requiredChunks);
                
                // Cargar chunks que faltan de forma no bloqueante
                await LoadMissingChunks(requiredChunks);
                
                Logger.Log($"DynamicChunkLoader: Cuadrícula actualizada. Chunks cargados: {_loadedChunks.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"DynamicChunkLoader: Error en UpdateChunkGridAsync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene la lista de chunks que deberían estar cargados (cuadrícula 3x3)
        /// </summary>
        private HashSet<Vector2I> GetRequiredChunks()
        {
            var required = new HashSet<Vector2I>();
            
            for (int x = -GRID_RADIUS; x <= GRID_RADIUS; x++)
            {
                for (int z = -GRID_RADIUS; z <= GRID_RADIUS; z++)
                {
                    Vector2I chunkPos = new Vector2I(_currentPlayerChunk.X + x, _currentPlayerChunk.Y + z);
                    required.Add(chunkPos);
                }
            }
            
            return required;
        }
        
        /// <summary>
        /// Carga los chunks que faltan en la cuadrícula requerida
        /// </summary>
        private async Task LoadMissingChunks(HashSet<Vector2I> requiredChunks)
        {
            var loadTasks = new List<Task>();
            
            foreach (var chunkPos in requiredChunks)
            {
                if (!_loadedChunks.Contains(chunkPos))
                {
                    // Iniciar carga de chunk sin esperar (fire-and-forget)
                    var loadTask = LoadSingleChunkAsync(chunkPos);
                    loadTasks.Add(loadTask);
                }
            }
            
            // Esperar solo un tiempo razonable, no bloquear completamente
            if (loadTasks.Count > 0)
            {
                // Esperar a que al menos algunos chunks se carguen, pero no todos necesariamente
                var timeoutTask = Task.Delay(100); // 100ms max timeout
                var completedTask = await Task.WhenAny(Task.WhenAll(loadTasks), timeoutTask);
                
                Logger.Log($"DynamicChunkLoader: Iniciada carga de {loadTasks.Count} chunks nuevos");
            }
        }
        
        /// <summary>
        /// Carga un chunk individual
        /// </summary>
        private async Task LoadSingleChunkAsync(Vector2I chunkPos)
        {
            try
            {
                // Logger.Log($"DynamicChunkLoader: Cargando chunk {chunkPos}");
                
                // Usar el TerrainManager para cargar o generar el chunk
                Chunk chunk = await _terrainManager.LoadChunk(chunkPos);
                
                if (chunk != null)
                {
                    _loadedChunks.Add(chunkPos);
                    EmitSignal(SignalName.ChunkLoaded, chunkPos);
                    // Logger.Log($"DynamicChunkLoader: ✅ Chunk {chunkPos} cargado exitosamente");
                }
                else
                {
                    Logger.LogError($"DynamicChunkLoader: ❌ Error al cargar chunk {chunkPos}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"DynamicChunkLoader: Error cargando chunk {chunkPos}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Descarga los chunks que ya no se necesitan
        /// </summary>
        private async Task UnloadExtraChunks(HashSet<Vector2I> requiredChunks)
        {
            var chunksToUnload = new List<Vector2I>();
            
            // Encontrar chunks cargados que ya no se necesitan
            foreach (var loadedChunk in _loadedChunks)
            {
                if (!requiredChunks.Contains(loadedChunk))
                {
                    chunksToUnload.Add(loadedChunk);
                }
            }
            
            // Descargar chunks extra
            foreach (var chunkPos in chunksToUnload)
            {
                try
                {
                    Logger.Log($"DynamicChunkLoader: Descargando chunk {chunkPos}");
                    
                    _terrainManager.UnloadChunk(chunkPos);
                    _loadedChunks.Remove(chunkPos);
                    EmitSignal(SignalName.ChunkUnloaded, chunkPos);
                    
                    // Logger.Log($"DynamicChunkLoader: ✅ Chunk {chunkPos} descargado");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"DynamicChunkLoader: Error descargando chunk {chunkPos}: {ex.Message}");
                }
            }
            
            if (chunksToUnload.Count > 0)
            {
                Logger.Log($"DynamicChunkLoader: Descargados {chunksToUnload.Count} chunks");
            }
        }
        
        /// <summary>
        /// Inicializa el sistema con la posición actual del jugador
        /// </summary>
        public async Task InitializeWithPlayerPosition()
        {
            if (_playerController == null)
            {
                Logger.LogError("DynamicChunkLoader: PlayerController no disponible para inicialización");
                return;
            }
            
            try
            {
                // Determinar chunk inicial del jugador
                Vector3 playerPos = _playerController.GetPlayerPosition();
                _currentPlayerChunk = WorldToChunkCoordinates(playerPos);
                
                Logger.Log($"DynamicChunkLoader: Inicializando con jugador en chunk {_currentPlayerChunk}");
                
                // Cargar cuadrícula inicial
                await UpdateChunkGridAsync();
                
                Logger.Log("DynamicChunkLoader: ✅ Inicialización completada");
            }
            catch (Exception ex)
            {
                Logger.LogError($"DynamicChunkLoader: Error en inicialización: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Fuerza una actualización inmediata de la cuadrícula de chunks
        /// </summary>
        public async Task ForceUpdateAsync()
        {
            if (_playerController == null)
                return;
                
            Vector3 playerPos = _playerController.GetPlayerPosition();
            _currentPlayerChunk = WorldToChunkCoordinates(playerPos);
            
            await UpdateChunkGridAsync();
        }
        
        /// <summary>
        /// Obtiene estadísticas del sistema
        /// </summary>
        public (int loaded, int required, Vector2I currentChunk) GetStats()
        {
            var required = GetRequiredChunks();
            return (_loadedChunks.Count, required.Count, _currentPlayerChunk);
        }
        
        /// <summary>
        /// Limpia todos los chunks cargados
        /// </summary>
        public void UnloadAllChunks()
        {
            try
            {
                Logger.Log($"DynamicChunkLoader: Descargando todos los chunks ({_loadedChunks.Count})");
                
                var chunksToUnload = new List<Vector2I>(_loadedChunks);
                foreach (var chunkPos in chunksToUnload)
                {
                    _terrainManager.UnloadChunk(chunkPos);
                }
                
                _loadedChunks.Clear();
                
                Logger.Log("DynamicChunkLoader: Todos los chunks descargados");
            }
            catch (Exception ex)
            {
                Logger.LogError($"DynamicChunkLoader: Error descargando chunks: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpia todos los recursos y resetea el sistema
        /// </summary>
        public void Cleanup()
        {
            Logger.Log("DynamicChunkLoader: Iniciando limpieza completa...");
            
            try
            {
                // Detener actualizaciones
                _isUpdating = false;
                _timeSinceLastUpdate = 0f;
                
                // Descargar todos los chunks
                UnloadAllChunks();
                
                // Limpiar referencias
                _terrainManager = null;
                _playerController = null;
                
                // Resetear estado
                _currentPlayerChunk = new Vector2I(0, 0);
                _loadedChunks.Clear();
                
                // NOTA: No resetear el singleton aquí para evitar null references
                // El singleton se reseteará cuando se cree una nueva instancia
                
                Logger.Log("DynamicChunkLoader: ✅ Limpieza completada");
            }
            catch (Exception ex)
            {
                Logger.LogError($"DynamicChunkLoader: Error durante limpieza: {ex.Message}");
            }
        }
    }
}
