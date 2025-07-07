
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;
using Rummy.Util.Nullable;
using System.Text;

namespace Rummy.AI;

[Tool, GlobalClass]
public partial class IntelligentPlayer : ComputerPlayer
{
    public IntelligentPlayer(int? seed) : base($"{nameof(IntelligentPlayer)}{(seed is not null ? $"<{seed}>" : "")}") {
        random = seed is not null ? new Random((int)seed) : new Random();
    }
    public IntelligentPlayer() : this(null) {}

    private readonly Random random;

    [Export] private double TakeMeldChance = 1.0;
    [Export] private double TakeLayOffChance = 1.0;
    [Export] private double TakeSingleFromDiscardChance = 0.3;
    [Export] private double TakeMultipleChance = 1.0;
    [Export] private double TakeMultipleChanceLossPerGainedCard = 0.1;

    public override Task TakeTurn() {
        // Default to drawing from deck
        (IDrawable Pile, int Index) drawSelection = (Round.Deck, 0);

        var usableDrawDownCards = FindUsableDiscardPileCards();
        // If a card in the discard pile would allow for a rummy, take that
        if (usableDrawDownCards.Any(x => x.Info.RummyConfig is not null))
            drawSelection = (Round.DiscardPile, usableDrawDownCards.FirstOrDefault(x => x.Info.RummyConfig is not null).Index);
        else if (usableDrawDownCards.Any()) {
            // For each connected card in the discard pile, we have a chance to select to draw it based on the set percentage chances
            // Chance is different for a single card vs multiple, and multiple chance is lowered for each additional gained card
            foreach (var (card, index, info) in usableDrawDownCards) {
                int cardsTaken = info.CardsTaken.Count();
                double takeChance = cardsTaken == 1 ? TakeSingleFromDiscardChance : TakeMultipleChance - TakeMultipleChanceLossPerGainedCard * cardsTaken;
                if (random.NextDouble() <= takeChance) { drawSelection = (Round.DiscardPile, index); break; }
            }
        }

        // Draw selected card
        var (drawnCards, cannotDiscard, mustUse) = Draw(drawSelection.Pile, drawSelection.Index);

        // If rummying
        if (Melds.Count == 0 && PotentialMoves.FindRummyConfiguration(Hand.Cards, this, Round) is PotentialMoves.RummyConfiguration rummyConfiguration) {
            Say("Rummying!");
            foreach (var meld in rummyConfiguration.Melds) Meld(meld);
            foreach (var layoff in rummyConfiguration.Layoffs) LayOff(layoff.Card, layoff.Meld);
            if (rummyConfiguration.Discard is Card cardToDiscard) Discard(cardToDiscard);
        }
        // If not rummying
        else {
            var (potentialMelds, nearMelds) = FindPotentialMelds(); var potentialLayOffs = FindPotentialLayOffs();
            // If can meld
            if (potentialMelds.Any()) {
                bool mustMeld = mustUse is not null && (!potentialLayOffs.Any(x => x.Key == mustUse) || Melds.None());

                // Should we start melding if able
                bool wantToMeld = random.Roll(TakeMeldChance);

                if (wantToMeld || mustMeld) {
                    // Valid melds to select from if not rummying (all potential melds, constrained to those containing the bottomost picked up card if you picked up multiple)
                    var validMelds = mustUse is Card mustUseCard && !potentialLayOffs.Any(x => x.Key == mustUse) ?
                        potentialMelds.Where(x => x.Cards.Contains(mustUseCard)) : potentialMelds;

                    // Select meld randomly
                    Meld(random.From(validMelds));
                }
            }

            // If can lay off
            if (Melds.Any()) {
                foreach (var (card, melds) in potentialLayOffs) {
                    // Decide on whether to take layoff (selecting meld randomly from those possible)
                    if (random.Roll(TakeLayOffChance)) LayOff(card, random.From(melds));
                }
            }

            // Discard a card if able
            if (Hand.Cards.Any()) {
                // The cannot discard rule does not apply to the final card in your hand
                var validCardsToDiscard = Hand.Cards.Count > 1 ? Hand.Cards.Where(x => x != cannotDiscard) : Hand.Cards;
                // Select discard randomly
                Discard(random.From(validCardsToDiscard));
            }
        }

        return Task.CompletedTask;
    }
}