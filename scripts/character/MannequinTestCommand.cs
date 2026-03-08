using Godot;
using Wild.Scripts.Player;

namespace Wild.Scripts.Character;

/// <summary>
/// Comando de prueba para spawnear maniquíes durante el juego
/// </summary>
public partial class MannequinTestCommand : Node
{
    private GameWorld? _gameWorld;
    private bool _hasSpawnedTest = false;
    
    public override void _Ready()
    {
        Logger.Log("🎭 MannequinTestCommand: Inicializando comando de prueba");
        
        // Obtener referencia al GameWorld
        _gameWorld = GetTree().CurrentScene as GameWorld;
        if (_gameWorld == null)
        {
            Logger.LogError("🎭 MannequinTestCommand: No se encontró GameWorld");
        }
    }
    
    public override void _Input(InputEvent @event)
    {
        // Tecla F10 para spawnear maniquí de prueba
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F10)
            {
                SpawnTestMannequin();
            }
            
            // Tecla F12 para limpiar todos los maniquíes
            if (keyEvent.Keycode == Key.F12)
            {
                ClearAllMannequins();
            }
        }
    }
    
    /// <summary>
    /// Spawnea un maniquí de prueba en coordenadas 50,50
    /// </summary>
    private async void SpawnTestMannequin()
    {
        if (_gameWorld == null)
        {
            Logger.LogError("🎭 MannequinTestCommand: GameWorld no disponible");
            return;
        }
        
        Logger.Log("🎭 MannequinTestCommand: Spawneando maniquí de prueba en 50,50");
        
        var mannequin = await _gameWorld.SpawnTestMannequinAsync();
        if (mannequin != null)
        {
            Logger.Log("🎭 MannequinTestCommand: ✅ Maniquí de prueba creado exitosamente");
            _hasSpawnedTest = true;
        }
        else
        {
            Logger.LogError("🎭 MannequinTestCommand: ❌ Error al crear maniquí de prueba");
        }
    }
    
    /// <summary>
    /// Elimina todos los maniquíes de la escena
    /// </summary>
    private void ClearAllMannequins()
    {
        if (_gameWorld == null)
        {
            Logger.LogError("🎭 MannequinTestCommand: GameWorld no disponible");
            return;
        }
        
        var mannequinSpawner = _gameWorld.GetNode<MannequinSpawner>("MannequinSpawner");
        if (mannequinSpawner == null)
        {
            Logger.LogError("🎭 MannequinTestCommand: No se encontró MannequinSpawner");
            return;
        }
        
        int count = 0;
        foreach (Node child in mannequinSpawner.GetChildren())
        {
            if (child.Name.ToString().Contains("Mannequin"))
            {
                child.QueueFree();
                count++;
            }
        }
        
        Logger.Log($"🎭 MannequinTestCommand: 🗑️ Eliminados {count} maniquíes");
        _hasSpawnedTest = false;
    }
    
    /// <summary>
    /// Muestra ayuda de comandos disponibles
    /// </summary>
    public override void _Notification(int what)
    {
        if (what == NotificationReady)
        {
            Logger.Log("🎭 MannequinTestCommand: Comandos disponibles:");
            Logger.Log("   F10: Spawnea maniquí de prueba en coordenadas 50,50");
            Logger.Log("   F12: Elimina todos los maniquíes");
        }
    }
}
