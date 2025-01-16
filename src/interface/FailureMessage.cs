using Godot;
using System;

namespace Rummy.Interface;

[Tool]
public partial class FailureMessage : PanelContainer
{
    [Export(PropertyHint.MultilineText)]
    public string Message {
        get => label is not null ? label.Text : "";
        set { if (label is not null) { label.Text = value; } }
    }

    private bool _useButton = false;
    public bool UseButton { get => _useButton; set { _useButton = value; if (buttonRoot is not null) { buttonRoot.Visible = value; } } }

    [Export] private Label label;
    [Export] private Control buttonRoot;
    [Export] private Button button;
    public Button Button => button;

    public void DisplayMessage(string msg, bool useButton = false) {
        Message = msg; UseButton = useButton;
        Show();
    }

    public override void _Ready() {
        if (!Engine.IsEditorHint()) { Visible = false; }
        buttonRoot.Visible = _useButton;
    }
}
