using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Result;
using static Rummy.Util.Option;
using System.Collections.Generic;
using System.Linq;
using Rummy.AI;
using System;

namespace Rummy.Interface;

public partial class GameManager : Node
{
    private Round round;
    private List<Player> players;

    private bool stateInvalid = false;

    private readonly UserPlayer userPlayer = new();

    [Export] private DrawableCardPileContainer Deck { get; set; }
    [Export] private DrawableCardPileContainer DiscardPile { get; set; }
    [Export] private PlayerHand PlayerHand { get; set; }
    [Export] private PlayerScoreDisplay PlayerScoreDisplay { get; set; }
    [Export] private Control EnemyScoreDisplayRoot { get; set; }
    [Export] private Control MeldRoot { get; set; }
    [Export] private Button DiscardButton { get; set; }
    [Export] private Button MeldButton { get; set; }
    [Export] private Button NextTurnButton { get; set; }
    [Export] private FailureMessage FailureMessage { get; set; }

    [Export] private PackedScene MeldScene { get; set; }
    [Export] private PackedScene EnemyScoreDisplayScene { get; set; }

    public override void _Ready() {
        PlayerHand.CardPile = userPlayer.Hand as CardPile;

        Deck.NotifyDrew += _ => round.Deck.Draw().Inspect(card => userPlayer.Hand.Add(card));
        DiscardPile.NotifyDrew += count => {
            var drawnCards = round.DiscardPile.Draw(count);
            drawnCards.ForEach(card => userPlayer.Hand.Add(card));
            if (count > 1) {
                PlayerHand.Select(drawnCards.Last());
            }
        };

        foreach (Node node in MeldRoot.GetChildren()) {
            MeldRoot.RemoveChild(node);
            node.QueueFree();
        }
        
        DiscardButton.Pressed += () => {
            var selected = PlayerHand.SelectedSequence.First();
            (userPlayer.Hand as IAccessibleCardPile).Cards.Remove(selected);
            round.DiscardPile.Discard(selected);
            round.EndTurn();
        };

        MeldButton.Pressed += () => {
            var sequence = PlayerHand.SelectedSequence;
            var set = new Set(sequence); var run = new Run(sequence);
            
            Result<Meld, string> melded = Err("Invalid meld");
            if (set.Valid)      { melded = round.Meld(set).And(set as Meld); }
            else if (run.Valid) { melded = round.Meld(run).And(run as Meld); }

            melded.Inspect(meld => meld.Cards.ToList().ForEach(card => userPlayer.Hand.Pop(card)));
            if (melded.IsOk) { RebuildMelds(); }
        };

        FailureMessage.Button.Pressed += () => {
            round.ResetTurn();
            stateInvalid = false;
            FailureMessage.Hide();
        };

        DiscardButton.Disabled = true;
        MeldButton.Disabled = true;
        
        NextTurnButton.Visible = false;

        players = new List<Player> {
            userPlayer,
            new RandomPlayer(),
            new RandomPlayer(),
            new RandomPlayer(5),
        };
        players.ForEach(player => {
            player.OnSayingMessage += (message) => GD.Print($"{player.Name}: {message}");
            player.OnThinkingMessage += (message) => GD.Print($"{player.Name}(Think): {message}");
        });

        round = new Round(players);
        Deck.CardPile = round.Deck;
        DiscardPile.CardPile = round.DiscardPile;
        round.NotifyTurnReset += RebuildMelds;
        round.NotifyMelded += (player, cards) => RebuildMelds();
        round.NotifyLaidOff += (player, card) => RebuildMelds();

        PlayerScoreDisplay.Player = userPlayer;
        PlayerScoreDisplay.Round = round;
        PlayerScoreDisplay.Visible = userPlayer is not null;

        foreach (Node node in EnemyScoreDisplayRoot.GetChildren()) {
            EnemyScoreDisplayRoot.RemoveChild(node);
            node.QueueFree();
        }
        round.Players.Where(x => x is not null && x != userPlayer).ToList().ForEach(player => {
            var node = EnemyScoreDisplayScene.Instantiate();
            EnemyScoreDisplayRoot.AddChild(node);
            node.SetOwner(EnemyScoreDisplayRoot);
            var display = node.GetNode("PlayerScoreDisplay") as PlayerScoreDisplay;
            display.Player = player;
            display.Round = round;
            var hand = node.FindChild("EnemyHand") as CardPileContainer;
            hand.CardPile = player.Hand as CardPile;
        });

        round.NotifyTurnBegan += player => {
            if (player == userPlayer) {
                Deck.AllowDraw = true;
                DiscardPile.AllowDraw = true;
                SetCanLayOff(false);
            }
        };
        round.NotifyTurnEnded += (player, result) => {
            Deck.AllowDraw = false;
            DiscardPile.AllowDraw = false;
            SetCanLayOff(false);

            if (round.NextPlayer != userPlayer && !round.Finished) {
                NextTurnButton.Visible = true;
                //NextTurnButton.GrabFocus();
            }
        };
        round.NotifyTurnEnded += (player, result) => {
            result.Inspect(_ => {
                DiscardButton.Disabled = true;
                MeldButton.Disabled = true;
                
                SetCanLayOff(false);
                FailureMessage.Hide();
            }).InspectErr(err => {
                FailureMessage.Message = err.Replace(". ", ".\n");
                FailureMessage.UseButton = true; FailureMessage.Show();
                stateInvalid = true;
                DiscardButton.Disabled = true;
                MeldButton.Disabled = true;
                NextTurnButton.Visible = false;
            });
        };
        round.NotifyTurnReset += () => {
            if (round.CurrentPlayer != userPlayer && !round.Finished) {
                NextTurnButton.Visible = true;
                //NextTurnButton.GrabFocus();
            }
        };

        NextTurnButton.Pressed += () => {
            NextTurnButton.Visible = false;
            round.BeginTurn();
        };

        round.NotifyGameEnded += (winner, score, isRummy) => {
            FailureMessage.Message = $"{round.Winner.Name} wins round{(isRummy ? " with a rummy" : "")}, scoring {score}.";
            GD.Print(FailureMessage.Message);
            FailureMessage.UseButton = false; FailureMessage.Show();
            NextTurnButton.Visible = false;
            SetCanLayOff(false);
        };
    }

