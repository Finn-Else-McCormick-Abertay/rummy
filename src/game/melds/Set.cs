
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;
using Rummy.AI;
using Rummy.Util;
using static Rummy.Util.Result;

namespace Rummy.Gameplay;

public class Set : Meld, IEquatable<Set>
{
    public override event Action<Card> NotifyLaidOff, NotifyLayOffUndone;

    public Set(IEnumerable<Card> cards) {
        _cards.Replace(cards);
        _cards.Sort(card => (int)card.Suit);
    }

    public override bool Valid => Count >= 3 && Count <= 4 && _cards.All(card => card.Rank == _cards.First().Rank);
    
    public override Result<Unit, Unit> LayOff(Card card) {
        if (!CouldLayOff(card)) { return Err(Unit.unit); }
        
        AddToBack(card);
        NotifyLaidOff?.Invoke(card);
        return Ok();
    }
    public override void InternalUndoLayOff(Card card) {
        _cards.Remove(card);
        NotifyLayOffUndone?.Invoke(card);
    }
    
    public override bool CouldLayOff(Card card) => new Set(_cards.DeepClone().Concat(new List<Card>{ card })).Valid;
    public override int IndexIfLaidOff(Card card) =>
        new Set(_cards.DeepClone().Concat([card])).Cards
        .ToList().FindLastIndex(x => x == card);
    
    public override string ToString() => $"Set [{string.Join('\u200B', Cards)}]";

    public override bool Equals(object obj) => obj is Set ? Equals(obj as Set) : false;
    public bool Equals(Set other) => other.Cards.All(card => Cards.Contains(card));
	public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);

    public override Meld Clone() => new Set(_cards.DeepClone());
    public override NearMeld AsNear() => new NearSet(_cards.DeepClone());
}