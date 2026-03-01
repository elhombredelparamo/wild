using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Wild;

namespace Wild.UI;

/// <summary>
/// Menú de selección de mundos con lista de mundos disponibles.
/// Permite seleccionar, cargar y eliminar mundos completos.
/// </summary>
public partial class WorldSelectMenu : Control
{
    private VBoxContainer _worldList = null!;
    private Button _buttonLoad = null!;
    private Button _buttonDelete = null!;
    private Label _labelCharacterInfo = null!;
    private List<WorldInfo> _worlds = new();
    private string _selectedWorld = "";
    
    public override void _Ready()
    {
        Logger.Log("WorldSelectMenu: Inicializando menú de selección de mundos");
        
        // Referencias a controles
        _labelCharacterInfo = GetNode<Label>("CenterContainer/Panel/MarginContainer/VBox/LabelCharacterInfo");
        _worldList = GetNode<VBoxContainer>("CenterContainer/Panel/MarginContainer/VBox/ScrollContainer/WorldList");
        _buttonLoad = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/Buttons/ButtonLoad");
        _buttonDelete = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBox/Buttons/ButtonDelete");
        
        // Actualizar información del personaje
        UpdateCharacterInfo();
        
        // Cargar lista de mundos
        RefreshWorldList();
        
        // Conectar señal para actualizar cuando volvemos
        TreeEntered += UpdateCharacterInfo;
    }
    
    /// <summary>Actualiza la información del personaje seleccionado.</summary>
    private void UpdateCharacterInfo()
    {
        if (CharacterManager.Instance?.CurrentCharacter != null)
        {
            var character = CharacterManager.Instance.CurrentCharacter;
            _labelCharacterInfo.Text = $"Jugando como: {character.DisplayName}";
        }
        else
        {
            _labelCharacterInfo.Text = "No hay personaje seleccionado";
        }
    }
    
    /// <summary>Actualiza la lista de mundos disponibles.</summary>
    private void RefreshWorldList()
    {
        Logger.Log("WorldSelectMenu: Actualizando lista de mundos");
        
        // Limpiar lista actual
        foreach (Node child in _worldList.GetChildren())
        {
            child.QueueFree();
        }
        
        // Obtener mundos
        _worlds = WorldManager.Instance.GetAllWorlds();
        _selectedWorld = "";
        
        if (_worlds.Count == 0)
        {
            // Mostrar mensaje si no hay mundos
            var label = new Label
            {
                Text = "No hay mundos disponibles. Crea uno nuevo desde el menú principal.",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = Colors.Gray
            };
            _worldList.AddChild(label);
        }
        else
        {
            // Crear botones para cada mundo
            foreach (var worldInfo in _worlds)
            {
                var button = new Button
                {
                    Text = worldInfo.GetDisplayText(),
                    CustomMinimumSize = new Vector2(0, 50),
                    ToggleMode = true
                };
                
                // Asignar datos del mundo al botón
                button.SetMeta("worldName", worldInfo.Name);
                
                // Estilo visual por defecto
                button.Modulate = Colors.LightGray;
                
                // Conectar evento
                button.Pressed += () => OnWorldSelected(worldInfo.Name, worldInfo);
                
                _worldList.AddChild(button);
            }
        }
        
        // Actualizar estado de botones
        UpdateButtonStates();
    }
    
    /// <summary>Se ejecuta al seleccionar un mundo.</summary>
    private void OnWorldSelected(string worldName, WorldInfo worldInfo)
    {
        Logger.Log($"WorldSelectMenu: Mundo seleccionado: {worldName}");
        
        // Deseleccionar todos los botones
        foreach (Button button in _worldList.GetChildren())
        {
            button.ButtonPressed = false;
        }
        
        // Seleccionar el botón actual
        var selectedButton = GetWorldButton(worldName);
        if (selectedButton != null)
        {
            selectedButton.ButtonPressed = true;
            
            // Resaltar visualmente el mundo seleccionado
            HighlightSelectedWorld(selectedButton, worldInfo);
        }
        
        _selectedWorld = worldName;
        UpdateButtonStates();
    }
    
