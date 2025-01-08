using Godot;
using System;

namespace Rummy.Interface;

public partial class FailureMessage : PanelContainer
{
    public string Message { get => IsNodeReady() ? label.Text : ""; set { if (IsNodeReady()) { label.Text = value; } }  }

    private bool _useButton = false;
    public bool UseButton { get => _useButton; set { _useButton = value; if (IsNodeReady()) { buttonRoot.Visible = value; } } }

    [Export] private Label label;
    [Export] private Control buttonRoot;
    [Export] private Button button;

    public Button Button => button;

    public override void _Ready() {
        Visible = false;
        buttonRoot.Visible = _useButton;
    }
}
