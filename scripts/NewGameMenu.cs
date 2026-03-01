using Godot;

namespace Wild;

/// <summary>
/// Menú de creación de nueva partida: muestra personaje actual y opciones de mundo.
/// Los personajes se gestionan por separado en el menú de selección de personajes.
/// </summary>
public partial class NewGameMenu : Control
{
    private LineEdit _editSeed = null!;
    private Button _buttonRandomSeed = null!;
    private Label _labelCharacterInfo = null!;
    private LineEdit _editWorldName = null!;
    private Button _buttonCreate = null!;
    private Button _buttonBack = null!;
    private Button _buttonChangeCharacter = null!;

    public override void _Ready()
    {
        _editSeed = GetNode<LineEdit>("CenterContainer/Panel/MarginContainer/VBox/GridSeed/EditSeed");
        _buttonRandomSeed = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/GridSeed/ButtonRandomSeed");
        _labelCharacterInfo = GetNode<Label>("CenterContainer/Panel/MarginContainer/VBox/LabelCharacterInfo");
        _editWorldName = GetNode<LineEdit>("CenterContainer/Panel/MarginContainer/VBox/EditWorldName");
        _buttonCreate = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/Buttons/ButtonCreate");
        _buttonBack = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/Buttons/ButtonBack");
        _buttonChangeCharacter = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/ButtonChangeCharacter");

        _buttonRandomSeed.Pressed += OnRandomSeedPressed;
        _buttonCreate.Pressed += OnCreatePressed;
        _buttonBack.Pressed += OnBackPressed;
        _buttonChangeCharacter.Pressed += OnChangeCharacterPressed;

        _editSeed.PlaceholderText = "Vacío = aleatorio";
        _editWorldName.PlaceholderText = "Vacío = nombre automático";
        SetRandomSeedInEdit();
        UpdateCharacterInfo();
        
        // Conectar señal de árbol para actualizar cuando volvemos
        TreeEntered += UpdateCharacterInfo;
    }

    private void SetRandomSeedInEdit()
    {
        _editSeed.Text = SessionData.RandomSeed().ToString();
    }

    private void OnRandomSeedPressed()
    {
        SetRandomSeedInEdit();
    }

    private void OnChangeCharacterPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenCharacterSelectMenu();
    }

    private void UpdateCharacterInfo()
    {
        if (CharacterManager.Instance?.CurrentCharacter != null)
        {
            var character = CharacterManager.Instance.CurrentCharacter;
            _labelCharacterInfo.Text = character.DisplayName;
        }
        else
        {
            _labelCharacterInfo.Text = "No hay personaje seleccionado";
        }
    }

    private void OnCreatePressed()
    {
        // Verificar que haya un personaje seleccionado
        if (CharacterManager.Instance?.CurrentCharacter == null)
        {
            Wild.Logger.Log("NewGameMenu: No hay personaje seleccionado, redirigiendo a selección de personajes");
            GetNode<GameFlow>("/root/GameFlow").OpenCharacterSelectMenu();
            return;
        }

        var session = GetNode<SessionData>("/root/SessionData");
        var flow = GetNode<GameFlow>("/root/GameFlow");

        // Semilla: si está vacío o no es número, usar aleatoria
        if (string.IsNullOrWhiteSpace(_editSeed.Text) || !long.TryParse(_editSeed.Text.Trim(), out var seed))
            seed = SessionData.RandomSeed();
        session.WorldSeed = seed;

        // Nombre del mundo: usar el del campo o generar fallback
        string worldName = _editWorldName.Text.Trim();
        if (string.IsNullOrEmpty(worldName))
        {
            // Fallback: generar nombre automático con timestamp
            worldName = $"Mundo_{DateTime.Now:yyyyMMdd_HHmmss}";
            Wild.Logger.Log($"NewGameMenu: Usando nombre de mundo fallback: {worldName}");
        }
        else
        {
            Wild.Logger.Log($"NewGameMenu: Usando nombre de mundo personalizado: {worldName}");
        }

        session.WorldName = worldName;

        // Iniciar nueva partida con el nombre del mundo
        flow.StartNewGame(worldName);
    }

    private void OnBackPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenMainMenu();
    }
}
