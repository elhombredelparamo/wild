using Godot;
using Wild.Network;
using Wild.Scripts.Terrain;
using Wild.Scripts.Player;
using Wild.Systems;
using System.Threading.Tasks;

namespace Wild;

/// <summary>
/// Autoload que centraliza el flujo del juego: menú principal, partida, cliente-servidor.
/// Ver contexto/menus-y-servidor.txt para la arquitectura.
/// </summary>
public partial class GameFlow : Node
{
    public const string SceneMainMenu = "res://scenes/main_menu.tscn";
    public const string SceneNewGameMenu = "res://scenes/new_game_menu.tscn";
    public const string SceneWorldSelectMenu = "res://scenes/world_select_menu.tscn";
    public const string SceneOptionsMenu = "res://scenes/options_menu.tscn";
    public const string SceneCharacterSelectMenu = "res://scenes/character_select_menu.tscn";
    public const string SceneCharacterCreateMenu = "res://scenes/character_create_menu.tscn";
    public const string SceneGameWorld = "res://scenes/game_world.tscn";
    public const string SceneLoading = "res://scenes/loading_scene.tscn";
    
    // Componentes de red
    private GameServer _gameServer = null!;
    private GameClient _gameClient = null!;
    private bool _isStartingGame = false;
    private bool _isGameStarting = false; // Nuevo flag para bloquear el botón
    
    public override void _Ready()
    {
        Logger.Log("GameFlow: Inicializando componentes de red");
        
        // Crear componentes de red
        _gameServer = new GameServer();
        _gameClient = new GameClient();
        
        // Añadir como hijos para que se gestionen automáticamente
        AddChild(_gameServer);
        AddChild(_gameClient);
        
        Logger.Log("GameFlow: Componentes de red inicializados");
        
        // Verificar si estamos en el menú principal
        var currentScene = GetTree().CurrentScene;
        Logger.Log($"GameFlow: Escena actual: {currentScene?.Name} ({currentScene?.SceneFilePath})");
    }

    /// <summary>Actualiza el texto del botón de nueva partida en el menú principal.</summary>
    public void UpdateNewGameButton(string text)
    {
        Logger.Log($"GameFlow: UpdateNewGameButton() llamado con texto: {text}");
        
        // Buscar el botón en el menú principal y actualizar su texto
        var mainMenu = GetTree().CurrentScene;
        if (mainMenu?.Name == "MainMenu")
        {
            var button = mainMenu.GetNode<Button>("CenterContainer/VBoxContainer/ButtonNewGame");
            if (button != null)
            {
                button.Text = text;
                Logger.Log($"GameFlow: Botón Nueva partida actualizado a: {text}");
            }
            else
            {
                Logger.LogError("GameFlow: ERROR - No se encontró el botón ButtonNewGame en MainMenu");
            }
        }
        else
        {
            Logger.Log($"GameFlow: Escena actual no es MainMenu: {mainMenu?.Name}");
        }
    }

    /// <summary>Actualiza el texto del botón de crear partida en el menú de nueva partida.</summary>
    public void UpdateCreateGameButton(string text)
    {
        Logger.Log($"GameFlow: UpdateCreateGameButton() llamado con texto: {text}");
        
        // Buscar el botón en el menú de nueva partida y actualizar su texto
        var newGameMenu = GetTree().CurrentScene;
        if (newGameMenu?.Name == "NewGameMenu")
        {
            var button = newGameMenu.GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/Buttons/ButtonCreate");
            if (button != null)
            {
                button.Text = text;
                Logger.Log($"GameFlow: Botón Crear partida actualizado a: {text}");
            }
            else
            {
                Logger.LogError("GameFlow: ERROR - No se encontró el botón ButtonCreate en NewGameMenu");
            }
        }
        else
        {
            Logger.Log($"GameFlow: Escena actual no es NewGameMenu: {newGameMenu?.Name}");
        }
    }

    /// <summary>Abre el menú de selección de mundos.</summary>
    public void OpenWorldSelectMenu()
    {
        Logger.Log("GameFlow: Abriendo menú de selección de mundos");
        GetTree().ChangeSceneToFile(SceneWorldSelectMenu);
    }

    /// <summary>Abre el menú de opciones (controles, gráficos).</summary>
    public void OpenOptions()
    {
        Logger.Log("GameFlow: Abriendo menú de opciones");
        GetTree().ChangeSceneToFile(SceneOptionsMenu);
    }

    /// <summary>Abre el menú de selección de personajes.</summary>
    public void OpenCharacterSelectMenu()
    {
        Logger.Log("GameFlow: Abriendo menú de selección de personajes");
        GetTree().ChangeSceneToFile(SceneCharacterSelectMenu);
    }

