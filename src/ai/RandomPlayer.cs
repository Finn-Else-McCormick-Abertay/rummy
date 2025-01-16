
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

[Tool]
[GlobalClass]
public partial class RandomPlayer : ComputerPlayer 
{
    public RandomPlayer() : base("RandomPlayer") { random = new Random(); }
    public RandomPlayer(int seed) : base($"RandomPlayer<{seed}>") { random = new Random(seed); }

    private readonly Random random;

    [Export] private double TakeMeldChance = 1.0;
    [Export] private double TakeLayOffChance = 1.0;
    [Export] private double TakeMultipleChance = 1.0;
    [Export] private double TakeMultipleChanceLossPerGainedCard = 0.0;

    public override Task TakeTurn() {
        var (potentialMelds, nearMelds) = FindPotentialMelds();

        HashSet<Card> usableDrawDownToCardsMeld = new(), usableDrawDownToCardsLayoff = new();
        Round.DiscardPile.Cards.ForEach(card => {
            var allCardsToBeTaken = Round.DiscardPile.Cards.Take(Round.DiscardPile.Cards.FindIndex(card) + 1);
            var potentialMeldsWith =
                PotentialMoves.FindMelds(Hand.Cards.Concat(allCardsToBeTaken)).Melds
                .Where(meld => meld.Cards.Contains(card));
            if (potentialMeldsWith.Any()) { usableDrawDownToCardsMeld.Add(card); }

            var potentialCardLayoffs = FindPotentialLayOffs(card);
            if (potentialCardLayoffs.Any()) {
                if (
                    Melds.Count > 0 || potentialMelds.Any() ||
                    PotentialMoves.FindMelds(Hand.Cards.Concat(allCardsToBeTaken.SkipLast(1))).Melds.Any()
                ) { usableDrawDownToCardsLayoff.Add(card); }
            }
        });

        var usableDrawDownToCardsAll = usableDrawDownToCardsMeld.Concat(usableDrawDownToCardsLayoff);

        if (usableDrawDownToCardsAll.Any()) { Think($"Possible cards to draw down to: {usableDrawDownToCardsAll.Select(card => $"[{Round.DiscardPile.Cards.TakeWhile(x => !x.Equals(card)).ToJoinedString(", ")}]({card})").ToJoinedString(", ")}"); }

        List<Card> drawnCards = new();
        Option<Card> topFromDiscardPile = None, bottomFromDiscardPile = None;

        bool mustMeldThisTurn = false;
        if (usableDrawDownToCardsAll.Any()) {
            int drawDownSelection = random.Next(usableDrawDownToCardsAll.Count());
            var card = usableDrawDownToCardsAll.ElementAt(drawDownSelection);

            var indexInDiscardPile = Round.DiscardPile.Cards.FindIndex(card);
            if (random.NextDouble() < Math.Max(TakeMultipleChance - TakeMultipleChanceLossPerGainedCard * indexInDiscardPile, 0d)) {
                var cards = Round.DiscardPile.Draw(indexInDiscardPile + 1);
                drawnCards.AddRange(cards);
                topFromDiscardPile = cards.First();
                bottomFromDiscardPile = cards.Last();
                bool forMeld = usableDrawDownToCardsMeld.Contains(card), forLayoff = usableDrawDownToCardsLayoff.Contains(card);
                mustMeldThisTurn = (forMeld && !forLayoff) || (forLayoff && !Melds.Any());
                Say($"Drew {cards.ToJoinedString(", ")} from discard pile. ({bottomFromDiscardPile.Value})");
            }
        }

        if (drawnCards.Count == 0) {
            // Draw from deck
            int drawSelection = random.Next(2);
            if (drawSelection == 0) {
                var card = Round.Deck.Draw().Inspect(card => drawnCards.Add(card));
                Say("Drew from deck.");
                Think($"Drew {card.Value} from deck.");
            }
            // Draw from discard
            if (drawSelection == 1 || drawnCards.Count == 0) {
                topFromDiscardPile = Round.DiscardPile.Draw().Inspect(card => drawnCards.Add(card));
                Say($"Drew {topFromDiscardPile.Value} from discard pile.");
            }
        }

        drawnCards.ForEach(card => Hand.Add(card));

        Think($"Hand: {string.Join(", ", Hand.Cards)}");

        // Update potential melds with respect to the card you just drew
        (potentialMelds, nearMelds) = FindPotentialMelds();

        var potentialLayOffs = FindPotentialLayOffs();

        if (potentialMelds.Any()) { Think($"Potential Melds: {string.Join(", ", potentialMelds)}"); }
        if (nearMelds.Any()) { Think($"Near Melds: {string.Join(", ", nearMelds.Select(x => string.Join(", ", x)))}"); }
        if (potentialLayOffs.Any()) { Think($"Potential Layoffs: {(Melds.Count == 0 ? "(cannot lay off)" : "")} {string.Join(", ", potentialLayOffs.Select(kvp => $"{kvp.Key} -> {(kvp.Value.Count > 1 ? "{" : "")}{string.Join(", ", kvp.Value)}{(kvp.Value.Count > 1 ? "}" : "")}"))}");  }

        var validPotentialMelds = bottomFromDiscardPile
            .AndThen(bottomCard =>
                usableDrawDownToCardsLayoff.Contains(bottomCard) ? None :
                    Some(potentialMelds.Where(meld => meld.Cards.Contains(bottomCard))))
            .Or(potentialMelds);
        
        if (bottomFromDiscardPile.IsSome) { Think($"Valid Melds: {string.Join(", ", validPotentialMelds)}"); }

        List<List<Meld>> rummyConfigurations = new();
        if (!Melds.Any()) {
            Dictionary<Meld, HashSet<Meld>> meldConfigurations = new();
            foreach (var meld in validPotentialMelds) {
                meldConfigurations.Add(meld, new());
                foreach (var otherMeld in validPotentialMelds) {
                    if (!ReferenceEquals(meld, otherMeld)) { meldConfigurations[meld].Add(otherMeld); }
                }
            }
            foreach (var (meld, others) in meldConfigurations) {
                var exclusiveMelds = others.Append(meld);
                var cardsTemp = Hand.Cards.DeepClone().ToList();
                exclusiveMelds.ForEach(meld => meld.Cards.ForEach(card => cardsTemp.Remove(card)));
                foreach (var (card, _) in potentialLayOffs) { cardsTemp.Remove(card); }
                if (cardsTemp.Count <= 1) {
                    rummyConfigurations.Add(exclusiveMelds.ToList());
                }
            }
        }

        bool isRummying = false;
        if (rummyConfigurations.Any()) {
            isRummying = true;
            var configuration = rummyConfigurations.ElementAt(random.Next(rummyConfigurations.Count));
            configuration.ForEach(meld => {
                Round.Meld(meld).Inspect(_ => {
                    meld.Cards.ForEach(card => Hand.Pop(card));
                    Think($"Melded {meld} (rummying)");
                }).InspectErr(err => Think($"Failed to meld {meld}: {err}"));
            });
            // Update layoffs with respect to the new melds
            potentialLayOffs = FindPotentialLayOffs();
        }
        else if (validPotentialMelds.Any() && (mustMeldThisTurn || random.NextDouble() <= TakeMeldChance)) {
            var meld = validPotentialMelds.ElementAt(random.Next(validPotentialMelds.Count()));
            Round.Meld(meld).Inspect(_ => {
                meld.Cards.ForEach(card => Hand.Pop(card));
                Think($"Melded {meld}");
            }).InspectErr(err => Think($"Failed to meld {meld}: {err}"));
            
            // Update layoffs with respect to the new meld
            potentialLayOffs = FindPotentialLayOffs();
        }

        // Can only lay off after having melded at least once
        if (Melds.Any()) {
            foreach (var (card, list) in potentialLayOffs) {
                if (random.NextDouble() <= TakeLayOffChance || isRummying ||
                        bottomFromDiscardPile.IsSomeAnd(bottomCard => bottomCard.Equals(card))) {
                    var meld = list.Count == 1 ? list.First() : list.ElementAt(random.Next(list.Count));
                    Think($"Laid off {card} to {meld}");
                    Hand.Pop(card).Inspect(card => meld.LayOff(card));
                }
            }
        }

        if (Hand.Cards.Any()) {
            Card cardToDiscard;
            do { cardToDiscard = Hand.Cards.ElementAt(random.Next(Hand.Count));
            } while(!isRummying && topFromDiscardPile.IsSomeAnd(topCard => cardToDiscard.Equals(topCard)));

            Hand.Pop(cardToDiscard).Inspect(card => Round.DiscardPile.Discard(card));

            Say($"Discarding {cardToDiscard}");
        }

        return Task.CompletedTask;
    }
}