using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Alias para evitar ambigüedad con System.IO.FileAccess y Godot.Logger
using FileAccess = Godot.FileAccess;
using Logger = Wild.Logger;

/// <summary>Manager global de personajes del juego.</summary>
public partial class CharacterManager : Node
{
    public static CharacterManager Instance { get; private set; } = null!;
    
    public CharacterProfile CurrentCharacter { get; private set; } = null!;
    public List<CharacterProfile> AllCharacters { get; private set; } = new();
    
    private const string CHARACTERS_FOLDER = "user://characters/";
    private const string PROFILES_FOLDER = "user://characters/profiles/";
    private const string STATS_FOLDER = "user://characters/stats/";
    private const string CURRENT_CHARACTER_FILE = "user://current_character.json";
    private const string DEFAULT_CHARACTER_ID = "jugador";
    
    private static string _persistentCharacterId = string.Empty;
    
    /// <summary>Obtiene o establece el ID del personaje persistente.</summary>
    public static string PersistentCharacterId 
    { 
        get => _persistentCharacterId; 
        private set => _persistentCharacterId = value; 
    }
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeCharacterSystem();
        }
        else
        {
            QueueFree(); // Eliminar duplicados
        }
    }
    
    /// <summary>Inicializa el sistema de personajes y crea el perfil por defecto si es necesario.</summary>
    private void InitializeCharacterSystem()
    {
        try
        {
            Logger.Log("CharacterManager: Inicializando sistema de personajes");
            
            // Crear directorios necesarios
            EnsureDirectoriesExist();
            
            // Cargar todos los personajes existentes
            LoadAllCharacters();
            
            // Si no hay personajes, crear el personaje por defecto
            if (AllCharacters.Count == 0)
            {
                Logger.Log("CharacterManager: No hay personajes, creando perfil por defecto");
                CreateDefaultCharacter();
            }
            
            // Cargar personaje guardado permanentemente si existe
            LoadPersistentCharacter();
            
            // Si no hay personaje seleccionado, usar el primero disponible
            if (CurrentCharacter == null && AllCharacters.Count > 0)
            {
                Logger.Log($"CharacterManager: No hay personaje seleccionado, usando primero disponible");
                SelectCharacter(AllCharacters[0].CharacterId);
            }
            
            Logger.Log($"CharacterManager: Estado final - CurrentCharacter: {(CurrentCharacter?.DisplayName ?? "NULL")}, Total: {AllCharacters.Count}");
        Logger.Log($"CharacterManager: Sistema inicializado con {AllCharacters.Count} personajes");
        }
        catch (Exception ex)
        {
            Logger.LogError($"CharacterManager: Error al inicializar: {ex.Message}");
        }
    }
    
    /// <summary>Asegura que los directorios necesarios existan.</summary>
    private void EnsureDirectoriesExist()
    {
        var dirs = new[] { CHARACTERS_FOLDER, PROFILES_FOLDER, STATS_FOLDER };
        
        foreach (var dir in dirs)
        {
            var dirAccess = DirAccess.Open("user://");
            if (dirAccess != null)
            {
                string relativePath = dir.Replace("user://", "");
                dirAccess.MakeDirRecursive(relativePath);
                Logger.Log($"CharacterManager: Directorio verificado/creado: {dir}");
            }
        }
    }
    
    /// <summary>Crea el personaje por defecto "jugador".</summary>
    private void CreateDefaultCharacter()
    {
        var defaultCharacter = new CharacterProfile
        {
            CharacterId = DEFAULT_CHARACTER_ID,
            DisplayName = "Jugador",
            CreatedAt = DateTime.UtcNow,
            TotalPlayTime = TimeSpan.Zero,
            WorldsVisited = new List<string>(),
            CurrentWorld = null,
            Appearance = new CharacterAppearance
            {
                Skin = "default",
                Color = "#FFFFFF"
            }
        };
        
        SaveCharacter(defaultCharacter);
        Logger.Log($"CharacterManager: Personaje por defecto creado: {DEFAULT_CHARACTER_ID}");
    }
    
    /// <summary>Carga todos los personajes existentes.</summary>
    private void LoadAllCharacters()
    {
        AllCharacters.Clear();
        
        var dir = DirAccess.Open(PROFILES_FOLDER);
        if (dir == null) return;
        
        dir.ListDirBegin();
        string fileName = dir.GetNext();
        
        while (!string.IsNullOrEmpty(fileName))
        {
            if (fileName.EndsWith(".json"))
            {
                try
                {
                    string characterId = fileName.Replace(".json", "");
                    var character = LoadCharacter(characterId);
                    if (character != null)
                    {
                        AllCharacters.Add(character);
                    }
                }
                catch (Exception ex)
                {
                Logger.LogError($"CharacterManager: Error al cargar personaje {fileName}: {ex.Message}");
                }
            }
            fileName = dir.GetNext();
        }
        
        dir.ListDirEnd();
        Logger.Log($"CharacterManager: {AllCharacters.Count} personajes cargados");
    }
    
    /// <summary>Guarda un personaje en disco.</summary>
    public void SaveCharacter(CharacterProfile character)
    {
        try
        {
            string filePath = Path.Combine(PROFILES_FOLDER, $"{character.CharacterId}.json");
            
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
            
            string jsonString = JsonSerializer.Serialize(character, jsonOptions);
            
            var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(jsonString);
                file.Close();
                
                Logger.Log($"CharacterManager: Personaje guardado: {character.CharacterId}");
            }
            else
            {
                Logger.LogError($"CharacterManager: Error al guardar personaje en: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"CharacterManager: Error al guardar personaje {character.CharacterId}: {ex.Message}");
        }
    }
    
    /// <summary>Carga un personaje desde disco.</summary>
    public CharacterProfile LoadCharacter(string characterId)
    {
        try
        {
            string filePath = Path.Combine(PROFILES_FOLDER, $"{characterId}.json");
            
            if (!FileAccess.FileExists(filePath))
            {
                Logger.Log($"CharacterManager: No existe el personaje: {characterId}");
                return null;
            }
            
            var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Logger.LogError($"CharacterManager: Error al leer personaje: {filePath}");
                return null;
            }
            
            string jsonString = file.GetAsText();
            file.Close();
            
            var character = JsonSerializer.Deserialize<CharacterProfile>(jsonString, _jsonOptions);
            
            if (character != null)
            {
                Logger.Log($"CharacterManager: Personaje cargado: {character.CharacterId}");
            }
            
            return character;
        }
        catch (Exception ex)
        {
            Logger.LogError($"CharacterManager: Error al cargar personaje {characterId}: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>Selecciona un personaje como el actual.</summary>
    public void SelectCharacter(string characterId)
    {
        var character = AllCharacters.FirstOrDefault(c => c.CharacterId == characterId);
        if (character != null)
        {
            CurrentCharacter = character;
            SavePersistentCharacter(characterId);
            Logger.Log($"CharacterManager: Personaje seleccionado: {characterId} - {character.DisplayName}");
        }
        else
        {
            Logger.LogError($"CharacterManager: No se encontró el personaje: {characterId}");
        }
    }
    
    /// <summary>Crea un nuevo personaje con el nombre especificado.</summary>
    public CharacterProfile CreateCharacter(string displayName)
    {
        // Generar ID único usando MD5 con sal
        string characterId = GenerateCharacterId(displayName);
        
        var newCharacter = new CharacterProfile
        {
            CharacterId = characterId,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            TotalPlayTime = TimeSpan.Zero,
            WorldsVisited = new List<string>(),
            CurrentWorld = null,
            Appearance = new CharacterAppearance
            {
                Skin = "default",
                Color = "#FFFFFF"
            }
        };
        
        SaveCharacter(newCharacter);
        AllCharacters.Add(newCharacter);
        
        Logger.Log($"CharacterManager: Nuevo personaje creado: {characterId}");
        return newCharacter;
    }
    public void UpdateCharacterStats(TimeSpan playTime, string worldName)
    {
        if (CurrentCharacter == null) return;
        
        CurrentCharacter.TotalPlayTime += playTime;
        
        if (!string.IsNullOrEmpty(worldName) && !CurrentCharacter.WorldsVisited.Contains(worldName))
        {
            CurrentCharacter.WorldsVisited.Add(worldName);
        }
        
        CurrentCharacter.CurrentWorld = worldName;
        SaveCharacter(CurrentCharacter);
    }
    
    /// <summary>Actualiza las estadísticas del personaje actual.</summary>
    public string GetCurrentCharacterId()
    {
        return CurrentCharacter?.CharacterId ?? DEFAULT_CHARACTER_ID;
    }
    
    /// <summary>Guarda el personaje actual en un archivo permanente.</summary>
    private void SavePersistentCharacter(string characterId)
    {
        try
        {
            Logger.Log($"CharacterManager: Guardando personaje permanente: {characterId}");
            
            var file = FileAccess.Open(CURRENT_CHARACTER_FILE, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(characterId);
                file.Close();
                PersistentCharacterId = characterId;
                Logger.Log($"CharacterManager: ✅ Personaje guardado en archivo: {characterId}");
                Logger.Log($"CharacterManager: ✅ PersistentCharacterId actualizado: {PersistentCharacterId}");
            }
            else
            {
                Logger.LogError($"CharacterManager: Error guardando personaje permanente en: {CURRENT_CHARACTER_FILE}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"CharacterManager: Error guardando personaje permanente: {ex.Message}");
        }
    }
    
    /// <summary>Carga el personaje guardado permanentemente.</summary>
    private void LoadPersistentCharacter()
    {
        try
        {
            if (!FileAccess.FileExists(CURRENT_CHARACTER_FILE))
            {
                Logger.Log("CharacterManager: No hay personaje guardado permanentemente");
                return;
            }
            
            var file = FileAccess.Open(CURRENT_CHARACTER_FILE, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Logger.LogError($"CharacterManager: Error al leer archivo permanente: {CURRENT_CHARACTER_FILE}");
                return;
            }
            
            PersistentCharacterId = file.GetAsText().Trim();
            file.Close();
            
            Logger.Log($"CharacterManager: ID leído desde archivo: '{PersistentCharacterId}'");
            Logger.Log($"CharacterManager: Personajes disponibles: {AllCharacters.Count}");
            
            if (!string.IsNullOrEmpty(PersistentCharacterId))
            {
                var savedCharacter = AllCharacters.FirstOrDefault(c => c.CharacterId == PersistentCharacterId);
                if (savedCharacter != null)
                {
                    CurrentCharacter = savedCharacter;
                    Logger.Log($"CharacterManager: ✅ Personaje restaurado: {PersistentCharacterId} - {savedCharacter.DisplayName}");
                }
                else
                {
                    Logger.LogWarning($"CharacterManager: ⚠️ No se encontró personaje con ID: {PersistentCharacterId}");
                    Logger.Log($"CharacterManager: IDs disponibles: {string.Join(", ", AllCharacters.Select(c => c.CharacterId))}");
                }
            }
            else
            {
                Logger.LogWarning("CharacterManager: ⚠️ ID de personaje permanente está vacío");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"CharacterManager: Error cargando personaje permanente: {ex.Message}");
        }
    }
    
    /// <summary>Genera un ID único basado en MD5 con sal para un personaje.</summary>
    private string GenerateCharacterId(string displayName)
    {
        // Generar sal aleatoria única para este personaje
        long salt = DateTime.UtcNow.Ticks + new Random().NextInt64();
        
        // Combinar nombre + sal para generar hash
        string input = $"{displayName}_{salt}";
        
        using (MD5 md5 = MD5.Create())
        {
            byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            
            // Usar primeros 16 caracteres para ID más manejable y única
            return hash.Substring(0, 16);
        }
    }
    
    /// <summary>Guarda el estado del personaje actual antes de cambiar de escena.</summary>
    public static void SaveCurrentCharacterState()
    {
        if (Instance?.CurrentCharacter != null)
        {
            Instance.SavePersistentCharacter(Instance.CurrentCharacter.CharacterId);
            Logger.Log($"CharacterManager: Guardando estado del personaje actual: {Instance.CurrentCharacter.CharacterId}");
        }
    }
    
    /// <summary>Restaura el estado del personaje actual al cambiar de escena.</summary>
    public static void RestoreCurrentCharacterState()
    {
        if (Instance != null)
        {
            Instance.LoadPersistentCharacter();
        }
    }
    
    /// <summary>Obtiene el nombre del personaje actual.</summary>
    public string GetCurrentCharacterName()
    {
        return CurrentCharacter?.DisplayName ?? "Jugador";
    }
    
    /// <summary>
    /// Resetea completamente el singleton CharacterManager
    /// Limpia estado temporal pero preserva datos de personajes para evitar corrupción entre partidas
    /// </summary>
    public static void ResetSingleton()
    {
        if (Instance != null)
        {
            Logger.Log("CharacterManager: Iniciando ResetSingleton()...");
            
            try
            {
                // Guardar estado actual antes de resetear (si hay personaje activo)
                SaveCurrentCharacterState();
                
                // NO limpiar AllCharacters - los personajes deben persistir entre partidas
                // NO limpiar CurrentCharacter - se restaurará desde archivo
                
                // Solo limpiar variables temporales si es necesario
                // Instance.CurrentCharacter = null!; // Comentado - preservar personaje
                // Instance.AllCharacters.Clear(); // Comentado - preservar lista
                
                // NO liberar la instancia del singleton - esto causa que Instance sea null
                // Instance = null!; // Comentado - mantener instancia para evitar null references
                
                Logger.Log("CharacterManager: ✅ ResetSingleton completado - datos de personajes preservados");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CharacterManager: Error en ResetSingleton: {ex.Message}");
            }
        }
        else
        {
            Logger.Log("CharacterManager: ResetSingleton llamado pero no hay instancia activa");
        }
    }
}

/// <summary>Perfil de un personaje.</summary>
public class CharacterProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public TimeSpan TotalPlayTime { get; set; }
    public List<string> WorldsVisited { get; set; } = new();
    public string? CurrentWorld { get; set; }
    public CharacterAppearance Appearance { get; set; } = new();
}

/// <summary>Apariencia de un personaje.</summary>
public class CharacterAppearance
{
    public string Skin { get; set; } = "default";
    public string Color { get; set; } = "#FFFFFF";
}
