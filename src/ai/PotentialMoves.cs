
using System;
using System.Collections.Generic;
using System.Linq;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

static class PotentialMoves
{
    public static (List<Meld> Melds, List<NearMeld> NearMelds) FindMelds(IEnumerable<Card> hand) {
        HashSet<Meld> melds = []; HashSet<NearMeld> nearMelds = [];
        foreach (var card in hand) {
            // Sets
            var sameRank = hand.Where(card.MatchesRank).ToList();
            if (sameRank.Count == 2) nearMelds.Add(new NearSet(sameRank));
            while (sameRank.Count >= 3) {
                melds.Add(new Set(sameRank.Count > 4 ? sameRank.SkipLast(sameRank.Count - 4) : sameRank));
                sameRank = [.. sameRank.Skip(Math.Min(sameRank.Count, 4))];
            }

            // Runs
            var sameSuit = hand.Where(card.MatchesSuit).ToList();
            HashSet<Card> potentialRun = [];

            foreach (var method in List.Of(RankImpl.IsAdjacentBelow, RankImpl.IsAdjacentAbove)) {
                Card? tempCard = card;
                while (tempCard is Card) {
                    potentialRun.Add(tempCard.Value);
                    var adjacent = sameSuit.Where(x => method(x.Rank, tempCard.Value.Rank));
                    tempCard = adjacent.Any() ? adjacent.First() : null;
                }
            }

            if (potentialRun.Count >= 3) melds.Add(new Run(potentialRun));
            else if (potentialRun.Count == 2) nearMelds.Add(new NearRun(potentialRun));

            if (sameSuit.Count > 1) {
                int rankDiff = sameSuit.Last().Rank - sameSuit.First().Rank + 1;
                if (rankDiff >= 3) nearMelds.Add(new NearRun(sameSuit));
            }
        }
        return ([.. melds], [.. nearMelds]);
    }

    public static Dictionary<Card, List<Meld>> FindLayOffs(IEnumerable<Card> hand, Round round) {
        Dictionary<Card, List<Meld>> potentialLayOffs = [];
        foreach (var player in round.Players) foreach (var meld in player.Melds)
                foreach (var card in hand) if (meld.CouldLayOff(card)) potentialLayOffs.GetOrCreate(card).Add(meld);
        return potentialLayOffs;
    }
    public static List<Meld> FindLayOffs(Card card, Round round) => FindLayOffs([card], round).TryGetValue(card, out var melds) ? melds : [];

    public static IEnumerable<(List<Meld> Melds, List<(Card Card, Meld Meld)> Layoffs, Card? Discard)> FindRummyConfigurations(
        IEnumerable<Card> hand, Player player, Round round,
        List<Meld> potentialMelds = null, Dictionary<Card, List<Meld>> potentialLayoffs = null
    ) {
        if (player.Melds.Count != 0) return [];
        HashSet<(List<Meld> Melds, List<(Card, Meld)> Layoffs, Card? Discard)> configurations = [];
        potentialMelds ??= FindMelds(hand).Melds; potentialLayoffs ??= FindLayOffs(hand, round);

        HashSet<IEnumerable<Meld>> meldConfigurations = [[]];
        foreach (var permutation in AllPermutations.Of(potentialMelds))
            meldConfigurations.Add(permutation.Where((x, i) => !permutation.Skip(i + 1).Any(y => x.Cards.Any(card => y.Cards.Contains(card)))));
        meldConfigurations = [..meldConfigurations.Where((x, i) => !meldConfigurations.Skip(i + 1).Any(y => Enumerable.SequenceEqual(x, y)))];

        foreach (var meldConfiguration in meldConfigurations) {
            var remainingCards = hand.DeepClone().ToList();
            meldConfiguration.ForEach(meld => meld.Cards.ForEach(card => remainingCards.Remove(card)));

            HashSet<List<(Card, Meld)>> layoffConfigurations = [[]];

            List<(Card Card, Meld Meld)> unwoundLayoffs = [];
            foreach (var (card, melds) in potentialLayoffs) foreach (var meld in melds) unwoundLayoffs.Add((card, meld));

            // Find all permutations of immediate layoffs
            var layoffPermutations = AllPermutations.Of(unwoundLayoffs).Select(x => x.ToList());
            foreach (var permutation in layoffPermutations) {
                HashSet<Card> previouslyUsedCards = [];
                for (int i = 0; i < permutation.Count; ++i) {
                    var thisCard = permutation.ElementAt(i).Card;
                    if (previouslyUsedCards.Contains(thisCard)) { permutation.RemoveAt(i); --i; }
                    previouslyUsedCards.Add(thisCard);                    
                }
            }

            foreach (var permutation in layoffPermutations) {
                var remainingCardsThisPermutation = remainingCards.DeepClone().Where(card => !permutation.Any(x => x.Card == card));

                /* TK!! -- Have to check not only layoffs, but new layoffs opened up by a given layoff */
                /*foreach (var (card, meld) in permutation) {
                    meld.AsNear().PotentialCards();
                }*/

                if (remainingCards.Count <= 1) configurations.Add((meldConfiguration.ToList(), permutation, remainingCardsThisPermutation.SingleOrDefault()));
            }
        }

        return configurations;
    }
}