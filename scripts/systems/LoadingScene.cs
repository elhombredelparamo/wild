using Godot;
using Wild;
using Wild.Scripts.Terrain;
using Wild.Network;
using Wild.Scripts.Player;
using Wild.Scripts.Character;
using System.Threading.Tasks;

namespace Wild.Systems
{
    /// <summary>
    /// Escena intermedia de carga que inicializa todos los sistemas antes de cambiar al juego
    /// Funciona como escena separada, no como overlay
    /// </summary>
    public partial class LoadingScene : Control
    {
        private Label _titleLabel;
        private Label _statusLabel;
        private ProgressBar _progressBar;
        private int _totalSteps = 6;
        private int _currentStep = 0;
        
        public override void _Ready()
        {
            Name = "LoadingScene";
            
            // Configurar para llenar toda la pantalla
            AnchorLeft = 0;
            AnchorTop = 0;
            AnchorRight = 1;
            AnchorBottom = 1;
            OffsetLeft = 0;
            OffsetTop = 0;
            OffsetRight = 0;
            OffsetBottom = 0;
            
            CreateUI();
            _ = StartLoadingProcess();
        }
        
        /// <summary>
        /// Crea la interfaz de usuario simple
        /// </summary>
        private void CreateUI()
        {
            // Fondo negro
            var backgroundColor = new StyleBoxFlat();
            backgroundColor.BgColor = new Color(0.05f, 0.05f, 0.1f, 1.0f);
            AddThemeStyleboxOverride("panel", backgroundColor);
            
            // Contenedor principal que llena toda la pantalla
            var mainContainer = new VBoxContainer();
            mainContainer.Name = "VBoxContainer";
            
            // Configurar para llenar toda la pantalla
            mainContainer.AnchorLeft = 0;
            mainContainer.AnchorTop = 0;
            mainContainer.AnchorRight = 1;
            mainContainer.AnchorBottom = 1;
            mainContainer.OffsetLeft = 0;
            mainContainer.OffsetTop = 0;
            mainContainer.OffsetRight = 0;
            mainContainer.OffsetBottom = 0;
            
            AddChild(mainContainer);
            
            // Contenedor centrador para el contenido
            var centerContainer = new CenterContainer();
            centerContainer.Name = "CenterContainer";
            centerContainer.CustomMinimumSize = new Vector2(0, GetWindow().Size.Y); // Usar altura completa para centrado vertical
            mainContainer.AddChild(centerContainer);
            
            // VBoxContainer dentro del CenterContainer para alinear verticalmente
            var contentContainer = new VBoxContainer();
            contentContainer.Name = "ContentContainer";
            contentContainer.AddThemeConstantOverride("separation", 20);
            centerContainer.AddChild(contentContainer);
            
            // Título
            _titleLabel = new Label();
            _titleLabel.Text = "Wild";
            _titleLabel.AddThemeFontSizeOverride("font_size", 64);
            _titleLabel.AddThemeColorOverride("font_color", Colors.White);
            _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            contentContainer.AddChild(_titleLabel);
            
            // Separador
            var separator = new Control();
            separator.CustomMinimumSize = new Vector2(0, 50);
            contentContainer.AddChild(separator);
            
            // Texto de estado
            _statusLabel = new Label();
            _statusLabel.Text = "Iniciando carga...";
            _statusLabel.AddThemeFontSizeOverride("font_size", 24);
            _statusLabel.AddThemeColorOverride("font_color", Colors.LightGray);
            _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            contentContainer.AddChild(_statusLabel);
            
            // Barra de progreso
            _progressBar = new ProgressBar();
            _progressBar.MaxValue = _totalSteps;
            _progressBar.Value = 0;
            _progressBar.CustomMinimumSize = new Vector2(400, 30);
            contentContainer.AddChild(_progressBar);
        }
        
