using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileAccess = Godot.FileAccess;

namespace Wild;

/// <summary>
/// Sistema de gestión de mundos/partidas.
/// Cada mundo tiene su propio directorio con todos los datos.
/// </summary>
public partial class WorldManager : Node
{
    public const string WORLDS_FOLDER = "user://worlds/";
    public const string WORLD_INFO_FILE = "world_info.json";
    
    private static WorldManager? _instance;
    public static WorldManager Instance => _instance ?? throw new InvalidOperationException("WorldManager no inicializado");
    
    private JsonSerializerOptions _jsonOptions = null!;
    
    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PrintErr("WorldManager: Ya existe una instancia");
            QueueFree();
            return;
        }
        
        _instance = this;
        
        // Configurar opciones JSON
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        
        // Crear carpeta de mundos si no existe
        EnsureWorldsFolderExists();
        
        Logger.Log("WorldManager: Sistema de mundos inicializado correctamente");
    }
    
    public override void _ExitTree()
    {
        _instance = null;
    }
    
    /// <summary>Asegura que la carpeta de mundos exista.</summary>
    private void EnsureWorldsFolderExists()
    {
        if (!DirAccess.DirExistsAbsolute(WORLDS_FOLDER))
        {
            var dir = DirAccess.Open("user://");
            if (dir != null)
            {
                dir.MakeDirRecursive("worlds");
                Logger.Log("WorldManager: Carpeta de mundos creada");
            }
            else
            {
                Logger.LogError("WorldManager: Error al crear carpeta de mundos");
            }
        }
    }
    
    /// <summary>Crea un nuevo mundo con el nombre especificado.</summary>
    public WorldInfo? CreateWorld(string worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName))
        {
            worldName = $"Mundo_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
        
        // Verificar si ya existe un mundo con ese nombre
        if (WorldExists(worldName))
        {
            Logger.LogError($"WorldManager: Ya existe un mundo con el nombre: {worldName}");
            return null;
        }
        
        try
        {
            // Crear directorio del mundo
            string worldPath = GetWorldPath(worldName);
            var dir = DirAccess.Open("user://worlds");
            if (dir == null)
            {
                Logger.LogError("WorldManager: Error al acceder a carpeta de mundos");
                return null;
            }
            
            dir.MakeDirRecursive(worldName);
            
            // Crear información del mundo
            var worldInfo = new WorldInfo
            {
                Name = worldName,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Seed = GenerateRandomSeed(),
                Version = "0.1.3",
                PlayTimeSeconds = 0
            };
            
            // Guardar información del mundo
            string infoPath = Path.Combine(worldPath, WORLD_INFO_FILE);
            string jsonString = JsonSerializer.Serialize(worldInfo, _jsonOptions);
            
            var file = FileAccess.Open(infoPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                Logger.LogError($"WorldManager: Error al crear archivo de información: {infoPath}");
                return null;
            }
            
            file.StoreString(jsonString);
            file.Close();
            
            // Crear estructura básica del mundo
            CreateWorldStructure(worldPath);
            
            Logger.Log($"WorldManager: Mundo creado correctamente: {worldName}");
            return worldInfo;
        }
        catch (Exception ex)
        {
            Logger.LogError($"WorldManager: Error al crear mundo: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>Carga la información de un mundo existente.</summary>
    public WorldInfo? LoadWorldInfo(string worldName)
    {
        string infoPath = GetWorldInfoPath(worldName);
        
        if (!FileAccess.FileExists(infoPath))
        {
            Logger.LogError($"WorldManager: No existe el mundo: {worldName}");
            return null;
        }
        
        try
        {
            var file = FileAccess.Open(infoPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Logger.LogError($"WorldManager: Error al leer información del mundo: {worldName}");
                return null;
            }
            
            string jsonString = file.GetAsText();
            file.Close();
            
            var worldInfo = JsonSerializer.Deserialize<WorldInfo>(jsonString, _jsonOptions);
            
            if (worldInfo != null)
            {
                Logger.Log($"WorldManager: Mundo cargado: {worldName}");
            }
            
            return worldInfo;
        }
        catch (Exception ex)
        {
            Logger.LogError($"WorldManager: Error al cargar información del mundo: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>Elimina un mundo completamente.</summary>
    public bool DeleteWorld(string worldName)
    {
        if (!WorldExists(worldName))
        {
            Logger.Log($"WorldManager: El mundo no existe: {worldName}");
            return true; // No hay nada que eliminar
        }
        
        try
        {
            string worldPath = GetWorldPath(worldName);
            var dir = DirAccess.Open("user://worlds");
            if (dir != null)
            {
                // Eliminar recursivamente el directorio del mundo
                bool success = RemoveDirectoryRecursive(dir, worldName);
                if (success)
                {
                    Logger.Log($"WorldManager: Mundo eliminado correctamente: {worldName}");
                }
                else
                {
                    Logger.LogError($"WorldManager: Error al eliminar mundo: {worldName}");
                }
                return success;
            }
            
            Logger.LogError("WorldManager: Error al acceder a carpeta de mundos");
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"WorldManager: Error al eliminar mundo: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>Obtiene la lista de todos los mundos disponibles.</summary>
    public List<WorldInfo> GetAllWorlds()
    {
        var worlds = new List<WorldInfo>();
        
        try
        {
            var dir = DirAccess.Open("user://worlds");
            if (dir == null)
            {
                Logger.LogError("WorldManager: Error al acceder a carpeta de mundos");
                return worlds;
            }
            
            dir.ListDirBegin();
            string worldName = dir.GetNext();
            
            while (!string.IsNullOrEmpty(worldName))
            {
                if (dir.CurrentIsDir())
                {
                    var worldInfo = LoadWorldInfo(worldName);
                    if (worldInfo != null)
                    {
                        worlds.Add(worldInfo);
                    }
                }
                worldName = dir.GetNext();
            }
            
            dir.ListDirEnd();
            
            // Ordenar por última fecha de juego (más reciente primero)
            worlds.Sort((a, b) => b.LastPlayed.CompareTo(a.LastPlayed));
        }
        catch (Exception ex)
        {
            Logger.LogError($"WorldManager: Error al obtener lista de mundos: {ex.Message}");
        }
        
        return worlds;
    }
    
    /// <summary>Verifica si existe un mundo.</summary>
    public bool WorldExists(string worldName)
    {
        string worldPath = GetWorldPath(worldName);
        return DirAccess.DirExistsAbsolute(worldPath);
    }
    
    /// <summary>Actualiza la información de un mundo (ej: tiempo de juego).</summary>
    public void UpdateWorldInfo(string worldName, Action<WorldInfo> updateAction)
    {
        var worldInfo = LoadWorldInfo(worldName);
        if (worldInfo == null)
            return;
        
        updateAction(worldInfo);
        worldInfo.LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // Guardar información actualizada
        string infoPath = GetWorldInfoPath(worldName);
        string jsonString = JsonSerializer.Serialize(worldInfo, _jsonOptions);
        
        var file = FileAccess.Open(infoPath, FileAccess.ModeFlags.Write);
        if (file != null)
        {
            file.StoreString(jsonString);
            file.Close();
            Logger.Log($"WorldManager: Información del mundo actualizada: {worldName}");
        }
        else
        {
            Logger.LogError($"WorldManager: Error al actualizar información del mundo: {worldName}");
        }
    }
    
    /// <summary>Obtiene la ruta completa del directorio de un mundo.</summary>
    public string GetWorldPath(string worldName)
    {
        return $"{WORLDS_FOLDER}{worldName}";
    }
    
    /// <summary>Obtiene la ruta del archivo de información de un mundo.</summary>
    private string GetWorldInfoPath(string worldName)
    {
        return Path.Combine(GetWorldPath(worldName), WORLD_INFO_FILE);
    }
    
    /// <summary>Genera una semilla aleatoria para el mundo.</summary>
    private string GenerateRandomSeed()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
    
    /// <summary>Crea la estructura básica de directorios de un mundo.</summary>
    private void CreateWorldStructure(string worldPath)
    {
        var dir = DirAccess.Open(worldPath);
        if (dir != null)
        {
            // Crear subdirectorios para diferentes tipos de datos
            dir.MakeDir("player");      // Datos del jugador
            dir.MakeDir("world");       // Datos del mundo
            dir.MakeDir("entities");    // Entidades y objetos
            dir.MakeDir("chunks");      // Chunks del terreno (si aplica)
            
            Logger.Log($"WorldManager: Estructura de directorios creada para: {worldPath}");
        }
    }
    
    /// <summary>Elimina recursivamente un directorio.</summary>
    private bool RemoveDirectoryRecursive(DirAccess dir, string dirName)
    {
        string currentPath = dir.GetCurrentDir();
        string targetPath = Path.Combine(currentPath, dirName);
        
        var targetDir = DirAccess.Open(targetPath);
        if (targetDir == null)
            return false;
        
        targetDir.ListDirBegin();
        string entry = targetDir.GetNext();
        
        while (!string.IsNullOrEmpty(entry))
        {
            if (entry == "." || entry == "..")
            {
                entry = targetDir.GetNext();
                continue;
            }
            
            if (targetDir.CurrentIsDir())
            {
                // Eliminar recursivamente subdirectorio
                RemoveDirectoryRecursive(targetDir, entry);
            }
            else
            {
                // Eliminar archivo
                targetDir.Remove(entry);
            }
            
            entry = targetDir.GetNext();
        }
        
        targetDir.ListDirEnd();
        targetDir = null;
        
        // Eliminar el directorio vacío
        var result = dir.Remove(dirName);
        return result == Error.Ok;
    }
}

/// <summary>Información básica de un mundo.</summary>
public class WorldInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    [JsonPropertyName("lastPlayed")]
    public long LastPlayed { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.1.3";
    
    [JsonPropertyName("playTimeSeconds")]
    public long PlayTimeSeconds { get; set; } = 0;
    
    /// <summary>Convierte timestamp a fecha legible.</summary>
    public string GetFormattedDate()
    {
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(LastPlayed);
        return dateTime.ToString("dd/MM/yyyy HH:mm:ss");
    }
    
    /// <summary>Convierte segundos de juego a formato HH:MM:SS.</summary>
    public string GetFormattedPlayTime()
    {
        var time = TimeSpan.FromSeconds(PlayTimeSeconds);
        return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
    }
    
    /// <summary>Obiene texto de display para listas.</summary>
    public string GetDisplayText()
    {
        return $"{Name} - {GetFormattedDate()} - {GetFormattedPlayTime()}";
    }
}
