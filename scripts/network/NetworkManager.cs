using Godot;
using System.Globalization;
using Wild.Network;
using Wild.Scripts.Player;
using Wild.Scripts.Terrain;

namespace Wild.Network;

/// <summary>
/// Gestiona toda la funcionalidad de red del juego (cliente-servidor)
/// Patrón Singleton para acceso global desde cualquier escena
/// </summary>
public partial class NetworkManager : Node
{
    private static NetworkManager _instance;
    public static NetworkManager Instance => _instance;
    
    // Componente de red
    private GameClient _gameClient = null!;
    
    // Estado de red - se activará cuando el cliente se conecte
    private bool _isNetworkMode = false;
    private string _localPlayerId = CharacterManager.PersistentCharacterId ?? string.Empty;
    
    // Sincronización con servidor
    private Vector3 _serverPosition = Vector3.Zero;
    private Vector3 _serverRotation = Vector3.Zero;
    
    // Control de envío de inputs al servidor
    private float _lastSentYaw = 0f;
    private float _lastSentPitch = 0f;
    
    // Control de envío de posición al servidor
    private ulong _lastPositionSendTime = 0;
    private const ulong PositionSendIntervalMs = 50; // 20 actualizaciones por segundo (1000/20 = 50ms)
    
    // Referencia al PlayerController para aplicar cambios
    private PlayerController _playerController = null!;
    
    // Referencia al TerrainManager para ajustar altura
    private TerrainManager _terrainManager = null!;
    
    // Eventos para comunicación con GameWorld
    [Signal]
    public delegate void NetworkModeChangedEventHandler(bool isNetworkMode);
    
    [Signal]
    public delegate void LocalPlayerIdAssignedEventHandler(string playerId);
    
    [Signal]
    public delegate void ServerPositionUpdatedEventHandler(Vector3 position);
    
    [Signal]
    public delegate void ServerRotationUpdatedEventHandler(Vector3 rotation);
    
    [Signal]
    public delegate void RemotePlayerJoinedEventHandler(string playerId, Vector3 position, Vector3 rotation);
    
    [Signal]
    public delegate void RemotePlayerLeftEventHandler(string playerId);
    
    [Signal]
    public delegate void RemotePlayerUpdatedEventHandler(string playerId, Vector3 position, Vector3 rotation);

    /// <summary>
    /// Inicializa el sistema de red
    /// </summary>
    public void Initialize()
    {
        // Patrón singleton
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Logger.LogWarning("NetworkManager: Instancia duplicada detectada, eliminando");
            QueueFree();
            return;
        }
        
        Logger.Log("NetworkManager: Inicializando sistema de red...");
        
        // Obtener referencia del cliente de red desde GameFlow
        var gameFlow = GetNode<GameFlow>("/root/GameFlow");
        if (gameFlow != null)
        {
            _gameClient = gameFlow.GetGameClient();
        }
        else
        {
            Logger.LogError("NetworkManager: GameFlow es nulo, no se puede obtener cliente");
            return;
        }
        
        Logger.Log($"NetworkManager: Cliente de red obtenido: {_gameClient != null}");
        
        // Obtener referencia del TerrainManager
        var gameWorld = GetTree().CurrentScene as GameWorld;
        if (gameWorld != null)
        {
            // El TerrainManager debería estar accesible a través del GameWorld
            // Por ahora, lo obtenemos del árbol de escena
            _terrainManager = gameWorld.GetNode<TerrainManager>("TerrainManager");
            Logger.Log($"NetworkManager: TerrainManager obtenido: {_terrainManager != null}");
        }
        else
        {
            Logger.LogWarning("NetworkManager: No se pudo obtener GameWorld para TerrainManager");
        }
        
