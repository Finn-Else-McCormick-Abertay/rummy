using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rummy.AI;
using Rummy.Game;
using Rummy.Util;

namespace Rummy.Interface;

public partial class NewGameMenu : ConfigMenu {
    [Export] private Control _playerEntryRoot;
    [Export] private PackedScene _playerEntryScene;

    [ExportGroup("Buttons")]
    [Export] private BaseButton _playButton;
    [Export] private BaseButton _simulateButton;
    [Export] private BaseButton _closeButton;
    [Export] private BaseButton _addButton;

    public override void _Ready() {
        _playButton?.Connect(BaseButton.SignalName.Pressed, () => AcceptAction(GameManager.BeginNewRound));
        _simulateButton?.Connect(BaseButton.SignalName.Pressed, () => AcceptAction(GameManager.SimulateRoundWithoutDisplay, false));
        _closeButton?.Connect(BaseButton.SignalName.Pressed, () => EmitSignal(ConfigMenu.SignalName.CloseRequested));
        _addButton?.Connect(BaseButton.SignalName.Pressed, AddPlayer);

        _playerEntryRoot.Connect("order_changed", OnReordered);
    }

    private void OnReordered() {
        var playerEntries = _playerEntryRoot.FindChildrenOfType<ConfigPlayerEntry>().Select(x => KeyValuePair.Create(x.Player, x)).ToDictionary();

        var players = GameManager.Players.OrderBy(x => playerEntries[x].GetIndex());
        GameManager.Players = [.. players];
    }

    protected override void Rebuild() {
        foreach (var child in _playerEntryRoot.GetChildren()) {
            _playerEntryRoot.RemoveChild(child); child.QueueFree();
        }
        if (GameManager.IsInvalid()) return;

        if (_playerEntryScene.IsValid())
            foreach (var player in GameManager.Players) {
                var entry = _playerEntryScene?.Instantiate<ConfigPlayerEntry>();
                entry.Player = player; entry.GameManager = GameManager;
                _playerEntryRoot.AddChild(entry);
            }
    }

    private void AcceptAction(Action gameAction, bool shouldClose = true) {
        void OnAccept() {
            gameAction?.Invoke();
            EmitSignal(ConfigMenu.SignalName.CloseRequested);
        }
        if (GameManager.Round is null) OnAccept();
        else Confirm(OnAccept, title: "Are you sure?", message: "Will overwrite current game.");
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

    private void AddPlayer() {
        UserPlayer newPlayer = new();
        GameManager.Players = [.. GameManager.Players.Concat([newPlayer])];
        Rebuild();
    }
}
