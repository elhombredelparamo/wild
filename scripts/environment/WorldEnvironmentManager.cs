using Godot;
using Wild.Systems;

namespace Wild.WorldEnvironment;

/// <summary>
/// Gestor del entorno del mundo usando el sistema oficial de Godot 4
/// Implementación optimizada con WorldEnvironment + Sky + PanoramaSkyMaterial
/// </summary>
public partial class WorldEnvironmentManager : Node
{
    private Godot.WorldEnvironment _worldEnvironment;
    private Godot.Environment _environment;
    private Sky _sky;
    private PanoramaSkyMaterial _panoramaMaterial;
    
    [Export] public string SkyboxPath { get; set; } = "res://assets/textures/skybox/cielo.png";
    [Export] public float RotationAngle { get; set; } = 0.0f;
    
    public override void _Ready()
    {
        Logger.Log("WorldEnvironment: Iniciando sistema de entorno oficial Godot 4...");
        CreateEnvironment();
    }
    
    private void CreateEnvironment()
    {
        try
        {
            // 1. Crear WorldEnvironment
            _worldEnvironment = new Godot.WorldEnvironment();
            AddChild(_worldEnvironment);
            Logger.Log("WorldEnvironment: WorldEnvironment creado");
            
            // 2. Crear Environment (sistema Godot)
            _environment = new Godot.Environment();
            Logger.Log("WorldEnvironment: Environment creado");
            
            // 3. Cargar textura panorámica
            var panoramaTexture = GD.Load<Texture2D>(SkyboxPath);
            if (panoramaTexture == null)
            {
                Logger.LogError($"WorldEnvironment: ERROR - No se pudo cargar la textura: {SkyboxPath}");
                return;
            }
            Logger.Log($"WorldEnvironment: Textura panorámica cargada: {panoramaTexture.GetWidth()}x{panoramaTexture.GetHeight()}");
            
            // 4. Crear PanoramaSkyMaterial
            _panoramaMaterial = new PanoramaSkyMaterial();
            _panoramaMaterial.Panorama = panoramaTexture;
            Logger.Log("WorldEnvironment: PanoramaSkyMaterial configurado");
            
            // 5. Crear Sky y asignar material
            _sky = new Sky();
            _sky.SkyMaterial = _panoramaMaterial;
            _sky.ProcessMode = Sky.ProcessModeEnum.Realtime; // Actualización en tiempo real
            Logger.Log("WorldEnvironment: Sky configurado con material panorámico");
            
            // 6. Configurar Environment
            _environment.BackgroundMode = Godot.Environment.BGMode.Sky;
            _environment.Sky = _sky;
            _environment.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
            _environment.TonemapMode = Godot.Environment.ToneMapper.Filmic;
            _environment.GlowEnabled = true;
            Logger.Log("WorldEnvironment: Environment configurado");
            
            // 7. Asignar Environment a WorldEnvironment
            _worldEnvironment.Environment = _environment;
            Logger.Log("WorldEnvironment: ✅ Sistema de entorno inicializado correctamente");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"WorldEnvironment: ❌ ERROR CRÍTICO: {ex.Message}");
            Logger.LogError($"WorldEnvironment: Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Cambia la textura del skybox
    /// </summary>
    public void ChangeSkybox(string newSkyboxPath)
    {
        var newTexture = GD.Load<Texture2D>(newSkyboxPath);
        if (newTexture != null && _panoramaMaterial != null)
        {
            _panoramaMaterial.Panorama = newTexture;
            SkyboxPath = newSkyboxPath;
            Logger.Log($"WorldEnvironment: Skybox cambiado a: {newSkyboxPath}");
        }
        else
        {
            Logger.LogError($"WorldEnvironment: ERROR - No se pudo cambiar el skybox a: {newSkyboxPath}");
        }
    }
    
    /// <summary>
    /// Rota el skybox (si el material lo soporta)
    /// </summary>
    public void SetRotation(float angleDegrees)
    {
        RotationAngle = angleDegrees;
        // Nota: PanoramaSkyMaterial no tiene rotación directa
        // Para rotación se necesitaría un ShaderMaterial personalizado
        Logger.Log($"WorldEnvironment: Rotación configurada a {angleDegrees}° (requiere shader personalizado)");
    }
    
    /// <summary>
    /// Habilita/deshabilita el glow (brillo atmosférico)
    /// </summary>
    public void SetGlowEnabled(bool enabled)
    {
        if (_environment != null)
        {
            _environment.GlowEnabled = enabled;
            Logger.Log($"WorldEnvironment: Glow {(enabled ? "habilitado" : "deshabilitado")}");
        }
    }
    
    /// <summary>
    /// Ajusta la intensidad de la luz ambiental
    /// </summary>
    public void SetAmbientLightEnergy(float energy)
    {
        if (_environment != null)
        {
            _environment.AmbientLightEnergy = energy;
            Logger.Log($"WorldEnvironment: Energía ambiental ajustada a {energy}");
        }
    }
    
    /// <summary>
    /// Obtiene información del sistema
    /// </summary>
    public void GetSystemInfo()
    {
        Logger.Log("=== WorldEnvironment System Info ===");
        Logger.Log($"Skybox Path: {SkyboxPath}");
        Logger.Log($"WorldEnvironment: {_worldEnvironment != null}");
        Logger.Log($"Environment: {_environment != null}");
        Logger.Log($"Sky: {_sky != null}");
        Logger.Log($"PanoramaMaterial: {_panoramaMaterial != null}");
        Logger.Log($"Glow Enabled: {_environment?.GlowEnabled}");
        Logger.Log($"Ambient Energy: {_environment?.AmbientLightEnergy}");
        Logger.Log($"=====================================");
    }
}
