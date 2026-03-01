using Godot;
using System;
using Wild;

/// <summary>Menú de creación de nuevos personajes.</summary>
public partial class CharacterCreateMenu : Control
{
    private LineEdit _nameInput = null!;
    private Label _titleLabel = null!;
    private Label _nameLabel = null!;
    private Label _feedbackLabel = null!;
    private Button _createButton = null!;
    private Button _cancelButton = null!;
    
    public override void _Ready()
    {
        SetupUI();
        UpdateUI();
    }
    
    /// <summary>Configura la interfaz de usuario.</summary>
    private void SetupUI()
    {
        // Obtener referencias a los elementos de la escena
        _titleLabel = GetNode<Label>("TitleLabel");
        _nameLabel = GetNode<Label>("NameLabel");
        _nameInput = GetNode<LineEdit>("NameInput");
        _feedbackLabel = GetNode<Label>("FeedbackLabel");
        _createButton = GetNode<Button>("ButtonContainer/CreateButton");
        _cancelButton = GetNode<Button>("ButtonContainer/CancelButton");
        
        // Configurar eventos
        _nameInput.TextChanged += OnNameTextChanged;
        _createButton.Pressed += OnCreatePressed;
        _cancelButton.Pressed += OnCancelPressed;
        
        // Enfocar el campo de nombre
        _nameInput.GrabFocus();
    }
    
    /// <summary>Maneja el cambio de texto en el campo de nombre.</summary>
    private void OnNameTextChanged(string newText)
    {
        UpdateUI();
    }
    
    /// <summary>Maneja el botón de crear personaje.</summary>
    private void OnCreatePressed()
    {
        string characterName = _nameInput.Text.Trim();
        
        // Validar nombre
        string validationError = ValidateCharacterName(characterName);
        if (!string.IsNullOrEmpty(validationError))
        {
            _feedbackLabel.Text = validationError;
            return;
        }
        
        try
        {
            // Crear personaje
            var newCharacter = CharacterManager.Instance.CreateCharacter(characterName);
            
            GD.Print($"CharacterCreateMenu: Personaje creado exitosamente: {newCharacter.CharacterId}");
            
            // Seleccionar el nuevo personaje
            CharacterManager.Instance.SelectCharacter(newCharacter.CharacterId);
            
            // Volver al menú de selección
            GetNode<GameFlow>("/root/GameFlow").OpenCharacterSelectMenu();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"CharacterCreateMenu: Error al crear personaje: {ex.Message}");
            _feedbackLabel.Text = "Error al crear personaje. Intente con otro nombre.";
        }
    }
    
    /// <summary>Maneja el botón de cancelar.</summary>
    private void OnCancelPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenCharacterSelectMenu();
    }
    
    /// <summary>Valida el nombre del personaje.</summary>
    private string ValidateCharacterName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "El nombre no puede estar vacío.";
        }
        
        // Verificar caracteres válidos
        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '-')
            {
                return "El nombre solo puede contener letras, números, espacios, guiones y guiones bajos.";
            }
        }
        
        // Con el nuevo sistema de IDs MD5, no necesitamos verificar duplicados de nombre
        // ya que cada personaje tendrá un ID único basado en hash + sal
        
        return string.Empty; // Sin errores
    }
    
    /// <summary>Actualiza el estado de los botones y feedback.</summary>
    private void UpdateUI()
    {
        string characterName = _nameInput.Text.Trim();
        
        if (string.IsNullOrEmpty(characterName))
        {
            _feedbackLabel.Text = "";
        }
        
        // Validar nombre
        string validationError = ValidateCharacterName(characterName);
        bool isValid = string.IsNullOrEmpty(validationError);
        
        // Habilitar botón de crear si el nombre es válido
        _createButton.Disabled = !isValid;
        
        // Cambiar color del feedback según validez
        if (string.IsNullOrEmpty(characterName))
        {
            _feedbackLabel.Modulate = Colors.Red;
        }
        else if (isValid)
        {
            _feedbackLabel.Modulate = Colors.Green;
            _feedbackLabel.Text = "Nombre válido";
        }
        else
        {
            _feedbackLabel.Modulate = Colors.Red;
            _feedbackLabel.Text = validationError;
        }
    }
    
    /// <summary>Maneja la tecla Enter para crear personaje.</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Enter)
            {
                OnCreatePressed();
                AcceptEvent();
            }
            else if (keyEvent.Keycode == Key.Escape)
            {
                OnCancelPressed();
                AcceptEvent();
            }
        }
    }
}
