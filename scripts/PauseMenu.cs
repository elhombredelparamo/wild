using Godot;

namespace Wild;

/// <summary>
/// Menú de pausa flotante que aparece con ESC durante el juego
/// </summary>
public partial class PauseMenu : Control
{
    private Button _buttonQuit = null!;
    private bool _isPaused = false;

    public override void _Ready()
    {
        // Ocultar inicialmente
        Visible = false;
        
        // Obtener referencia al botón
        try {
            _buttonQuit = GetNode<Button>("CenterContainer/Panel/MarginContainer/VBoxContainer/ButtonQuit");
            Logger.Log("PauseMenu: Botón SALIR encontrado y conectado");
            Logger.Log($"PauseMenu: Botón visible: {_buttonQuit.Visible}, habilitado: {_buttonQuit.Disabled}");
        } catch (System.Exception ex) {
            Logger.Log($"PauseMenu: ERROR - No se encontró el botón SALIR: {ex.Message}");
            return;
        }
        
        // Usar método Connect con lambda (similar a onClick de Java)
        _buttonQuit.Connect(Button.SignalName.Pressed, Callable.From(() => {
            Logger.Log("PauseMenu: Botón SALIR presionado (vía Connect)");
            HidePause();
            GetNode<GameFlow>("/root/GameFlow").ReturnToMainMenu();
        }));
        
        Logger.Log("PauseMenu: _Ready() completado con método Connect");
    }

    public override void _Process(double delta)
    {
        // Ya no necesitamos detección manual de clics
        // El botón está conectado correctamente con Connect()
    }

    public override void _GuiInput(InputEvent @event)
    {
        // Eliminado - ya no necesitamos detección manual de clics
    }

    public override void _Input(InputEvent ev)
    {
        // ESC para mostrar/ocultar menú de pausa
        if (ev.IsActionPressed("ui_cancel") || ev.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            TogglePause();
        }
    }

    private void TogglePause()
    {
        if (_isPaused)
        {
            HidePause();
        }
        else
        {
            ShowPause();
        }
    }

    private void ShowPause()
    {
        Visible = true;
        _isPaused = true;
        
        // Habilitar el botón SALIR
        _buttonQuit.Disabled = false;
        
        // Congelar movimiento del jugador y cámara
        if (GetTree().CurrentScene is GameWorld gameWorld)
        {
            gameWorld.FreezePlayer();
        }
        
        // Mostrar cursor
        Input.MouseMode = Input.MouseModeEnum.Visible;
        
        Logger.Log("PauseMenu: Menú de pausa mostrado - jugador y cámara congelados");
        Logger.Log($"PauseMenu: Botón habilitado: {!_buttonQuit.Disabled}");
    }

    private void HidePause()
    {
        Visible = false;
        _isPaused = false;
        
        // Descongelar movimiento del jugador y cámara
        if (GetTree().CurrentScene is GameWorld gameWorld)
        {
            gameWorld.UnfreezePlayer();
        }
        
        // Ocultar cursor (capturar para juego)
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        Logger.Log("PauseMenu: Menú de pausa ocultado - jugador y cámara descongelados");
    }

    private void OnQuitPressed()
    {
        Logger.Log("PauseMenu: Botón SALIR presionado - volviendo al menú principal");
        
        try {
            // Ocultar menú de pausa
            HidePause();
            
            // Volver al menú principal
            GetNode<GameFlow>("/root/GameFlow").ReturnToMainMenu();
            Logger.Log("PauseMenu: ReturnToMainMenu() ejecutado");
        } catch (System.Exception ex) {
            Logger.Log($"PauseMenu: ERROR al volver al menú principal: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica si el menú está en pausa
    /// </summary>
    public bool IsPaused()
    {
        return _isPaused;
    }
}
