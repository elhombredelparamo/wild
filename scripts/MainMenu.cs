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
    private Button _buttonCharacterSelect = null!;
    private Button _buttonOptions = null!;
    private Button _buttonQuit = null!;
    private ColorRect _backgroundRect = null!;

    public override void _Ready()
    {
        Logger.Log("[MainMenu] _Ready() llamado");
        
        // Configurar fondo con imagen usando sistema de importación
        _backgroundRect = GetNode<ColorRect>("ColorRect");
        try
        {
            var texture = GD.Load<Texture2D>("res://assets/ui/fondomenuprincipal.png");
            if (texture != null)
            {
                // Reemplazar ColorRect con TextureRect
                var parent = _backgroundRect.GetParent();
                var textureRect = new TextureRect();
                textureRect.Name = "TextureRect";
                textureRect.AnchorLeft = 0;
                textureRect.AnchorTop = 0;
                textureRect.AnchorRight = 1;
                textureRect.AnchorBottom = 1;
                textureRect.OffsetLeft = 0;
                textureRect.OffsetTop = 0;
                textureRect.OffsetRight = 0;
                textureRect.OffsetBottom = 0;
                textureRect.Texture = texture;
                textureRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
                textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
                
                parent.RemoveChild(_backgroundRect);
                parent.AddChild(textureRect);
                
                // Mover los botones al frente (después de la imagen de fondo)
                var centerContainer = GetNode<CenterContainer>("CenterContainer");
                centerContainer.MoveToFront();
                
                Logger.Log("[MainMenu] Imagen de fondo cargada mediante sistema de importación");
            }
            else
            {
                Logger.Log("[MainMenu] No se pudo cargar la imagen mediante importación, manteniendo ColorRect de fallback");
            }
        }
        catch (System.Exception ex)
        {
            Logger.Log($"[MainMenu] ERROR cargando imagen de fondo: {ex.Message}");
        }
        
        try
        {
            _buttonNewGame = GetNode<Button>("CenterContainer/VBoxContainer/ButtonNewGame");
            Logger.Log("[MainMenu] Botón Nueva partida encontrado");
        }
        catch
        {
            Logger.Log("[MainMenu] ERROR: No se encontró el botón Nueva partida");
        }
        
        try
        {
            _buttonLoadGame = GetNode<Button>("CenterContainer/VBoxContainer/ButtonLoadGame");
            Logger.Log("[MainMenu] Botón Cargar partida encontrado");
        }
        catch
        {
            Logger.Log("[MainMenu] ERROR: No se encontró el botón Cargar partida");
        }
        
        try
        {
            _buttonCharacterSelect = GetNode<Button>("CenterContainer/VBoxContainer/ButtonCharacterSelect");
            Logger.Log("[MainMenu] Botón Seleccionar Personaje encontrado");
        }
        catch
        {
            Logger.Log("[MainMenu] ERROR: No se encontró el botón Seleccionar Personaje");
        }
        
        try
        {
            _buttonOptions = GetNode<Button>("CenterContainer/VBoxContainer/ButtonOptions");
            Logger.Log("[MainMenu] Botón Opciones encontrado");
        }
        catch
        {
            Logger.Log("[MainMenu] ERROR: No se encontró el botón Opciones");
        }
        
        try
        {
            _buttonQuit = GetNode<Button>("CenterContainer/VBoxContainer/ButtonQuit");
            Logger.Log("[MainMenu] Botón Salir encontrado");
        }
        catch
        {
            Logger.Log("[MainMenu] ERROR: No se encontró el botón Salir");
        }

        try
        {
            _buttonNewGame.Pressed += OnNewGamePressed;
            _buttonLoadGame.Pressed += OnLoadGamePressed;
            _buttonCharacterSelect.Pressed += OnCharacterSelectPressed;
            _buttonOptions.Pressed += OnOptionsPressed;
            _buttonQuit.Pressed += OnQuitPressed;
            Logger.Log("[MainMenu] Eventos de botones configurados");
        }
        catch (Exception ex)
        {
            Logger.Log($"[MainMenu] ERROR configurando eventos: {ex.Message}");
        }
        
        Logger.Log("[MainMenu] _Ready() completado");
    }

    private void OnNewGamePressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenNewGameMenu();
    }

    private void OnLoadGamePressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenWorldSelectMenu();
    }

    private void OnCharacterSelectPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").OpenCharacterSelectMenu();
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
