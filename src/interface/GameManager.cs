using Godot;
using Rummy.Game;
using Rummy.Util;
using System.Collections.Generic;
using System.Linq;

namespace Rummy.Interface;

public partial class GameManager : Node
{
    private Round round;
    private List<Player> players;

    private readonly UserPlayer userPlayer = new();

    [Export] private CardPileDisplay Deck { get; set; }
    [Export] private CardPileDisplay DiscardPile { get; set; }
    [Export] private PlayerHand PlayerHand { get; set; }
    [Export] private CardPileDisplay EnemyHand { get; set; }
    [Export] private Button DiscardButton { get; set; }
    [Export] private Button MeldButton { get; set; }

    public override void _Ready() {
        PlayerHand.HookTo(userPlayer.Hand as CardPile);
        userPlayer.TurnBegin += PlayerTurnBegin;

        Deck.NotifyDrew += (_) => { userPlayer.Hand.Add(round.Deck.Draw()); };
        DiscardPile.NotifyDrew += (count) => { foreach (Card card in round.DiscardPile.Draw(count)) { userPlayer.Hand.Add(card); } };
        
        DiscardButton.Pressed += () => {
            var selected = PlayerHand.SelectedSequence.First();
            (userPlayer.Hand as IAccessibleCardPile).Cards.Remove(selected);
            round.DiscardPile.Discard(selected);
            round.EndTurn();
            DiscardButton.Disabled = true;
            MeldButton.Disabled = true;
        };

        MeldButton.Pressed += () => {
            var sequence = PlayerHand.SelectedSequence;
            Set set = new(sequence); Run run = new(sequence);
            
            var melded = Result.Ok();
            if (set.Valid) { melded = round.Meld(set); }
            else if (run.Valid) { melded = round.Meld(set); }

            if (melded) { foreach (Card card in sequence) { userPlayer.Hand.Pop(card); } }
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
            player.Hand.Add(round.Deck.Draw());
            round.DiscardPile.Discard((Card)player.Hand.PopAt(0));
            round.EndTurn();
        };
    }

    public override void _Process(double delta) {
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
