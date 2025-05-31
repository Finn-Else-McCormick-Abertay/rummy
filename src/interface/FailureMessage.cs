using Godot;
using System;

namespace Rummy.Interface;

[Tool]
public partial class FailureMessage : PanelContainer
{
    [Export(PropertyHint.MultilineText)]
    public string Message { get => label?.Text ?? ""; set { label?.Set(Label.PropertyName.Text, value); } }

    public bool UseButton { get; set { field = value; buttonRoot?.Set(CanvasItem.PropertyName.Visible, value); } } = false;

    [Export] private Label label;
    [Export] private Control buttonRoot;
    [Export] private Button button;
    public Button Button => button;

    public void DisplayMessage(string msg, bool useButton = false) {
        Message = msg; UseButton = useButton; Show();
    }

    public override void _Ready() {
        if (!Engine.IsEditorHint()) Hide();
        buttonRoot.Visible = UseButton;
    }
}
