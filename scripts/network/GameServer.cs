using Godot;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;

namespace Wild.Network;

/// <summary>
/// Información de un jugador conectado al servidor
/// </summary>
public class PlayerInfo
{
    public string PlayerId { get; set; } = string.Empty;
    public TcpClient Client { get; set; } = null!;
    public NetworkStream Stream { get; set; } = null!;
    public Vector3 Position { get; set; } = new Vector3(0, 2, 5);
    public Vector3 Rotation { get; set; } = Vector3.Zero;
    public DateTime LastActivity { get; set; } = DateTime.Now;
}

/// <summary>
/// Servidor local que gestiona toda la lógica del juego.
/// Escucha conexiones de clientes y maneja el estado del mundo.
/// </summary>
public partial class GameServer : Node
{
    private TcpListener? _listener;
    private Dictionary<string, PlayerInfo> _players = new();
    private int _nextPlayerId = 1;
    private CancellationTokenSource _cts = new CancellationTokenSource();
    private CancellationToken _cancellationToken;
    
    // Estado del juego (servidor)
    private Dictionary<string, object> _worldState = new();
    private bool _isRunning = false;
    
    // Configuración
    private const int DefaultPort = 7778;
    private const string Host = "127.0.0.1";
    private static int Port = DefaultPort;
    private const float PlayerMoveSpeed = 1.11f; // 4 km/h = 1.11 m/s exactos
    private const float ServerTickRate = 60f; // 60 ticks por segundo
    private double _lastTickTime = 0;
    
    public override void _Ready()
    {
        Logger.Log("GameServer: Inicializado");
    }
    
    public override void _ExitTree()
    {
        Logger.Log("GameServer: _ExitTree() llamado - limpiando recursos");
        
        // Forzar detención completa del servidor
        StopServer();
    }
    
