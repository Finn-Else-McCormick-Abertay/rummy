
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Rummy.Game;

public interface IMeld : IReadableCardPile
{
    public bool Valid { get; }

    public bool CanLayOff(Card card);
    public void LayOff(Card card);
}

public class Run : CardPile, IMeld
{
	public new ReadOnlyCollection<Card> Cards { get => _cards.ToList().AsReadOnly(); }

    public Run(List<Card> cards) {
        _cards = new ObservableCollection<Card>(cards);
        ReorderBy(card => (int)card.Rank);
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
}

public class Set : CardPile, IMeld
{
	public new ReadOnlyCollection<Card> Cards { get => _cards.ToList().AsReadOnly(); }
    
    public Set(List<Card> cards) {
        _cards = new ObservableCollection<Card>(cards);
        ReorderBy(card => (int)card.Suit);
    }
    
    public bool CanLayOff(Card card) {
        var cardsTemp = _cards.Select(x => x with {}).ToList();
        cardsTemp.Add(card);
        var setTemp = new Set(cardsTemp);
        return setTemp.Valid;
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
}