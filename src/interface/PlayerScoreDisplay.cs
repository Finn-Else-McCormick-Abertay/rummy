using Godot;
using Rummy.Gameplay;
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

    public bool Highlighted { get; set { field = value; this.OnReady(UpdateStyle); } } = false;
    public bool Invalid { get; set { field = value; this.OnReady(UpdateStyle); } } = false;

    public Player Player {
        get;
        set {
            if (Player.IsValid()) { Player.NotifyScoreChanged -= UpdateText; Player.NotifyNameChanged -= UpdateText; }
            field = value;
            if (Player.IsValid()) this.OnReady(() => { UpdateText(); Player.NotifyScoreChanged += UpdateText; Player.NotifyNameChanged += UpdateText; });
        }
    }

    public Round Round {
        get;
        set {
            if (Round.IsValid()) {
                Round.NotifyTurnBegan -= OnTurnBegan;
                Round.NotifyTurnEnded -= OnTurnEnded;
                Round.NotifyTurnReset -= OnTurnReset;
            }
            field = value;
            if (Round.IsValid()) this.OnReady(() => {
                Highlighted = Player == Round.CurrentPlayer;
                Round.NotifyTurnBegan += OnTurnBegan;
                Round.NotifyTurnEnded += OnTurnEnded;
                Round.NotifyTurnReset += OnTurnReset;
            });
        }
    }

    private void UpdateText() {
        if (!nameLabel.IsValid() || !nameLabel.IsNodeReady() || !scoreLabel.IsValid() || !scoreLabel.IsNodeReady() || !Player.IsValid()) return;

        nameLabel.Text = Player.Name;
        scoreLabel.Text = Player.Score.ToString();
    }

    private void UpdateStyle() {
        if (Player.IsInvalid() || Round.IsInvalid()) return;

        ThemeTypeVariation = Invalid ? InvalidTypeVariationName : Highlighted ? HighlightedTypeVariationName : EmptyTypeVariationName;
    }

    private void OnTurnBegan(Player player) {
        Highlighted = Player == player;
    }

    private void OnTurnEnded(Player player, Result<Round.TurnRecord, string> result) {
        if (Player is not null && player == Player) {
            if (result.IsErr) Invalid = true;
        }
        /*if (result.IsOk && !Round.Finished) {
            Highlighted = Player == Round.NextPlayer;
        }*/
    }

    private void OnTurnReset() {
        Invalid = false;
        Highlighted = Player == Round.CurrentPlayer;
    }

    public override void _Notification(int what) {
        if (what == NotificationPredelete) {
            Player = null; Round = null;
        }
    }

}
