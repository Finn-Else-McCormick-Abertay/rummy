using System;
using Godot;
using Rummy.Util;

namespace Rummy.Interface;

public partial class NewGameMenu : ConfigMenu
{
    [Export] private Control _playerEntryRoot;
    [Export] private PackedScene _playerEntryScene;

    [ExportGroup("Buttons")]
    [Export] private BaseButton _playButton;
    [Export] private BaseButton _simulateButton;
    [Export] private BaseButton _closeButton;

    public override void _Ready() {
        _closeButton?.Connect(BaseButton.SignalName.Pressed, () => EmitSignal(ConfigMenu.SignalName.CloseRequested));
    }

    protected override void Rebuild() {
        foreach (var child in _playerEntryRoot.GetChildren()) child.QueueFree();
        if (GameManager.IsInvalid()) return;

        foreach (var player in GameManager.Players) {
            var entry = _playerEntryScene.Instantiate<ConfigPlayerEntry>();
            entry.Player = player;
            _playerEntryRoot.AddChild(entry);
            entry.GameManager = GameManager;
        }
    }

    private void Confirm(Action onConfirm, string title = null, string message = null, string acceptText = null) {
        var confirmationDialog = new ConfirmationDialog();
        confirmationDialog.Confirmed += onConfirm;

        if (title is not null) confirmationDialog.Title = title;
        if (message is not null) confirmationDialog.DialogText = message;
        if (acceptText is not null) confirmationDialog.OkButtonText = acceptText;

        AddChild(confirmationDialog);
        confirmationDialog.PopupCentered();
        confirmationDialog.Show();
    }
}
