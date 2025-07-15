
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Option;
using static Rummy.Util.Result;

namespace Rummy.Gameplay;

public interface ICountable {
	public int Count { get; }
	public bool Empty { get; }
}

public delegate void OnCardAdded(Card card);

public abstract class CardPile : ICountable
{
    public event OnCardAdded OnCardAdded;
	public event NotifyCollectionChangedEventHandler OnChanged {
		add => _cards.CollectionChanged += value;
		remove => _cards.CollectionChanged -= value;
	}

	protected SortableObservableCollection<Card> _cards = new([]);
	protected IList<Card> Cards => _cards;

	public int Count => _cards.Count;
	public bool Empty => Count == 0;

	protected void AddToFront(Card card) {
		_cards.Insert(0, card);
        OnCardAdded?.Invoke(card);
	}
	protected void AddToBack(Card card) {
		_cards.Add(card);
        OnCardAdded?.Invoke(card);
	}

    public void Append(CardPile pile) { foreach (Card card in pile._cards) AddToBack(card); }
	public void AppendFlipped(CardPile pile) { foreach (Card card in pile._cards) AddToFront(card); }
    public void Clear() => _cards.Clear();
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
	public List<Card> Draw(int count) {
		if (count != 1) throw new IndexOutOfRangeException("Attempted to draw multiple cards from a single-draw pile.");
		var card = Draw(); return card.IsSome ? [card.Value] : [];
	}
	public void InternalUndoDraw(Card card);

    public abstract event OnCardDrawn OnCardDrawn;
    public abstract event OnEmptied OnEmptied;
}

public interface IDrawableMulti : IDrawable {
    public new List<Card> Draw(int count);
}