    public override void _Process(double delta) {
        if (stateInvalid || round.CurrentPlayer != userPlayer) { return; }

        if (!round.MidTurn && !round.Finished) {
            round.BeginTurn();
        }
        if (round.MidTurn) {
            if (round.HasDrawn) {
                Deck.AllowDraw = false;
                DiscardPile.AllowDraw = false;
                SetCanLayOff(true);

                var selected = PlayerHand.SelectedSequence;
                DiscardButton.Disabled = selected.Count != 1;
                MeldButton.Disabled = !(new Run(selected).Valid || new Set(selected).Valid);
            }
            else {
                Deck.AllowDraw = true;
                DiscardPile.AllowDraw = true;
                DiscardButton.Disabled = true;
                MeldButton.Disabled = true;
                SetCanLayOff(false);
            }
        }
    }

    private void RebuildMelds() {
        foreach (Node node in MeldRoot.GetChildren()) {
            MeldRoot.RemoveChild(node);
            node.QueueFree();
        }
        round.Melds.ToList().ForEach(meld => {
            var meldContainer = MeldScene.Instantiate() as MeldContainer;
            MeldRoot.AddChild(meldContainer);
            meldContainer.SetOwner(MeldRoot);
            meldContainer.CardPile = meld as CardPile;
            meldContainer.PlayerHand = PlayerHand;
            meldContainer.NotifyLaidOff += card => {
                if (meldContainer.CardPile is Meld) {
                    userPlayer.Hand.Pop(card);
                    (meldContainer.CardPile as Meld).LayOff(card);
                }
            };
        });
    }

    private void SetCanLayOff(bool canLayOff) {
        foreach (MeldContainer container in MeldRoot.GetChildren().Cast<MeldContainer>()) {
            container.CanLayOff = canLayOff;
        }
    }
}
