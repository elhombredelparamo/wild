using Godot;

namespace Wild;

/// <summary>
/// Menú principal. Ver contexto/menus-y-servidor.txt.
/// Botones: Nueva partida, Cargar partida, Opciones, Salir.
/// </summary>
public partial class MainMenu : Control
{
    private Button _buttonNewGame = null!;
    private Button _buttonLoadGame = null!;
    private Button _buttonOptions = null!;
    private Button _buttonQuit = null!;

    public override void _Ready()
    {
        _buttonNewGame = GetNode<Button>("CenterContainer/VBoxContainer/ButtonNewGame");
        _buttonLoadGame = GetNode<Button>("CenterContainer/VBoxContainer/ButtonLoadGame");
        _buttonOptions = GetNode<Button>("CenterContainer/VBoxContainer/ButtonOptions");
        _buttonQuit = GetNode<Button>("CenterContainer/VBoxContainer/ButtonQuit");

        _buttonNewGame.Pressed += OnNewGamePressed;
        _buttonLoadGame.Pressed += OnLoadGamePressed;
        _buttonOptions.Pressed += OnOptionsPressed;
        _buttonQuit.Pressed += OnQuitPressed;
    }

    private void OnNewGamePressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenNewGameMenu();
    }

    private void OnLoadGamePressed()
    {
        // TODO: abrir pantalla de cargar partida
        GetNode<GameFlow>("/root/GameFlow").LoadGame();
    }

    private void OnOptionsPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenOptions();
    }

    private void OnQuitPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").QuitGame();
    }
}
