using System;
using Godot;
using Wild;

namespace Wild.Scripts.Terrain
{
    /// <summary>
    /// Representa un chunk de terreno en el mundo
    /// </summary>
    public partial class Chunk : Node3D
    {
        private ChunkData _data = null!;
        private StaticBody3D _staticBody = null!;
        private CollisionShape3D _collisionShape = null!;
        private SubChunk[,] _subChunks = null!;
        private Vector3 _worldPosition;
        
        // Constantes
        public const int SIZE = 100; // metros por chunk
        public const int SUB_CHUNKS_PER_SIDE = 10; // 10x10 sub-chunks
        public const int SUB_CHUNK_SIZE = 10; // metros por sub-chunk
        public const int VERTICES_PER_CHUNK = SIZE + 1; // 101 vértices para 100 quads
        public const float SCALE = 1f; // 1 metro por unidad = 1 metro
        
        public ChunkData Data => _data;
        public Vector2I ChunkPosition { get; private set; }
        
        public override void _Ready()
        {
            // El TerrainManager se obtendrá cuando se inicialice el chunk
            // CreateTerrainMesh() se llamará desde Initialize()
        }
        
        /// <summary>
        /// Inicializa el chunk con datos y posición mundial
        /// </summary>
        public async Task InitializeAsync(ChunkData data, Vector2I chunkPosition)
        {
            _data = data;
            ChunkPosition = chunkPosition;
            Name = $"Chunk_{chunkPosition.X}_{chunkPosition.Y}";
            
            // Calcular posición mundial del chunk
            _worldPosition = new Vector3(
                chunkPosition.X * SIZE * SCALE,
                0,
                chunkPosition.Y * SIZE * SCALE
            );
            
            Position = _worldPosition;
            
            // Crear sistema de sub-chunks y colisiones
            await CreateSubChunkSystemAsync(); // Esperar a que se cree el sistema
            CreateCollisionSystem();
            
            // Generar mallas de sub-chunks
            GenerateSubChunkMeshes();
            GenerateSubChunkMeshesAsync();
            CreateChunkBoundaries();
        }
        