    /// <summary>Abre el menú de creación de personajes.</summary>
    public void OpenCharacterCreateMenu()
    {
        Logger.Log("GameFlow: Abriendo menú de creación de personajes");
        GetTree().ChangeSceneToFile(SceneCharacterCreateMenu);
    }

    /// <summary>Abre el menú principal.</summary>
    public void OpenMainMenu()
    {
        Logger.Log("GameFlow: Abriendo menú principal");
        GetTree().ChangeSceneToFile(SceneMainMenu);
    }

    /// <summary>Abre el menú de creación de nueva partida (semilla, personaje, mundo).</summary>
    public void OpenNewGameMenu()
    {
        GetTree().ChangeSceneToFile(SceneNewGameMenu);
    }

    /// <summary>Inicia una partida nueva creando un nuevo mundo.</summary>
    public async void StartNewGame(string worldName = "")
    {
        // Protección contra múltiples clics
        if (_isStartingGame || _isGameStarting)
        {
            Logger.Log("GameFlow: StartNewGame() ya en ejecución - ignorando clic duplicado");
            return;
        }
        
        _isGameStarting = true;
        
        Logger.Log($"GameFlow: StartNewGame() - creando nuevo mundo: {worldName}");
        
        // Actualizar botón para mostrar estado de carga
        UpdateCreateGameButton("Creando mundo...");
        
        // Pequeña pausa para que el usuario vea el estado
        await Task.Delay(500);
        
        try
        {
            // Crear nuevo mundo
            var worldInfo = WorldManager.Instance.CreateWorld(worldName);
            if (worldInfo == null)
            {
                Logger.LogError("GameFlow: ERROR CRÍTICO - No se pudo crear el mundo");
                _isGameStarting = false;
                UpdateCreateGameButton("Crear partida"); // Restaurar texto
                return;
            }
            
            Logger.Log($"GameFlow: Mundo creado: {worldInfo.Name} (Seed: {worldInfo.Seed})");
            
            // Iniciar partida con el nuevo mundo
            await StartGameWithWorld(worldInfo);
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameFlow: EXCEPCIÓN CRÍTICA en StartNewGame: {ex.Message}");
            _isGameStarting = false;
            UpdateCreateGameButton("Crear partida"); // Restaurar texto
        }
    }
    
    /// <summary>
    /// Inicia el juego con un mundo específico (cargado)
    /// </summary>
    private async Task StartGameWithLoadedWorld(WorldInfo worldInfo)
    {
        try
        {
            Logger.Log($"GameFlow: Iniciando partida con mundo cargado: {worldInfo.Name}");
            
            // Establecer el mundo actual ANTES de cambiar de escena
            WorldManager.Instance.SetCurrentWorld(worldInfo.Name);
            Logger.Log($"GameFlow: Mundo actual establecido: {worldInfo.Name}");
            
            // Configurar SessionData para que LoadingScene pueda acceder al nombre del mundo
            var sessionData = GetNode<SessionData>("/root/SessionData");
            if (sessionData != null)
            {
                sessionData.WorldName = worldInfo.Name;
                // Convertir Seed de string a long
                if (long.TryParse(worldInfo.Seed, out long seedValue))
                {
                    sessionData.WorldSeed = seedValue;
                }
                else
                {
                    sessionData.WorldSeed = 0; // Valor por defecto si no se puede convertir
                    Logger.LogWarning($"GameFlow: No se pudo convertir la semilla '{worldInfo.Seed}' a long, usando 0");
                }
                Logger.Log($"GameFlow: SessionData configurado - WorldName: {worldInfo.Name}, Seed: {sessionData.WorldSeed}");
            }
            else
            {
                Logger.LogError("GameFlow: ERROR - No se encontró SessionData");
            }
            
            Logger.Log($"GameFlow: Cambiando a escena de carga para mundo: {worldInfo.Name}");
            
            try
            {
                // Guardar el estado del personaje actual antes de cambiar de escena
                CharacterManager.SaveCurrentCharacterState();
                
                // Cambiar a la escena de carga
                GetTree().ChangeSceneToFile(SceneLoading);
                Logger.Log($"GameFlow: Cambiando a escena de carga sin excepciones");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"GameFlow: ERROR en ChangeSceneToFile: {ex.Message}");
                _isGameStarting = false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameFlow: EXCEPCIÓN CRÍTICA en StartGameWithLoadedWorld: {ex.Message}");
            _gameServer.StopServer();
            _gameClient.Disconnect();
            _isGameStarting = false;
        }
        finally
        {
            _isStartingGame = false;
        }
    }
    
