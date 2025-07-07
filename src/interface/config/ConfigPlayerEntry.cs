using Godot;
using Rummy.AI;
using Rummy.Game;
using Rummy.Util;
using System;
using System.Collections.Generic;

namespace Rummy.Interface;

public partial class ConfigPlayerEntry : PanelContainer {
    public GameManager GameManager { get; set; }
    public Player Player { get; set { field = value; this.OnReady(Rebuild); } }

    [Export] private Godot.Collections.Dictionary<string, Texture2D> _iconTextures = [];

    [Export] private Label _label;
    [Export] private TextureRect _icon;
    [Export] private Button _deleteButton;

    public override void _Ready() {
        _deleteButton.Pressed += OnDeletePressed;
    }

    private void Rebuild() {
        _label.Text = Player?.Name ?? "Invalid Player";
        _icon.Texture = Player switch {
            UserPlayer => _iconTextures.GetValueOrDefault("user"),
            RandomPlayer when _iconTextures.ContainsKey("random") => _iconTextures.GetValueOrDefault("random"),
            IntelligentPlayer when _iconTextures.ContainsKey("intelligent") => _iconTextures.GetValueOrDefault("intelligent"),
            ComputerPlayer or IntelligentPlayer or RandomPlayer => _iconTextures.GetValueOrDefault("computer"),
            _ => null
        };
    }

    private void OnDeletePressed() {
        if (GameManager.IsInvalid()) return;

        var players = GameManager.Players;
        players.Remove(Player);
        GameManager.Players = players;

        QueueFree();
    }
}
