
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

public abstract class NearMeld
{
    public ImmutableList<Card> Cards { get; init; }
    protected NearMeld(IEnumerable<Card> cards) {
        var cardsTemp = cards.ToList(); cardsTemp.Sort(); cardsTemp.Reverse();
        Cards = cardsTemp.ToImmutableList();
    }

    public abstract NearMeld With(IEnumerable<Card> cards);
    public NearMeld With(Card card) => With(new List<Card>{ card });

    // Not always exhaustive - at least contains any cards required to become valid, and cards adjacent to edges
    public abstract List<Card> PotentialCards();

    // Is it already breaking a rule of the meld so that additions could never make it become valid
    public abstract bool Invalid { get; }

    public abstract bool ContainsValidMeld();

    public abstract Meld AsMeld();
}

public class NearSet : NearMeld, IEquatable<NearSet>
{
    public NearSet(IEnumerable<Card> cards) : base(cards) {}

    private List<Card> _potentialCards = null;
    public override List<Card> PotentialCards() {
        // Cache result - the list of cards in NearMeld is immutable, so this won't change between invocations
        if (_potentialCards is not null) { return _potentialCards; }

        _potentialCards = new();
        var rank = Cards.First().Rank;
        foreach (Suit suit in Enum.GetValues(typeof(Suit))) {
            var newCard = new Card(rank, suit);
            if (!Cards.Contains(newCard)) { _potentialCards.Add(newCard); }
        }
        return _potentialCards;
    }
    public override NearMeld With(IEnumerable<Card> cards) => new NearSet(Cards.ToList().Concat(cards));
    public override Meld AsMeld() => new Set(Cards);

    public override bool Invalid => !(Cards.Count <= 4 && Cards.All(card => card.Rank == Cards.First().Rank));

    public override bool ContainsValidMeld() => Cards.Count >= 3 && !Invalid;

    public override string ToString() => $"Near Set [{string.Join(", ", Cards)}]";
    
    public override bool Equals(object obj) => obj is NearSet ? Equals(obj as NearSet) : false;
    public bool Equals(NearSet other) => other.Cards.All(card => Cards.Contains(card));
    public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);
}

public class NearRun : NearMeld, IEquatable<NearRun>
{
    public NearRun(IEnumerable<Card> cards) : base(cards) {}

    private List<Card> _potentialCards = null;
    public override List<Card> PotentialCards() {
        // Cache result - the list of cards in NearMeld is immutable, so this won't change between invocations
        if (_potentialCards is not null) { return _potentialCards; }

        _potentialCards = new();
        var suit = Cards.First().Suit;

        if (Cards.First().Rank - 1 >= Rank.Ace) { _potentialCards.Add(new Card(Cards.First().Rank - 1, suit)); }
        if (Cards.Last().Rank + 1 <= Rank.King) { _potentialCards.Add(new Card(Cards.Last().Rank + 1, suit)); }

        Option<Card> prevCard = None;
        foreach (Card card in Cards) {
            prevCard.Inspect(prevCard => {
                if (prevCard.Rank + 1 == card.Rank) { return; }
                for (int i = (int)prevCard.Rank + 1; i < (int)card.Rank; ++i) { _potentialCards.Add(new Card((Rank)i, suit)); }
            });
            prevCard = Some(card);
        }
        _potentialCards = _potentialCards.Distinct().OrderBy(x => x.Rank).ToList();
        return _potentialCards;
    }
    public override NearMeld With(IEnumerable<Card> cards) => new NearRun(Cards.ToList().Concat(cards));
    public override Meld AsMeld() => new Run(Cards);

    public override bool Invalid => !Cards.All(card => card.Suit == Cards.First().Suit);

    public override bool ContainsValidMeld() {
        if (Cards.Count < 3 || Invalid) { return false; }

        IEnumerable<Card> cardsTemp = Cards.DeepClone();
        List<IEnumerable<Card>> contiguousRuns = new();
        while (cardsTemp.Any()) {
            List<Card> contiguousRun = new();
            foreach (int i in Util.Range.Over(cardsTemp)) {
                if (i == 0 || cardsTemp.ElementAt(i).Rank == cardsTemp.ElementAt(i - 1).Rank + 1) {
                    contiguousRun.Add(cardsTemp.ElementAt(i));
                }
                else { break; }
            }
            if (contiguousRun.Any()) { contiguousRuns.Add(contiguousRun); }
            cardsTemp = cardsTemp.Where((card, index) => index > contiguousRun.Count);
        }
        return contiguousRuns.Any(run => run.Count() >= 3);
    }

    public override string ToString() => $"Near Run [{string.Join(", ", Cards)}]";
    
    public override bool Equals(object obj) => obj is NearRun ? Equals(obj as NearRun) : false;
    public bool Equals(NearRun other) => other.Cards.All(card => Cards.Contains(card));
    public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);
}