    /// <summary>Carga una partida existente.</summary>
    public async void LoadGame(string worldName, string characterId = null)
    {
        Logger.Log($"GameFlow: LoadGame() - cargando mundo: {worldName}, personaje: {characterId ?? "usando actual"}");
        
        try
        {
            // Si se proporciona un ID de personaje, establecerlo como actual
            if (!string.IsNullOrEmpty(characterId))
            {
                CharacterManager.Instance.SelectCharacter(characterId);
                Logger.Log($"GameFlow: Personaje seleccionado para carga: {characterId}");
            }
            // Cargar información del mundo
            var worldInfo = WorldManager.Instance.LoadWorldInfo(worldName);
            if (worldInfo == null)
            {
                Logger.LogError($"GameFlow: ERROR - No se pudo cargar información del mundo: {worldName}");
                return;
            }
            
            Logger.Log($"GameFlow: Mundo cargado: {worldInfo.Name} (Seed: {worldInfo.Seed})");
            
            // Iniciar partida con el mundo cargado
            await StartGameWithLoadedWorld(worldInfo);
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameFlow: Error en LoadGame: {ex.Message}");
        }
    }
    
    /// <summary>Inicia el juego con un mundo específico (nuevo o cargado).</summary>
    private async Task StartGameWithWorld(WorldInfo worldInfo)
    {
        try
        {
            Logger.Log($"GameFlow: Iniciando partida con mundo: {worldInfo.Name}");
            
            // Establecer el mundo actual ANTES de cambiar de escena
            WorldManager.Instance.SetCurrentWorld(worldInfo.Name);
            Logger.Log($"GameFlow: Mundo actual establecido: {worldInfo.Name}");
            
            // Configurar SessionData para que GameWorld pueda acceder al nombre del mundo
            var sessionData = GetNode<SessionData>("/root/SessionData");
            if (sessionData != null)
            {
                sessionData.WorldName = worldInfo.Name;
                // Convertir Seed de string a long
                if (long.TryParse(worldInfo.Seed, out long seedValue))
                {
                    sessionData.WorldSeed = seedValue;
                }
                else
                {
                    sessionData.WorldSeed = 0; // Valor por defecto si no se puede convertir
                    Logger.LogWarning($"GameFlow: No se pudo convertir la semilla '{worldInfo.Seed}' a long, usando 0");
                }
                Logger.Log($"GameFlow: SessionData configurado - WorldName: {worldInfo.Name}, Seed: {sessionData.WorldSeed}");
            }
            else
            {
                Logger.LogError("GameFlow: ERROR - No se encontró SessionData");
            }
            
            Logger.Log($"GameFlow: Cambiando a escena de carga para mundo: {worldInfo.Name}");
            
            try
            {
                // Validar que haya un personaje seleccionado antes de iniciar la red
                if (string.IsNullOrEmpty(CharacterManager.PersistentCharacterId))
                {
                    Logger.LogError("GameFlow: No hay personaje seleccionado para iniciar la partida");
                    throw new InvalidOperationException("Se requiere seleccionar un personaje antes de iniciar la partida");
                }
                
                Logger.Log($"GameFlow: Iniciando red con personaje: {CharacterManager.PersistentCharacterId}");
                
                // Iniciar el servidor antes de cambiar de escena
                Logger.Log("GameFlow: Iniciando servidor...");
                _gameServer.StartServer();
                
                // Esperar un momento a que el servidor se inicie completamente
                await Task.Delay(100);
                
                // Conectar el cliente al servidor
                Logger.Log("GameFlow: Conectando cliente al servidor...");
                await _gameClient.ConnectToServer();
                
                // Esperar a que se establezca la conexión
                await Task.Delay(100);
                
                Logger.Log($"GameFlow: Servidor iniciado y cliente conectado - IsConnected: {_gameClient.IsConnected}");
                
                // Guardar el estado del personaje actual antes de cambiar de escena
                CharacterManager.SaveCurrentCharacterState();
                
                // Cambiar a la escena de carga (no a GameWorld directamente)
                GetTree().ChangeSceneToFile(SceneLoading);
                Logger.Log($"GameFlow: Cambiando a escena de carga sin excepciones");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"GameFlow: ERROR en ChangeSceneToFile: {ex.Message}");
                _isGameStarting = false;
                UpdateCreateGameButton("Crear partida"); // Restaurar texto
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameFlow: EXCEPCIÓN CRÍTICA en StartGameWithWorld: {ex.Message}");
            _gameServer.StopServer();
            _gameClient.Disconnect();
            _isGameStarting = false;
            UpdateCreateGameButton("Crear partida"); // Restaurar texto
        }
        finally
        {
            _isStartingGame = false;
        }
    }
    
