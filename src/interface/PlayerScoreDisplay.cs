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

    private bool _highlighted = false;
    public bool Highlighted { get => _highlighted; set { _highlighted = value; if (IsNodeReady()) { UpdateStyle(); } } }

    private bool _invalid = false;
    public bool Invalid { get => _invalid; set { _invalid = value; if (IsNodeReady()) { UpdateStyle(); } } }

    private Player _player;
    public Player Player {
        get => _player;
        set {
            if (Player is not null) {
                Player.NotifyScoreChanged -= UpdateText;
                Player.NotifyNameChanged -= UpdateText;
            }
            _player = value;
            if (IsNodeReady() && Player is not null) {
                UpdateText();
                Player.NotifyScoreChanged += UpdateText;
                Player.NotifyNameChanged += UpdateText;
            }
        }
    }

    private Round _round;
    public Round Round {
        get => _round;
        set {
            if (Round is not null) {
                Round.NotifyTurnBegan -= OnTurnBegan;
                Round.NotifyTurnEnded -= OnTurnEnded;
                Round.NotifyTurnReset -= OnTurnReset;
            }
            _round = value;
            if (IsNodeReady() && Round is not null) {
                Round.NotifyTurnBegan += OnTurnBegan;
                Round.NotifyTurnEnded += OnTurnEnded;
                Round.NotifyTurnReset += OnTurnReset;
            }
        }
    }

    public override void _Ready() {
        Player = _player;
        Round = _round;
        Highlighted = _highlighted;
    }

    private void UpdateText() {
        if (!IsNodeReady() || Player is null || nameLabel is null || scoreLabel is null) { return; }

        nameLabel.Text = Player.Name;
        scoreLabel.Text = Player.Score.ToString();
    }

    private void UpdateStyle() {
        if (Player is null || Round is null) { return; }

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
