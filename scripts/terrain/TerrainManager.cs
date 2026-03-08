using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using FileAccess = Godot.FileAccess;
using Wild;

namespace Wild.Scripts.Terrain
{
    /// <summary>
    /// Gestiona la carga, generación y almacenamiento de chunks
    /// Patrón Singleton para acceso global desde cualquier escena
    /// </summary>
    public partial class TerrainManager : Node
    {
        private static TerrainManager _instance;
        public static TerrainManager Instance => _instance;
        
        private bool _isInitialized = false;
        private ChunkGenerator _chunkGenerator;
        private Dictionary<Vector2I, Chunk> _loadedChunks;
        private Dictionary<Vector2I, ChunkData> _chunkDataCache;
        private string _worldDirectory;
        public string WorldDirectory => _worldDirectory;
        public string ChunksDirectory => _chunksDirectory;
        private string _chunksDirectory;
        
        // Configuración
        public const int CHUNK_SIZE = 100;
        public const int LOAD_RADIUS = 1; // Cargar 1 chunk alrededor del jugador (obsoleto, usar DynamicChunkLoader)
        public const int CHUNK_CACHE_SIZE = 50; // Máximo chunks en memoria
        
        public override void _Ready()
        {
            // Patrón singleton - reemplazar instancia existente si es necesario
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Logger.LogWarning("TerrainManager: Instancia duplicada detectada, reemplazando la anterior");
                // Si hay una instancia anterior, liberarla primero
                if (IsInstanceValid(_instance))
                {
                    _instance.QueueFree();
                }
                _instance = this;
            }
            
            _chunkGenerator = new ChunkGenerator();
            _chunkGenerator.Name = "ChunkGenerator";
            AddChild(_chunkGenerator);
            
            _loadedChunks = new Dictionary<Vector2I, Chunk>();
            _chunkDataCache = new Dictionary<Vector2I, ChunkData>();
            
            Name = "TerrainManager";
        }
        
        /// <summary>
        /// Inicializa el sistema de terreno para un mundo
        /// </summary>
        public void InitializeWorld(string worldName, int seed)
        {
            _worldDirectory = $"user://worlds/{worldName}";
            _chunksDirectory = $"{_worldDirectory}/chunks";
            
            // Inicializar ChunkGenerator si no está inicializado
            if (_chunkGenerator == null)
            {
                _chunkGenerator = new ChunkGenerator();
                _chunkGenerator.Name = "ChunkGenerator";
                AddChild(_chunkGenerator);
            }
            
            // Inicializar diccionarios si no están inicializados
            if (_loadedChunks == null)
            {
                _loadedChunks = new Dictionary<Vector2I, Chunk>();
            }
            if (_chunkDataCache == null)
            {
                _chunkDataCache = new Dictionary<Vector2I, ChunkData>();
            }
            
            // Crear directorios si no existen
            if (!DirAccess.DirExistsAbsolute(_worldDirectory))
            {
                var dir = DirAccess.Open("user://");
                if (dir != null)
                {
                    dir.MakeDir($"worlds/{worldName}");
                }
            }
            
            if (!DirAccess.DirExistsAbsolute(_chunksDirectory))
            {
                var dir = DirAccess.Open(_worldDirectory);
                if (dir != null)
                {
                    dir.MakeDir("chunks");
                }
            }
            
            _chunkGenerator.SetSeed(seed);
            
            Logger.Log($"TerrainManager inicializado para mundo: {worldName}");
            Logger.Log($"Directorio de chunks: {_chunksDirectory}");
            _isInitialized = true;
        }
        
        /// <summary>
        /// Verifica si el TerrainManager está inicializado
        /// </summary>
        public bool IsInitialized()
        {
            return _isInitialized;
        }
        
