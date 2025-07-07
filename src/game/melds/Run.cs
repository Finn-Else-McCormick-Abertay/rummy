
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;
using Rummy.AI;
using Rummy.Util;
using static Rummy.Util.Result;

namespace Rummy.Game;

public class Run : Meld, IEquatable<Run>
{
    public override event Action<Card> NotifyLaidOff, NotifyLayOffUndone;

    public Run(IEnumerable<Card> cards) {
        _cards.Replace(cards);
        _cards.Sort(card => (int)card.Rank);
    }

    public override bool Valid {
        get {
            if (Count < 3 || !_cards.All(card => card.Suit == _cards.First().Suit)) { return false; }
            for (int i = 0; i < _cards.Count; ++i) { if (_cards[i].Rank != _cards.First().Rank + i) { return false; } }
            return true;
        }
    }

    public override Result<Unit, Unit> LayOff(Card card) {
        if (!CouldLayOff(card)) { return Err(Unit.unit); }
        if (card.Rank < _cards.First().Rank) { AddToFront(card); } else { AddToBack(card); }
        NotifyLaidOff?.Invoke(card);
        return Ok();
    }
    public override void InternalUndoLayOff(Card card) {
        _cards.Remove(card);
        NotifyLayOffUndone?.Invoke(card);
    }

    public override bool CouldLayOff(Card card) => new Run(_cards.DeepClone().Concat(new List<Card> { card })).Valid;

    public override int IndexIfLaidOff(Card card) =>
        (_cards.Contains(card) ? Cards : new Run(_cards.DeepClone().Concat(new List<Card> { card })).Cards)
        .ToList().FindIndex(x => x == card);

    public override string ToString() => $"Run [{string.Join(", ", Cards)}]";

    public override bool Equals(object obj) => obj is Run ? Equals(obj as Run) : false;
    public bool Equals(Run other) => other.Cards.All(Cards.Contains);
    public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);

    public override Meld Clone() => new Run(_cards.DeepClone());
    public override NearMeld AsNear() => new NearRun(_cards.DeepClone());
}
