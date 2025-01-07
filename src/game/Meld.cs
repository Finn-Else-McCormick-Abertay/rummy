
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Rummy.Game;

public interface IMeld : IReadableCardPile
{
    public bool Valid { get; }

    public bool CanLayOff(Card card);
    public void LayOff(Card card);
    public void InternalUndoLayOff(Card card);
}

public class Run : CardPile, IMeld, IEquatable<Run>
{
	public new ReadOnlyCollection<Card> Cards { get => _cards.ToList().AsReadOnly(); }

    public Run(List<Card> cards) {
        _cards.Replace(cards);
        _cards.Sort(card => (int)card.Rank);
    }

    public bool CanLayOff(Card card) {
        var cardsTemp = _cards.Select(x => x with {}).ToList();
        cardsTemp.Add(card);
        var runTemp = new Run(cardsTemp);
        return runTemp.Valid;
    }

    public void LayOff(Card card) {
        if (card.Rank < _cards[0].Rank) { AddToFront(card); }
        else { AddToBack(card); }
    }
    public void InternalUndoLayOff(Card card) {
        _cards.Remove(card);
    }

    public bool Valid {
        get {
            if (Count < 3) { return false; }
            if (!_cards.All(card => card.Suit == _cards[0].Suit)) { return false; }
            var prevRank = _cards[0].Rank;
            for (int i = 1; i < _cards.Count; ++i) {
                var rank = _cards[i].Rank;
                if (rank != prevRank + 1) { return false; }
                prevRank = rank;
            }
            return true;
        }
    }

    public override string ToString() {
        return $"Run {Cards}";
    }

    public bool Equals(Run other) {
        return other.Cards.All(card => Cards.Contains(card));
    }
}

public class Set : CardPile, IMeld, IEquatable<Set>
{
	public new ReadOnlyCollection<Card> Cards { get => _cards.ToList().AsReadOnly(); }
    
    public Set(List<Card> cards) {
        _cards.Replace(cards);
        _cards.Sort(card => (int)card.Suit);
    }
    
    public bool CanLayOff(Card card) {
        var cardsTemp = _cards.Select(x => x with {}).ToList();
        cardsTemp.Add(card);
        var setTemp = new Set(cardsTemp);
        return setTemp.Valid;
    }
    public void InternalUndoLayOff(Card card) {
        _cards.Remove(card);
    }
    
    public void LayOff(Card card) {
        AddToBack(card);
    }

    public bool Valid {
        get {
            if (Count < 3 || Count > 4) { return false; }
            if (!_cards.All(card => card.Rank == _cards[0].Rank)) { return false; }
            return true;
        }
    }
    
    public override string ToString() {
        return $"Set {Cards}";
    }

    public bool Equals(Set other) {
        return other.Cards.All(card => Cards.Contains(card));
    }
}