    /// <summary>Resalta visualmente el mundo seleccionado.</summary>
    private void HighlightSelectedWorld(Button button, WorldInfo worldInfo)
    {
        // Restaurar colores de todos los botones
        foreach (Button btn in _worldList.GetChildren())
        {
            btn.Modulate = Colors.LightGray;
            btn.CustomMinimumSize = new Vector2(0, 50);
            
            // Quitar estilos personalizados
            btn.RemoveThemeStyleboxOverride("normal");
        }
        
        // Resaltar el botón seleccionado con colores más vivos
        button.Modulate = Colors.White;
        
        // Aumentar tamaño para mayor visibilidad
        button.CustomMinimumSize = new Vector2(0, 55);
        
        // Añadir borde visible
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = Colors.LightGreen;
        styleBox.BorderColor = Colors.Green;
        styleBox.BorderWidthLeft = 3;
        styleBox.BorderWidthRight = 3;
        styleBox.BorderWidthTop = 3;
        styleBox.BorderWidthBottom = 3;
        styleBox.CornerRadiusTopLeft = 6;
        styleBox.CornerRadiusTopRight = 6;
        styleBox.CornerRadiusBottomLeft = 6;
        styleBox.CornerRadiusBottomRight = 6;
        button.AddThemeStyleboxOverride("normal", styleBox);
    }
    
    /// <summary>Obtiene el botón correspondiente a un mundo.</summary>
    private Button? GetWorldButton(string worldName)
    {
        foreach (Button button in _worldList.GetChildren())
        {
            if ((string)button.GetMeta("worldName") == worldName)
                return button;
        }
        return null;
    }
    
    /// <summary>Actualiza el estado de los botones de acción.</summary>
    private void UpdateButtonStates()
    {
        bool hasSelection = !string.IsNullOrEmpty(_selectedWorld);
        
        _buttonLoad.Disabled = !hasSelection;
        _buttonDelete.Disabled = !hasSelection;
    }
    
    /// <summary>Vuelve al menú principal.</summary>
    private void _on_button_back_pressed()
    {
        Logger.Log("WorldSelectMenu: Volviendo al menú principal");
        GetTree().ChangeSceneToFile(GameFlow.SceneMainMenu);
    }
    
    /// <summary>Elimina el mundo seleccionado con confirmación.</summary>
    private void _on_button_delete_pressed()
    {
        if (string.IsNullOrEmpty(_selectedWorld))
            return;
        
        Logger.Log($"WorldSelectMenu: Solicitando confirmación para eliminar mundo: {_selectedWorld}");
        
        // Usar AcceptDialog para confirmación simple
        var confirmDialog = new AcceptDialog();
        confirmDialog.DialogText = $"¿Estás seguro de que quieres eliminar el mundo '{_selectedWorld}'?\n\nEsta acción no se puede deshacer y se eliminarán todos los datos del mundo permanentemente.";
        confirmDialog.Title = "Eliminar Mundo";
        confirmDialog.OkButtonText = "Eliminar";
        
        // Conectar evento de confirmación
        confirmDialog.Confirmed += () => OnDeleteConfirmed(_selectedWorld);
        
        // Mostrar diálogo
        AddChild(confirmDialog);
        confirmDialog.PopupCentered();
        
        // Después de mostrar el diálogo, buscar y modificar el botón de cancelación
        // AcceptDialog tiene un botón de cancelación por defecto con texto "Cancel"
        var cancelButton = confirmDialog.FindChild("CancelButton", true, false) as Button;
        if (cancelButton != null)
        {
            cancelButton.Text = "Cancelar";
        }
    }
    
    /// <summary>Se ejecuta cuando el usuario confirma la eliminación.</summary>
    private void OnDeleteConfirmed(string worldName)
    {
        Logger.Log($"WorldSelectMenu: Confirmación recibida - eliminando mundo: {worldName}");
        
        bool success = WorldManager.Instance.DeleteWorld(worldName);
        if (success)
        {
            Logger.Log($"WorldSelectMenu: Mundo eliminado correctamente: {worldName}");
            RefreshWorldList();
        }
        else
        {
            Logger.LogError($"WorldSelectMenu: Error al eliminar mundo: {worldName}");
        }
    }
    
    /// <summary>Carga el mundo seleccionado.</summary>
    private void _on_button_load_pressed()
    {
        if (string.IsNullOrEmpty(_selectedWorld))
            return;
        
        Logger.Log($"WorldSelectMenu: Cargando mundo: {_selectedWorld}");
        
        // Cargar mundo a través de GameFlow
        var gameFlow = GetNode<GameFlow>("/root/GameFlow");
        gameFlow.LoadGame(_selectedWorld);
    }
}