    /// <summary>Elimina el método AddLoadingOverlay ya que no se necesita.</summary>

    /// <summary>Se llama cuando la escena actual está saliendo (antes del cambio).</summary>
    private void OnCurrentSceneExiting()
    {
        Logger.Log($"GameFlow: Escena actual saliendo: {GetTree().CurrentScene?.Name}");
        // Esperar un frame y verificar la nueva escena
        CallDeferred(nameof(VerifySceneChanged));
    }

    /// <summary>Verifica si la escena ha cambiado correctamente.</summary>
    private void VerifySceneChanged()
    {
        var currentScene = GetTree().CurrentScene;
        Logger.Log($"GameFlow: Verificando escena actual después del cambio: {currentScene?.Name}");
        
        if (currentScene?.Name == "GameWorld")
        {
            Logger.Log("GameFlow: ✅ Escena GameWorld cargada correctamente");
            _isGameStarting = false; // Liberar el botón
            UpdateCreateGameButton("Crear partida"); // Restaurar texto
        }
        else
        {
            Logger.LogError($"GameFlow: ❌ Error - Escena esperada: GameWorld, actual: {currentScene?.Name}");
            _isGameStarting = false;
            UpdateCreateGameButton("Crear partida");
        }
    }

    /// <summary>Obtiene el cliente de red de forma segura.</summary>
    public GameClient GetGameClient()
    {
        return _gameClient;
    }

    /// <summary>Notifica que el GameWorld está listo para ser usado (método obsoleto).</summary>
    public void NotifyGameWorldReady()
    {
        Logger.Log("GameFlow: NotifyGameWorldReady llamado (método obsoleto con nuevo flujo)");
        // Este método ya no se necesita con el nuevo flujo LoadingScene → GameWorld
    }
    
