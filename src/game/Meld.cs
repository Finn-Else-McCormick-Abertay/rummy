
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;
using Rummy.Util;
using static Rummy.Util.Result;

namespace Rummy.Game;

public interface IMeld : IReadableCardPile
{
    public bool Valid { get; }

    public bool CouldLayOff(Card card);
    public int IndexIfLaidOff(Card card);
    public Result<Unit, Unit> LayOff(Card card);
    public void InternalUndoLayOff(Card card);

    // Clone of Meld with current cards and without any current listeners
    public IMeld Clone();
}

public class Run : CardPile, IMeld, IEquatable<Run>
{
	public new ReadOnlyCollection<Card> Cards { get => _cards.ToList().AsReadOnly(); }

    public Run(IEnumerable<Card> cards) {
        _cards.Replace(cards);
        _cards.Sort(card => (int)card.Rank);
    }

    public bool CouldLayOff(Card card) {
        var cardsTemp = _cards.ToList().ConvertAll(x => x);
        cardsTemp.Add(card);
        var runTemp = new Run(cardsTemp);
        return runTemp.Valid;
    }

    public int IndexIfLaidOff(Card card) {
        if (!CouldLayOff(card)) { return -1; }

        var cardsTemp = _cards.ToList().ConvertAll(x => x);
        cardsTemp.Add(card);
        var runTemp = new Run(cardsTemp);
        return runTemp.Cards.ToList().FindIndex(x => x == card);
    }

    public Result<Unit, Unit> LayOff(Card card) {
        if (!CouldLayOff(card)) { return Err(Unit.unit); }

        if (card.Rank < _cards.First().Rank) { AddToFront(card); } else { AddToBack(card); }
        return Ok();
    }
    public void InternalUndoLayOff(Card card) {
        _cards.Remove(card);
    }

    public bool Valid {
        get {
            if (Count < 3) { return false; }
            if (!_cards.All(card => card.Suit == _cards.First().Suit)) { return false; }
            for (int i = 0; i < _cards.Count; ++i) {
                var rank = _cards[i].Rank;
                if (rank != _cards.First().Rank + i) { return false; }
            }
            return true;
        }
    }

    public override string ToString() {
        return $"Run [{string.Join(", ", Cards)}]";
    }

    public override bool Equals(object obj) => obj is Run ? Equals(obj as Run) : false;
    public bool Equals(Run other) => other.Cards.All(card => Cards.Contains(card));
	public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);
    
    public IMeld Clone() {
        return new Run(_cards.ToList().ConvertAll(x => x));
    }
}

public class Set : CardPile, IMeld, IEquatable<Set>
{
	public new ReadOnlyCollection<Card> Cards { get => _cards.ToList().AsReadOnly(); }
    
    public Set(IEnumerable<Card> cards) {
        _cards.Replace(cards);
        _cards.Sort(card => (int)card.Suit);
    }
    
    public bool CouldLayOff(Card card) {
        var cardsTemp = _cards.ToList().ConvertAll(x => x);
        cardsTemp.Add(card);
        var setTemp = new Set(cardsTemp);
        return setTemp.Valid;
    }
    public int IndexIfLaidOff(Card card) {
        if (!CouldLayOff(card)) { return -1; }

        var cardsTemp = _cards.ToList().ConvertAll(x => x);
        cardsTemp.Add(card);
        var setTemp = new Set(cardsTemp);
        return setTemp.Cards.ToList().FindIndex(x => x == card);
    }
    
    public Result<Unit, Unit> LayOff(Card card) {
        if (!CouldLayOff(card)) { return Err(Unit.unit); }
        
        AddToBack(card);
        return Ok();
    }
    public void InternalUndoLayOff(Card card) {
        _cards.Remove(card);
    }

    public bool Valid {
        get {
            if (Count < 3 || Count > 4) { return false; }
            if (!_cards.All(card => card.Rank == _cards[0].Rank)) { return false; }
            return true;
        }
    }
    
    public override string ToString() {
        return $"Set [{string.Join(", ", Cards)}]";
    }

    public override bool Equals(object obj) => obj is Set ? Equals(obj as Set) : false;
    public bool Equals(Set other) => other.Cards.All(card => Cards.Contains(card));
	public override int GetHashCode() => Cards.ToList().ConvertAll(x => x.GetHashCode()).Aggregate(HashCode.Combine);

    public IMeld Clone() {
        return new Set(_cards.ToList().ConvertAll(x => x));
    }
}