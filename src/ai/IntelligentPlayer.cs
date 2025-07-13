
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rummy.Config;
using Rummy.Game;
using Rummy.Util;

namespace Rummy.AI;

[Tool, GlobalClass]
public partial class IntelligentPlayer : ComputerPlayer {
    public IntelligentPlayer() : base(nameof(IntelligentPlayer)) { }

    private List<Meld> _potentialMelds;
    private List<NearMeld> _nearMelds;
    private Dictionary<Card, List<Meld>> _potentialLayoffs;

    private Player GetPrecedingPlayer() {
        if (Round.Players.IndexOf(this) is int selfIndex && selfIndex == -1) return null;
        int precedingIndex = selfIndex - 1;
        if (precedingIndex < 0) precedingIndex += Round.Players.Count;
        return Round.Players[precedingIndex];
    }

    private readonly Dictionary<Player, List<Card>> _knownPlayerCards = [];
    private IEnumerable<Card> KnownCardsInOtherPlayersHands => _knownPlayerCards.Where(x => x.Key != this).SelectMany(x => x.Value);
    private IEnumerable<Card> KnownCardsInPrecedingPlayersHand => _knownPlayerCards.GetValueOrDefault(GetPrecedingPlayer()) ?? [];

    protected override void OnAddedToRound(Round round) {
        _knownPlayerCards.Clear();
        foreach (var player in round.Players) _knownPlayerCards[player] = [];
        round.NotifyDrewFromDiscardPile += OnPlayerDrew; round.NotifyMelded += OnPlayerMelded; round.NotifyLaidOff += OnPlayerLaidOff; round.NotifyDiscarded += OnPlayerDiscarded;
    }
    protected override void OnRemovedFromRound(Round round) {
        round.NotifyDrewFromDiscardPile -= OnPlayerDrew; round.NotifyMelded -= OnPlayerMelded; round.NotifyLaidOff -= OnPlayerLaidOff; round.NotifyDiscarded -= OnPlayerDiscarded;
        _knownPlayerCards.Clear();
    }

    private void OnPlayerDrew(Player player, ReadOnlyCollection<Card> cards) => _knownPlayerCards[player].AddRange(cards);
    private void OnPlayerMelded(Player player, ReadOnlyCollection<Card> cards) => _knownPlayerCards[player].RemoveAll(cards.Contains);
    private void OnPlayerLaidOff(Player player, Card card) => _knownPlayerCards[player].Remove(card);
    private void OnPlayerDiscarded(Player player, Card card) => _knownPlayerCards[player].Remove(card);

    [ExportGroup("Card Accessibility")]
    [Export, ExportDescription(type: "PercentageFloat", tooltip: "Accessibility loss when a card is buried in the discard pile. This value is multiplied by its depth.")]
    double InDiscardPileCardAccessibilityLossPerDepth = 0.25;

    [Export, ExportDescription(type: "PercentageFloat", tooltip: "When a card is known to be in the hand of a player more than one index behind you.")]
    double InNonPrecedingPlayerHandCardAccessibility = 0;

    [Export, ExportDescription(type: "PercentageFloat", tooltip: "When a card is known to be in preceding player's hand, and form a valid meld there.")]
    double FullyConnectedInPrevPlayerHandCardAccessibility = 0;

    [Export, ExportDescription(type: "PercentageFloat", tooltip: "When a card is known to be in preceding player's hand, and is connected to other known cards there.")]
    double PartiallyConnectedInPrevPlayerHandCardAccessibility = 0.2;

    [Export, ExportDescription(type: "PercentageFloat", tooltip: "When a card is known to be in preceding player's hand with no connections.")]
    double UnconnectedInPrevPlayerHandCardAccessibility = 0.7;

    [Export, ExportDescription(type: "PercentageFloat", tooltip: "When the location of a card is wholly unknown.")]
    double UnknownCardAccessibility = 0.5;

    // From 0 (impossible to get) to 1 (currently in hand)
    private double GetCardAccessibility(Card card) => 0 switch {
        _ when Hand.Cards.Contains(card) => 1,
        // Is already in a meld (wholly inaccessible)
        _ when Round.Melds.Any(x => x.Cards.Contains(card)) => 0,
        // Card is in discard pile
        _ when Round.DiscardPile.Cards.Contains(card) => 1 - Round.DiscardPile.Cards.IndexOf(card) * InDiscardPileCardAccessibilityLossPerDepth,
        // Card is in preceding player's hand
        _ when KnownCardsInPrecedingPlayersHand.Contains(card) => 0 switch {
            // But could be laid off
            _ when PotentialMoves.FindLayOffs(card, Round).Count > 0 => 0,
            // But is connected to something else we know is in their hand
            _ when PotentialMoves.FindMelds(KnownCardsInPrecedingPlayersHand) is var (melds, nearMelds)
                && melds.Any(x => x.Cards.Contains(card)) is bool inMeld
                && nearMelds.Any(x => x.Cards.Contains(card)) is bool inNearMeld
                && (inMeld || inNearMeld) => 0 switch {
                    _ when inNearMeld => PartiallyConnectedInPrevPlayerHandCardAccessibility,
                    _ => FullyConnectedInPrevPlayerHandCardAccessibility
                },
            // Is relatively likely to be discarded
            _ => UnconnectedInPrevPlayerHandCardAccessibility
        },
        // Is in a different player's hand
        _ when KnownCardsInOtherPlayersHands.Contains(card) => InNonPrecedingPlayerHandCardAccessibility,
        // Is either in deck or unknown in someone else's hand
        _ => UnknownCardAccessibility
    };

