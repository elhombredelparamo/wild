using System;
using Godot;
using Wild;

namespace Wild.Scripts.Terrain
{
    /// <summary>
    /// Generador procedural de terreno usando ruido Perlin
    /// </summary>
    public partial class ChunkGenerator : Node
    {
        private FastNoiseLite _noise;
        private const float HEIGHT_MIN = -100f;
        private const float HEIGHT_MAX = 1000f;
        private const int CHUNK_SIZE = 100;
        
        public override void _Ready()
        {
            InitializeNoise();
        }
        
        /// <summary>
        /// Inicializa el generador de ruido Perlin
        /// </summary>
        private void InitializeNoise()
        {
            _noise = new FastNoiseLite();
            _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _noise.Frequency = 0.001f; // Escala del terreno
            _noise.FractalOctaves = 4;
            _noise.FractalGain = 0.5f;
            _noise.FractalLacunarity = 2.0f;
            _noise.Seed = (int)GD.Randi(); // Semilla aleatoria por defecto
        }
        
        /// <summary>
        /// Establece la semilla para la generación
        /// </summary>
        public void SetSeed(int seed)
        {
            // Inicializar _noise si no está inicializado
            if (_noise == null)
            {
                InitializeNoise();
            }
            
            _noise.Seed = seed;
        }
        
        /// <summary>
        /// Genera los datos de un chunk completo
        /// </summary>
        public ChunkData GenerateChunk(int chunkX, int chunkZ)
        {
            var chunkData = new ChunkData(chunkX, chunkZ, CHUNK_SIZE + 1); // +1 para el vértice extra en el borde
            
            // Logger.Log($"ChunkGenerator: Generando chunk ({chunkX}, {chunkZ})");
            
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            int sampleCount = 0;
            float totalHeight = 0f;
            
            for (int x = 0; x < CHUNK_SIZE + 1; x++) // Incluir vértice extra
            {
                for (int z = 0; z < CHUNK_SIZE + 1; z++) // Incluir vértice extra
                {
                    // Convertir coordenadas locales a mundiales para el ruido
                    float worldX = chunkX * CHUNK_SIZE + x;
                    float worldZ = chunkZ * CHUNK_SIZE + z;
                    
                    // Generar altura usando ruido Perlin
                    float noiseValue = _noise.GetNoise2D(worldX, worldZ);
                    
                    // Normalizar de [-1, 1] a [0, 1]
                    float normalizedValue = (noiseValue + 1f) / 2f;
                    
                    // Mapear al rango de alturas [-100, 1000]
                    float height = Mathf.Lerp(HEIGHT_MIN, HEIGHT_MAX, normalizedValue);
                    
                    chunkData.SetHeight(x, z, height);
                    
                    // Estadísticas (sin logging individual para reducir spam)
                    minHeight = Mathf.Min(minHeight, height);
                    maxHeight = Mathf.Max(maxHeight, height);
                    totalHeight += height;
                    sampleCount++;
                }
            }
            
            float avgHeight = totalHeight / sampleCount;
            // Logger.Log($"ChunkGenerator: Chunk ({chunkX}, {chunkZ}) - Alturas: min={minHeight:F2}, max={maxHeight:F2}, avg={avgHeight:F2}");
            
            return chunkData;
        }
        
        /// <summary>
        /// Genera la altura para un punto específico del mundo
        /// </summary>
        public float GetHeightAt(float worldX, float worldZ)
        {
            float noiseValue = _noise.GetNoise2D(worldX, worldZ);
            float normalizedValue = (noiseValue + 1f) / 2f;
            return Mathf.Lerp(HEIGHT_MIN, HEIGHT_MAX, normalizedValue);
        }
    }
}