        /// <summary>
        /// Crea el sistema de sub-chunks para renderizado optimizado (versión síncrona)
        /// </summary>
        private void CreateSubChunkSystem()
        {
            try
            {
                // Inicializar matriz de sub-chunks
                _subChunks = new SubChunk[SUB_CHUNKS_PER_SIDE, SUB_CHUNKS_PER_SIDE];
                
                // Crear sub-chunks
                for (int x = 0; x < SUB_CHUNKS_PER_SIDE; x++)
                {
                    for (int z = 0; z < SUB_CHUNKS_PER_SIDE; z++)
                    {
                        var subChunk = new SubChunk();
                        var localPos = new Vector2I(x, z);
                        subChunk.Initialize(localPos, _worldPosition);
                        AddChild(subChunk);
                        _subChunks[x, z] = subChunk;
                    }
                }
                
                Logger.Log($"Chunk: Sistema de sub-chunks creado");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Chunk: Error en CreateSubChunkSystem(): {ex.Message}");
                Logger.LogError($"Chunk: Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// Crea el sistema de sub-chunks para renderizado optimizado (versión asíncrona)
        /// </summary>
        private async Task CreateSubChunkSystemAsync()
        {
            try
            {
                // Inicializar matriz de sub-chunks
                _subChunks = new SubChunk[SUB_CHUNKS_PER_SIDE, SUB_CHUNKS_PER_SIDE];
                
                // Crear sub-chunks en background para evitar parones
                var subChunks = await Task.Run(() =>
                {
                    var chunks = new List<(SubChunk chunk, Vector2I localPos)>();
                    
                    for (int x = 0; x < SUB_CHUNKS_PER_SIDE; x++)
                    {
                        for (int z = 0; z < SUB_CHUNKS_PER_SIDE; z++)
                        {
                            var subChunk = new SubChunk();
                            var localPos = new Vector2I(x, z);
                            subChunk.Initialize(localPos, _worldPosition);
                            chunks.Add((subChunk, localPos));
                        }
                    }
                    
                    return chunks;
                });
                
                // Añadir sub-chunks a la escena en main thread (necesario para Godot)
                foreach (var (chunk, localPos) in subChunks)
                {
                    AddChild(chunk);
                    _subChunks[localPos.X, localPos.Y] = chunk;
                }
                
                // Logger.Log($"Chunk: Sistema de sub-chunks creado: {SUB_CHUNKS_PER_SIDE}x{SUB_CHUNKS_PER_SIDE} = {SUB_CHUNKS_PER_SIDE * SUB_CHUNKS_PER_SIDE} sub-chunks");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Chunk: Error en CreateSubChunkSystemAsync(): {ex.Message}");
                Logger.LogError($"Chunk: Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// Crea el sistema de colisiones para el chunk completo
        /// </summary>
        private void CreateCollisionSystem()
        {
            // Crear StaticBody3D para colisiones del terreno
            _staticBody = new StaticBody3D();
            _staticBody.Name = "TerrainCollision";
            AddChild(_staticBody);
            
            // Crear CollisionShape3D para el terreno
            _collisionShape = new CollisionShape3D();
            _collisionShape.Name = "Shape";
            _staticBody.AddChild(_collisionShape);
        }
        
        /// <summary>
        /// Genera las mallas de todos los sub-chunks
        /// </summary>
        private void GenerateSubChunkMeshes()
        {
            try
            {
                // Logger.Log($"Chunk: Generando mallas de sub-chunks para chunk {ChunkPosition}");
                var chunkOffset = new Vector2I(_data.ChunkX, _data.ChunkZ);
                
                for (int x = 0; x < SUB_CHUNKS_PER_SIDE; x++)
                {
                    for (int z = 0; z < SUB_CHUNKS_PER_SIDE; z++)
                    {
                        _subChunks[x, z].GenerateMesh(_data, chunkOffset);
                    }
                }
                
                // Logger.Log($"Chunk: Mallas de sub-chunks generadas para chunk {ChunkPosition}");
                
                // Crear colisiones para el chunk completo
                _ = CreateCollisionShapeForChunkAsync();
                
                // Logger.Log($"Chunk: Colisiones creadas para chunk {ChunkPosition}");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Chunk: Error en GenerateSubChunkMeshes(): {ex.Message}");
                Logger.LogError($"Chunk: Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// Genera las mallas de todos los sub-chunks (versión asíncrona)
        /// </summary>
        private async Task GenerateSubChunkMeshesAsync()
        {
            try
            {
                // Logger.Log($"Chunk: Generando mallas de sub-chunks para chunk {ChunkPosition}");
                var chunkOffset = new Vector2I(_data.ChunkX, _data.ChunkZ);
                
                // Generar meshes en background para evitar parones
                await Task.Run(() =>
                {
                    for (int x = 0; x < SUB_CHUNKS_PER_SIDE; x++)
                    {
                        for (int z = 0; z < SUB_CHUNKS_PER_SIDE; z++)
                        {
                            _subChunks[x, z].GenerateMesh(_data, chunkOffset);
                        }
                    }
                });
                
                // Logger.Log($"Chunk: Mallas de sub-chunks generadas para chunk {ChunkPosition}");
                
                // Crear colisiones para el chunk completo (en main thread)
                _ = CreateCollisionShapeForChunkAsync();
                // Logger.Log($"Chunk: Colisiones creadas para chunk {ChunkPosition}");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Chunk: Error en GenerateSubChunkMeshesAsync(): {ex.Message}");
                Logger.LogError($"Chunk: Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// Crea barreras invisibles alrededor del chunk para impedir que el jugador salga
        /// </summary>
        private void CreateChunkBoundaries()
        {
            // Crear StaticBody3D para barreras físicas (bloqueo real)
            var boundaryBody = new StaticBody3D();
            boundaryBody.Name = "ChunkBoundaries";
            boundaryBody.CollisionLayer = 2; // Capa 2 para barreras de terreno
            boundaryBody.CollisionMask = 0; // No necesita detectar, solo bloquear
            AddChild(boundaryBody);
            
            // Dimensiones del chunk
            float chunkSize = SIZE * SCALE;
            float wallHeight = 1200f; // Altura para cubrir rango completo (-100 a 1000m)
            float wallThickness = 1f; // Grosor mínimo pero efectivo
            
            // Crear las 4 barreras físicas
            CreateBoundaryWall(boundaryBody, "North", new Vector3(chunkSize/2, wallHeight/2, chunkSize), 
                              new Vector3(chunkSize, wallHeight, wallThickness));
            
            CreateBoundaryWall(boundaryBody, "South", new Vector3(chunkSize/2, wallHeight/2, 0), 
                              new Vector3(chunkSize, wallHeight, wallThickness));
            
            CreateBoundaryWall(boundaryBody, "East", new Vector3(chunkSize, wallHeight/2, chunkSize/2), 
                              new Vector3(wallThickness, wallHeight, chunkSize));
            
            CreateBoundaryWall(boundaryBody, "West", new Vector3(0, wallHeight/2, chunkSize/2), 
                              new Vector3(wallThickness, wallHeight, chunkSize));
        }
        
        /// <summary>
        /// Crea una barrera invisible con colisión física
        /// </summary>
        private void CreateBoundaryWall(StaticBody3D parent, string direction, Vector3 position, Vector3 size)
        {
            var collisionShape = new CollisionShape3D();
            var boxShape = new BoxShape3D();
            boxShape.Size = size;
            collisionShape.Shape = boxShape;
            collisionShape.Position = position;
            collisionShape.Name = $"Boundary_{direction}";
            parent.AddChild(collisionShape);
        }
        
        /// <summary>
        /// Elimina una barrera específica (usado cuando se carga un chunk adyacente)
        /// </summary>
        public void RemoveBoundary(string direction)
        {
            var boundaryBody = GetNode<StaticBody3D>("ChunkBoundaries");
            if (boundaryBody != null)
            {
                var boundary = boundaryBody.GetNode<CollisionShape3D>($"Boundary_{direction}");
                if (boundary != null)
                {
                    boundary.QueueFree();
                }
            }
        }
        
        /// <summary>
        /// Elimina todas las barreras (usado cuando se descarga el chunk)
        /// </summary>
        public void RemoveAllBoundaries()
        {
            var boundaryBody = GetNode<StaticBody3D>("ChunkBoundaries");
            if (boundaryBody != null)
            {
                boundaryBody.QueueFree();
            }
        }
        
        /// <summary>
        /// Actualiza la visibilidad de los sub-chunks según la distancia al jugador
        /// </summary>
        public void UpdateSubChunkVisibility(Vector3 playerPosition, float renderDistance)
        {
            float renderDistanceSquared = renderDistance * renderDistance;
            
            for (int x = 0; x < SUB_CHUNKS_PER_SIDE; x++)
            {
                for (int z = 0; z < SUB_CHUNKS_PER_SIDE; z++)
                {
                    try
                    {
                        var subChunk = _subChunks[x, z];
                        
                        // Verificar si el sub-chunk no está disposed
                        if (subChunk == null || !IsInstanceValid(subChunk))
                        {
                            continue;
                        }
                        
                        // Calcular distancia solo en plano XZ (ignorar altura)
                        Vector3 playerPos2D = new Vector3(playerPosition.X, 0, playerPosition.Z);
                        Vector3 subChunkCenter2D = new Vector3(subChunk.WorldCenter.X, 0, subChunk.WorldCenter.Z);
                        float distanceSquared = playerPos2D.DistanceSquaredTo(subChunkCenter2D);
                        
                        bool shouldBeVisible = distanceSquared <= renderDistanceSquared;
                        
                        if (subChunk.IsSubChunkVisible != shouldBeVisible)
                        {
                            subChunk.SetVisible(shouldBeVisible);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogWarning($"Chunk: Error actualizando visibilidad de sub-chunk [{x},{z}]: {ex.Message}");
                        // Continuar con los demás sub-chunks
                    }
                }
            }
        }
        
        /// <summary>
        /// Obtiene el número de sub-chunks visibles
        /// </summary>
        public int GetVisibleSubChunkCount()
        {
            int count = 0;
            for (int x = 0; x < SUB_CHUNKS_PER_SIDE; x++)
            {
                for (int z = 0; z < SUB_CHUNKS_PER_SIDE; z++)
                {
                    if (_subChunks[x, z].IsSubChunkVisible)
                        count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// Crea la forma de colisión para el chunk completo (versión asíncrona)
        /// </summary>
        private async Task CreateCollisionShapeForChunkAsync()
        {
            try
            {
                // Generar mesh completo en background thread
                var arrayMesh = await Task.Run(() =>
                {
                    var st = new SurfaceTool();
                    st.Begin(Mesh.PrimitiveType.Triangles);
                    GenerateTerrainVertices(st);
                    st.Index();
                    
                    var mesh = new ArrayMesh();
                    st.Commit(mesh);
                    return mesh;
                });

                // Crear colisiones directamente (esto es rápido en main thread)
                var collisionShape = arrayMesh.CreateTrimeshShape();
                _collisionShape.Shape = collisionShape;
                
                // Logger.Log($"Chunk: Colisiones asíncronas creadas para chunk {ChunkPosition}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Chunk: Error en CreateCollisionShapeForChunkAsync(): {ex.Message}");
            }
        }
        
        /// <summary>
        /// Genera los vértices del terreno completo (solo para colisiones)
        /// </summary>
        private void GenerateTerrainVertices(SurfaceTool surfaceTool)
        {
            int size = _data.Size; // Ahora es CHUNK_SIZE + 1 = 101
            
            for (int x = 0; x < size - 1; x++) // 0 a 99 para 100 quads
            {
                for (int z = 0; z < size - 1; z++) // 0 a 99 para 100 quads
                {
                    // Obtener alturas de los 4 vértices del quad
                    float h00 = _data.GetHeight(x, z);
                    float h10 = _data.GetHeight(x + 1, z);
                    float h01 = _data.GetHeight(x, z + 1);
                    float h11 = _data.GetHeight(x + 1, z + 1);
                    
                    // Posiciones de los vértices
                    Vector3 v00 = new Vector3(x * SCALE, h00, z * SCALE);
                    Vector3 v10 = new Vector3((x + 1) * SCALE, h10, z * SCALE);
                    Vector3 v01 = new Vector3(x * SCALE, h01, (z + 1) * SCALE);
                    Vector3 v11 = new Vector3((x + 1) * SCALE, h11, (z + 1) * SCALE);
                    
                    // Calcular normales
                    Vector3 normal = CalculateNormal(v00, v10, v01);
                    
                    // Coordenadas UV
                    Vector2 uv00 = new Vector2(x * 0.1f, z * 0.1f);
                    Vector2 uv10 = new Vector2((x + 1) * 0.1f, z * 0.1f);
                    Vector2 uv01 = new Vector2(x * 0.1f, (z + 1) * 0.1f);
                    Vector2 uv11 = new Vector2((x + 1) * 0.1f, (z + 1) * 0.1f);
                    
                    // Primer triángulo
                    surfaceTool.SetNormal(normal);
                    surfaceTool.SetUV(uv00);
                    surfaceTool.AddVertex(v00);
                    
                    surfaceTool.SetNormal(normal);
                    surfaceTool.SetUV(uv10);
                    surfaceTool.AddVertex(v10);
                    
                    surfaceTool.SetNormal(normal);
                    surfaceTool.SetUV(uv01);
                    surfaceTool.AddVertex(v01);
                    
                    // Segundo triángulo
                    surfaceTool.SetNormal(normal);
                    surfaceTool.SetUV(uv10);
                    surfaceTool.AddVertex(v10);
                    
                    surfaceTool.SetNormal(normal);
                    surfaceTool.SetUV(uv11);
                    surfaceTool.AddVertex(v11);
                    
                    surfaceTool.SetNormal(normal);
                    surfaceTool.SetUV(uv01);
                    surfaceTool.AddVertex(v01);
                }
            }
        }
        
        /// <summary>
        /// Calcula la normal de un triángulo para iluminación correcta
        /// </summary>
        private Vector3 CalculateNormal(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;
            return edge1.Cross(edge2).Normalized();
        }
        
        
        /// <summary>
        /// Libera recursos del chunk
        /// </summary>
        public void Dispose()
        {
            QueueFree();
        }
    }
}
