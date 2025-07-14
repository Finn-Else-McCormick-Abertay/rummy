using Godot;
using Rummy.Util;
using System;

[Tool]
public partial class TitleLine : Control
{
    [Export] public string Title { get; set { field = value; this.OnReady(() => _titleLabel.IfValid(x => x.Text = Title)); } }
    [Export] public bool ShowClose { get; set { field = value; this.OnReady(() => _closeButton.IfValid(x => x.Visible = ShowClose)); } } = true;

    [ExportGroup("Nodes")]
    [Export] private Label _titleLabel;
    [Export] private BaseButton _closeButton;

    public BaseButton CloseButton => _closeButton;
}
