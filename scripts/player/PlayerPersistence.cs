using Godot;
using System.Text.Json;
using System.IO;
using FileAccess = Godot.FileAccess;

namespace Wild.Scripts.Player;

/// <summary>
/// Gestiona la persistencia de datos del jugador (posición, rotación, etc.)
/// </summary>
public partial class PlayerPersistence : Node
{
    private SessionData _sessionData = null!;
    
    // Sistema de auto-save
    private PlayerController _playerController = null!;
    private ulong _lastSaveTime = 0;
    private const ulong SaveIntervalMs = 5000; // Guardar cada 5 segundos
    private bool _autoSaveEnabled = false;
    
    public override void _Ready()
    {
        _sessionData = GetNode<SessionData>("/root/SessionData");
    }
    
    public override void _ExitTree()
    {
        // Guardar posición al salir del árbol de escenas
        SaveOnExit();
    }
    
    /// <summary>
    /// Inicializa el sistema de persistencia con el PlayerController
    /// </summary>
    public void Initialize(PlayerController playerController)
    {
        _playerController = playerController;
        Logger.Log($"PlayerPersistence: Inicializado con PlayerController: {_playerController != null}");
    }
    
    /// <summary>
    /// Inicia el sistema de auto-save
    /// </summary>
    public void StartAutoSave()
    {
        if (_playerController == null)
        {
            Logger.LogError("PlayerPersistence: No se puede iniciar auto-save - PlayerController es nulo");
            return;
        }
        
        _autoSaveEnabled = true;
        _lastSaveTime = Time.GetTicksMsec();
        Logger.Log("PlayerPersistence: Auto-save iniciado (cada 5 segundos)");
    }
    
    /// <summary>
    /// Detiene el sistema de auto-save
    /// </summary>
    public void StopAutoSave()
    {
        _autoSaveEnabled = false;
        Logger.Log("PlayerPersistence: Auto-save detenido");
    }
    
    /// <summary>
    /// Procesa el auto-save (debe llamarse desde _Process)
    /// </summary>
    public void ProcessAutoSave(double delta)
    {
        if (!_autoSaveEnabled || _playerController == null)
            return;
        
        var currentTime = Time.GetTicksMsec();
        if (currentTime - _lastSaveTime >= SaveIntervalMs)
        {
            SavePlayerData(_playerController);
            _lastSaveTime = currentTime;
        }
    }
    
    /// <summary>
    /// Carga la posición del jugador (método de conveniencia para GameWorld)
    /// </summary>
    public bool LoadPlayerPosition()
    {
        if (_playerController == null)
        {
            Logger.LogError("PlayerPersistence: No se puede cargar posición - PlayerController es nulo");
            return false;
        }
        
        bool loaded = LoadPlayerData(_playerController);
        if (loaded)
        {
            Logger.Log("PlayerPersistence: ✅ Posición del jugador cargada");
        }
        else
        {
            Logger.Log("PlayerPersistence: No hay posición guardada del jugador");
        }
        
        return loaded;
    }
    
