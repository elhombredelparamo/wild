using Godot;

namespace Wild;

/// <summary>
/// Autoload que centraliza el flujo del juego: menú principal, partida, servidor local.
/// Ver contexto/menus-y-servidor.txt para la arquitectura.
/// </summary>
public partial class GameFlow : Node
{
    public const string SceneMainMenu = "res://scenes/main_menu.tscn";
    public const string SceneNewGameMenu = "res://scenes/new_game_menu.tscn";
    public const string SceneOptionsMenu = "res://scenes/options_menu.tscn";
    public const string SceneGameWorld = "res://scenes/game_world.tscn";

    /// <summary>Abre el menú de opciones (controles, gráficos).</summary>
    public void OpenOptions()
    {
        GetTree().ChangeSceneToFile(SceneOptionsMenu);
    }

    /// <summary>Abre el menú de creación de nueva partida (semilla, personaje, mundo).</summary>
    public void OpenNewGameMenu()
    {
        GetTree().ChangeSceneToFile(SceneNewGameMenu);
    }

    /// <summary>Inicia una partida nueva (cambia a la escena del mundo).</summary>
    public void StartNewGame()
    {
        Logger.Log($"GameFlow: StartNewGame() - cargando escena: {SceneGameWorld}");
        
        try
        {
            // Verificar si el archivo existe
            if (!Godot.FileAccess.FileExists(SceneGameWorld))
            {
                Logger.LogError($"GameFlow: ERROR - La escena no existe: {SceneGameWorld}");
                return;
            }
            
            GetTree().ChangeSceneToFile(SceneGameWorld);
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"GameFlow: Excepción al cambiar de escena: {ex.Message}");
        }
    }

    /// <summary>Carga una partida (por ahora igual que nueva; persistencia pendiente).</summary>
    public void LoadGame(string savePath = "")
    {
        // TODO: cargar estado desde disco y luego cambiar escena con ese estado
        GetTree().ChangeSceneToFile(SceneGameWorld);
    }

    /// <summary>Vuelve al menú principal (cierra "servidor local" / partida).</summary>
    public void ReturnToMainMenu()
    {
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