    /// <summary>Inicia el servidor y conecta el cliente (llamado por LoadingScene).</summary>
    public async Task<bool> InitializeNetwork()
    {
        try
        {
            // Primero detener cualquier servidor existente
            Logger.Log("GameFlow: Deteniendo servidor existente antes de iniciar nuevo");
            _gameServer.StopServer();
            await Task.Delay(100); // Pequeña pausa para asegurar limpieza
            
            Logger.Log("GameFlow: Iniciando servidor...");
            var serverStarted = await _gameServer.StartServerWithPortFallback();
            Logger.Log($"GameFlow: Servidor iniciado: {serverStarted}");
            
            if (!serverStarted)
            {
                Logger.LogError("GameFlow: ERROR CRÍTICO - No se pudo iniciar el servidor");
                return false;
            }
            
            Logger.Log("GameFlow: Conectando cliente al servidor...");
            var clientConnected = await _gameClient.ConnectToServer();
            Logger.Log($"GameFlow: Cliente conectado: {clientConnected}");
            
            if (!clientConnected)
            {
                Logger.LogError("GameFlow: ERROR CRÍTICO - No se pudo conectar el cliente al servidor");
                _gameServer.StopServer();
                return false;
            }
            
            Logger.Log("GameFlow: ✅ Red inicializada correctamente");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"GameFlow: Error en InitializeNetwork: {ex.Message}");
            _gameServer.StopServer();
            _gameClient.Disconnect();
            return false;
        }
    }

    /// <summary>Vuelve al menú principal (cierra servidor y desconecta cliente).</summary>
    public void ReturnToMainMenu()
    {
        Logger.Log("GameFlow: ReturnToMainMenu() - INICIANDO CIERRE ORDENADO");
        Logger.Log($"GameFlow: Estado actual - IsStartingGame: {_isStartingGame}, IsGameStarting: {_isGameStarting}");
        
        // FASE 1: CONGELAR JUGADOR Y DETENER SISTEMAS ACTIVOS
        Logger.Log("GameFlow: 🥶 FASE 1 - Congelando jugador y deteniendo sistemas activos...");
        
        // Resetear flags de inicio de juego
        _isStartingGame = false;
        _isGameStarting = false;
        
        // Congelar jugador
        var gameWorld = GetTree().CurrentScene as GameWorld;
        if (gameWorld != null)
        {
            gameWorld.Call("FreezePlayer");
        }
        
        // Detener auto-save - buscar PlayerPersistence en el GameWorld actual
        try
        {
            PlayerPersistence playerPersistence = null;
            
            // Primero intentar obtenerlo del GameWorld actual
            if (gameWorld != null)
            {
                playerPersistence = gameWorld.GetNode<PlayerPersistence>("PlayerPersistence");
            }
            
            // Si no se encuentra, intentar en el árbol raíz (fallback)
            if (playerPersistence == null)
            {
                playerPersistence = GetTree().Root.GetNode<PlayerPersistence>("PlayerPersistence");
            }
            
            if (playerPersistence != null && IsInstanceValid(playerPersistence))
            {
                playerPersistence.StopAutoSave();
                Logger.Log("GameFlow: ✅ Auto-save detenido");
            }
            else
            {
                Logger.Log("GameFlow: ⚠️ PlayerPersistence no encontrado o inválido");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error deteniendo auto-save: {ex.Message}");
        }
        
        // FASE 2: CERRAR CONEXIONES DE RED
        Logger.Log("GameFlow: 🌐 FASE 2 - Cerrando conexiones de red...");
        
        try
        {
            _gameClient.Disconnect();
            Logger.Log("GameFlow: ✅ Cliente desconectado");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error desconectando cliente: {ex.Message}");
        }
        
        try
        {
            _gameServer.StopServer();
            Logger.Log("GameFlow: ✅ Servidor detenido");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error deteniendo servidor: {ex.Message}");
        }
        
        // Resetear NetworkManager
        try
        {
            var networkManager = NetworkManager.Instance;
            if (networkManager != null)
            {
                networkManager.Reset();
                Logger.Log("GameFlow: ✅ NetworkManager reseteado");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error reseteando NetworkManager: {ex.Message}");
        }
        
        // RESETEAR SINGLETONS CRÍTICOS - NUEVO
        Logger.Log("GameFlow: 🔄 RESETEANDO SINGLETONS CRÍTICOS...");
        
        try
        {
            // Resetear NetworkManager singleton (desconecta eventos y libera instancia)
            NetworkManager.ResetSingleton();
            Logger.Log("GameFlow: ✅ NetworkManager singleton reseteado");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error reseteando NetworkManager singleton: {ex.Message}");
        }
        
        try
        {
            // Resetear PlayerController singleton (limpia instancia y estado)
            PlayerController.ResetSingleton();
            Logger.Log("GameFlow: ✅ PlayerController singleton reseteado");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error reseteando PlayerController singleton: {ex.Message}");
        }
        
        try
        {
            // Resetear CharacterManager singleton (guarda estado y limpia datos)
            CharacterManager.ResetSingleton();
            Logger.Log("GameFlow: ✅ CharacterManager singleton reseteado");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error reseteando CharacterManager singleton: {ex.Message}");
        }
        
        // FASE 3: GUARDAR DATOS FINALES
        Logger.Log("GameFlow: 💾 FASE 3 - Guardando datos finales...");
        
        if (gameWorld != null)
        {
            try
            {
                gameWorld.Call("SavePlayerPosition");
                Logger.Log("GameFlow: ✅ Posición del jugador guardada");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"GameFlow: Error guardando posición: {ex.Message}");
            }
        }
        
        // FASE 4: LIBERAR RECURSOS
        Logger.Log("GameFlow: �️ FASE 4 - Liberando recursos...");
        
        // Limpiar TerrainManager
        try
        {
            var terrainManager = TerrainManager.Instance;
            if (terrainManager != null)
            {
                terrainManager.CleanupAllChunks();
                terrainManager.Reset();
                Logger.Log("GameFlow: ✅ TerrainManager limpiado");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error limpiando TerrainManager: {ex.Message}");
        }
        
        // Limpiar GameWorldInitializer (incluye DynamicChunkLoader)
        try
        {
            if (GameWorldInitializer.Instance != null)
            {
                GameWorldInitializer.Instance.Cleanup();
                Logger.Log("GameFlow: ✅ GameWorldInitializer limpiado");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Error limpiando GameWorldInitializer: {ex.Message}");
        }
        
        // Forzar garbage collection
        System.GC.Collect();
        System.Threading.Tasks.Task.Delay(100).Wait();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        Logger.Log("GameFlow: ✅ Garbage collection completado");
        
        // FASE 5: CAMBIAR A MENÚ PRINCIPAL
        Logger.Log("GameFlow: � FASE 5 - Cambiando a menú principal");
        
        // Mostrar cursor para el menú principal
        Input.MouseMode = Input.MouseModeEnum.Visible;
        
        Logger.Log("GameFlow: 🚀 Iniciando cambio de escena a MainMenu...");
        GetTree().ChangeSceneToFile(SceneMainMenu);
        Logger.Log("GameFlow: ✅ Escena cambiada a MainMenu - CIERRE COMPLETADO");
    }

    /// <summary>Sale del juego.</summary>
    public void QuitGame()
    {
        GetTree().Quit();
    }
}
