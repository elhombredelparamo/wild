using System;
using Godot;
using Wild;

namespace Wild.Scripts.Terrain
{
    /// <summary>
    /// Representa un sub-chunk de 10x10m para renderizado optimizado
    /// </summary>
    public partial class SubChunk : Node3D
    {
        private MeshInstance3D _meshInstance;
        private ArrayMesh _mesh;
        private bool _isVisible;
        private Vector2I _localPosition; // Posición dentro del chunk (0-9, 0-9)
        private Vector3 _worldCenter;
        
        // Constantes
        public const int SIZE = 10; // 10x10 metros
        public const int VERTICES_PER_SIDE = 11; // 10 metros + 1 vértice extra
        
        public Vector2I LocalPosition => _localPosition;
        public Vector3 WorldCenter => _worldCenter;
        public bool IsSubChunkVisible => _isVisible;
        
        public override void _Ready()
        {
            // _meshInstance se inicializará en Initialize() para evitar problemas de orden
        }
        
        /// <summary>
        /// Inicializa el sub-chunk
        /// </summary>
        public void Initialize(Vector2I localPosition, Vector3 chunkWorldPosition)
        {
            _localPosition = localPosition;
            
            // Crear MeshInstance3D aquí para asegurar que existe
            _meshInstance = new MeshInstance3D();
            _meshInstance.Name = "SubChunkMesh";
            AddChild(_meshInstance);
            
            // Calcular posición mundial del centro del sub-chunk
            float worldX = chunkWorldPosition.X + (localPosition.X * SIZE) + (SIZE / 2f);
            float worldZ = chunkWorldPosition.Z + (localPosition.Y * SIZE) + (SIZE / 2f);
            _worldCenter = new Vector3(worldX, 0, worldZ);
            
            Position = new Vector3(localPosition.X * SIZE, 0, localPosition.Y * SIZE);
            Name = $"SubChunk_{localPosition.X}_{localPosition.Y}";
            
            // Por defecto visible durante depuración - cambiar a false después
            Visible = true;
            _isVisible = true;
        }
        
        /// <summary>
        /// Genera la malla del sub-chunk con datos de altura
        /// </summary>
        public void GenerateMesh(ChunkData chunkData, Vector2I chunkOffset)
        {
            try
            {
                var surfaceTool = new SurfaceTool();
                surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
                
                // Generar vértices para este sub-chunk
                int startZ = _localPosition.Y * (VERTICES_PER_SIDE - 1);
                int startX = _localPosition.X * (VERTICES_PER_SIDE - 1);
                
                int vertexCount = 0;
                for (int localX = 0; localX < VERTICES_PER_SIDE - 1; localX++)
                {
                    for (int localZ = 0; localZ < VERTICES_PER_SIDE - 1; localZ++)
                    {
                        // Coordenadas globales en el chunk
                        int globalX = startX + localX;
                        int globalZ = startZ + localZ;
                        
                        // Obtener alturas de los 4 vértices del quad
                        float h00 = chunkData.GetHeight(globalX, globalZ);
                        float h10 = chunkData.GetHeight(globalX + 1, globalZ);
                        float h01 = chunkData.GetHeight(globalX, globalZ + 1);
                        float h11 = chunkData.GetHeight(globalX + 1, globalZ + 1);
                        
                        // Posiciones locales de los vértices
                        Vector3 v00 = new Vector3(localX * 1f, h00, localZ * 1f);
                        Vector3 v10 = new Vector3((localX + 1) * 1f, h10, localZ * 1f);
                        Vector3 v01 = new Vector3(localX * 1f, h01, (localZ + 1) * 1f);
                        Vector3 v11 = new Vector3((localX + 1) * 1f, h11, (localZ + 1) * 1f);
                        
                        // Calcular normales
                        Vector3 normal = CalculateNormal(v00, v10, v01);
                        
                        // Coordenadas UV (repetir textura cada 10 metros)
                        Vector2 uv00 = new Vector2((globalX + chunkOffset.X * Chunk.SIZE) * 0.1f, 
                                                 (globalZ + chunkOffset.Y * Chunk.SIZE) * 0.1f);
                        Vector2 uv10 = new Vector2((globalX + 1 + chunkOffset.X * Chunk.SIZE) * 0.1f, 
                                                 (globalZ + chunkOffset.Y * Chunk.SIZE) * 0.1f);
                        Vector2 uv01 = new Vector2((globalX + chunkOffset.X * Chunk.SIZE) * 0.1f, 
                                                 (globalZ + 1 + chunkOffset.Y * Chunk.SIZE) * 0.1f);
                        Vector2 uv11 = new Vector2((globalX + 1 + chunkOffset.X * Chunk.SIZE) * 0.1f, 
                                                 (globalZ + 1 + chunkOffset.Y * Chunk.SIZE) * 0.1f);
                        
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
                        
                        vertexCount += 6; // 2 triángulos = 6 vértices
                    }
                }
                
                surfaceTool.Index();
                _mesh = new ArrayMesh();
                surfaceTool.Commit(_mesh);
                
                _meshInstance.Mesh = _mesh;
                
                // Asignar material - versión ultra visible para debugging
                var material = new StandardMaterial3D();
                var texture = GD.Load<Texture2D>("res://assets/textures/Grass004.png");
                
                if (texture == null)
                {
                    Logger.LogError($"SubChunk [{_localPosition.X},{_localPosition.Y}]: ERROR - Textura Grass004.png no cargada");
                    // Usar color sólido si no hay textura
                    material.AlbedoColor = Colors.LimeGreen;
                }
                else
                {
                    material.AlbedoTexture = texture;
                    // Color blanco para mostrar la textura original con Unshaded
                    material.AlbedoColor = Colors.White;
                }
                
                material.Uv1Scale = new Vector3(10f, 10f, 1f);
                material.Roughness = 0.8f; // Más rugoso para terreno
                material.Metallic = 0f;
                material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear;
                material.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
                material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; // SIN ILUMINACIÓN - color puro
                material.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Ambas caras visibles
                
                // Desactivar emisión para debugging
                material.EmissionEnabled = false;
                material.Emission = Colors.Black;
                
                _meshInstance.MaterialOverride = material;
                
                // Forzar visibilidad
                Visible = true;
                if (_meshInstance != null)
                {
                    _meshInstance.Visible = true;
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"SubChunk [{_localPosition.X},{_localPosition.Y}]: Error generando malla: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Establece visibilidad del sub-chunk
        /// </summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            Visible = visible;
            if (_meshInstance != null)
            {
                _meshInstance.Visible = visible;
            }
        }
        
        /// <summary>
        /// Calcula la normal de un triángulo
        /// </summary>
        private Vector3 CalculateNormal(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;
            return edge1.Cross(edge2).Normalized();
        }
        
        /// <summary>
        /// Obtiene la distancia al cuadrado a un punto mundial (más eficiente)
        /// </summary>
        public float GetDistanceSquaredTo(Vector3 worldPosition)
        {
            return _worldCenter.DistanceSquaredTo(worldPosition);
        }
        
        /// <summary>
        /// Obtiene la distancia a un punto mundial
        /// </summary>
        public float GetDistanceTo(Vector3 worldPosition)
        {
            return _worldCenter.DistanceTo(worldPosition);
        }
        
        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            QueueFree();
        }
    }
}
