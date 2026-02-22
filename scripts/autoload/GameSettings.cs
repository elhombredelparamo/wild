using Godot;

namespace Wild;

/// <summary>
/// Opciones del juego (gráficos, controles, etc.). Persistencia pendiente (user://settings.cfg).
/// </summary>
public partial class GameSettings : Node
{
    private const float DefaultRenderDistanceMetres = 150f;
    private const float MinRenderDistanceMetres = 25f;
    private const float MaxRenderDistanceMetres = 500f;

    private float _renderDistanceMetres = DefaultRenderDistanceMetres;

    /// <summary>Distancia de renderizado en metros desde la posición del jugador.</summary>
    public float RenderDistanceMetres
    {
        get => _renderDistanceMetres;
        set => _renderDistanceMetres = Mathf.Clamp(value, MinRenderDistanceMetres, MaxRenderDistanceMetres);
    }

    public static float MinRenderDistance => 25f;
    public static float MaxRenderDistance => 500f;

    public override void _Ready()
    {
        // TODO: cargar desde user://settings.cfg
    }

    /// <summary>Guarda opciones a disco (pendiente de implementar).</summary>
    public void Save()
    {
        // TODO: guardar en user://settings.cfg
    }
}
