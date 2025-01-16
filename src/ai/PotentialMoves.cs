
using System;
using System.Collections.Generic;
using System.Linq;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

static class PotentialMoves {
    public static (List<Meld> Melds, List<NearMeld> NearMelds) FindMelds(IEnumerable<Card> hand) {
        HashSet<Meld> melds = new(); HashSet<NearMeld> nearMelds = new();
        hand.ForEach(card => {
            // Sets
            var sameRank = hand.Where(card.MatchesRank);
            if (sameRank.Count() == 2) { nearMelds.Add(new NearSet(sameRank)); }
            while (sameRank.Count() >= 3) {
                if (sameRank.Count() > 4)   { melds.Add(new Set(sameRank.SkipLast(sameRank.Count() - 4))); }
                else                        { melds.Add(new Set(sameRank)); }
                sameRank = sameRank.Skip(Math.Min(sameRank.Count(), 4));
            }

            // Runs
            var sameSuit = hand.Where(card.MatchesSuit);
            HashSet<Card> potentialRun = new() { card };
            Option<Card> lowCard = card, highCard = card;
            while (lowCard.IsSome) {
                potentialRun.Add(lowCard.Value);
                var adj = sameSuit.Where(lowCard.Value.IsAdjacentRankBelow);
                lowCard = adj.Any() ? adj.First() : None;
            }
            while (highCard.IsSome) {
                potentialRun.Add(highCard.Value);
                var adj = sameSuit.Where(highCard.Value.IsAdjacentRankAbove);
                highCard = adj.Any() ? adj.First() : None;
            }

            if (potentialRun.Count >= 3)        { melds.Add(new Run(potentialRun)); }
            else if (potentialRun.Count == 2)   { nearMelds.Add(new NearRun(potentialRun)); }
            
            if (sameSuit.Count() > 1) {
                int rankDiff = sameSuit.Last().Rank - sameSuit.First().Rank + 1;
                if (rankDiff >= 3) { nearMelds.Add(new NearRun(sameSuit)); }
            }
        });
        return (melds.ToList(), nearMelds.ToList());
    }
    
    public static Dictionary<Card, List<Meld>> FindLayOffs(IEnumerable<Card> hand, Round round) {
        Dictionary<Card, List<Meld>> potentialLayOffs = new();

        round.Players.ToList().ForEach(player => {
            player.Melds.ForEach(meld => {
                hand.ForEach(card => { if (meld.CouldLayOff(card)) { potentialLayOffs.GetOrCreate(card).Add(meld); } });
            });
        });
        return potentialLayOffs;
    }
    public static List<Meld> FindLayOffs(Card card, Round round) {
        var layoffs = FindLayOffs(new List<Card>{ card }, round);
        return layoffs.ContainsKey(card) ? layoffs[card] : new();
    }
}