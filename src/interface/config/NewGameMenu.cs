using Godot;
using Rummy.Interface;
using Rummy.Util;
using System;
using System.Collections.Generic;

namespace Rummy.Interface;

public partial class NewGameMenu : Control
{
    public GameManager GameManager { get; set; }
    [Export] private Control _playerEntryRoot;

    [Export] private PackedScene _playerEntryScene;

    public override void _Ready() {
        RebuildPlayerEntries();
    }

    private void RebuildPlayerEntries() {
        if (GameManager.IsInvalid()) return;

        foreach (var child in _playerEntryRoot.GetChildren()) child.QueueFree();

        foreach (var player in GameManager.Players) {
            var entry = _playerEntryScene.Instantiate<ConfigPlayerEntry>();
            entry.Player = player;
            _playerEntryRoot.AddChild(entry);
            entry.GameManager = GameManager;
        }
    }
}