        /// <summary>
        /// Genera el chunk inicial (0,0) para un mundo nuevo
        /// </summary>
        public async Task<Chunk> GenerateInitialChunk()
        {
            Vector2I chunkPos = new Vector2I(0, 0);
            
            Logger.Log("Generando chunk inicial (0,0)...");
            
            try
            {
                // Generar datos del chunk
                ChunkData chunkData = _chunkGenerator.GenerateChunk(0, 0);
                
                // Guardar en disco
                await SaveChunkData(chunkData);
                
                // Crear y cargar el chunk
                Chunk chunk = await LoadChunk(chunkPos);
                
                if (chunk != null)
                {
                    Logger.Log("Chunk inicial generado y cargado");
                }
                else
                {
                    Logger.LogError("Error: LoadChunk retornó null");
                }
                
                return chunk;
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error en GenerateInitialChunk: {ex.Message}");
                Logger.LogError($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
        
        /// <summary>
        /// Carga un chunk desde disco o lo genera si no existe
        /// </summary>
        public async Task<Chunk> LoadChunk(Vector2I chunkPos)
        {
            try
            {
                // Logger.Log($"LoadChunk: Cargando chunk en posición {chunkPos}");
                
                // Si ya está cargado, retornarlo
                if (_loadedChunks.ContainsKey(chunkPos))
                {
                    Logger.Log($"LoadChunk: Chunk {chunkPos} ya estaba cargado");
                    return _loadedChunks[chunkPos];
                }
                
                // Cargar datos del chunk
                ChunkData chunkData = await LoadChunkData(chunkPos);
                
                if (chunkData == null)
                {
                    Logger.Log($"LoadChunk: Generando nuevo chunk {chunkPos}");
                    // Generación en background thread para evitar parones
                    chunkData = await Task.Run(() => _chunkGenerator.GenerateChunk(chunkPos.X, chunkPos.Y));
                    await SaveChunkData(chunkData);
                }
                else
                {
                    // Logger.Log($"LoadChunk: Chunk {chunkPos} cargado desde disco");
                }
                
                // Crear instancia del chunk
                // Logger.Log($"LoadChunk: Creando instancia de Chunk para {chunkPos}");
                
                Chunk chunk = new Chunk();
                await chunk.InitializeAsync(chunkData, chunkPos); // Usar versión asíncrona
                
                // Añadir a la escena actual
                var currentScene = GetTree().CurrentScene;
                if (currentScene != null)
                {
                    // Añadir chunk a la escena raíz para persistencia entre cambios de escena
                    GetTree().Root.AddChild(chunk);
                    // Logger.Log($"LoadChunk: Chunk {chunkPos} añadido a escena raíz para persistencia - Parent: {chunk.GetParent()?.Name ?? "NULL"}");
                }
                else
                {
                    Logger.LogError("LoadChunk: CurrentScene es nulo, no se puede añadir chunk");
                    return null;
                }
                _loadedChunks[chunkPos] = chunk;
                
                // Actualizar barreras de chunks vecinos
                UpdateChunkBoundaries(chunkPos);
                
                // Logger.Log($"LoadChunk: Chunk {chunkPos} cargado exitosamente");
                
                // Para chunks nuevos, no esperar a meshes asíncronos para no bloquear
                if (chunkData.ChunkX == 0 && chunkData.ChunkZ == 0 && !FileAccess.FileExists($"{_chunksDirectory}/chunk_0_0.dat"))
                {
                    Logger.Log($"LoadChunk: Chunk inicial detectado, meshes generándose en background");
                }
                
                return chunk;
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error en LoadChunk: {ex.Message}");
                Logger.LogError($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
        
        /// <summary>
        /// Guarda los datos de un chunk en disco
        /// </summary>
        private async Task SaveChunkData(ChunkData chunkData)
        {
            string fileName = $"chunk_{chunkData.ChunkX}_{chunkData.ChunkZ}.dat";
            string filePath = Path.Combine(_chunksDirectory, fileName);
            
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                Logger.LogError($"Error al guardar chunk: {filePath}");
                return;
            }
            
            // Guardar datos básicos
            file.Store32((uint)chunkData.ChunkX);
            file.Store32((uint)chunkData.ChunkZ);
            file.Store32((uint)chunkData.Size);
            
            // Guardar mapa de alturas
            for (int x = 0; x < chunkData.Size; x++)
            {
                for (int z = 0; z < chunkData.Size; z++)
                {
                    file.StoreFloat(chunkData.GetHeight(x, z));
                }
            }
            
            file.Close();
            
            // Guardar en caché
            Vector2I chunkPos = new Vector2I(chunkData.ChunkX, chunkData.ChunkZ);
            _chunkDataCache[chunkPos] = chunkData;
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Carga los datos de un chunk desde disco
        /// </summary>
        private async Task<ChunkData> LoadChunkData(Vector2I chunkPos)
        {
            // Verificar que _chunksDirectory no sea null
            if (string.IsNullOrEmpty(_chunksDirectory))
            {
                Logger.LogError("TerrainManager: ❌ _chunksDirectory es null en LoadChunkData()");
                throw new System.Exception("_chunksDirectory no está inicializado - llame a InitializeWorld() primero");
            }
            
            string fileName = $"chunk_{chunkPos.X}_{chunkPos.Y}.dat";
            string filePath = Path.Combine(_chunksDirectory, fileName);
            
            if (!FileAccess.FileExists(filePath))
            {
                return null; // El chunk no existe
            }
            
            // Verificar caché primero
            if (_chunkDataCache.ContainsKey(chunkPos))
            {
                return _chunkDataCache[chunkPos];
            }
            
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Logger.LogError($"Error al cargar chunk: {filePath}");
                return null;
            }
            
            // Leer datos básicos
            int chunkX = (int)file.Get32();
            int chunkZ = (int)file.Get32();
            int size = (int)file.Get32();
            
            var chunkData = new ChunkData(chunkX, chunkZ, size);
            
            // Leer mapa de alturas
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    float height = file.GetFloat();
                    chunkData.SetHeight(x, z, height);
                }
            }
            
            file.Close();
            
            // Guardar en caché
            _chunkDataCache[chunkPos] = chunkData;
            
            await Task.CompletedTask;
            
            return chunkData;
        }
        
        /// <summary>
        /// Descarga un chunk liberando recursos
        /// </summary>
        public void UnloadChunk(Vector2I chunkPos)
        {
            if (_loadedChunks.ContainsKey(chunkPos))
            {
                Chunk chunk = _loadedChunks[chunkPos];
                chunk.Dispose();
                _loadedChunks.Remove(chunkPos);
                
                // Logger.Log($"Chunk {chunkPos.X},{chunkPos.Y} descargado");
            }
        }
        
        /// <summary>
        /// Obtiene chunks cargados alrededor de una posición (obsoleto, usar DynamicChunkLoader)
        /// </summary>
        public List<Chunk> GetLoadedChunksAround(Vector2I centerChunk)
        {
            var chunks = new List<Chunk>();
            
            for (int x = -LOAD_RADIUS; x <= LOAD_RADIUS; x++)
            {
                for (int z = -LOAD_RADIUS; z <= LOAD_RADIUS; z++)
                {
                    Vector2I chunkPos = new Vector2I(centerChunk.X + x, centerChunk.Y + z);
                    if (_loadedChunks.ContainsKey(chunkPos))
                    {
                        chunks.Add(_loadedChunks[chunkPos]);
                    }
                }
            }
            
            return chunks;
        }
        
        /// <summary>
        /// Obtiene todos los chunks actualmente cargados
        /// </summary>
        public Dictionary<Vector2I, Chunk> GetAllLoadedChunks()
        {
            return new Dictionary<Vector2I, Chunk>(_loadedChunks);
        }
        
        /// <summary>
        /// Verifica si un chunk está cargado
        /// </summary>
        public bool IsChunkLoaded(Vector2I chunkPos)
        {
            return _loadedChunks.ContainsKey(chunkPos);
        }
        
        /// <summary>
        /// Limpia todos los chunks cargados
        /// </summary>
        public void UnloadAllChunks()
        {
            foreach (var chunk in _loadedChunks.Values)
            {
                chunk.Dispose();
            }
            
            _loadedChunks.Clear();
            _chunkDataCache.Clear();
        }
        
        /// <summary>
        /// Obtiene el ChunkGenerator para consultas de altura
        /// </summary>
        public ChunkGenerator GetChunkGenerator()
        {
            return _chunkGenerator;
        }
        
        /// <summary>
        /// Obtiene la ruta del directorio de chunks
        /// </summary>
        public string GetChunksDirectory()
        {
            return _chunksDirectory;
        }
        
        /// <summary>
        /// Actualiza las barreras de un chunk y sus vecinos cuando se carga/descarga un chunk
        /// </summary>
        private void UpdateChunkBoundaries(Vector2I chunkPos)
        {
            // Actualizar barreras del chunk recién cargado
            UpdateSingleChunkBoundaries(chunkPos);
            
            // Actualizar barreras de los chunks vecinos
            var neighbors = GetNeighborPositions(chunkPos);
            foreach (var neighborPos in neighbors)
            {
                if (_loadedChunks.ContainsKey(neighborPos))
                {
                    UpdateSingleChunkBoundaries(neighborPos);
                }
            }
        }
        
        /// <summary>
        /// Actualiza las barreras de un solo chunk según los chunks vecinos cargados
        /// </summary>
        private void UpdateSingleChunkBoundaries(Vector2I chunkPos)
        {
            if (!_loadedChunks.ContainsKey(chunkPos))
                return;
                
            var chunk = _loadedChunks[chunkPos];
            
            // Eliminar barreras donde hay chunks vecinos cargados
            
            // Norte (Z + 1)
            var northNeighbor = new Vector2I(chunkPos.X, chunkPos.Y + 1);
            if (_loadedChunks.ContainsKey(northNeighbor))
            {
                chunk.RemoveBoundary("North");
            }
            
            // Sur (Z - 1)
            var southNeighbor = new Vector2I(chunkPos.X, chunkPos.Y - 1);
            if (_loadedChunks.ContainsKey(southNeighbor))
            {
                chunk.RemoveBoundary("South");
            }
            
            // Este (X + 1)
            var eastNeighbor = new Vector2I(chunkPos.X + 1, chunkPos.Y);
            if (_loadedChunks.ContainsKey(eastNeighbor))
            {
                chunk.RemoveBoundary("East");
            }
            
            // Oeste (X - 1)
            var westNeighbor = new Vector2I(chunkPos.X - 1, chunkPos.Y);
            if (_loadedChunks.ContainsKey(westNeighbor))
            {
                chunk.RemoveBoundary("West");
            }
        }
        
        /// <summary>
        /// Obtiene las posiciones de los chunks vecinos
        /// </summary>
        private Vector2I[] GetNeighborPositions(Vector2I chunkPos)
        {
            return new Vector2I[]
            {
                new Vector2I(chunkPos.X, chunkPos.Y + 1), // Norte
                new Vector2I(chunkPos.X, chunkPos.Y - 1), // Sur
                new Vector2I(chunkPos.X + 1, chunkPos.Y), // Este
                new Vector2I(chunkPos.X - 1, chunkPos.Y)  // Oeste
            };
        }
        
        /// <summary>
        /// Actualiza chunks cargados según posición del jugador y render distance
        /// </summary>
        public void UpdateChunksForPlayer(Vector3 playerPosition)
        {
            // Obtener render distance desde GameSettings
            var gameSettings = GetNode<GameSettings>("/root/GameSettings");
            float renderDistance = gameSettings.RenderDistanceMetres;
            
            // Actualizar visibilidad de sub-chunks en todos los chunks cargados
            foreach (var chunk in _loadedChunks.Values)
            {
                chunk.UpdateSubChunkVisibility(playerPosition, renderDistance);
            }
            
            // Log de rendimiento - solo cada 60 frames (aproximadamente cada segundo) para reducir spam
            if (Engine.GetFramesDrawn() % 60 == 0)
            {
                int totalSubChunks = _loadedChunks.Count * Chunk.SUB_CHUNKS_PER_SIDE * Chunk.SUB_CHUNKS_PER_SIDE;
                int visibleSubChunks = 0;
                foreach (var chunk in _loadedChunks.Values)
                {
                    visibleSubChunks += chunk.GetVisibleSubChunkCount();
                }
                
                // Logger.Log($"Render Distance: {renderDistance}m | Sub-chunks visibles: {visibleSubChunks}/{totalSubChunks}");
            }
        }
        
        /// <summary>
        /// Obtiene la altura del terreno en una posición específica del mundo
        /// </summary>
        /// <param name="worldX">Coordenada X del mundo</param>
        /// <param name="worldZ">Coordenada Z del mundo</param>
        /// <returns>Altura del terreno en la posición especificada</returns>
        public float GetTerrainHeightAt(float worldX, float worldZ)
        {
            try
            {
                if (_chunkGenerator == null)
                    return 0f;
                
                // Obtener altura usando el generador de ruido
                return _chunkGenerator.GetHeightAt(worldX, worldZ);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"TerrainManager: Error al obtener altura del terreno: {ex.Message}");
                return 0f;
            }
        }
        
        /// <summary>
        /// Limpia todos los chunks cargados de memoria
        /// </summary>
        public void CleanupAllChunks()
        {
            Logger.Log("TerrainManager: Iniciando limpieza de todos los chunks...");
            
            try
            {
                // Liberar todos los chunks cargados
                if (_loadedChunks != null)
                {
                    foreach (var chunkPair in _loadedChunks)
                    {
                        var chunk = chunkPair.Value;
                        if (chunk != null && IsInstanceValid(chunk))
                        {
                            chunk.QueueFree();
                        }
                    }
                    _loadedChunks.Clear();
                    Logger.Log($"TerrainManager: {_loadedChunks.Count} chunks liberados");
                }
                
                // Limpiar caché de datos
                if (_chunkDataCache != null)
                {
                    _chunkDataCache.Clear();
                    Logger.Log("TerrainManager: Caché de datos de chunks limpiada");
                }
                
                Logger.Log("TerrainManager: ✅ Limpieza de chunks completada");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"TerrainManager: Error durante limpieza de chunks: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Resetea el TerrainManager a su estado inicial
        /// </summary>
        public void Reset()
        {
            Logger.Log("TerrainManager: Reseteando a estado inicial...");
            
            try
            {
                // Limpiar chunks primero
                CleanupAllChunks();
                
                // Resetear estado de inicialización
                _isInitialized = false;
                
                // Limpiar referencias a directorios
                _worldDirectory = null;
                _chunksDirectory = null;
                
                // Resetear ChunkGenerator
                if (_chunkGenerator != null)
                {
                    _chunkGenerator.QueueFree();
                    _chunkGenerator = null;
                }
                
                // NOTA: No resetear el singleton aquí para evitar null references
                // El singleton se reseteará cuando se cree una nueva instancia
                
                Logger.Log("TerrainManager: ✅ Reset completado");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"TerrainManager: Error durante reset: {ex.Message}");
            }
        }
    }
}