    /// <summary>Intenta iniciar servidor en el puerto configurado, si falla prueba puertos consecutivos.</summary>
    public async Task<bool> StartServerWithPortFallback()
    {
        for (int port = Port; port <= Port + 10; port++)
        {
            Port = port;
            Logger.Log($"GameServer: Intentando iniciar servidor en puerto {Port}");
            
            try
            {
                var result = await StartServer();
                if (result)
                {
                    Logger.Log($"GameServer: ✅ Servidor iniciado exitosamente en puerto {Port}");
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Logger.Log($"GameServer: ❌ Error en puerto {Port}: {ex.Message}");
            }
        }
        
        Logger.LogError($"GameServer: ❌ No se pudo iniciar el servidor en ningún puerto del {Port} al {Port + 10}");
        return false;
    }
    
    public override void _PhysicsProcess(double delta)
    {
        if (!_isRunning) return;
        
        _lastTickTime += delta;
        if (_lastTickTime >= 1.0 / ServerTickRate)
        {
            ProcessServerTick();
            _lastTickTime = 0;
        }
    }
    
    /// <summary>Procesa un tick del servidor a tasa fija.</summary>
    private void ProcessServerTick()
    {
        // Aquí se procesaría la lógica del servidor
        // Por ahora vacío ya que procesamos inputs cuando llegan
    }
    
    /// <summary>Inicia el servidor local en el puerto configurado.</summary>
    public async Task<bool> StartServer()
    {
        if (_isRunning) return false;
        
        _isRunning = true;
        _cts = new CancellationTokenSource();
        _cancellationToken = _cts.Token;
        
        try
        {
            _listener = new TcpListener(IPAddress.Parse(Host), Port);
            _listener.Start();
            
            Logger.Log($"GameServer: Servidor iniciado en {Host}:{Port}");
            
            // Iniciar escucha de clientes en segundo plano
            _ = Task.Run(AcceptClientsAsync, _cts.Token);
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameServer: Error al iniciar servidor: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>Detiene el servidor y desconecta clientes.</summary>
    public void StopServer()
    {
        _isRunning = false;
        _cts.Cancel();
        
        // Desconectar todos los jugadores
        foreach (var player in _players.Values)
        {
            player.Client?.Close();
            player.Stream?.Close();
        }
        _players.Clear();
        
        _listener?.Stop();
        
        Logger.Log($"GameServer: Servidor detenido. {_players.Count} jugadores desconectados");
    }
    
    private async Task AcceptClientsAsync()
    {
        try
        {
            while (_isRunning)
            {
                var client = await _listener!.AcceptTcpClientAsync();
                
                // Crear nuevo jugador con ID único
                var playerId = $"player_{_nextPlayerId++}";
                var playerInfo = new PlayerInfo
                {
                    PlayerId = playerId,
                    Client = client,
                    Stream = client.GetStream(),
                    Position = new Vector3(0, 2, 5), // Posición inicial
                    Rotation = Vector3.Zero,
                    LastActivity = DateTime.Now
                };
                
                _players[playerId] = playerInfo;
                
                Logger.Log($"GameServer: Jugador {playerId} conectado. Total: {_players.Count}");
                
                // Enviar estado inicial al nuevo jugador
                await SendInitialStateAsync(playerInfo);
                
                // Notificar a otros jugadores sobre el nuevo jugador
                await BroadcastPlayerJoined(playerInfo);
                
                // Iniciar procesamiento de mensajes del nuevo jugador
                _ = Task.Run(() => ProcessClientMessagesAsync(playerInfo), _cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal al cancelar
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameServer: Error aceptando clientes: {ex.Message}");
        }
    }
    
    private async Task ProcessClientMessagesAsync(PlayerInfo playerInfo)
    {
        var buffer = new byte[1024];
        
        try
        {
            while (_isRunning && playerInfo.Stream != null)
            {
                var bytesRead = await playerInfo.Stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                
                if (bytesRead == 0)
                {
                    // Cliente desconectado
                    break;
                }
                
                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                ProcessClientMessage(message, playerInfo);
                
                // Actualizar actividad del jugador
                playerInfo.LastActivity = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameServer: Error procesando mensajes de {playerInfo.PlayerId}: {ex.Message}");
        }
        finally
        {
            DisconnectPlayer(playerInfo);
        }
    }
    
    private void ProcessClientMessage(string message, PlayerInfo playerInfo)
    {
        try
        {
            // Logger.Log($"GameServer: Mensaje recibido de {playerInfo.PlayerId}: {message}");
            
            // Formato: "TIPO:datos"
            var parts = message.Split(':', 2);
            if (parts.Length < 2) return;
            
            var type = parts[0];
            var data = parts[1];
            
            Logger.Log($"GameServer: Tipo: {type}, Datos: {data}");
            
            switch (type)
            {
                case "INPUT":
                    ProcessInput(data, playerInfo);
                    break;
                    
                case "POSITION_UPDATE":
                    ProcessPositionUpdate(data, playerInfo);
                    break;
                    
                case "REQUEST_STATE":
                    SendWorldState(playerInfo);
                    break;
                    
                default:
                    Logger.Log($"GameServer: Tipo de mensaje desconocido: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameServer: Error procesando mensaje de {playerInfo.PlayerId}: {ex.Message}");
        }
    }
    
    private void ProcessInput(string inputData, PlayerInfo playerInfo)
    {
        // Formato: "INPUT:movimiento:x,y,z|rotacion:pitch,yaw"
        var parts = inputData.Split('|');
        
        Vector3 inputDirection = Vector3.Zero;
        Vector3 inputRotation = Vector3.Zero;
        
        foreach (var part in parts)
        {
            var keyValue = part.Split(':');
            if (keyValue.Length != 2) continue;
            
            var key = keyValue[0];
            var value = keyValue[1];
            
            switch (key)
            {
                case "movimiento":
                    var coords = value.Split(',');
                    if (coords.Length == 3)
                    {
                        inputDirection = new Vector3(
                            float.Parse(coords[0], CultureInfo.InvariantCulture),
                            float.Parse(coords[1], CultureInfo.InvariantCulture),
                            float.Parse(coords[2], CultureInfo.InvariantCulture)
                        );
                    }
                    break;
                    
                case "rotacion":
                    var rots = value.Split(',');
                    if (rots.Length == 2)
                    {
                        inputRotation = new Vector3(
                            float.Parse(rots[0], CultureInfo.InvariantCulture),  // pitch
                            float.Parse(rots[1], CultureInfo.InvariantCulture),  // yaw
                            0
                        );
                    }
                    break;
            }
        }
        
        // Aplicar input inmediatamente (sin esperar ticks)
        ApplyInputImmediate(inputDirection, inputRotation, playerInfo);
    }
    
    private void ProcessPositionUpdate(string positionData, PlayerInfo playerInfo)
    {
        // Este método ya no se usa en el flujo principal
        // El cliente ahora envía inputs en lugar de actualizaciones de posición
        // Logger.Log($"GameServer: ProcessPositionUpdate obsoleto - ignorando: {positionData}");
    }
    
    private void ApplyInputImmediate(Vector3 direction, Vector3 rotation, PlayerInfo playerInfo)
    {
        // Aplicar movimiento si hay dirección
        if (direction.LengthSquared() > 0.001f)
        {
            playerInfo.Position += direction.Normalized() * PlayerMoveSpeed * (1/60f);
        }
        
        // Actualizar rotación directamente (sin multiplicar)
        playerInfo.Rotation = rotation;
        
        // Logger.Log($"GameServer: Input aplicado para {playerInfo.PlayerId} - Pos: {playerInfo.Position}, Rot: {playerInfo.Rotation}");
        
        // Enviar estado actualizado solo cuando se aplica input
        BroadcastPlayerState(playerInfo);
    }
    
    private async Task SendInitialStateAsync(PlayerInfo playerInfo)
    {
        // Enviar ID del jugador junto con el estado inicial
        var message = $"STATE_INIT:{playerInfo.PlayerId}:{playerInfo.Position.X.ToString(CultureInfo.InvariantCulture)},{playerInfo.Position.Y.ToString(CultureInfo.InvariantCulture)},{playerInfo.Position.Z.ToString(CultureInfo.InvariantCulture)}|{playerInfo.Rotation.X.ToString(CultureInfo.InvariantCulture)},{playerInfo.Rotation.Y.ToString(CultureInfo.InvariantCulture)}";
        await SendMessageToPlayerAsync(playerInfo, message);
    }
    
    private async void SendWorldState(PlayerInfo playerInfo)
    {
        var message = $"STATE_UPDATE:{playerInfo.Position.X.ToString(CultureInfo.InvariantCulture)},{playerInfo.Position.Y.ToString(CultureInfo.InvariantCulture)},{playerInfo.Position.Z.ToString(CultureInfo.InvariantCulture)}|{playerInfo.Rotation.X.ToString(CultureInfo.InvariantCulture)},{playerInfo.Rotation.Y.ToString(CultureInfo.InvariantCulture)}";
        
        // Debug: mostrar estado que se va a enviar
        Logger.Log($"GameServer: Enviando estado a {playerInfo.PlayerId}: {message}");
        
        await SendMessageToPlayerAsync(playerInfo, message);
    }
    
    private async Task SendMessageToPlayerAsync(PlayerInfo playerInfo, string message)
    {
        if (playerInfo.Stream == null) return;
        
        try
        {
            var buffer = Encoding.UTF8.GetBytes(message + "\n"); // Añadir separador de línea
            await playerInfo.Stream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameServer: Error enviando mensaje a {playerInfo.PlayerId}: {ex.Message}");
        }
    }
    
    private async Task BroadcastPlayerState(PlayerInfo playerInfo)
    {
        var message = $"PLAYER_STATE:{playerInfo.PlayerId}:{playerInfo.Position.X.ToString(CultureInfo.InvariantCulture)},{playerInfo.Position.Y.ToString(CultureInfo.InvariantCulture)},{playerInfo.Position.Z.ToString(CultureInfo.InvariantCulture)}|{playerInfo.Rotation.X.ToString(CultureInfo.InvariantCulture)},{playerInfo.Rotation.Y.ToString(CultureInfo.InvariantCulture)}";
        
        // Enviar a todos los jugadores excepto al que envió el estado
        foreach (var otherPlayer in _players.Values.Where(p => p.PlayerId != playerInfo.PlayerId))
        {
            await SendMessageToPlayerAsync(otherPlayer, message);
        }
    }
    
    private async Task BroadcastPlayerJoined(PlayerInfo newPlayer)
    {
        var message = $"PLAYER_JOINED:{newPlayer.PlayerId}:{newPlayer.Position.X.ToString(CultureInfo.InvariantCulture)},{newPlayer.Position.Y.ToString(CultureInfo.InvariantCulture)},{newPlayer.Position.Z.ToString(CultureInfo.InvariantCulture)}|{newPlayer.Rotation.X.ToString(CultureInfo.InvariantCulture)},{newPlayer.Rotation.Y.ToString(CultureInfo.InvariantCulture)}";
        
        // Enviar a todos los jugadores excepto al nuevo
        foreach (var existingPlayer in _players.Values.Where(p => p.PlayerId != newPlayer.PlayerId))
        {
            await SendMessageToPlayerAsync(existingPlayer, message);
        }
    }
    
    private async Task BroadcastPlayerLeft(string playerId)
    {
        var message = $"PLAYER_LEFT:{playerId}";
        
        // Enviar a todos los jugadores restantes
        foreach (var player in _players.Values)
        {
            await SendMessageToPlayerAsync(player, message);
        }
    }
    
    private void DisconnectPlayer(PlayerInfo playerInfo)
    {
        if (_players.ContainsKey(playerInfo.PlayerId))
        {
            _players.Remove(playerInfo.PlayerId);
            playerInfo.Client?.Close();
            playerInfo.Stream?.Close();
            
            Logger.Log($"GameServer: Jugador {playerInfo.PlayerId} desconectado. Total: {_players.Count}");
            
            // Notificar a otros jugadores sobre la desconexión
            _ = Task.Run(() => BroadcastPlayerLeft(playerInfo.PlayerId), _cancellationToken);
        }
    }
    
    }