    [ExportGroup("Utility Thresholds")]
    [Export] double DrawSingleUtilityThreshold = 5;
    [Export] double DrawMultipleUtilityThreshold = 15;
    [Export] double LayoffUtilityThreshold = 1;
    [Export] double FirstMeldUtilityThreshold = 30;
    [Export] double FurtherMeldUtilityThreshold = 0;

    private double GetDrawUtilityThreshold(int count = 1) => count switch { 1 => DrawSingleUtilityThreshold, _ => DrawMultipleUtilityThreshold };
    private double GetLayoffUtilityThreshold() => LayoffUtilityThreshold;
    private double GetMeldUtilityThreshold(bool isFirst) => isFirst ? FirstMeldUtilityThreshold : FurtherMeldUtilityThreshold;

    private double EvaluateDrawUtility(Card card, IEnumerable<Card> cardsTaken, IEnumerable<Meld> meldsWith, IEnumerable<Meld> layoffs) {
        // TK - factor in melds within the leftover cards
        double LeftOverPenalty(Func<Card, bool> cardPred) => Hand.Cards.Concat(cardsTaken).Where(cardPred).Count() * 5;

        double utility = 0;

        var meldUtilities = meldsWith.Select(meld => EvaluateMeldUtility(meld) - LeftOverPenalty(card => !meld.Cards.Contains(card)));
        if (meldUtilities.Any()) {
            double meldAverage = meldUtilities.Average();
            //Think($"Meld average {meldAverage}");
            if (meldAverage > 0) utility += meldUtilities.Average() * 5;
        }

        // TK - factor in further layoffs to a run
        var posLayoffUtilities = layoffs.Select(meld => EvaluateLayoffUtility(card, meld) - LeftOverPenalty(x => x != card)).Where(x => x > 0);
        if (posLayoffUtilities.Any()) utility += posLayoffUtilities.Average();

        var nearMeldUtilities =
            _nearMelds.Where(x => x.PotentialCards().Contains(card))
                .Select(x => x.With(cardsTaken.Where(y => x.PotentialCards().Contains(y))))
                .Select(EvaluateNearMeldUtility);
        if (nearMeldUtilities.Any()) {
            double nearMeldAverage = nearMeldUtilities.Average();
            //Think($"Near meld average {nearMeldAverage}");
            if (nearMeldAverage > 0) utility += nearMeldAverage;
        }

        return utility;
    }

    private double EvaluateLayoffUtility(Card card, Meld meld) {
        if (_potentialMelds is null || _nearMelds is null) (_potentialMelds, _nearMelds) = FindPotentialMelds();
        double utility = 5;

        utility -= _potentialMelds.Where(x => x.Cards.Contains(card)).Select(EvaluateMeldUtility).Sum();

        return utility;
    }

    private double EvaluateMeldUtility(Meld meld) {
        double utility = 0;

        utility += meld.Count * 5;
        utility += meld.Cards.Select(x => x.Score).Sum(); // It is good to prioritise getting high scoring cards out of your hand

        return utility;
    }

    private double EvaluateNearMeldUtility(NearMeld meld) {
        if (meld.ContainsValidMeld() && meld.AsMeld().Valid) return EvaluateMeldUtility(meld.AsMeld());

        double utility = 0;
        utility += meld.Cards.Count * 5;
        utility += meld.PotentialCards().Select(x => 2 * GetCardAccessibility(x)).Sum();
        utility -= meld.Cards.Select(x => x.Score).Sum() * 0.5; // It is good to prioritise not holding on to high scoring cards

        return utility;
    }

    private double EvaluateDiscardUtility(Card card) {
        if (_potentialMelds is null || _nearMelds is null) (_potentialMelds, _nearMelds) = FindPotentialMelds();
        double utility = 0;
        utility += card.Score * 0.2;
        if (_potentialLayoffs.ContainsKey(card)) utility -= _potentialLayoffs[card].Count() * 5;
        utility -= _potentialMelds.Where(x => x.Cards.Contains(card)).Select(EvaluateMeldUtility).Sum();
        utility -= _nearMelds.Where(x => x.Cards.Contains(card)).Select(EvaluateNearMeldUtility).Sum();
        return utility;
    }