        // No verificar conexión aquí - se hará después de iniciar el servidor y conectar el cliente
        Logger.Log("NetworkManager: Inicialización completada - esperando conexión del cliente");
    }
    
    /// <summary>
    /// Establece la referencia al PlayerController para aplicar cambios de red
    /// </summary>
    public void SetPlayerController(PlayerController playerController)
    {
        _playerController = playerController;
        Logger.Log($"NetworkManager: PlayerController establecido: {_playerController != null}");
    }
    
    /// <summary>
    /// Verifica que el cliente esté conectado al servidor y activa el modo red
    /// </summary>
    public void UpdateNetworkMode()
    {
        if (_gameClient != null && _gameClient.IsConnected)
        {
            bool wasNotNetworkMode = !_isNetworkMode;
            _isNetworkMode = true;
            Logger.Log("NetworkManager: ✅ Modo red verificado - cliente conectado");
            
            // Conectar señales del cliente si no estaban conectadas
            ConnectClientSignals();
            
            // Emitir señal de cambio de modo si es la primera vez
            if (wasNotNetworkMode)
            {
                EmitSignal(SignalName.NetworkModeChanged, true);
            }
        }
        else
        {
            Logger.LogError("NetworkManager: ❌ ERROR CRÍTICO - Cliente no conectado al servidor");
            throw new System.Exception("Se requiere conexión al servidor para funcionar");
        }
    }
    
    /// <summary>
    /// Conecta las señales del cliente de red
    /// </summary>
    private void ConnectClientSignals()
    {
        Logger.Log("NetworkManager: Conectando señales del cliente de red");
        _gameClient.OnPositionUpdated += OnServerPositionUpdated;
        _gameClient.OnRotationUpdated += OnServerRotationUpdated;
        _gameClient.OnLocalPlayerIdAssigned += OnLocalPlayerIdAssigned;
        _gameClient.OnRemotePlayerJoined += OnRemotePlayerJoined;
        _gameClient.OnRemotePlayerLeft += OnRemotePlayerLeft;
        _gameClient.OnRemotePlayerUpdated += OnRemotePlayerUpdated;
        Logger.Log("NetworkManager: ✅ Señales del cliente conectadas");
    }
    
    /// <summary>
    /// Procesa el movimiento (solo si está en modo red)
    /// </summary>
    public void ProcessNetworkMovement(PlayerController playerController)
    {
        if (!_isNetworkMode)
            return;
        
        // Procesar en modo red
        var playerPos = playerController.GetPlayerPosition();
        var angles = playerController.GetCameraAngles();
        
        if (playerPos != Vector3.Zero || angles.X != _lastSentYaw || angles.Y != _lastSentPitch)
        {
            // Calcular dirección de movimiento (esto necesitaría ser expuesto por PlayerController)
            // Por ahora, enviamos la posición directamente
            SendInputToServer(Vector3.Zero, new Vector3(angles.Y, angles.X, 0));
            _lastSentYaw = angles.X;
            _lastSentPitch = angles.Y;
        }
    }
    
    /// <summary>
    /// Envía inputs al servidor en lugar de posición
    /// </summary>
    private async void SendInputToServer(Vector3 direction, Vector3 rotation)
    {
        try
        {
            // Formato: "INPUT:movimiento:x,y,z|rotacion:pitch,yaw"
            var message = $"INPUT:movimiento:{direction.X.ToString(CultureInfo.InvariantCulture)},{direction.Y.ToString(CultureInfo.InvariantCulture)},{direction.Z.ToString(CultureInfo.InvariantCulture)}|rotacion:{rotation.X.ToString(CultureInfo.InvariantCulture)},{rotation.Y.ToString(CultureInfo.InvariantCulture)}";
            
            await _gameClient.SendPlayerInput(direction, rotation);
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"NetworkManager: Error al enviar input al servidor: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Notifica al servidor que una posición fue bloqueada por las barreras
    /// </summary>
    public async void NotifyBlockedPosition(Vector3 blockedPosition, Vector3 correctedPosition)
    {
        try
        {
            if (_gameClient != null)
            {
                // Enviar mensaje especial al servidor indicando posición bloqueada
                string blockMessage = $"BLOCKED_POS:{blockedPosition.X}:{blockedPosition.Y}:{blockedPosition.Z}|{correctedPosition.X}:{correctedPosition.Y}:{correctedPosition.Z}";
                await _gameClient.SendPositionUpdate(blockMessage);
                Logger.Log($"NetworkManager: Notificado al servidor - Posición bloqueada: {blockedPosition}, Corregida: {correctedPosition}");
            }
            else
            {
                Logger.Log("NetworkManager: No se pudo obtener el cliente de red para notificar posición bloqueada");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"NetworkManager: Error al notificar posición bloqueada: {ex.Message}");
        }
    }
    
    // Getters para estado de red
    public bool IsNetworkMode => _isNetworkMode;
    public string LocalPlayerId => _localPlayerId;
    public Vector3 ServerPosition => _serverPosition;
    public Vector3 ServerRotation => _serverRotation;
    
    // Event handlers del cliente de red
    private void OnLocalPlayerIdAssigned(string playerId)
    {
        _localPlayerId = playerId;
        Logger.Log($"NetworkManager: ID de jugador local asignado: {playerId}");
        EmitSignal(SignalName.LocalPlayerIdAssigned, playerId);
    }
    
    private void OnServerPositionUpdated(Vector3 position)
    {
        _serverPosition = position;
        
        // Aplicar posición directamente al PlayerController
        if (_playerController != null && _terrainManager != null)
        {
            // Obtener altura del terreno en la posición del servidor
            float terrainHeight = _terrainManager.GetTerrainHeightAt(position.X, position.Z);
            
            // Ajustar la altura para que el jugador esté sobre el terreno
            // Usar la altura del terreno directamente (sin +2f adicional)
            Vector3 adjustedPosition = new Vector3(position.X, terrainHeight + 1.5f, position.Z); // 1.5f = altura del jugador desde los pies
            
            _playerController.SetPlayerGlobalPosition(adjustedPosition);
            Logger.Log($"NetworkManager: Posición sincronizada con servidor y ajustada a terreno: {adjustedPosition}");
        }
        else if (_playerController != null)
        {
            // Fallback si no hay TerrainManager
            _playerController.SetPlayerGlobalPosition(position);
            Logger.Log($"NetworkManager: Posición sincronizada con servidor (sin ajuste): {position}");
        }
        
        EmitSignal(SignalName.ServerPositionUpdated, position);
    }
    
    private void OnServerRotationUpdated(Vector3 rotation)
    {
        _serverRotation = rotation;
        
        // Aplicar rotación directamente al PlayerController
        if (_playerController != null)
        {
            _playerController.SetCameraAngles(rotation.Y, rotation.X);
            Logger.Log($"NetworkManager: Rotación sincronizada con servidor: {rotation}");
        }
        
        EmitSignal(SignalName.ServerRotationUpdated, rotation);
    }
    
    private void OnRemotePlayerJoined(string playerId, Vector3 position, Vector3 rotation)
    {
        Logger.Log($"NetworkManager: Jugador remoto {playerId} se unió en pos={position}, rot={rotation}");
        EmitSignal(SignalName.RemotePlayerJoined, playerId, position, rotation);
    }
    
    private void OnRemotePlayerLeft(string playerId)
    {
        Logger.Log($"NetworkManager: Jugador remoto {playerId} se desconectó");
        EmitSignal(SignalName.RemotePlayerLeft, playerId);
    }
    
    private void OnRemotePlayerUpdated(string playerId, Vector3 position, Vector3 rotation)
    {
        Logger.Log($"NetworkManager: Jugador remoto {playerId} actualizado: pos={position}, rot={rotation}");
        EmitSignal(SignalName.RemotePlayerUpdated, playerId, position, rotation);
    }
    
    /// <summary>
    /// Resetea el NetworkManager a su estado inicial
    /// </summary>
    public void Reset()
    {
        Logger.Log("NetworkManager: Reseteando a estado inicial...");
        
        try
        {
            // Resetear estado de red
            _isNetworkMode = false;
            _localPlayerId = string.Empty;
            
            // Resetear sincronización
            _serverPosition = Vector3.Zero;
            _serverRotation = Vector3.Zero;
            
            // Resetear control de envío
            _lastSentYaw = 0f;
            _lastSentPitch = 0f;
            _lastPositionSendTime = 0;
            
            // Limpiar referencias (sin liberar los objetos, solo las referencias)
            _playerController = null!;
            _terrainManager = null!;
            
            // NOTA: No resetear el singleton aquí para evitar null references
            // El singleton se reseteará cuando se cree una nueva instancia
            
            Logger.Log("NetworkManager: ✅ Reset completado");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"NetworkManager: Error durante reset: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Resetea completamente el singleton NetworkManager
    /// Desconecta eventos y limpia todas las referencias para evitar corrupción entre partidas
    /// </summary>
    public static void ResetSingleton()
    {
        if (_instance != null)
        {
            Logger.Log("NetworkManager: Iniciando ResetSingleton()...");
            
            try
            {
                // Desconectar eventos del cliente si existe
                if (_instance._gameClient != null)
                {
                    _instance.DisconnectClientSignals();
                    Logger.Log("NetworkManager: ✅ Eventos del cliente desconectados");
                }
                
                // Limpiar cliente de red
                _instance._gameClient = null!;
                
                // Resetear estado completo
                _instance.Reset();
                
                // Liberar la instancia del singleton
                _instance = null!;
                
                Logger.Log("NetworkManager: ✅ ResetSingleton completado - singleton liberado");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"NetworkManager: Error en ResetSingleton: {ex.Message}");
            }
        }
        else
        {
            Logger.Log("NetworkManager: ResetSingleton llamado pero no hay instancia activa");
        }
    }
    
    /// <summary>
    /// Desconecta todas las señales del cliente de red
    /// </summary>
    private void DisconnectClientSignals()
    {
        if (_gameClient != null)
        {
            Logger.Log("NetworkManager: Desconectando señales del cliente de red");
            
            try
            {
                _gameClient.OnPositionUpdated -= OnServerPositionUpdated;
                _gameClient.OnRotationUpdated -= OnServerRotationUpdated;
                _gameClient.OnLocalPlayerIdAssigned -= OnLocalPlayerIdAssigned;
                _gameClient.OnRemotePlayerJoined -= OnRemotePlayerJoined;
                _gameClient.OnRemotePlayerLeft -= OnRemotePlayerLeft;
                _gameClient.OnRemotePlayerUpdated -= OnRemotePlayerUpdated;
                
                Logger.Log("NetworkManager: ✅ Todas las señales del cliente desconectadas");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"NetworkManager: Error al desconectar señales: {ex.Message}");
            }
        }
    }
}
