using Godot;

namespace Wild;

/// <summary>
/// Opciones del juego (gráficos, controles, etc.). Persistencia en user://settings.cfg.
/// </summary>
public partial class GameSettings : Node
{
    private const float DefaultRenderDistanceMetres = 20f;
    private const float MinRenderDistanceMetres = 10f;
    private const float MaxRenderDistanceMetres = 99f;
    private const string SettingsFileName = "user://settings.cfg";

    private float _renderDistanceMetres = DefaultRenderDistanceMetres;
    private ConfigFile _configFile = new();

    /// <summary>Distancia de renderizado en metros desde la posición del jugador.</summary>
    public float RenderDistanceMetres
    {
        get => _renderDistanceMetres;
        set => _renderDistanceMetres = Mathf.Clamp(value, MinRenderDistanceMetres, MaxRenderDistanceMetres);
    }

    public static float MinRenderDistance => 10f;
    public static float MaxRenderDistance => 99f;

    public override void _Ready()
    {
        LoadSettings();
    }

    /// <summary>Carga opciones desde user://settings.cfg</summary>
    private void LoadSettings()
    {
        var error = _configFile.Load(SettingsFileName);
        if (error != Error.Ok)
        {
            Logger.Log($"GameSettings: No se pudo cargar {SettingsFileName}, creando archivo con valores por defecto. Error: {error}");
            CreateDefaultSettings();
            return;
        }

        // Cargar distancia de renderizado
        if (_configFile.HasSectionKey("graphics", "render_distance"))
        {
            _renderDistanceMetres = (float)_configFile.GetValue("graphics", "render_distance");
            Logger.Log($"GameSettings: Cargada distancia de renderizado: {_renderDistanceMetres}m");
        }
        else
        {
            Logger.Log("GameSettings: No se encontró distancia de renderizado en settings.cfg, añadiendo valor por defecto");
            CreateDefaultSettings();
        }
    }

    /// <summary>Crea el archivo settings.cfg con valores por defecto</summary>
    private void CreateDefaultSettings()
    {
        _configFile.SetValue("graphics", "render_distance", DefaultRenderDistanceMetres);
        _renderDistanceMetres = DefaultRenderDistanceMetres;
        
        var error = _configFile.Save(SettingsFileName);
        if (error == Error.Ok)
        {
            Logger.Log($"GameSettings: Creado {SettingsFileName} con distancia de renderizado por defecto: {DefaultRenderDistanceMetres}m");
        }
        else
        {
            Logger.LogError($"GameSettings: Error al crear archivo de configuración: {error}");
        }
    }

    /// <summary>Guarda opciones a disco en user://settings.cfg</summary>
    public void Save()
    {
        // Guardar distancia de renderizado
        _configFile.SetValue("graphics", "render_distance", _renderDistanceMetres);

        var error = _configFile.Save(SettingsFileName);
        if (error == Error.Ok)
        {
            Logger.Log($"GameSettings: Configuración guardada en {SettingsFileName}");
            Logger.Log($"GameSettings: Distancia de renderizado guardada: {_renderDistanceMetres}m");
        }
        else
        {
            Logger.LogError($"GameSettings: Error al guardar configuración: {error}");
        }
    }
}
