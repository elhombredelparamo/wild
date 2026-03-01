using Godot;
using Wild.Network;

namespace Wild;

/// <summary>
/// Autoload que centraliza el flujo del juego: menú principal, partida, servidor local.
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
    
    /// <summary>Carga una partida existente.</summary>
    public async void LoadGame(string worldName)
    {
        Logger.Log($"GameFlow: LoadGame() - cargando mundo: {worldName}");
        
        try
        {
            // Cargar información del mundo
            var worldInfo = WorldManager.Instance.LoadWorldInfo(worldName);
            if (worldInfo == null)
            {
                Logger.LogError($"GameFlow: ERROR - No se pudo cargar información del mundo: {worldName}");
                return;
            }
            
            Logger.Log($"GameFlow: Mundo cargado: {worldInfo.Name} (Seed: {worldInfo.Seed})");
            
            // Iniciar partida con el mundo cargado
            await StartGameWithWorld(worldInfo);
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
            
            // Actualizar botón para mostrar estado de carga
            UpdateCreateGameButton("Iniciando servidor...");
            
            // TODO: Aquí se pasaría la información del mundo al servidor
            // Por ahora, iniciamos servidor como antes
            
            Logger.Log("GameFlow: Paso 1 - Iniciando servidor local");
            var serverStarted = await _gameServer.StartServerWithPortFallback();
            Logger.Log($"GameFlow: Servidor iniciado: {serverStarted}");
            
            if (!serverStarted)
            {
                Logger.LogError("GameFlow: ERROR CRÍTICO - No se pudo iniciar el servidor local");
                _isGameStarting = false;
                UpdateCreateGameButton("Crear partida"); // Restaurar texto
                return;
            }
            
            Logger.Log("GameFlow: Paso 2 - Conectando cliente al servidor");
            var clientConnected = await _gameClient.ConnectToServer();
            Logger.Log($"GameFlow: Cliente conectado: {clientConnected}");
            
            if (!clientConnected)
            {
                Logger.LogError("GameFlow: ERROR CRÍTICO - No se pudo conectar el cliente al servidor");
                _gameServer.StopServer();
                _isGameStarting = false;
                UpdateCreateGameButton("Crear partida"); // Restaurar texto
                return;
            }
            
            Logger.Log("GameFlow: Paso 3 - Verificando escena");
            if (!Godot.FileAccess.FileExists(SceneGameWorld))
            {
                Logger.LogError($"GameFlow: ERROR CRÍTICO - La escena no existe: {SceneGameWorld}");
                _isGameStarting = false;
                UpdateCreateGameButton("Crear partida"); // Restaurar texto
                return;
            }
            
            Logger.Log($"GameFlow: Paso 4 - Cambiando a escena del mundo: {worldInfo.Name}");
            
            try
            {
                GetTree().CurrentScene.TreeExiting += OnCurrentSceneExiting;
                GetTree().ChangeSceneToFile(SceneGameWorld);
                Logger.Log($"GameFlow: ChangeSceneToFile llamado sin excepciones");
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

    /// <summary>Vuelve al menú principal (cierra servidor local y desconecta cliente).</summary>
    public void ReturnToMainMenu()
    {
        Logger.Log("GameFlow: ReturnToMainMenu() - deteniendo servidor y cliente");
        
        // Guardar posición del jugador ANTES de detener el servidor
        var gameWorld = GetTree().CurrentScene as GameWorld;
        if (gameWorld != null)
        {
            Logger.Log("GameFlow: Guardando posición del jugador ANTES de detener el servidor");
            gameWorld.Call("SavePlayerPosition");
            
            // Pequeña pausa para asegurar que el guardado se complete
            System.Threading.Tasks.Task.Delay(50).Wait();
        }
        
        // Resetear flag de inicio de juego
        _isStartingGame = false;
        
        // Detener componentes de red
        _gameClient.Disconnect();
        _gameServer.StopServer();
        
        // Mostrar cursor para el menú principal
        Input.MouseMode = Input.MouseModeEnum.Visible;
        
        GetTree().ChangeSceneToFile(SceneMainMenu);
    }

    /// <summary>Sale del juego.</summary>
    public void QuitGame()
    {
        GetTree().Quit();
    }
}
