
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;
using Rummy.Util;
using static Rummy.Util.Result;

namespace Rummy.Game;

public abstract class Meld : CardPile, IReadableCardPile
{
	public new ReadOnlyCollection<Card> Cards { get => _cards.ToList().AsReadOnly(); }

    // Is this a valid (playable) meld?
    public abstract bool Valid { get; }
    
    public abstract Result<Unit, Unit> LayOff(Card card);
    public abstract void InternalUndoLayOff(Card card);

    // Would layoff be successful?
    public abstract bool CouldLayOff(Card card);
    public abstract int IndexIfLaidOff(Card card);

    // Clone of Meld with current cards and without any current listeners
    public abstract Meld Clone();
    
    public abstract event Action<Card> NotifyLaidOff, NotifyLayOffUndone;
}

public class Run : Meld, IEquatable<Run>
{
    public override event Action<Card> NotifyLaidOff, NotifyLayOffUndone;

    public Run(IEnumerable<Card> cards) {
        _cards.Replace(cards);
        _cards.Sort(card => (int)card.Rank);
    }

    public override bool Valid { get {
        if (Count < 3 || !_cards.All(card => card.Suit == _cards.First().Suit)) { return false; }
        for (int i = 0; i < _cards.Count; ++i) { if (_cards[i].Rank != _cards.First().Rank + i) { return false; } }
        return true;
    }}
    
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

    public override bool CouldLayOff(Card card) => new Run(_cards.DeepClone().Concat(new List<Card>{ card })).Valid;

    public override int IndexIfLaidOff(Card card) =>
        (_cards.Contains(card) ? Cards : new Run(_cards.DeepClone().Concat(new List<Card>{ card })).Cards)
        .ToList().FindIndex(x => x == card);

    public override string ToString() => $"Run [{string.Join(", ", Cards)}]";

    public override bool Equals(object obj) => obj is Run ? Equals(obj as Run) : false;
    public bool Equals(Run other) => other.Cards.All(card => Cards.Contains(card));
	public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);
    
    public override Meld Clone() => new Run(_cards.DeepClone());
}

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
        new Set(_cards.DeepClone().Concat(new List<Card>{ card })).Cards
        .ToList().FindLastIndex(x => x == card);
    
    public override string ToString() => $"Set [{string.Join(", ", Cards)}]";

    public override bool Equals(object obj) => obj is Set ? Equals(obj as Set) : false;
    public bool Equals(Set other) => other.Cards.All(card => Cards.Contains(card));
	public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);

    public override Meld Clone() => new Set(_cards.DeepClone());
}