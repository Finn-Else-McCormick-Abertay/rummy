using Godot;
using System;
using Rummy.Util;
using System.Linq;

namespace Rummy.Interface;

[Tool]
public partial class FailureMessage : PanelContainer
{
    public GameManager GameManager { get; set; }

    [Export(PropertyHint.MultilineText)]
    public string Message { get => label?.Text ?? ""; set { label?.Set(Label.PropertyName.Text, value); } }

    public bool UseButton { get; set { field = value; buttonRoot?.Set(CanvasItem.PropertyName.Visible, value); } } = false;

    [Export] private Label label;
    [Export] private Control buttonRoot;
    [Export] private Button button;
    public Button Button => button;

    [Export] private Control _newGameButtonsRoot;
    [Export] private Control _playButtonRoot;
    [Export] private Control _simulateButtonRoot;
    private BaseButton PlayButton => _playButtonRoot?.FindChildOfType<BaseButton>();
    private BaseButton SimulateButton => _simulateButtonRoot?.FindChildOfType<BaseButton>();

    public void DisplayMessage(string msg, bool useButton = false) {
        Message = msg; UseButton = useButton; Show();
        _newGameButtonsRoot.Visible = !GameManager.InGame;
        _simulateButtonRoot.Visible = !GameManager.Players.Any(x => x is UserPlayer);
    }

    public override void _Ready() {
        if (!Engine.IsEditorHint()) Hide();
        buttonRoot.Visible = UseButton;

        PlayButton.Pressed += OnPlayButtonPressed;
        SimulateButton.Pressed += OnSimulateButtonPressed;
    }

    private void OnPlayButtonPressed() {
        GameManager.BeginNewRound();
    }

    private void OnSimulateButtonPressed() {
        GameManager.SimulateRoundWithoutDisplay();
    }
}
