using Godot;

namespace Wild;

/// <summary>
/// Menú de opciones con panel lateral: Controles, Gráficos.
/// Controles: movimiento WASD, saltar ESPACIO, agacharse CTRL.
/// Gráficos: distancia de renderizado (metros).
/// </summary>
public partial class OptionsMenu : Control
{
    private Button _buttonControls = null!;
    private Button _buttonGraphics = null!;
    private Button _buttonBack = null!;
    private Control _panelControls = null!;
    private Control _panelGraphics = null!;
    private SpinBox _spinRenderDistance = null!;
    private Label _labelRenderDistanceValue = null!;

    public override void _Ready()
    {
        _buttonControls = GetNode<Button>("HBox/PanelLeft/VBox/ButtonControls");
        _buttonGraphics = GetNode<Button>("HBox/PanelLeft/VBox/ButtonGraphics");
        _buttonBack = GetNode<Button>("HBox/PanelLeft/VBox/ButtonBack");
        _panelControls = GetNode<Control>("HBox/PanelContent/Margin/ContentStack/PanelControls");
        _panelGraphics = GetNode<Control>("HBox/PanelContent/Margin/ContentStack/PanelGraphics");
        _spinRenderDistance = GetNode<SpinBox>("HBox/PanelContent/Margin/ContentStack/PanelGraphics/VBox/RenderRow/SpinRenderDistance");
        _labelRenderDistanceValue = GetNode<Label>("HBox/PanelContent/Margin/ContentStack/PanelGraphics/VBox/RenderRow/LabelRenderValue");

        _buttonControls.Pressed += () => ShowSubmenu(0);
        _buttonGraphics.Pressed += () => ShowSubmenu(1);
        _buttonBack.Pressed += OnBackPressed;

        _spinRenderDistance.MinValue = GameSettings.MinRenderDistance;
        _spinRenderDistance.MaxValue = GameSettings.MaxRenderDistance;
        _spinRenderDistance.Step = 5;
        _spinRenderDistance.Value = GetNode<GameSettings>("/root/GameSettings").RenderDistanceMetres;
        _spinRenderDistance.ValueChanged += OnRenderDistanceChanged;

        UpdateRenderDistanceLabel();
        ShowSubmenu(0);
    }

    private void ShowSubmenu(int index)
    {
        _panelControls.Visible = index == 0;
        _panelGraphics.Visible = index == 1;
    }

    private void OnRenderDistanceChanged(double value)
    {
        var settings = GetNode<GameSettings>("/root/GameSettings");
        settings.RenderDistanceMetres = (float)value;
        UpdateRenderDistanceLabel();
        settings.Save();
    }

    private void UpdateRenderDistanceLabel()
    {
        var v = GetNode<GameSettings>("/root/GameSettings").RenderDistanceMetres;
        _labelRenderDistanceValue.Text = $"{v:F0} m";
    }

    private void OnBackPressed()
    {
        GetNode<GameFlow>("/root/GameFlow").ReturnToMainMenu();
    }
}
