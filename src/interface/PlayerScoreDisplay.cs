using Godot;
using Rummy.Game;
using Rummy.Util;
using System;
using System.Linq;

namespace Rummy.Interface;

[Tool]
public partial class PlayerScoreDisplay : PanelContainer
{
    [Export] private Label nameLabel;
    [Export] private Label scoreLabel;

    private static readonly StringName EmptyTypeVariationName = "";
    private static readonly StringName HighlightedTypeVariationName = "ScoreDisplayHighlighted";
    private static readonly StringName InvalidTypeVariationName = "ScoreDisplayInvalid";

    public bool Highlighted { get; set { field = value; if (IsNodeReady()) UpdateStyle(); } } = false;
    public bool Invalid { get; set { field = value; if (IsNodeReady()) UpdateStyle(); } } = false;

    public Player Player {
        get;
        set {
            if (Player is not null) { Player.NotifyScoreChanged -= UpdateText; Player.NotifyNameChanged -= UpdateText; }
            field = value;
            if (IsNodeReady() && Player is not null) {
                UpdateText(); Player.NotifyScoreChanged += UpdateText; Player.NotifyNameChanged += UpdateText;
            }
        }
    }

    public Round Round {
        get;
        set {
            if (Round is not null) { Round.NotifyTurnBegan -= OnTurnBegan; Round.NotifyTurnEnded -= OnTurnEnded; Round.NotifyTurnReset -= OnTurnReset; }
            field = value;
            if (IsNodeReady() && Round is not null) {
                Highlighted = Player == Round.CurrentPlayer;
                Round.NotifyTurnBegan += OnTurnBegan; Round.NotifyTurnEnded += OnTurnEnded; Round.NotifyTurnReset += OnTurnReset;
            }
        }
    }

    public override void _Ready() {
        Player = Player; Round = Round; Highlighted = Highlighted;
    }

    private void UpdateText() {
        if (!nameLabel.IsValid() || !nameLabel.IsNodeReady() || !scoreLabel.IsValid() || !scoreLabel.IsNodeReady() || !Player.IsValid()) return;

        nameLabel.Text = Player.Name;
        scoreLabel.Text = Player.Score.ToString();
    }

    private void UpdateStyle() {
        if (Player is null || Round is null) return;

        ThemeTypeVariation = Invalid ? InvalidTypeVariationName : Highlighted ? HighlightedTypeVariationName : EmptyTypeVariationName;
    }

    private void OnTurnBegan(Player player) {
        Highlighted = Player == player;
    }

    private void OnTurnEnded(Player player, Result<Round.TurnRecord, string> result) {
        if (Player is not null && player == Player) {
            if (result.IsErr) { Invalid = true; }
        }
        if (result.IsOk && !Round.Finished) {
            Highlighted = Player == Round.NextPlayer;
        }
    }

    private void OnTurnReset() {
        Invalid = false;
        Highlighted = Player == Round.CurrentPlayer;
    }
}