    /// <summary>
    /// Guarda al salir del juego (método de conveniencia para GameWorld)
    /// </summary>
    public void SaveOnExit()
    {
        try
        {
            if (_playerController != null && IsInstanceValid(_playerController))
            {
                SavePlayerData(_playerController);
                Logger.Log("PlayerPersistence: Posición guardada al salir del juego");
            }
            else
            {
                Logger.LogWarning("PlayerPersistence: No se puede guardar al salir - PlayerController es null o inválido");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"PlayerPersistence: Error al guardar datos del jugador: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Guarda la posición y rotación actual del jugador
    /// </summary>
    /// <param name="playerController">Controlador del jugador para obtener posición/rotación</param>
    /// <returns>True si el guardado fue exitoso</returns>
    public bool SavePlayerData(PlayerController playerController)
    {
        try
        {
            if (playerController == null)
            {
                Logger.LogError("PlayerPersistence: PlayerController es nulo");
                return false;
            }
            
            // Obtener ID del jugador
            string playerId = GetPlayerId();
            if (string.IsNullOrEmpty(playerId))
            {
                Logger.Log("PlayerPersistence: No se puede guardar - no hay ID de jugador");
                return false;
            }
            
            // Obtener posición y rotación actual
            Vector3 position;
            Vector3 globalPosition;
            Vector2 angles;
            
            try
            {
                position = playerController.GetPlayerPosition();
                globalPosition = playerController.GetPlayerGlobalPosition();
                angles = playerController.GetCameraAngles();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"PlayerPersistence: Error al obtener datos del jugador: {ex.Message}");
                return false;
            }
            
            var rotation = new Vector3(angles.X, angles.Y, 0);
            
            // Crear datos del jugador
            var playerData = new
            {
                PlayerId = playerId,
                Position = new { X = position.X, Y = position.Y, Z = position.Z },
                GlobalPosition = new { X = globalPosition.X, Y = globalPosition.Y, Z = globalPosition.Z },
                Rotation = new { Yaw = rotation.X, Pitch = rotation.Y },
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            
            // Serializar a JSON
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            string jsonString = JsonSerializer.Serialize(playerData, jsonOptions);
            
            // Guardar en el directorio del mundo actual
            string worldPath = GetWorldPath();
            if (!string.IsNullOrEmpty(worldPath))
            {
                string playerDataPath = Path.Combine(worldPath, "player", $"player_{playerId}.json");
                
                // Asegurar que el directorio exista
                var dir = DirAccess.Open(worldPath);
                if (dir != null)
                {
                    dir.MakeDirRecursive("player");
                    
                    // Guardar archivo
                    var file = FileAccess.Open(playerDataPath, FileAccess.ModeFlags.Write);
                    if (file != null)
                    {
                        file.StoreString(jsonString);
                        file.Close();
                        
                        // Solo loggear si hay movimiento real
                        if (position != Vector3.Zero)
                        {
                            Logger.Log($"PlayerPersistence: Posición guardada para jugador {playerId}");
                        }
                        return true;
                    }
                    else
                    {
                        Logger.LogError($"PlayerPersistence: Error al guardar posición en: {playerDataPath}");
                    }
                }
                else
                {
                    Logger.LogError($"PlayerPersistence: Error al acceder al directorio del mundo: {worldPath}");
                }
            }
            else
            {
                Logger.Log("PlayerPersistence: No se puede guardar - no hay mundo activo");
            }
            
            return false;
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"PlayerPersistence: Error al guardar datos del jugador: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Carga la posición y rotación guardada del jugador
    /// </summary>
    /// <param name="playerController">Controlador del jugador para restaurar posición/rotación</param>
    /// <returns>True si la carga fue exitosa, false si no hay datos guardados</returns>
    public bool LoadPlayerData(PlayerController playerController)
    {
        try
        {
            if (playerController == null)
            {
                Logger.LogError("PlayerPersistence: PlayerController es nulo");
                return false;
            }
            
            // Obtener ID del jugador
            string playerId = GetPlayerId();
            if (string.IsNullOrEmpty(playerId))
            {
                Logger.Log("PlayerPersistence: No se puede cargar - no hay ID de jugador");
                return false;
            }
            
            // Obtener ruta del mundo actual
            string worldPath = GetWorldPath();
            if (string.IsNullOrEmpty(worldPath))
            {
                Logger.Log("PlayerPersistence: No se puede cargar - no hay mundo activo");
                return false;
            }
            
            string playerDataPath = Path.Combine(worldPath, "player", $"player_{playerId}.json");
            
            if (!FileAccess.FileExists(playerDataPath))
            {
                Logger.Log($"PlayerPersistence: No hay datos guardados para jugador {playerId}");
                return false;
            }
            
            // Cargar archivo
            var file = FileAccess.Open(playerDataPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Logger.LogError($"PlayerPersistence: Error al leer datos desde: {playerDataPath}");
                return false;
            }
            
            string jsonString = file.GetAsText();
            file.Close();
            
            // Deserializar JSON
            using var document = JsonDocument.Parse(jsonString);
            var root = document.RootElement;
            
            // Verificar que el archivo pertenezca al jugador correcto
            if (root.TryGetProperty("playerId", out var playerIdProp))
            {
                string savedPlayerId = playerIdProp.GetString();
                if (savedPlayerId != playerId)
                {
                    Logger.Log($"PlayerPersistence: El archivo pertenece a otro jugador ({savedPlayerId})");
                    return false;
                }
            }
            
            // Cargar posición
            bool positionLoaded = false;
            
            if (root.TryGetProperty("globalPosition", out var globalPositionProp))
            {
                float x = globalPositionProp.GetProperty("x").GetSingle();
                float y = globalPositionProp.GetProperty("y").GetSingle();
                float z = globalPositionProp.GetProperty("z").GetSingle();
                var loadedPosition = new Vector3(x, y, z);
                
                // Verificar si la posición global es válida (no es 0,0,0)
                if (loadedPosition != Vector3.Zero)
                {
                    try
                    {
                        playerController.SetPlayerGlobalPosition(loadedPosition);
                        positionLoaded = true;
                        Logger.Log($"PlayerPersistence: Posición global cargada para jugador {playerId}: ({x}, {y}, {z})");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError($"PlayerPersistence: Error al establecer posición global: {ex.Message}");
                        positionLoaded = false;
                    }
                }
            }
            
            // Si no se usó posición global o era inválida, usar posición local
            if (!positionLoaded && root.TryGetProperty("position", out var positionProp))
            {
                float x = positionProp.GetProperty("x").GetSingle();
                float y = positionProp.GetProperty("y").GetSingle();
                float z = positionProp.GetProperty("z").GetSingle();
                
                try
                {
                    playerController.SetPlayerPosition(new Vector3(x, y, z));
                    Logger.Log($"PlayerPersistence: Posición local cargada para jugador {playerId}: ({x}, {y}, {z})");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"PlayerPersistence: Error al establecer posición local: {ex.Message}");
                }
            }
            
            // Cargar rotación
            if (root.TryGetProperty("rotation", out var rotationProp))
            {
                float yaw = rotationProp.GetProperty("yaw").GetSingle();
                float pitch = rotationProp.GetProperty("pitch").GetSingle();
                
                try
                {
                    playerController.SetCameraAngles(yaw, pitch);
                    Logger.Log($"PlayerPersistence: Rotación cargada para jugador {playerId}: Yaw={yaw}, Pitch={pitch}");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"PlayerPersistence: Error al establecer rotación: {ex.Message}");
                }
            }
            
            Logger.Log($"PlayerPersistence: ✅ Datos del jugador {playerId} cargados correctamente");
            return true;
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"PlayerPersistence: Error al cargar datos del jugador: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Verifica si existen datos guardados para el jugador actual
    /// </summary>
    /// <returns>True si existen datos guardados</returns>
    public bool HasSavedData()
    {
        try
        {
            string playerId = GetPlayerId();
            if (string.IsNullOrEmpty(playerId))
                return false;
            
            string worldPath = GetWorldPath();
            if (string.IsNullOrEmpty(worldPath))
                return false;
            
            string playerDataPath = Path.Combine(worldPath, "player", $"player_{playerId}.json");
            return FileAccess.FileExists(playerDataPath);
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"PlayerPersistence: Error al verificar datos guardados: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Obtiene la ruta del directorio del mundo actual
    /// </summary>
    private string GetWorldPath()
    {
        try
        {
            if (_sessionData != null && !string.IsNullOrEmpty(_sessionData.WorldName))
            {
                return $"user://worlds/{_sessionData.WorldName}";
            }
            return string.Empty;
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"PlayerPersistence: Error al obtener ruta del mundo: {ex.Message}");
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Obtiene el ID único del jugador local
    /// </summary>
    private string GetPlayerId()
    {
        try
        {
            // Usar el CharacterManager para obtener el ID del personaje actual
            if (CharacterManager.Instance != null && CharacterManager.Instance.CurrentCharacter != null)
            {
                return CharacterManager.Instance.GetCurrentCharacterId();
            }
            
            return "jugador";
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"PlayerPersistence: Error al obtener ID de personaje: {ex.Message}");
            return "jugador";
        }
    }
}
