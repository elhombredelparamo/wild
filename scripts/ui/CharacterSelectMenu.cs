using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using Wild;

/// <summary>Menú de selección de personajes.</summary>
public partial class CharacterSelectMenu : Control
{
    [Export] public PackedScene CharacterItemScene { get; set; } = null!;
    
    private VBoxContainer _characterList = null!;
    private Button _createNewButton = null!;
    private Button _selectButton = null!;
    private Button _deleteButton = null!;
    private Button _backButton = null!;
    private Label _titleLabel = null!;
    
    private string _selectedCharacterId = string.Empty;
    
    public override void _Ready()
    {
        Wild.Logger.Log("CharacterSelectMenu: _Ready() llamado");
        SetupUI();
        LoadCharacters();
        UpdateUI();
        Wild.Logger.Log("CharacterSelectMenu: _Ready() completado");
    }
    
    /// <summary>Configura la interfaz de usuario.</summary>
    private void SetupUI()
    {
        // Obtener referencias a los elementos de la escena
        _titleLabel = GetNode<Label>("TitleLabel");
        _characterList = GetNode<VBoxContainer>("CharacterList");
        _createNewButton = GetNode<Button>("ButtonContainer/CreateNewButton");
        _selectButton = GetNode<Button>("ButtonContainer/SelectButton");
        _deleteButton = GetNode<Button>("ButtonContainer/DeleteButton");
        _backButton = GetNode<Button>("ButtonContainer/BackButton");
        
        // Configurar eventos de botones
        _createNewButton.Pressed += OnCreateNewPressed;
        _selectButton.Pressed += OnSelectPressed;
        _deleteButton.Pressed += OnDeletePressed;
        _backButton.Pressed += OnBackPressed;
    }
    
    /// <summary>Carga la lista de personajes.</summary>
    private void LoadCharacters()
    {
        Wild.Logger.Log("CharacterSelectMenu: LoadCharacters() llamado");
        
        // Limpiar lista actual
        foreach (Node child in _characterList.GetChildren())
        {
            child.QueueFree();
        }
        
        // Cargar personajes desde CharacterManager
        var characters = CharacterManager.Instance.AllCharacters;
        Wild.Logger.Log($"CharacterSelectMenu: Cargando {characters.Count} personajes");
        
        bool foundCurrentCharacter = false;
        CheckBox currentCheckBox = null!;
        
        foreach (var character in characters)
        {
            var item = CreateCharacterItem(character);
            _characterList.AddChild(item);
            Wild.Logger.Log($"CharacterSelectMenu: Añadido personaje a la lista: {character.DisplayName}");
            
            // Obtener referencia al checkbox de este personaje
            var checkBox = item.GetChild(0) as CheckBox;
            
            // Verificar si este es el personaje actual
            if (character.CharacterId == CharacterManager.Instance.GetCurrentCharacterId())
            {
                foundCurrentCharacter = true;
                _selectedCharacterId = character.CharacterId;
                currentCheckBox = checkBox;
                Wild.Logger.Log($"CharacterSelectMenu: Personaje actual encontrado y seleccionado: {_selectedCharacterId}");
            }
        }
        
        // Si no hay personaje actual pero hay personajes, seleccionar el primero
        if (!foundCurrentCharacter && characters.Count > 0)
        {
            _selectedCharacterId = characters[0].CharacterId;
            // Marcar el checkbox del primer personaje
            if (_characterList.GetChildCount() > 0)
            {
                var firstItem = _characterList.GetChild(0);
                currentCheckBox = firstItem.GetChild(0) as CheckBox;
            }
            Wild.Logger.Log($"CharacterSelectMenu: No hay personaje actual, seleccionando el primero: {_selectedCharacterId}");
        }
        
        // Marcar el checkbox correspondiente
        if (currentCheckBox != null)
        {
            currentCheckBox.ButtonPressed = true;
            Wild.Logger.Log($"CharacterSelectMenu: Checkbox marcado para personaje: {_selectedCharacterId}");
        }
        
        Wild.Logger.Log($"CharacterSelectMenu: LoadCharacters() completado. _selectedCharacterId='{_selectedCharacterId}'");
    }
    
    /// <summary>Crea un elemento visual para un personaje.</summary>
    private Control CreateCharacterItem(CharacterProfile character)
    {
        var container = new HBoxContainer();
        
        // Checkbox de selección
        var checkBox = new CheckBox();
        checkBox.Toggled += (bool pressed) => OnCharacterSelected(character.CharacterId, pressed);
        container.AddChild(checkBox);
        
        // Nombre del personaje
        var nameLabel = new Label { Text = character.DisplayName };
        nameLabel.CustomMinimumSize = new Vector2(200, 0);
        container.AddChild(nameLabel);
        
        // ID del personaje
        var idLabel = new Label { Text = $"({character.CharacterId})" };
        idLabel.Modulate = Colors.Gray;
        container.AddChild(idLabel);
        
        // Tiempo de juego
        var timeLabel = new Label 
        { 
            Text = $"Tiempo: {FormatTime(character.TotalPlayTime)}",
            CustomMinimumSize = new Vector2(150, 0)
        };
        container.AddChild(timeLabel);
        
        // Mundos visitados
        var worldsLabel = new Label 
        { 
            Text = $"Mundos: {character.WorldsVisited.Count}",
            CustomMinimumSize = new Vector2(100, 0)
        };
        container.AddChild(worldsLabel);
        
        // Espaciador
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(50, 0);
        container.AddChild(spacer);
        
        // Si es el personaje actual, marcarlo
        if (character.CharacterId == CharacterManager.Instance.GetCurrentCharacterId())
        {
            checkBox.ButtonPressed = true;
            _selectedCharacterId = character.CharacterId;
        }
        
        return container;
    }
    
