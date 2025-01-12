
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

public abstract class NearMeld
{
    public ImmutableList<Card> Cards { get; init; }
    protected NearMeld(IEnumerable<Card> cards) {
        var cardsTemp = cards.ToList(); cardsTemp.Sort();
        Cards = cardsTemp.ToImmutableList();
    }

    // Not always exhaustive - at least contains any cards required to become valid, and cards adjacent to edges
    public abstract List<Card> PotentialCards();

    // Is still valid with gaps, but invalid if it's already breaking a rule of the meld
    // so that additions could never make it become valid
    public abstract bool Valid { get; }
}

public class NearSet : NearMeld, IEquatable<NearSet>
{
    public NearSet(IEnumerable<Card> cards) : base(cards) {}

    public override List<Card> PotentialCards() {
        List<Card> potentialCards = new();
        var rank = Cards.First().Rank;
        foreach (Suit suit in Enum.GetValues(typeof(Suit))) {
            var newCard = new Card(rank, suit);
            if (!Cards.Contains(newCard)) { potentialCards.Add(newCard); }
        }
        return potentialCards;
    }

    public override bool Valid => Cards.Count <= 4 && Cards.All(card => card.Rank == Cards.First().Rank);

    public override string ToString() => $"Near Set [{string.Join(", ", Cards)}]";
    
    public override bool Equals(object obj) => obj is NearSet ? Equals(obj as NearSet) : false;
    public bool Equals(NearSet other) => other.Cards.All(card => Cards.Contains(card));
    public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);
}

public class NearRun : NearMeld, IEquatable<NearRun>
{
    public NearRun(IEnumerable<Card> cards) : base(cards) {}

    public override List<Card> PotentialCards() {
        List<Card> potentialCards = new();
        var suit = Cards.First().Suit;

        if (Cards.First().Rank - 1 >= Rank.Ace) { potentialCards.Add(new Card(Cards.First().Rank - 1, suit)); }
        if (Cards.Last().Rank + 1 <= Rank.King) { potentialCards.Add(new Card(Cards.First().Rank + 1, suit)); }

        Option<Card> prevCard = None;
        foreach (Card card in Cards) {
            prevCard.Inspect(prevCard => {
                if (prevCard.Rank + 1 == card.Rank) { return; }
                for (int i = (int)prevCard.Rank + 1; i < (int)card.Rank; ++i) { potentialCards.Add(new Card((Rank)i, suit)); }
            });
            prevCard = Some(card);
        }
        return potentialCards;
    }

    public override bool Valid => Cards.All(card => card.Suit == Cards.First().Suit);

    public override string ToString() => $"Near Run [{string.Join(", ", Cards)}]";
    
    public override bool Equals(object obj) => obj is NearRun ? Equals(obj as NearRun) : false;
    public bool Equals(NearRun other) => other.Cards.All(card => Cards.Contains(card));
    public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);
}