        /// <summary>
        /// Inicia el proceso de carga asíncrono
        /// </summary>
        private async Task StartLoadingProcess()
        {
            try
            {
                Logger.Log("LoadingScene: Iniciando proceso de carga");
                
                // Paso 1: Inicializar sistemas básicos (sin red)
                await UpdateProgress(1, "Inicializando sistemas básicos...");
                await InitializeBasicSystems();
                
                // Paso 2: Iniciar el servidor
                await UpdateProgress(2, "Iniciando servidor...");
                if (!await StartServer())
                {
                    throw new System.Exception("No se pudo iniciar el servidor");
                }
                
                // Paso 3: Generar terreno inicial
                await UpdateProgress(3, "Generando terreno...");
                await GenerateInitialTerrain();
                
                // Paso 4: Cargar componentes del jugador
                await UpdateProgress(4, "Cargando componentes del jugador...");
                await LoadPlayerComponents();
                
                // Paso 5: Conectar cliente al servidor
                await UpdateProgress(5, "Conectando al servidor...");
                if (!await ConnectClient())
                {
                    throw new System.Exception("No se pudo conectar el cliente");
                }
                
                // Paso 6: Preparar transición
                await UpdateProgress(6, "Finalizando...");
                await Task.Delay(500); // Pequeña pausa para el efecto visual
                
                // Cambiar a la escena del juego
                await TransitionToGame();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"LoadingScene: Error en carga: {ex.Message}");
                Logger.LogError($"LoadingScene: Stack trace completo: {ex.StackTrace}");
                Logger.LogError($"LoadingScene: Source: {ex.Source}");
                Logger.LogError($"LoadingScene: Inner Exception: {ex.InnerException?.Message ?? "None"}");
                
                // Mostrar error al usuario y volver al menú principal
                await ShowErrorAndReturnToMenu("Error al cargar el juego", ex.Message);
            }
        }
        
        /// <summary>
        /// Inicializa los sistemas básicos (TerrainManager, PlayerController)
        /// </summary>
        private async Task InitializeBasicSystems()
        {
            Logger.Log("LoadingScene: Inicializando sistemas básicos");
            
            // Obtener SessionData
            var sessionData = GetNode<SessionData>("/root/SessionData");
            if (sessionData == null)
            {
                throw new System.Exception("No se encontró SessionData");
            }
            
            // Verificar y reinicializar TerrainManager si es necesario
            var terrainManager = TerrainManager.Instance;
            if (terrainManager == null)
            {
                Logger.Log("LoadingScene: Creando nuevo TerrainManager");
                terrainManager = new TerrainManager();
                terrainManager.Name = "TerrainManager";
                // Añadir a la escena raíz para persistencia entre cambios de escena
                GetTree().Root.AddChild(terrainManager);
            }
            else
            {
                Logger.Log("LoadingScene: TerrainManager existe, verificando estado");
            }
            
            // Verificar si el TerrainManager está inicializado para el mundo actual
            if (!terrainManager.IsInitialized() || terrainManager.WorldDirectory != $"user://worlds/{sessionData.WorldName}")
            {
                Logger.Log($"LoadingScene: Reinicializando TerrainManager para mundo: {sessionData.WorldName}");
                terrainManager.InitializeWorld(sessionData.WorldName, (int)sessionData.WorldSeed);
            }
            else
            {
                Logger.Log($"LoadingScene: TerrainManager ya está inicializado para: {sessionData.WorldName}");
            }
            
            Logger.Log("LoadingScene: TerrainManager listo para usar");
            
            // PlayerController se inicializará en LoadPlayerComponents
            await Task.Delay(100);
        }
        
