
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;
using static Rummy.AI.DecisionTree;
using Rummy.Util.Nullable;
using System.Text;

namespace Rummy.AI;

[Tool]
[GlobalClass]
public partial class RandomPlayer : ComputerPlayer
{
    public RandomPlayer(int? seed) : base($"{nameof(RandomPlayer)}{(seed is not null ? $"<{seed}>" : "")}") {
        random = seed is not null ? new Random((int)seed) : new Random();
    }
    public RandomPlayer() : this(null) {}

    private readonly Random random;

    [Export] private double TakeMeldChance = 1.0;
    [Export] private double TakeLayOffChance = 1.0;
    [Export] private double TakeMultipleChance = 1.0;
    [Export] private double TakeMultipleChanceLossPerGainedCard = 0.0;

    public override Task TakeTurn() {
        // Find usable cards in the discard pile
        Dictionary<(Card Card, int Index),
            (IEnumerable<Meld> Melds, IEnumerable<Meld> Layoffs, IEnumerable<(List<Meld> Melds, List<(Card, Meld)> Layoffs, Card? Discard)> RummyConfigs, IEnumerable<Card> CardsTaken)>
            usableDrawDownCards = [];
        foreach (var (index, card) in Round.DiscardPile.Cards.Index()) {
            var cardsTaken = Round.DiscardPile.Cards.Take(index + 1);
            var potentialMeldsWith = PotentialMoves.FindMelds(Hand.Cards.Concat(cardsTaken)).Melds.Where(meld => meld.Cards.Contains(card));
            var potentialLayoffs = Melds.Any() || PotentialMoves.FindMelds(Hand.Cards.Concat(cardsTaken.SkipLast())).Melds.Any() ? FindPotentialLayOffs(card) : [];

            var rummyConfigsWith = PotentialMoves.FindRummyConfigurations(Hand.Cards.Concat(cardsTaken), this, Round);

            // TK - May need to add logic for cards which fit with a partial meld, but don't complete it
            if (potentialMeldsWith.Any() || potentialLayoffs.Any() || rummyConfigsWith.Any()) usableDrawDownCards[(card, index)] = (potentialMeldsWith, potentialLayoffs, rummyConfigsWith, cardsTaken);
        }

        if (usableDrawDownCards.Any()) Think("Possible cards to draw down to: ".ToBuilder().AppendJoin(", ",
            usableDrawDownCards.Select((index, info) => "".ToBuilder().AppendJoinWrapped(info.CardsTaken.Count() > 1 ? "[]" : "", ", ",
                info.CardsTaken.SkipLast().AsStrings().Concat(info.CardsTaken.TakeLast().Select(x => $"({x})"))))));

        // Default to drawing from deck
        (IDrawable Pile, int Index) drawSelection = (Round.Deck, 0);

        // If a card in the discard pile would allow for a rummy, take that
        if (usableDrawDownCards.FirstOrDefault(x => x.Value.RummyConfigs.Any()) is var drawDownCardForRummy)
            drawSelection = (Round.DiscardPile, drawDownCardForRummy.Key.Index);
        else if (usableDrawDownCards.Any()) {
            foreach (var ((card, index), info) in usableDrawDownCards) {
                if (random.NextDouble() <= TakeMultipleChance - TakeMultipleChanceLossPerGainedCard * info.CardsTaken.Count()) {
                    drawSelection = (Round.DiscardPile, index); break;
                }
            }
        }

        var drawnCards = drawSelection.Pile.Draw(drawSelection.Index + 1);
        drawnCards.ForEach(Hand.Add);

        Card? cannotDiscard = drawSelection.Pile == Round.DiscardPile ? drawnCards.First() : null;
        Card? mustUse = drawSelection.Pile == Round.DiscardPile && drawnCards.Count > 1 ? drawnCards.Last() : null;

        if (drawSelection.Pile == Round.DiscardPile)
            Say("Drew ".ToBuilder().AppendJoin(", ", drawnCards).Append(" from discard pile.")
                .AppendIf(mustUse is not null, $" Must use {mustUse}.")
                .AppendIf(cannotDiscard is not null, $" Cannot discard {cannotDiscard}."));
        else SayAndThink("Drew from deck.", $"Drew {drawnCards.SingleOrDefault()} from deck.");

        Think("Hand: ".ToBuilder().AppendJoin(", ", Hand.Cards));

        // Update potential melds and layoffs with respect to the card you just drew
        var (potentialMelds, nearMelds) = FindPotentialMelds();
        var potentialLayOffs = FindPotentialLayOffs();

        if (potentialMelds.Count > 0)   Think("Potential Melds: ".ToBuilder().AppendJoin(", ", potentialMelds));
        if (nearMelds.Count > 0)        Think("Near Melds: ".ToBuilder().AppendJoin(", ", nearMelds));

        if (potentialLayOffs.Count > 0) Think("Potential Layoffs".ToBuilder().AppendIf(Melds.None(), " (cannot lay off)").Append(": ")
                                        .AppendJoin(", ", potentialLayOffs.Select((card, melds) =>
                                            $"{card} -> ".ToBuilder().AppendIf(melds.Count > 1, '{').AppendJoin(", ", melds).AppendIf(melds.Count > 1, '}'))));

        var rummyConfigurations = PotentialMoves.FindRummyConfigurations(Hand.Cards, this, Round, potentialMelds, potentialLayOffs);

        // If rummying
        if (Melds.Count == 0 && rummyConfigurations.Any()) {
            var config = rummyConfigurations.First();

            foreach (var meld in config.Melds) Meld(meld);
            foreach (var layoff in config.Layoffs) LayOff(layoff.Card, layoff.Meld);
            if (config.Discard is Card cardToDiscard) Discard(cardToDiscard);

            Say($"Rummying! ".ToBuilder()
                .AppendIf(config.Melds.Any(), $"Melding: [{config.Melds.ToJoinedString(", ")}]")
                .AppendIf(config.Melds.Any() && config.Layoffs.Any(), ", ")
                .AppendIf(config.Layoffs.Any(), $"Laying off: [{config.Layoffs.Select(x => $"{x.Card} to {x.Meld}").ToJoinedString(", ")}]")
                .AppendIf((config.Melds.Any() || config.Layoffs.Any()) && config.Discard is not null, ", ")
                .AppendIf(config.Discard is not null, $"Discarding {config.Discard}")
            );
        }
        // If not rummying
        else {
            // If can meld
            if (potentialMelds.Any()) {
                bool mustMeld = mustUse is not null && (!potentialLayOffs.Any(x => x.Key == mustUse) || Melds.None());

                // Should we start melding if able
                bool wantToMeld = random.NextDouble() <= TakeMeldChance;

                if (wantToMeld || mustMeld) {
                    // Valid melds to select from if not rummying (all potential melds, constrained to those containing the bottomost picked up card if you picked up multiple)
                    var validMelds = mustUse is Card mustUseCard && !potentialLayOffs.Any(x => x.Key == mustUse) ?
                        potentialMelds.Where(x => x.Cards.Contains(mustUseCard)) : potentialMelds;

                    // Select meld randomly
                    Meld(validMelds.ElementAt(random.Next(validMelds.Count())));
                }
            }

            // If can lay off
            if (Melds.Any()) {
                foreach (var (card, melds) in potentialLayOffs) {
                    // Decide on whether to take layoff
                    if (random.NextDouble() <= TakeLayOffChance) {
                        // Select meld randomly
                        LayOff(card, melds.ElementAt(random.Next(melds.Count)));
                    }
                }
            }

            // Discard a card if able
            if (Hand.Cards.Any()) {
                // The cannot discard rule does not apply to the final card in your hand
                var validCardsToDiscard = Hand.Cards.Count > 1 ? Hand.Cards.Where(x => x != cannotDiscard) : Hand.Cards;
                // Select discard randomly
                Discard(validCardsToDiscard.ElementAt(random.Next(validCardsToDiscard.Count())));
            }
        }

        return Task.CompletedTask;
    }
}