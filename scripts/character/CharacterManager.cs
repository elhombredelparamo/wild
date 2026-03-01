using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Alias para evitar ambigüedad con System.IO.FileAccess
using FileAccess = Godot.FileAccess;

/// <summary>Manager global de personajes del juego.</summary>
public partial class CharacterManager : Node
{
    public static CharacterManager Instance { get; private set; } = null!;
    
    public CharacterProfile CurrentCharacter { get; private set; } = null!;
    public List<CharacterProfile> AllCharacters { get; private set; } = new();
    
    private const string CHARACTERS_FOLDER = "user://characters/";
    private const string PROFILES_FOLDER = "user://characters/profiles/";
    private const string STATS_FOLDER = "user://characters/stats/";
    private const string DEFAULT_CHARACTER_ID = "jugador";
    
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
            GD.Print("CharacterManager: Inicializando sistema de personajes");
            
            // Crear directorios necesarios
            EnsureDirectoriesExist();
            
            // Cargar todos los personajes existentes
            LoadAllCharacters();
            
            // Si no hay personajes, crear el personaje por defecto
            if (AllCharacters.Count == 0)
            {
                GD.Print("CharacterManager: No hay personajes, creando perfil por defecto");
                CreateDefaultCharacter();
            }
            
            // Seleccionar el primer personaje como actual
            if (AllCharacters.Count > 0)
            {
                SelectCharacter(AllCharacters[0].CharacterId);
            }
            
            GD.Print($"CharacterManager: Sistema inicializado con {AllCharacters.Count} personajes");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"CharacterManager: Error al inicializar: {ex.Message}");
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
                GD.Print($"CharacterManager: Directorio verificado/creado: {dir}");
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
        GD.Print($"CharacterManager: Personaje por defecto creado: {DEFAULT_CHARACTER_ID}");
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
                    GD.PrintErr($"CharacterManager: Error al cargar personaje {fileName}: {ex.Message}");
                }
            }
            fileName = dir.GetNext();
        }
        
        dir.ListDirEnd();
        GD.Print($"CharacterManager: {AllCharacters.Count} personajes cargados");
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
                
                GD.Print($"CharacterManager: Personaje guardado: {character.CharacterId}");
            }
            else
            {
                GD.PrintErr($"CharacterManager: Error al guardar personaje en: {filePath}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"CharacterManager: Error al guardar personaje {character.CharacterId}: {ex.Message}");
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
                GD.Print($"CharacterManager: No existe el personaje: {characterId}");
                return null;
            }
            
            var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"CharacterManager: Error al leer personaje: {filePath}");
                return null;
            }
            
            string jsonString = file.GetAsText();
            file.Close();
            
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
            
            var character = JsonSerializer.Deserialize<CharacterProfile>(jsonString, jsonOptions);
            
            if (character != null)
            {
                GD.Print($"CharacterManager: Personaje cargado: {character.CharacterId}");
            }
            
            return character;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"CharacterManager: Error al cargar personaje {characterId}: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>Selecciona un personaje como el actual.</summary>
    public void SelectCharacter(string characterId)
    {
        var character = AllCharacters.Find(c => c.CharacterId == characterId);
        if (character != null)
        {
            CurrentCharacter = character;
            GD.Print($"CharacterManager: Personaje seleccionado: {characterId}");
        }
        else
        {
            GD.PrintErr($"CharacterManager: No se encontró el personaje: {characterId}");
        }
    }
    
    /// <summary>Crea un nuevo personaje.</summary>
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
        
        GD.Print($"CharacterManager: Nuevo personaje creado: {characterId}");
        return newCharacter;
    }
    
    /// <summary>Actualiza las estadísticas del personaje actual.</summary>
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
    
    /// <summary>Obtiene el ID del personaje actual.</summary>
    public string GetCurrentCharacterId()
    {
        return CurrentCharacter?.CharacterId ?? DEFAULT_CHARACTER_ID;
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
    
    /// <summary>Obtiene el nombre del personaje actual.</summary>
    public string GetCurrentCharacterName()
    {
        return CurrentCharacter?.DisplayName ?? "Jugador";
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