    public override Task TakeTurn() {
        (_potentialMelds, _nearMelds) = FindPotentialMelds();
        _potentialLayoffs = null;

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

            Think($"Draw utilities:\n{drawUtilities.OrderBy(x => x.Utility).Select(x => $" - {x.CardInfo.Card}({x.CardInfo.Info.CardsTaken.ToJoinedString(Delimiter.ZeroWidth)}): {x.Utility}").ToJoinedString(Delimiter.LineBreak)}");

            // Select highest utility card if its utility exceeds threshold
            if (singleDraws.Any(x => x.Utility >= GetDrawUtilityThreshold()))
                drawSelection = (Round.DiscardPile, singleDraws.Single().CardInfo.Index);
            else if (multiDraws.Any(x => x.Utility >= GetDrawUtilityThreshold(x.CardInfo.Info.CardsTaken.Count())))
                drawSelection = (Round.DiscardPile, multiDraws.Last(x => x.Utility >= GetDrawUtilityThreshold(x.CardInfo.Info.CardsTaken.Count())).CardInfo.Index);
        }

        // Draw selected card
        var (drawnCards, cannotDiscard, mustUse) = Draw(drawSelection.Pile, drawSelection.Index);

        var drawnCardInfo = usableDrawDownCards.FirstOrDefault(x => x.Card == drawnCards.Last()).Info;

        // If rummying
        if (Melds.Count == 0 && drawnCardInfo.RummyConfig is not null) {
            Say("Rummying!");
            foreach (var meld in drawnCardInfo.RummyConfig.Melds) Meld(meld);
            foreach (var layoff in drawnCardInfo.RummyConfig.Layoffs) LayOff(layoff.Card, layoff.Meld);
            if (drawnCardInfo.RummyConfig.Discard is Card cardToDiscard) Discard(cardToDiscard);
        }
        // If not rummying
        else {
            (_potentialMelds, _nearMelds) = FindPotentialMelds(); _potentialLayoffs = FindPotentialLayOffs();
            // If can meld
            if (_potentialMelds.Any()) {
                bool mustMeld = mustUse is not null && (!_potentialLayoffs.Any(x => x.Key == mustUse) || Melds.None());

                // Valid melds to select from if not rummying (all potential melds, constrained to those containing the bottomost picked up card if you picked up multiple)
                var validMelds = mustUse is Card mustUseCard && !_potentialLayoffs.Any(x => x.Key == mustUse) ?
                    _potentialMelds.Where(x => x.Cards.Contains(mustUseCard)) : _potentialMelds;

                if (validMelds.Any()) {
                    double meldUtilityThreshold = GetMeldUtilityThreshold(Melds.Count == 0);
                    // Select highest utility meld
                    var meldUtilities = validMelds.Select(x => new { Meld = x, Utility = EvaluateMeldUtility(x) });
                    var selectedMeld = meldUtilities.OrderBy(x => x.Utility).Last();

                    Think($"Meld utilities (threshold = {meldUtilityThreshold}):\n{meldUtilities.OrderBy(x => x.Utility).Select(x => $" - {x.Meld}: {x.Utility}").ToJoinedString(Delimiter.LineBreak)}");

                    if (mustMeld || selectedMeld.Utility >= meldUtilityThreshold)
                        Meld(selectedMeld.Meld);
                }
            }

            // If can lay off
            if (Melds.Any()) {
                // Update potential melds and layoffs based on cards left in hand
                (_potentialMelds, _nearMelds) = FindPotentialMelds();
                _potentialLayoffs = FindPotentialLayOffs();

                double layoffUtilityThreshold = GetLayoffUtilityThreshold();
                foreach (var (card, melds) in _potentialLayoffs) {
                    // Find highest utility place card can be laid off
                    var layoffUtilities = melds.Select(meld => new { Meld = meld, Utility = EvaluateLayoffUtility(card, meld) });
                    var highestUtilityLayoff = layoffUtilities.OrderBy(x => x.Utility).Last();

                    Think($"Layoff utilities for ${card} (threshold = {layoffUtilityThreshold}):\n{layoffUtilities.Select(x => $" - {x.Meld}: {x.Utility}").ToJoinedString(Delimiter.LineBreak)}");

                    // Only lay off if its greater than the threshold
                    if (highestUtilityLayoff.Utility >= layoffUtilityThreshold) LayOff(card, highestUtilityLayoff.Meld);
                }
            }

            // Discard a card if able
            if (Hand.Cards.Any()) {
                // Update potential melds and layoffs based on cards left in hand
                (_potentialMelds, _nearMelds) = FindPotentialMelds();
                _potentialLayoffs = FindPotentialLayOffs();

                // The cannot discard rule does not apply to the final card in your hand
                var validCardsToDiscard = Hand.Cards.Count > 1 ? Hand.Cards.Where(x => x != cannotDiscard) : Hand.Cards;
                // Select highest utility discard
                var discardUtilities = validCardsToDiscard.Select(x => new { Card = x, Utility = EvaluateDiscardUtility(x) });
                Think($"Discard utilities:\n{discardUtilities.OrderBy(x => x.Utility).Select(x => $" - {x.Card}: {x.Utility}").ToJoinedString(Delimiter.LineBreak)}");

                var selectedCard = discardUtilities.OrderBy(x => x.Utility).Last();
                Discard(selectedCard.Card);
            }
        }

        return Task.CompletedTask;
    }
}