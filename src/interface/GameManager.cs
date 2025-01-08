using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Result;
using static Rummy.Util.Option;
using System.Collections.Generic;
using System.Linq;

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
    [Export] private CardPileContainer EnemyHand { get; set; }
    [Export] private Button DiscardButton { get; set; }
    [Export] private Button MeldButton { get; set; }
    [Export] private FailureMessage FailureMessage { get; set; }

    public override void _Ready() {
        PlayerHand.CardPile = userPlayer.Hand as CardPile;
        userPlayer.TurnBegin += PlayerTurnBegin;

        Deck.NotifyDrew += _ => round.Deck.Draw().Inspect(card => userPlayer.Hand.Add(card));
        DiscardPile.NotifyDrew += count => round.DiscardPile.Draw(count).ForEach(card => userPlayer.Hand.Add(card));
        
        DiscardButton.Pressed += () => {
            var selected = PlayerHand.SelectedSequence.First();
            (userPlayer.Hand as IAccessibleCardPile).Cards.Remove(selected);
            round.DiscardPile.Discard(selected);
            round.EndTurn().Match(
                _ => {
                    DiscardButton.Disabled = true;
                    MeldButton.Disabled = true;
                    FailureMessage.Hide();
                },
                err => {
                    FailureMessage.Message = err;
                    FailureMessage.UseButton = true;
                    FailureMessage.Show();
                    stateInvalid = true;
                    DiscardButton.Disabled = true;
                    MeldButton.Disabled = true;
                }
            );
        };

        MeldButton.Pressed += () => {
            var sequence = PlayerHand.SelectedSequence;
            var set = new Set(sequence);
            var run = new Run(sequence);
            
            Result<Unit, string> melded = Err("Invalid meld");
            if (set.Valid) { melded = round.Meld(set); } else if (run.Valid) { melded = round.Meld(run); }

            melded.Match(
                _ => sequence.ForEach(card => userPlayer.Hand.Pop(card)),
                err => {
                    FailureMessage.Message = err;
                    FailureMessage.UseButton = false;
                    FailureMessage.Show();
                    GetTree().CreateTimer(5.0).Timeout += () => {
                        FailureMessage.Hide();
                    };
                }
            );
        };

        FailureMessage.Button.Pressed += () => {
            round.ResetTurn();
            stateInvalid = false;
            FailureMessage.Hide();
        };

        DiscardButton.Disabled = true;
        MeldButton.Disabled = true;

        players = new List<Player> {
            userPlayer,
            new UserPlayer(),
        };

        round = new Round(players);
        Deck.CardPile = round.Deck;
        DiscardPile.CardPile = round.DiscardPile;

        EnemyHand.CardPile = players[1].Hand as CardPile;

        (players[1] as UserPlayer).TurnBegin += (player, round) => {
            round.Deck.Draw().Inspect(card => player.Hand.Add(card));
            player.Hand.PopAt(0).Inspect(card => round.DiscardPile.Discard(card));
            round.EndTurn();
        };

        var melds = new List<IMeld> {
            new Run(new List<Card> { new(Rank.Two, Suit.Hearts), new(Rank.Three, Suit.Hearts), new(Rank.Four, Suit.Hearts) }),
            new Run(new List<Card> { new(Rank.Five, Suit.Hearts), new(Rank.Seven, Suit.Hearts), new(Rank.Ace, Suit.Hearts) }),
            new Run(new List<Card> { new(Rank.Six, Suit.Hearts), new(Rank.Seven, Suit.Hearts), new(Rank.Eight, Suit.Hearts), new(Rank.Nine, Suit.Hearts) }),
            new Run(new List<Card> { new(Rank.Ten, Suit.Clubs), new(Rank.Jack, Suit.Clubs), new(Rank.King, Suit.Clubs), new(Rank.Queen, Suit.Clubs) }),
        };
        melds.ForEach(meld => GD.Print($"{meld} ", meld.Valid ? "is valid." : "is not valid"));
    }

    public override void _Process(double delta) {
        if (stateInvalid) { return; }

        if (!round.Finished && !round.MidTurn) {
            round.BeginTurn();
        }
        if (round.CurrentPlayer == userPlayer && round.MidTurn) {
            if (round.HasDrawn) {
                Deck.AllowDraw = false;
                DiscardPile.AllowDraw = false;

                var selected = PlayerHand.SelectedSequence;
                DiscardButton.Disabled = selected.Count != 1;
                MeldButton.Disabled = !(new Run(selected).Valid || new Set(selected).Valid);
            }
            else {
                Deck.AllowDraw = true;
                DiscardPile.AllowDraw = true;
                DiscardButton.Disabled = true;
                MeldButton.Disabled = true;
            }
        }
    }

    private void PlayerTurnBegin(Player player, Round round) {
        Deck.AllowDraw = true;
        DiscardPile.AllowDraw = true;
    }
}
