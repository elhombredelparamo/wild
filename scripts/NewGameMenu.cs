using Godot;

namespace Wild;

/// <summary>
/// Menú intermedio al crear una nueva partida: semilla, nombre personaje, género, nombre mundo.
/// Al confirmar guarda los datos en SessionData y pasa a la partida.
/// </summary>
public partial class NewGameMenu : Control
{
    private LineEdit _editSeed = null!;
    private Button _buttonRandomSeed = null!;
    private LineEdit _editCharacterName = null!;
    private OptionButton _optionGender = null!;
    private LineEdit _editWorldName = null!;
    private Button _buttonCreate = null!;
    private Button _buttonBack = null!;

    public override void _Ready()
    {
        _editSeed = GetNode<LineEdit>("CenterContainer/Panel/MarginContainer/VBox/GridSeed/EditSeed");
        _buttonRandomSeed = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/GridSeed/ButtonRandomSeed");
        _editCharacterName = GetNode<LineEdit>("CenterContainer/Panel/MarginContainer/VBox/EditCharacterName");
        _optionGender = GetNode<OptionButton>("CenterContainer/Panel/MarginContainer/VBox/OptionGender");
        _editWorldName = GetNode<LineEdit>("CenterContainer/Panel/MarginContainer/VBox/EditWorldName");
        _buttonCreate = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/Buttons/ButtonCreate");
        _buttonBack = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/Buttons/ButtonBack");

        _buttonRandomSeed.Pressed += OnRandomSeedPressed;
        _buttonCreate.Pressed += OnCreatePressed;
        _buttonBack.Pressed += OnBackPressed;

        _optionGender.AddItem("Hombre", 0);
        _optionGender.AddItem("Mujer", 1);
        _optionGender.Selected = 0;

        _editSeed.PlaceholderText = "Vacío = aleatorio";
        SetRandomSeedInEdit();
    }

    private void SetRandomSeedInEdit()
    {
        _editSeed.Text = SessionData.RandomSeed().ToString();
    }

    private void OnRandomSeedPressed()
    {
        SetRandomSeedInEdit();
    }

    private void OnCreatePressed()
    {
        var session = GetNode<SessionData>("/root/SessionData");
        var flow = GetNode<GameFlow>("/root/GameFlow");

        // Semilla: si está vacío o no es número, usar aleatoria
        if (string.IsNullOrWhiteSpace(_editSeed.Text) || !long.TryParse(_editSeed.Text.Trim(), out var seed))
            seed = SessionData.RandomSeed();
        session.WorldSeed = seed;

        session.CharacterName = _editCharacterName.Text.Trim();
        if (string.IsNullOrEmpty(session.CharacterName))
            session.CharacterName = "Jugador";

        session.Gender = _optionGender.Selected == 0 ? "Hombre" : "Mujer";

        session.WorldName = _editWorldName.Text.Trim();
        if (string.IsNullOrEmpty(session.WorldName))
            session.WorldName = "Mundo";

        flow.StartNewGame();
    }

    private void OnBackPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").ReturnToMainMenu();
    }
}
