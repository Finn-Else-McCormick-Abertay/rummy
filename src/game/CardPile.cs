
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Option;
using static Rummy.Util.Result;

namespace Rummy.Game;

public interface ICountable {
	public int Count { get; }
	public bool Empty { get; }
}

public delegate void OnCardAdded(Card card);

public abstract class CardPile : ICountable
{
    public event OnCardAdded OnCardAdded;
	public event NotifyCollectionChangedEventHandler OnChanged {
		add => _cards.CollectionChanged += value; remove => _cards.CollectionChanged -= value;
	}

	protected SortableObservableCollection<Card> _cards = new(new List<Card>());
	protected IList<Card> Cards { get => _cards; }

	public int Count { get => _cards.Count; }
	public bool Empty { get => Count == 0; }

	protected void AddToFront(Card card) {
		_cards.Insert(0, card);
        OnCardAdded?.Invoke(card);
	}
	protected void AddToBack(Card card) {
		_cards.Add(card);
        OnCardAdded?.Invoke(card);
	}

    public void Append(CardPile pile) {
		foreach (Card card in pile._cards) { AddToBack(card); }
    }
    public void Clear() {
        _cards.Clear();
    }
}

public interface IReadableCardPile {
	public ReadOnlyCollection<Card> Cards { get; }
}

public interface IAccessibleCardPile {
	public IList<Card> Cards { get; }
	public SortableObservableCollection<Card> CardsRaw { get; }
}

public delegate void OnCardDrawn(Card card);
public delegate void OnEmptied();

public interface IDrawable {
    public Option<Card> Draw();
	public void InternalUndoDraw(Card card);

    public abstract event OnCardDrawn OnCardDrawn;
    public abstract event OnEmptied OnEmptied;
}

public interface IDrawableMulti : IDrawable {
    public List<Card> Draw(int count);
}

public class Deck : CardPile, IDrawable
{
    public event OnCardDrawn OnCardDrawn;
    public event OnEmptied OnEmptied;

	public Option<Card> Draw() {
		if (Empty) { return None; }
		var card = Cards[0];
		Cards.RemoveAt(0);
        OnCardDrawn?.Invoke(card);
        if (Empty) { OnEmptied?.Invoke(); }
		return Some(card);
	}

	public void InternalUndoDraw(Card card) {
		_cards.Insert(0, card);
	}

	public void AddPack() {
		foreach (Suit suit in Enum.GetValues(typeof(Suit))) {
			foreach (Rank rank in Enum.GetValues(typeof(Rank))) {
				AddToBack(new Card(rank, suit));
			}
		}
	}
    
	public void Shuffle(Random random) {
        _cards.Sort(x => random.Next());
	}
	public void Shuffle() { Shuffle(Random.Shared); }

    public void Flip() {
		_cards.Replace(_cards.Reverse().ToList().ConvertAll(x => x));
	}
}

public class DiscardPile : CardPile, IReadableCardPile, IDrawableMulti
{
    public event OnCardDrawn OnCardDrawn;
    public event OnEmptied OnEmptied;

    public Option<Card> Draw() {
		if (Empty) { return None; }
		var card = _cards[0];
		_cards.RemoveAt(0);
        OnCardDrawn?.Invoke(card);
        if (Empty) { OnEmptied?.Invoke(); }
		return Some(card);
	}

	public List<Card> Draw(int count) {
		var drawnCards = new List<Card>();
		for (int i = 0; i < count; ++i) { Draw().Inspect(card => drawnCards.Add(card)); }
		return drawnCards;
	}
	
	public void InternalUndoDraw(Card card) {
		_cards.Insert(0, card);
	}

	public void Discard(Card card) {
		AddToFront(card);
	}

	public Result<Unit,Unit> InternalUndoDiscard(Card card) {
		return _cards.Remove(card) ? Ok() : Err(Unit.unit);
	}
	
	public new ReadOnlyCollection<Card> Cards { get => _cards.ToList().AsReadOnly(); }
}