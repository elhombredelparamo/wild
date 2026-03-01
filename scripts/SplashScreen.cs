using Godot;
using System;

namespace Wild;

/// <summary>
/// Splash screen inicial con fade automático y opción de saltar
/// </summary>
public partial class SplashScreen : Control
{
    private TextureRect _splashImage = null!;
    private Godot.Timer _timer = null!;
    private const float FadeDuration = 1.0f;
    private const float DisplayDuration = 3.0f;
    
    public override void _Ready()
    {
        Logger.Log("SplashScreen: Iniciando splash screen");
        
        // Configurar pantalla completa
        AnchorLeft = 0;
        AnchorTop = 0;
        AnchorRight = 1;
        AnchorBottom = 1;
        OffsetLeft = 0;
        OffsetTop = 0;
        OffsetRight = 0;
        OffsetBottom = 0;
        
        // Crear imagen del splash
        _splashImage = new TextureRect();
        _splashImage.AnchorLeft = 0;
        _splashImage.AnchorTop = 0;
        _splashImage.AnchorRight = 1;
        _splashImage.AnchorBottom = 1;
        _splashImage.OffsetLeft = 0;
        _splashImage.OffsetTop = 0;
        _splashImage.OffsetRight = 0;
        _splashImage.OffsetBottom = 0;
        _splashImage.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _splashImage.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        
        // Cargar la imagen directamente desde archivo (sin importación)
        var splashTexture = new ImageTexture();
        try {
            var image = new Image();
            if (image.Load("res://assets/ui/splash.png") == Error.Ok)
            {
                splashTexture.SetImage(image);
                _splashImage.Texture = splashTexture;
                Logger.Log("SplashScreen: Imagen splash.png cargada directamente desde archivo");
            }
            else
            {
                throw new System.Exception("No se pudo cargar la imagen");
            }
        }
        catch (System.Exception ex)
        {
            Logger.Log($"SplashScreen: ERROR - No se encontró assets/ui/splash.png: {ex.Message}");
            
            // Verificar si el archivo existe físicamente
            if (Godot.FileAccess.FileExists("res://assets/ui/splash.png"))
            {
                Logger.Log("SplashScreen: El archivo existe pero no se pudo cargar. Usando imagen de respaldo.");
            }
            else
            {
                Logger.Log("SplashScreen: El archivo no existe en la ruta esperada.");
            }
            
            // Crear una imagen de respaldo simple
            CreateFallbackImage();
        }
        
        AddChild(_splashImage);
        
        // Configurar timer para el display
        _timer = new Godot.Timer();
        _timer.WaitTime = DisplayDuration;
        _timer.OneShot = true;
        _timer.Timeout += OnDisplayTimerTimeout;
        AddChild(_timer);
        
        // Iniciar con fade-in
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, FadeDuration);
        
        // Iniciar el timer después del fade-in
        GetTree().CreateTimer(FadeDuration).Timeout += () => {
            _timer.Start();
            Logger.Log("SplashScreen: Timer de display iniciado");
        };
        
        Logger.Log("SplashScreen: Configuración completada");
    }
    
    public override void _Input(InputEvent @event)
    {
        // Permitir saltar el splash con cualquier tecla o clic
        if (@event is InputEventKey or InputEventMouseButton)
        {
            if (@event.IsPressed())
            {
                Logger.Log("SplashScreen: Usuario saltó el splash screen");
                SkipSplash();
            }
        }
    }
    
    private void CreateFallbackImage()
    {
        // Crear una imagen simple de respaldo si no se encuentra splash.png
        var image = Image.CreateEmpty(1920, 1080, false, Image.Format.Rgb8);
        image.Fill(Colors.DarkGreen);
        
        var fallbackTexture = ImageTexture.CreateFromImage(image);
        _splashImage.Texture = fallbackTexture;
        
        Logger.Log("SplashScreen: Imagen de respaldo creada");
    }
    
    private void OnDisplayTimerTimeout()
    {
        Logger.Log("SplashScreen: Timer completado, iniciando transición");
        TransitionToMainMenu();
    }
    
    private void SkipSplash()
    {
        _timer.Stop();
        TransitionToMainMenu();
    }
    
    private void TransitionToMainMenu()
    {
        Logger.Log("SplashScreen: Iniciando fade-out y transición al menú principal");
        
        // Fade-out y luego cambiar de escena
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", Colors.Transparent, FadeDuration);
        tween.TweenCallback(Callable.From(() => {
            Logger.Log("SplashScreen: Ejecutando ChangeSceneToFile a main_menu.tscn");
            try
            {
                var result = GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
                Logger.Log($"SplashScreen: ChangeSceneToFile resultado: {result}");
            }
            catch (System.Exception ex)
            {
                Logger.Log($"SplashScreen: ERROR en ChangeSceneToFile: {ex.Message}");
            }
        }));
    }
}