    /// <summary>Maneja la selección de un personaje.</summary>
    private void OnCharacterSelected(string characterId, bool selected)
    {
        Wild.Logger.Log($"CharacterSelectMenu: OnCharacterSelected - characterId='{characterId}', selected={selected}");
        
        if (!selected)
        {
            // Si se deselecciona, limpiar selección
            Wild.Logger.Log("CharacterSelectMenu: Deseleccionando personaje");
            _selectedCharacterId = string.Empty;
            UpdateUI();
            return;
        }
        
        // Deseleccionar otros checkboxes
        foreach (Node child in _characterList.GetChildren())
        {
            if (child is HBoxContainer container && container.GetChild(0) is CheckBox otherCheckBox)
            {
                if (container.GetChild(1) is Label nameLabel && 
                    nameLabel.Text != CharacterManager.Instance.AllCharacters.Find(c => c.CharacterId == characterId)?.DisplayName)
                {
                    otherCheckBox.ButtonPressed = false;
                }
            }
        }
        
        _selectedCharacterId = characterId;
        Wild.Logger.Log($"CharacterSelectMenu: Personaje seleccionado: {_selectedCharacterId}");
        UpdateUI();
    }
    
    /// <summary>Maneja el botón de crear nuevo personaje.</summary>
    private void OnCreateNewPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenCharacterCreateMenu();
    }
    
    /// <summary>Maneja el botón de seleccionar personaje.</summary>
    private void OnSelectPressed()
    {
        if (string.IsNullOrEmpty(_selectedCharacterId))
        {
            GD.Print("CharacterSelectMenu: No hay personaje seleccionado");
            return;
        }
        
        CharacterManager.Instance.SelectCharacter(_selectedCharacterId);
        GD.Print($"CharacterSelectMenu: Personaje seleccionado: {_selectedCharacterId}");
        
        // Volver al menú principal
        GetNode<GameFlow>("/root/GameFlow").OpenMainMenu();
    }
    
    /// <summary>Maneja el botón de eliminar personaje.</summary>
    private void OnDeletePressed()
    {
        Wild.Logger.Log($"CharacterSelectMenu: OnDeletePressed llamado. _selectedCharacterId='{_selectedCharacterId}'");
        
        if (string.IsNullOrEmpty(_selectedCharacterId))
        {
            Wild.Logger.Log("CharacterSelectMenu: No hay personaje seleccionado para eliminar");
            return;
        }
        
        int totalCharacters = CharacterManager.Instance.AllCharacters.Count;
        
        // No permitir eliminar si solo queda un personaje
        if (totalCharacters <= 1)
        {
            Wild.Logger.Log("CharacterSelectMenu: No se puede eliminar el último personaje restante");
            return;
        }
        
        // Eliminar personaje
        var character = CharacterManager.Instance.AllCharacters.Find(c => c.CharacterId == _selectedCharacterId);
        if (character != null)
        {
            try
            {
                // Eliminar de la lista en memoria
                CharacterManager.Instance.AllCharacters.Remove(character);
                
                // Eliminar archivo físico
                string filePath = $"user://characters/profiles/{_selectedCharacterId}.json";
                if (Godot.FileAccess.FileExists(filePath))
                {
                    Wild.Logger.Log($"CharacterSelectMenu: Eliminando archivo: {filePath}");
                    DirAccess.RemoveAbsolute(filePath);
                }
                
                Wild.Logger.Log($"CharacterSelectMenu: Personaje eliminado: {_selectedCharacterId}");
                _selectedCharacterId = string.Empty;
                
                // Si era el personaje actual, seleccionar el primero disponible
                if (CharacterManager.Instance.CurrentCharacter?.CharacterId == _selectedCharacterId && 
                    CharacterManager.Instance.AllCharacters.Count > 0)
                {
                    CharacterManager.Instance.SelectCharacter(CharacterManager.Instance.AllCharacters[0].CharacterId);
                }
                
                // Recargar lista y actualizar UI
                LoadCharacters();
                UpdateUI();
            }
            catch (Exception ex)
            {
                Wild.Logger.LogError($"CharacterSelectMenu: Error al eliminar personaje: {ex.Message}");
            }
        }
        else
        {
            Wild.Logger.LogError($"CharacterSelectMenu: No se encontró el personaje con ID: {_selectedCharacterId}");
        }
    }
    
    /// <summary>Maneja el botón de volver.</summary>
    private void OnBackPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenMainMenu();
    }
    
    /// <summary>Actualiza el estado de los botones.</summary>
    private void UpdateUI()
    {
        bool hasSelection = !string.IsNullOrEmpty(_selectedCharacterId);
        int totalCharacters = CharacterManager.Instance.AllCharacters.Count;
        
        // No se puede eliminar si:
        // 1. No hay selección
        // 2. Solo hay un personaje en total (debe quedar siempre al menos uno)
        bool canDelete = hasSelection && totalCharacters > 1;
        
        Wild.Logger.Log($"CharacterSelectMenu: UpdateUI - hasSelection={hasSelection}, totalCharacters={totalCharacters}, _selectedCharacterId='{_selectedCharacterId}', canDelete={canDelete}");
        
        _selectButton.Disabled = !hasSelection;
        _deleteButton.Disabled = !canDelete;
        
        Wild.Logger.Log($"CharacterSelectMenu: UpdateUI - SelectButton.Disabled={_selectButton.Disabled}, DeleteButton.Disabled={_deleteButton.Disabled}");
    }
    
    /// <summary>Formatea el tiempo de juego.</summary>
    private string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        }
        else if (time.TotalMinutes >= 1)
        {
            return $"{time.Minutes}m {time.Seconds}s";
        }
        else
        {
            return $"{time.Seconds}s";
        }
    }
}