        /// <summary>
        /// Inicia el servidor de juego
        /// </summary>
        private async Task<bool> StartServer()
        {
            try
            {
                Logger.Log("LoadingScene: Iniciando servidor...");
                
                // Crear NetworkManager como singleton si no existe
                if (NetworkManager.Instance == null)
                {
                    var networkManager = new NetworkManager();
                    networkManager.Name = "NetworkManager";
                    GetTree().Root.AddChild(networkManager);
                    networkManager.Initialize();
                    Logger.Log("LoadingScene: NetworkManager creado e inicializado");
                }
                
                var gameFlow = GetNode<GameFlow>("/root/GameFlow");
                if (gameFlow != null)
                {
                    // Usar el método interno de GameFlow para iniciar servidor
                    var serverStarted = await gameFlow.InitializeNetwork();
                    Logger.Log($"LoadingScene: Servidor iniciado: {serverStarted}");
                    
                    // Actualizar el modo de red del NetworkManager después de conectar
                    if (serverStarted && NetworkManager.Instance != null)
                    {
                        NetworkManager.Instance.UpdateNetworkMode();
                    }
                    
                    return serverStarted;
                }
                else
                {
                    Logger.LogError("LoadingScene: ERROR - No se encontró GameFlow");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"LoadingScene: Error iniciando servidor: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Conecta el cliente al servidor
        /// </summary>
        private async Task<bool> ConnectClient()
        {
            try
            {
                Logger.Log("LoadingScene: Conectando cliente al servidor...");
                
                // NetworkManager ya debería estar inicializado por StartServer()
                var networkManager = NetworkManager.Instance;
                if (networkManager != null)
                {
                    // El cliente ya debería estar conectado desde InitializeNetwork()
                    Logger.Log("LoadingScene: Cliente ya conectado al servidor");
                    return true;
                }
                else
                {
                    Logger.LogError("LoadingScene: ERROR - NetworkManager no está inicializado");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"LoadingScene: Error conectando cliente: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Genera el terreno inicial alrededor del jugador
        /// </summary>
        private async Task GenerateInitialTerrain()
        {
            Logger.Log("LoadingScene: Generando terreno inicial");
            
            var terrainManager = TerrainManager.Instance;
            if (terrainManager != null)
            {
                // Generar chunk inicial en (0,0)
                var initialChunkPos = new Vector2I(0, 0);
                await terrainManager.LoadChunk(initialChunkPos);
                Logger.Log($"LoadingScene: Chunk inicial generado en {initialChunkPos}");
            }
            else
            {
                throw new System.Exception("TerrainManager no está inicializado");
            }
            
            await Task.Delay(500); // Esperar a que se genere el terreno
        }
        
        /// <summary>
        /// Carga los componentes del jugador
        /// </summary>
        private async Task LoadPlayerComponents()
        {
            Logger.Log("LoadingScene: Cargando componentes del jugador");
            
            // NO crear PlayerController aquí - se creará en GameWorld._Ready()
            // El PlayerController necesita las referencias al jugador y cámara de la escena GameWorld
            Logger.Log("LoadingScene: PlayerController se inicializará en GameWorld con referencias válidas");
            
            // Validar que los sistemas estén listos
            if (TerrainManager.Instance == null)
            {
                throw new Exception("TerrainManager no disponible");
            }
            
            if (NetworkManager.Instance == null)
            {
                throw new Exception("NetworkManager no disponible");
            }
            
            await Task.Delay(100);
        }
        
        /// <summary>
        /// Actualiza el progreso de carga
        /// </summary>
        private async Task UpdateProgress(int step, string message)
        {
            // Verificar si el objeto no está disposed
            if (IsInstanceValid(this))
            {
                _currentStep = step;
                _statusLabel.Text = message;
                _progressBar.Value = step;
                
                Logger.Log($"LoadingScene: Paso {step}/{_totalSteps} - {message}");
                
                // Pequeña pausa para que la UI se actualice
                await Task.Delay(100);
            }
            else
            {
                Logger.LogWarning("LoadingScene: Intentando actualizar progreso en objeto disposed - ignorando");
            }
        }
        
        /// <summary>
        /// Configura los controles del jugador
        /// </summary>
        private void SetupControls()
        {
            var playerController = PlayerController.Instance;
            
            if (playerController != null)
            {
                // Activar controles del jugador
                playerController.SetProcess(true);
                playerController.SetPhysicsProcess(true);
                playerController.SetProcessInput(true);
                
                // Capturar el ratón para controles FPS
                Input.MouseMode = Input.MouseModeEnum.Captured;
                
                // VERIFICACIÓN: Solo loguear éxito si realmente se activaron
                bool processActive = playerController.IsProcessing();
                bool physicsActive = playerController.IsPhysicsProcessing();
                bool inputActive = playerController.IsProcessingInput();
                
                if (processActive && physicsActive && inputActive)
                {
                    Logger.Log("LoadingScene: ✅ Controles activados y ratón capturado CORRECTAMENTE");
                }
                else
                {
                    Logger.LogError($"LoadingScene: ❌ ERROR - Controles no se activaron completamente. Process: {processActive}, Physics: {physicsActive}, Input: {inputActive}");
                }
            }
            else
            {
                Logger.LogError("LoadingScene: ERROR - PlayerController es nulo, no se activan controles ni se captura ratón");
            }
        }
        
        /// <summary>
        /// Muestra un mensaje de error y vuelve al menú principal
        /// </summary>
        private async Task ShowErrorAndReturnToMenu(string title, string errorMessage)
        {
            Logger.LogError($"LoadingScene: ERROR CRÍTICO - {title}: {errorMessage}");
            Logger.LogError($"LoadingScene: Stack trace completo será mostrado en UI");
            
            // Actualizar UI para mostrar error
            _titleLabel.Text = "❌ Error Crítico";
            _statusLabel.Text = $"{title}\n\n{errorMessage}\n\nPresiona OK para continuar";
            _statusLabel.AddThemeColorOverride("font_color", Colors.Red);
            _progressBar.Value = 0;
            
            // Crear botón OK
            var okButton = new Button();
            okButton.Text = "OK";
            okButton.CustomMinimumSize = new Vector2(200, 50);
            okButton.Pressed += OnOkButtonPressed;
            
            // Añadir botón al contenedor de contenido
            var contentContainer = GetNode<VBoxContainer>("VBoxContainer/CenterContainer/ContentContainer");
            if (contentContainer != null)
            {
                contentContainer.AddChild(okButton);
            }
            
            // Esperar a que el usuario presione OK (en lugar de delay)
            Logger.Log("LoadingScene: Esperando interacción del usuario para volver al menú");
        }
        
        /// <summary>
        /// Maneja el botón OK del mensaje de error
        /// </summary>
        private void OnOkButtonPressed()
        {
            Logger.Log("LoadingScene: Usuario presionó OK - volviendo al menú principal");
            
            // Volver al menú principal
            var gameFlow = GetNode<GameFlow>("/root/GameFlow");
            if (gameFlow != null)
            {
                // Detener servidor y cliente antes de volver
                gameFlow.ReturnToMainMenu();
            }
            else
            {
                // Fallback directo si GameFlow no está disponible
                GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
            }
        }
        
        /// <summary>
        /// Transiciona a la escena del juego
        /// </summary>
        private async Task TransitionToGame()
        {
            Logger.Log("LoadingScene: Transicionando al juego - iniciando");
            
            try
            {
                // Fade out
                var tween = CreateTween();
                tween.TweenProperty(this, "modulate:a", 0, 0.5);
                
                await Task.Delay(500);
                
                Logger.Log("LoadingScene: Cambiando a escena GameWorld");
                
                // Cambiar directamente a la escena del juego
                GetTree().ChangeSceneToFile("res://scenes/game_world.tscn");
                
                Logger.Log("LoadingScene: ✅ Transición completada exitosamente");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"LoadingScene: ERROR en TransitionToGame: {ex.Message}");
            }
        }
    }
}
