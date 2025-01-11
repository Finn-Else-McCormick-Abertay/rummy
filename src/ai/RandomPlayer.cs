
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

class RandomPlayer : ComputerPlayer 
{
    public RandomPlayer() : base("RandomPlayer") {
        random = new Random();
    }
    public RandomPlayer(int seed) : base($"RandomPlayer<{seed}>") {
        random = new Random(seed);
    }

    private readonly Random random;

    private readonly double TakeMeldChance = 1.0;//0.85;
    private readonly double TakeLayOffChance = 1.0;//0.6;
    
    public override void OnAddedToRound(Round round) {}
    public override void OnRemovedFromRound(Round round) {}

    public override void BeginTurn(Round round) {
        List<Card> drawnCards = new();
        Option<Card> topFromDiscardPile = None;

        // Draw from deck
        if (random.Next(2) == 0) {
            var card = round.Deck.Draw().Inspect(card => drawnCards.Add(card));
            Say($"Drew from deck.");
            Think($"Drew {card.Value} from deck.");
        }
        // Draw from discard
        else {
            topFromDiscardPile = round.DiscardPile.Draw().Inspect(card => drawnCards.Add(card));
            Say($"Drew {topFromDiscardPile.Value} from discard pile.");
        }
        drawnCards.ForEach(card => Hand.Add(card));

        Think($"Hand: {string.Join(", ", Hand.Cards)}");

        FindPotentialMelds(out var potentialMelds, out var nearSets, out var nearRuns);

        if (potentialMelds.Any()) { Think($"Potential Melds: {string.Join(", ", potentialMelds)}"); }
        if (nearSets.Any()) { Think($"Near Sets: {string.Join(", ", nearSets.Select(x => $"[{string.Join(", ", x)}]"))}"); }
        if (nearRuns.Any()) { Think($"Near Runs: {string.Join(", ", nearRuns.Select(x => $"[{string.Join(", ", x)}]"))}"); }

        if (potentialMelds.Count > 0 && random.NextDouble() <= TakeMeldChance) {
            var meld = potentialMelds.ElementAt(random.Next(potentialMelds.Count));
            round.Meld(meld).Inspect(_ => {
                meld.Cards.ToList().ForEach(card => Hand.Pop(card));
                Think($"Melded {meld}");
            }).InspectErr(err => Think($"Failed to meld {meld}: {err}"));
        }

        Dictionary<Card, List<IMeld>> potentialLayOffs = FindPotentialLayOffs(round);

        if (potentialLayOffs.Any()) { Think($"Potential Layoffs: {(Melds.Count == 0 ? "(cannot lay off)" : "")} {string.Join(", ", potentialLayOffs.Select(kvp => $"{kvp.Key} -> {(kvp.Value.Count > 1 ? "{" : "")}{string.Join(", ", kvp.Value)}{(kvp.Value.Count > 1 ? "}" : "")}"))}");  }
        // Can only lay off after having melded at least once
        if (Melds.Count > 0) {
            foreach (var (card, list) in potentialLayOffs) {
                if (random.NextDouble() <= TakeLayOffChance) {
                    var meld = list.Count == 1 ? list.First() : list.ElementAt(random.Next(list.Count));
                    Think($"Laid off {card} to {meld}");
                    Hand.Pop(card).Inspect(card => meld.LayOff(card));
                }
            }
        }

        Card cardToDiscard;
        do {
            cardToDiscard = Hand.Cards.ElementAt(random.Next(Hand.Count));
        } while(topFromDiscardPile.IsSomeAnd(topCard => cardToDiscard == topCard));

        Hand.Pop(cardToDiscard).Inspect(card => round.DiscardPile.Discard(card));

        Say($"Discarding {cardToDiscard}");

        round.EndTurn();
    }
}