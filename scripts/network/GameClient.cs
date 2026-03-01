using Godot;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace Wild.Network;

/// <summary>
/// Información de un jugador remoto
/// </summary>
public class RemotePlayer
{
    public string PlayerId { get; set; } = string.Empty;
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero;
}

/// <summary>
/// Cliente local que conecta con el servidor local.
/// Envía inputs del jugador y recibe estados del mundo.
/// </summary>
public partial class GameClient : Node
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _isConnected = false;
    private CancellationTokenSource _cancellationToken = new();
    
    // Estado local (sincronizado con servidor)
    private Vector3 _serverPosition = Vector3.Zero;
    private Vector3 _serverRotation = Vector3.Zero;
    private string _localPlayerId = string.Empty; // ID de nuestro jugador
    
    // Jugadores remotos
    private Dictionary<string, RemotePlayer> _remotePlayers = new();
    
    // Configuración
    private const int Port = 7778;
    private const string Host = "127.0.0.1";
    
    // Eventos para notificar al GameWorld
    [Signal]
    public delegate void OnPositionUpdatedEventHandler(Vector3 position);
    
    [Signal]
    public delegate void OnRotationUpdatedEventHandler(Vector3 rotation);
    
    [Signal]
    public delegate void OnLocalPlayerIdAssignedEventHandler(string playerId);
    
    [Signal]
    public delegate void OnRemotePlayerJoinedEventHandler(string playerId, Vector3 position, Vector3 rotation);
    
    [Signal]
    public delegate void OnRemotePlayerLeftEventHandler(string playerId);
    
    [Signal]
    public delegate void OnRemotePlayerUpdatedEventHandler(string playerId, Vector3 position, Vector3 rotation);
    
    public override void _Ready()
    {
        Logger.Log("GameClient: Inicializado");
    }
    
    /// <summary>Conecta con el servidor local.</summary>
    public async Task<bool> ConnectToServer()
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(Host, Port);
            
            if (_client.Connected)
            {
                _stream = _client.GetStream();
                _isConnected = true;
                
                Logger.Log($"GameClient: Conectado al servidor {Host}:{Port}");
                
                // Iniciar recepción de mensajes en segundo plano
                _ = Task.Run(ReceiveMessagesAsync, _cancellationToken.Token);
                
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameClient: Error conectando al servidor: {ex.Message}");
        }
        
        return false;
    }
    
    /// <summary>Desconecta del servidor.</summary>
    public void Disconnect()
    {
        _isConnected = false;
        _cancellationToken.Cancel();
        
        _stream?.Close();
        _client?.Close();
        
        _stream = null;
        _client = null;
        
        Logger.Log("GameClient: Desconectado del servidor");
    }
    
    /// <summary>Envía input del jugador al servidor.</summary>
    public async Task SendPlayerInput(Vector3 direction, Vector3 rotation)
    {
        if (!_isConnected || _stream == null) return;
        
        try
        {
            // Formato: "INPUT:movimiento:x,y,z|rotacion:pitch,yaw"
            var message = $"INPUT:movimiento:{direction.X.ToString(CultureInfo.InvariantCulture)},{direction.Y.ToString(CultureInfo.InvariantCulture)},{direction.Z.ToString(CultureInfo.InvariantCulture)}|rotacion:{rotation.X.ToString(CultureInfo.InvariantCulture)},{rotation.Y.ToString(CultureInfo.InvariantCulture)}";
            
            // Debug: mostrar mensaje que se va a enviar
            // Logger.Log($"GameClient: Enviando mensaje: {message}");
            
            var buffer = Encoding.UTF8.GetBytes(message);
            await _stream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameClient: Error enviando input: {ex.Message}");
        }
    }
    
    /// <summary>Envía actualización de posición al servidor.</summary>
    public async Task SendPositionUpdate(string positionMessage)
    {
        if (!_isConnected || _stream == null) return;
        
        try
        {
            // Debug: mostrar mensaje que se va a enviar
            // Logger.Log($"GameClient: Enviando posición: {positionMessage}");
            
            var buffer = Encoding.UTF8.GetBytes(positionMessage);
            await _stream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            // Logger.LogError($"GameClient: Error enviando posición: {ex.Message}");
        }
    }
    
    /// <summary>Solicita estado completo del mundo.</summary>
    public async Task RequestWorldState()
    {
        if (!_isConnected || _stream == null) return;
        
        try
        {
            var message = "REQUEST_STATE:";
            var buffer = Encoding.UTF8.GetBytes(message);
            
            await _stream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameClient: Error solicitando estado: {ex.Message}");
        }
    }
    
    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[1024];
        
        try
        {
            while (_isConnected && _stream != null)
            {
                var message = await ReadLineAsync(_stream);
                if (!string.IsNullOrEmpty(message))
                {
                    ProcessServerMessage(message);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameClient: Error recibiendo mensajes: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }
    
    private async Task<string> ReadLineAsync(NetworkStream stream)
    {
        var buffer = new List<byte>();
        var readBuffer = new byte[1];
        
        while (true)
        {
            try
            {
                var bytesRead = await stream.ReadAsync(readBuffer, 0, 1);
                
                if (bytesRead == 0)
                {
                    Logger.Log("GameClient: Conexión cerrada por el servidor");
                    break;
                }
                
                if (readBuffer[0] == '\n')
                {
                    var line = Encoding.UTF8.GetString(buffer.ToArray());
                    // Logger.Log($"GameClient: Línea leída: '{line}'");
                    return line;
                }
                
                buffer.Add(readBuffer[0]);
            }
            catch (Exception ex)
            {
                Logger.LogError($"GameClient: Error en ReadLineAsync: {ex.Message}");
                break;
            }
        }
        
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
    
    private void ProcessServerMessage(string message)
    {
        try
        {
            // Logger.Log($"GameClient: Mensaje recibido del servidor: '{message}'");
            
            // El servidor puede enviar múltiples mensajes en una línea, separarlos
            var messages = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var msg in messages)
            {
                if (string.IsNullOrEmpty(msg.Trim())) continue;
                
                // Logger.Log($"GameClient: Procesando mensaje individual: '{msg.Trim()}'");
                
                // Formato: "TIPO:datos"
                var parts = msg.Split(':', 2);
                if (parts.Length < 2) 
                {
                    Logger.Log($"GameClient: Mensaje inválido (menos de 2 partes): '{msg}'");
                    continue;
                }
                
                var type = parts[0];
                var data = parts[1];
                
                // Logger.Log($"GameClient: Procesando mensaje - Tipo: '{type}', Datos: '{data}'");
                
                switch (type)
                {
                    case "STATE_INIT":
                        Logger.Log("GameClient: Caso STATE_INIT detectado");
                        ProcessStateInit(data);
                        break;
                        
                    case "STATE_UPDATE":
                        Logger.Log("GameClient: Caso STATE_UPDATE detectado");
                        ProcessStateUpdate(data);
                        break;
                        
                    case "PLAYER_STATE":
                        Logger.Log("GameClient: Caso PLAYER_STATE detectado");
                        ProcessPlayerState(data);
                        break;
                        
                    case "PLAYER_JOINED":
                        Logger.Log("GameClient: Caso PLAYER_JOINED detectado");
                        ProcessPlayerJoined(data);
                        break;
                        
                    case "PLAYER_LEFT":
                        Logger.Log("GameClient: Caso PLAYER_LEFT detectado");
                        ProcessPlayerLeft(data);
                        break;
                        
                    default:
                        Logger.Log($"GameClient: Tipo de mensaje desconocido: {type}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameClient: Error procesando mensaje: {ex.Message}");
        }
    }
    
    private void ProcessStateUpdate(string stateData)
    {
        // Logger.Log($"GameClient: ProcessStateUpdate llamado con datos: '{stateData}'");
        
        // Formato: "x,y,z|pitch,yaw"
        var parts = stateData.Split('|');
        if (parts.Length != 2) 
        {
            Logger.Log($"GameClient: STATE_UPDATE inválido (esperado 2 partes, recibido {parts.Length}): '{stateData}'");
            return;
        }
        
        // Posición
        var posCoords = parts[0].Split(',');
        if (posCoords.Length == 3)
        {
            _serverPosition = new Vector3(
                float.Parse(posCoords[0], CultureInfo.InvariantCulture),
                float.Parse(posCoords[1], CultureInfo.InvariantCulture),
                float.Parse(posCoords[2], CultureInfo.InvariantCulture)
            );
            
            // Logger.Log($"GameClient: Posición actualizada: {_serverPosition}");
            
            // Notificar al GameWorld
            CallDeferred(nameof(EmitPositionUpdated), _serverPosition);
        }
        else
        {
            // Logger.Log($"GameClient: Coordenadas de posición inválidas (esperado 3, recibido {posCoords.Length}): '{parts[0]}'");
        }
        
        // Rotación
        var rotCoords = parts[1].Split(',');
        if (rotCoords.Length == 2)
        {
            _serverRotation = new Vector3(
                float.Parse(rotCoords[0], CultureInfo.InvariantCulture),
                float.Parse(rotCoords[1], CultureInfo.InvariantCulture),
                0
            );
            
            // Logger.Log($"GameClient: Rotación actualizada: {_serverRotation}");
            
            // Notificar al GameWorld
            CallDeferred(nameof(EmitRotationUpdated), _serverRotation);
        }
        else
        {
            Logger.Log($"GameClient: Coordenadas de rotación inválidas (esperado 2, recibido {rotCoords.Length}): '{parts[1]}'");
        }
    }
    
    private void ProcessStateInit(string stateData)
    {
        Logger.Log($"GameClient: ProcessStateInit llamado con datos: '{stateData}'");
        
        // Formato: "STATE_INIT:playerId:x,y,z|pitch,yaw"
        var parts = stateData.Split(':', 3);
        if (parts.Length < 3) return;
        
        var playerId = parts[0];
        var positionData = parts[1];
        var rotationData = parts[2];
        
        // Almacenar nuestro ID de jugador
        _localPlayerId = playerId;
        Logger.Log($"GameClient: ID de jugador local asignado: {_localPlayerId}");
        
        // Notificar al GameWorld sobre nuestro ID
        CallDeferred(nameof(EmitLocalPlayerIdAssigned), _localPlayerId);
        
        // Procesar posición y rotación inicial
        ProcessStateUpdate($"{positionData}|{rotationData}");
    }
    
    private void ProcessPlayerState(string stateData)
    {
        // Formato: "PLAYER_STATE:playerId:x,y,z|pitch,yaw"
        var parts = stateData.Split(':', 3);
        if (parts.Length < 3) return;
        
        var playerId = parts[0];
        var positionData = parts[1];
        var rotationData = parts[2];
        
        // Ignorar actualizaciones de nuestro propio jugador
        if (playerId == _localPlayerId)
        {
            Logger.Log($"GameClient: Ignorando actualización propia del jugador {_localPlayerId}");
            return;
        }
        
        // Parse posición y rotación
        var posCoords = positionData.Split(',');
        var rotCoords = rotationData.Split(',');
        
        if (posCoords.Length == 3 && rotCoords.Length == 2)
        {
            var position = new Vector3(
                float.Parse(posCoords[0], CultureInfo.InvariantCulture),
                float.Parse(posCoords[1], CultureInfo.InvariantCulture),
                float.Parse(posCoords[2], CultureInfo.InvariantCulture)
            );
            
            var rotation = new Vector3(
                float.Parse(rotCoords[0], CultureInfo.InvariantCulture),
                float.Parse(rotCoords[1], CultureInfo.InvariantCulture),
                0
            );
            
            // Actualizar o añadir jugador remoto
            _remotePlayers[playerId] = new RemotePlayer
            {
                PlayerId = playerId,
                Position = position,
                Rotation = rotation
            };
            
            // Logger.Log($"GameClient: Jugador remoto {playerId} actualizado: pos={position}, rot={rotation}");
            
            // Notificar al GameWorld
            CallDeferred(nameof(EmitRemotePlayerUpdated), playerId, position, rotation);
        }
    }
    
    private void ProcessPlayerJoined(string joinData)
    {
        // Formato: "PLAYER_JOINED:playerId:x,y,z|pitch,yaw"
        var parts = joinData.Split(':', 3);
        if (parts.Length < 3) return;
        
        var playerId = parts[0];
        var positionData = parts[1];
        var rotationData = parts[2];
        
        // Parse posición y rotación
        var posCoords = positionData.Split(',');
        var rotCoords = rotationData.Split(',');
        
        if (posCoords.Length == 3 && rotCoords.Length == 2)
        {
            var position = new Vector3(
                float.Parse(posCoords[0], CultureInfo.InvariantCulture),
                float.Parse(posCoords[1], CultureInfo.InvariantCulture),
                float.Parse(posCoords[2], CultureInfo.InvariantCulture)
            );
            
            var rotation = new Vector3(
                float.Parse(rotCoords[0], CultureInfo.InvariantCulture),
                float.Parse(rotCoords[1], CultureInfo.InvariantCulture),
                0
            );
            
            // Añadir jugador remoto
            _remotePlayers[playerId] = new RemotePlayer
            {
                PlayerId = playerId,
                Position = position,
                Rotation = rotation
            };
            
            // Logger.Log($"GameClient: Jugador remoto {playerId} se unió: pos={position}, rot={rotation}");
            
            // Notificar al GameWorld
            CallDeferred(nameof(EmitRemotePlayerJoined), playerId, position, rotation);
        }
    }
    
    private void ProcessPlayerLeft(string leftData)
    {
        // Formato: "PLAYER_LEFT:playerId"
        var playerId = leftData;
        
        if (_remotePlayers.ContainsKey(playerId))
        {
            _remotePlayers.Remove(playerId);
            Logger.Log($"GameClient: Jugador remoto {playerId} se desconectó");
            
            // Notificar al GameWorld
            CallDeferred(nameof(EmitRemotePlayerLeft), playerId);
        }
    }
    
    private void EmitLocalPlayerIdAssigned(string playerId)
    {
        Logger.Log($"GameClient: EmitLocalPlayerIdAssigned llamado con playerId: {playerId}");
        EmitSignal(SignalName.OnLocalPlayerIdAssigned, playerId);
    }
    
    private void EmitPositionUpdated(Vector3 position)
    {
        // Logger.Log($"GameClient: EmitPositionUpdated llamado con posición: {position}");
        EmitSignal(SignalName.OnPositionUpdated, position);
    }
    
    private void EmitRotationUpdated(Vector3 rotation)
    {
        // Logger.Log($"GameClient: EmitRotationUpdated llamado con rotación: {rotation}");
        EmitSignal(SignalName.OnRotationUpdated, rotation);
    }
    
    private void EmitRemotePlayerJoined(string playerId, Vector3 position, Vector3 rotation)
    {
        // Logger.Log($"GameClient: EmitRemotePlayerJoined llamado con playerId: {playerId}, pos: {position}, rot: {rotation}");
        EmitSignal(SignalName.OnRemotePlayerJoined, playerId, position, rotation);
    }
    
    private void EmitRemotePlayerLeft(string playerId)
    {
        Logger.Log($"GameClient: EmitRemotePlayerLeft llamado con playerId: {playerId}");
        EmitSignal(SignalName.OnRemotePlayerLeft, playerId);
    }
    
    private void EmitRemotePlayerUpdated(string playerId, Vector3 position, Vector3 rotation)
    {
        // Logger.Log($"GameClient: EmitRemotePlayerUpdated llamado con playerId: {playerId}, pos: {position}, rot: {rotation}");
        EmitSignal(SignalName.OnRemotePlayerUpdated, playerId, position, rotation);
    }
    
    public new bool IsConnected => _isConnected;
    
    public override void _ExitTree()
    {
        Disconnect();
    }
}
