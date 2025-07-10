
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rummy.Game;
using Rummy.Util;

namespace Rummy.AI;

[Tool, GlobalClass]
public partial class IntelligentPlayer : ComputerPlayer
{
    public IntelligentPlayer() : base(nameof(IntelligentPlayer)) {}

    [Export] int DrawSingleUtilityThreshold = 5;
    [Export] int DrawMultipleUtilityThreshold = 15;
    [Export] int LayoffUtilityThreshold = 1;
    [Export] int FirstMeldUtilityThreshold = 15;
    [Export] int FurtherMeldUtilityThreshold = 0;

    private double GetDrawUtilityThreshold(int count = 1) => count switch { 1 => DrawSingleUtilityThreshold, _ => DrawMultipleUtilityThreshold };
    private double GetLayoffUtilityThreshold() => LayoffUtilityThreshold;
    private double GetMeldUtilityThreshold(bool isFirst) => isFirst ? FirstMeldUtilityThreshold : FurtherMeldUtilityThreshold;

    private double EvaluateDrawUtility(Card card, IEnumerable<Card> cardsTaken, IEnumerable<Meld> meldsWith, IEnumerable<Meld> layoffs) {
        // TK - factor in melds within the leftover cards
        double LeftOverPenalty(Func<Card, bool> cardPred) => Hand.Cards.Concat(cardsTaken).Where(cardPred).Count() * 5;

        double utility = 0;

        var meldUtilities = meldsWith.Select(meld => EvaluateMeldUtility(meld) - LeftOverPenalty(card => !meld.Cards.Contains(card)));
        if (meldUtilities.Any()) utility += meldUtilities.Average();

        // TK - factor in further layoffs to a run
        var posLayoffUtilities = layoffs.Select(meld => EvaluateLayoffUtility(card, meld) - LeftOverPenalty(x => x != card)).Where(x => x > 0);
        if (posLayoffUtilities.Any()) utility += posLayoffUtilities.Average();
        
        // TK - factor in near melds

        return 0;
    }

    private double EvaluateLayoffUtility(Card card, Meld meld, IEnumerable<Meld> potentialMelds = null, List<NearMeld> nearMelds = null) {
        double utility = 5;
        if (potentialMelds is null || nearMelds is null) (potentialMelds, nearMelds) = FindPotentialMelds();

        utility -= potentialMelds.Where(x => x.Cards.Contains(card)).Select(EvaluateMeldUtility).Sum();

        return utility;
    }

    private double EvaluateMeldUtility(Meld meld) {
        double utility = 0;

        utility += meld.Count * 5;
        utility += meld.Cards.Select(x => x.Score).Sum();

        return utility;
    }

    private double EvaluateDiscardUtility(Card card, IEnumerable<Meld> melds, List<NearMeld> nearMelds) {
        double utility = 0;
        utility -= card.Score;
        utility -= melds.Where(x => x.Cards.Contains(card)).Select(EvaluateMeldUtility).Sum();

        // TK - factor in near melds
        //nearMelds.Where(x => x.);
        return utility;
    }

    public override Task TakeTurn() {
        // Default to drawing from deck
        (IDrawable Pile, int Index) drawSelection = (Round.Deck, 0);

        var usableDrawDownCards = FindUsableDiscardPileCards();
        // If a card in the discard pile would allow for a rummy, take that
        if (usableDrawDownCards.Any(x => x.Info.RummyConfig is not null))
            drawSelection = (Round.DiscardPile, usableDrawDownCards.FirstOrDefault(x => x.Info.RummyConfig is not null).Index);
        else if (usableDrawDownCards.Any()) {
            // Evaluate utility of each usable discard pile card
            var drawUtilities = usableDrawDownCards.Select(x => new { CardInfo = x, Utility = EvaluateDrawUtility(x.Card, x.Info.CardsTaken, x.Info.Melds, x.Info.Layoffs) });

            var singleDraws = drawUtilities.Where(x => x.CardInfo.Info.CardsTaken.Count() == 1);
            var multiDraws = drawUtilities.Where(x => x.CardInfo.Info.CardsTaken.Count() > 1).OrderBy(x => x.Utility);

            // Select highest utility card if its utility exceeds threshold
            if (singleDraws.Any(x => x.Utility >= GetDrawUtilityThreshold()))
                drawSelection = (Round.DiscardPile, singleDraws.Single().CardInfo.Index);
            else if (multiDraws.Any(x => x.Utility >= GetDrawUtilityThreshold(x.CardInfo.Info.CardsTaken.Count())))
                drawSelection = (Round.DiscardPile, multiDraws.Last(x => x.Utility >= GetDrawUtilityThreshold(x.CardInfo.Info.CardsTaken.Count())).CardInfo.Index);
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
                
                // Valid melds to select from if not rummying (all potential melds, constrained to those containing the bottomost picked up card if you picked up multiple)
                var validMelds = mustUse is Card mustUseCard && !potentialLayOffs.Any(x => x.Key == mustUse) ?
                    potentialMelds.Where(x => x.Cards.Contains(mustUseCard)) : potentialMelds;

                if (validMelds.Any()) {
                    // Select highest utility meld
                    var selectedMeld = validMelds.Select(x => new { Meld = x, Utility = EvaluateMeldUtility(x) }).OrderBy(x => x.Utility).Last();

                    if (mustMeld || selectedMeld.Utility >= GetMeldUtilityThreshold(Melds.Count == 0))
                        Meld(selectedMeld.Meld);
                }
            }

            // If can lay off
            if (Melds.Any()) {
                // Update potential melds based on cards left in hand
                (potentialMelds, nearMelds) = FindPotentialMelds();

                foreach (var (card, melds) in potentialLayOffs) {
                    // Find highest utility place card can be laid off
                    var highestUtilityLayoff = melds.Select(meld => new { Meld = meld, Utility = EvaluateLayoffUtility(card, meld, potentialMelds, nearMelds) }).OrderBy(x => x.Utility).Last();
                    // Only lay off if its greater than the threshold
                    if (highestUtilityLayoff.Utility >= GetLayoffUtilityThreshold()) LayOff(card, highestUtilityLayoff.Meld);
                }
            }

            // Discard a card if able
            if (Hand.Cards.Any()) {
                // Update potential melds based on cards left in hand
                (potentialMelds, nearMelds) = FindPotentialMelds();

                // The cannot discard rule does not apply to the final card in your hand
                var validCardsToDiscard = Hand.Cards.Count > 1 ? Hand.Cards.Where(x => x != cannotDiscard) : Hand.Cards;
                // Select highest utility discard
                var selectedCard = validCardsToDiscard.Select(x => new { Card = x, Utility = EvaluateDiscardUtility(x, potentialMelds, nearMelds) }).OrderBy(x => x.Utility).Last();
                Discard(selectedCard.Card);
            }
        }

        return Task.CompletedTask;
    }
}