using Godot;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Wild.Scripts.Terrain;

namespace Wild.Scripts.Character;

/// <summary>
/// Sistema para cargar dinámicamente modelos .obj externos y crear maniquíes
/// </summary>
public partial class MannequinSpawner : Node3D
{
    private TerrainManager _terrainManager;
    private readonly string _assetsBasePath = @"C:\Users\MGA-ADMIN\Desktop\canteraAssets\Mesh\";
    
    // Partes del modelo survivor que cargaremos
    private readonly string[] _modelParts = {
        "survivor_head.obj",
        "survivor_shirt.obj",
        "survivor_pants.obj",
        "survivor_shoes.obj"
    };
    
    public override void _Ready()
    {
        Logger.Log("🎭 MannequinSpawner: Inicializando sistema de maniquíes");
        
        // Obtener referencia al TerrainManager
        _terrainManager = GetNode<TerrainManager>("../TerrainManager");
        if (_terrainManager == null)
        {
            Logger.LogError("🎭 MannequinSpawner: No se encontró TerrainManager");
        }
    }
    
    /// <summary>
    /// Crea un maniquí en las coordenadas especificadas
    /// </summary>
    /// <param name="x">Coordenada X</param>
    /// <param name="z">Coordenada Z</param>
    /// <param name="name">Nombre del maniquí</param>
    /// <returns>El nodo contenedor del maniquí o null si falla</returns>
    public async Task<Node3D> SpawnMannequinAsync(float x, float z, string name = "Mannequin")
    {
        Logger.Log($"🎭 MannequinSpawner: Creando maniquí '{name}' en coordenadas ({x}, {z})");
        
        try
        {
            // Obtener altura del terreno en las coordenadas especificadas
            float terrainHeight = 0f;
            if (_terrainManager != null)
            {
                terrainHeight = _terrainManager.GetTerrainHeightAt(x, z);
                Logger.Log($"🎭 MannequinSpawner: Altura del terreno en ({x}, {z}): {terrainHeight}");
            }
            else
            {
                Logger.LogWarning("🎭 MannequinSpawner: TerrainManager no disponible, usando altura 0");
            }
            
            // Crear nodo contenedor para el maniquí
            var mannequinContainer = new Node3D
            {
                Name = name
            };
            
            // Posicionar el contenedor en las coordenadas correctas
            mannequinContainer.Position = new Vector3(x, terrainHeight, z);
            
            // Cargar cada parte del modelo
            foreach (var partFile in _modelParts)
            {
                var partNode = await LoadObjPartAsync(partFile);
                if (partNode != null)
                {
                    mannequinContainer.AddChild(partNode);
                    Logger.Log($"🎭 MannequinSpawner: Cargada parte '{partFile}'");
                }
                else
                {
                    Logger.LogWarning($"🎭 MannequinSpawner: No se pudo cargar la parte '{partFile}'");
                }
            }
            
            // Añadir el maniquí a la escena
            AddChild(mannequinContainer);
            
            Logger.Log($"🎭 MannequinSpawner: ✅ Maniquí '{name}' creado exitosamente");
            return mannequinContainer;
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"🎭 MannequinSpawner: ❌ Error al crear maniquí: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Carga una parte específica del modelo .obj de forma asíncrona
    /// </summary>
    /// <param name="filename">Nombre del archivo .obj</param>
    /// <returns>Node3D con la malla cargada o null si falla</returns>
    private async Task<Node3D> LoadObjPartAsync(string filename)
    {
        try
        {
            string fullPath = Path.Combine(_assetsBasePath, filename);
            
            // Verificar que el archivo existe
            if (!File.Exists(fullPath))
            {
                Logger.LogError($"🎭 MannequinSpawner: No existe el archivo {fullPath}");
                return null;
            }
            
            Logger.Log($"🎭 MannequinSpawner: Cargando {filename} desde ruta externa");
            
            // Godot no puede cargar .obj directamente desde rutas externas fácilmente
            // Necesitamos leer el contenido y procesarlo manualmente
            var mesh = await ParseObjFileAsync(fullPath);
            if (mesh == null)
            {
                return null;
            }
            
            // Crear MeshInstance3D para visualizar la malla
            var meshInstance = new MeshInstance3D
            {
                Name = Path.GetFileNameWithoutExtension(filename),
                Mesh = mesh
            };
            
            // Añadir material básico
            var material = new StandardMaterial3D
            {
                AlbedoColor = Colors.Gray,
                Metallic = 0.1f,
                Roughness = 0.8f
            };
            meshInstance.MaterialOverride = material;
            
            return meshInstance;
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"🎭 MannequinSpawner: Error al cargar {filename}: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Parsea un archivo .obj y crea una ArrayMesh
    /// </summary>
    /// <param name="filePath">Ruta al archivo .obj</param>
    /// <returns>ArrayMesh con la geometría parseada o null si falla</returns>
    private async Task<ArrayMesh> ParseObjFileAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                var indices = new List<int>();
                
                string[] lines = File.ReadAllLines(filePath);
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                    
                    string[] parts = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0)
                        continue;
                    
                    switch (parts[0])
                    {
                        case "v":
                            if (parts.Length >= 4)
                            {
                                float x = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                                float y = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                                float z = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                                vertices.Add(new Vector3(x, y, z));
                            }
                            break;
                            
                        case "vn":
                            if (parts.Length >= 4)
                            {
                                float nx = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                                float ny = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                                float nz = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                                normals.Add(new Vector3(nx, ny, nz));
                            }
                            break;
                            
                        case "vt":
                            if (parts.Length >= 3)
                            {
                                float u = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                                float v = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                                uvs.Add(new Vector2(u, v));
                            }
                            break;
                            
                        case "f":
                            if (parts.Length >= 4)
                            {
                                // Procesar caras triangulares o cuadrangulares
                                for (int i = 1; i < parts.Length - 1; i++)
                                {
                                    // Triangular si es cuadrángulo
                                    ProcessFaceVertex(parts[1], vertices.Count, indices);
                                    ProcessFaceVertex(parts[i + 1], vertices.Count, indices);
                                    ProcessFaceVertex(parts[i], vertices.Count, indices);
                                }
                            }
                            break;
                    }
                }
                
                if (vertices.Count == 0)
                {
                    Logger.LogWarning($"🎭 MannequinSpawner: No se encontraron vértices en {filePath}");
                    return null;
                }
                
                // Crear ArrayMesh
                var mesh = new ArrayMesh();
                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                
                // Vertices
                var vertexArray = new Vector3[vertices.Count];
                vertices.CopyTo(vertexArray);
                arrays[(int)Mesh.ArrayType.Vertex] = vertexArray;
                
                // Índices
                if (indices.Count > 0)
                {
                    var indexArray = new int[indices.Count];
                    indices.CopyTo(indexArray);
                    arrays[(int)Mesh.ArrayType.Index] = indexArray;
                }
                
                // Normales (si existen)
                if (normals.Count > 0)
                {
                    var normalArray = new Vector3[normals.Count];
                    normals.CopyTo(normalArray);
                    arrays[(int)Mesh.ArrayType.Normal] = normalArray;
                }
                
                // UVs (si existen)
                if (uvs.Count > 0)
                {
                    var uvArray = new Vector2[uvs.Count];
                    uvs.CopyTo(uvArray);
                    arrays[(int)Mesh.ArrayType.TexUV] = uvArray;
                }
                
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                
                Logger.Log($"🎭 MannequinSpawner: Parseado {filePath} - {vertices.Count} vértices, {indices.Count/3} triángulos");
                return mesh;
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"🎭 MannequinSpawner: Error al parsear {filePath}: {ex.Message}");
                return null;
            }
        });
    }
    
    /// <summary>
    /// Procesa un vértice de cara (formato v/vt/vn o v//vn o v/vt)
    /// </summary>
    private void ProcessFaceVertex(string faceVertex, int vertexCount, List<int> indices)
    {
        string[] parts = faceVertex.Split('/');
        
        // El primer número es siempre el índice del vértice
        if (int.TryParse(parts[0], out int vertexIndex))
        {
            // Los archivos .obj usan índices basados en 1
            indices.Add(vertexIndex - 1);
        }
    }
    
    /// <summary>
    /// Método de conveniencia para crear un maniquí en coordenadas 50,50
    /// </summary>
    public async Task<Node3D> SpawnTestMannequinAsync()
    {
        return await SpawnMannequinAsync(50f, 50f, "TestMannequin");
    }
}
