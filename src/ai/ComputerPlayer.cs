
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

class ComputerPlayer : Player 
{
    protected new HandInternal Hand => _hand;
    public ComputerPlayer(string name = "ComputerPlayer") : base(name) {}
    
    public override void OnAddedToRound(Round round) {}
    public override void OnRemovedFromRound(Round round) {}

    public override void BeginTurn(Round round) {
        round.Deck.Draw().Inspect(card => Hand.Add(card));
        Say("Drew from deck.");
        Hand.PopAt(0).Inspect(card => {
            round.DiscardPile.Discard(card);
            Say($"Discarded {card}.");
        });
        round.EndTurn();
    }

    protected void FindPotentialMelds(out List<Meld> melds, out List<NearMeld> nearMelds) {
        HashSet<Meld> potentialMelds = new();
        HashSet<NearMeld> potentialNearMelds = new();
        Hand.ForEach(card => {
            // Sets
            var sameRank = Hand.Where(card.MatchesRank);
            if (sameRank.Count() == 2) {
                potentialNearMelds.Add(new NearSet(sameRank));
            }
            while (sameRank.Count() >= 3) {
                if (sameRank.Count() > 4) {
                    var firstFour = sameRank.SkipLast(sameRank.Count() - 4);
                    potentialMelds.Add(new Set(firstFour));
                }
                else { potentialMelds.Add(new Set(sameRank)); }
                sameRank = sameRank.Skip(Math.Min(sameRank.Count(), 4));
            }

            // Runs
            var sameSuit = Hand.Where(card.MatchesSuit);
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

            if (potentialRun.Count >= 3) {
                potentialMelds.Add(new Run(potentialRun.AsEnumerable()));
            }
            else if (potentialRun.Count == 2) {
                potentialNearMelds.Add(new NearRun(potentialRun));
            }
        });
        melds = potentialMelds.Distinct().ToList();
        nearMelds = potentialNearMelds.Distinct().ToList();
    }
    
    protected Dictionary<Card, List<Meld>> FindPotentialLayOffs(Round round) {
        Dictionary<Card, List<Meld>> potentialLayOffs = new();

        round.Players.ToList().ForEach(player => {
            if (player != this && player.Hand.Count < 4) { Think($"{player.Name} only has {player.Hand.Count} cards left."); }

            player.Melds.ForEach(meld => {
                Hand.ForEach(card => {
                    if (meld.CouldLayOff(card)) { potentialLayOffs.GetOrCreate(card).Add(meld); }
                });
            });
        });
        return potentialLayOffs;
    }
}