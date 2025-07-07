using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Option;
using static Rummy.Util.Result;

namespace Rummy.Game;

public class DiscardPile : CardPile, IReadableCardPile, IDrawableMulti
{
    public new ReadOnlyCollection<Card> Cards => _cards.ToList().AsReadOnly();

    public event OnCardDrawn OnCardDrawn;
    public event OnEmptied OnEmptied;

    public Option<Card> Draw()
    {
        if (Empty) return None;
        var card = _cards[0];
        _cards.RemoveAt(0);
        OnCardDrawn?.Invoke(card);
        if (Empty) OnEmptied?.Invoke();
        return Some(card);
    }

    public List<Card> Draw(int count)
    {
        var drawnCards = new List<Card>();
        for (int i = 0; i < count; ++i) Draw().Inspect(card => drawnCards.Add(card));
        return drawnCards;
    }

    public void Discard(Card card) => AddToFront(card);

    public void InternalUndoDraw(Card card) => _cards.Insert(0, card);
    public Result<Unit, Unit> InternalUndoDiscard(Card card) => _cards.Remove(card) ? Ok() : Err(Unit.unit);
}