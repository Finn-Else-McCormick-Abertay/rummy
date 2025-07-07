using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Option;
using static Rummy.Util.Result;

namespace Rummy.Game;

public class Deck : CardPile, IDrawable
{
    public event OnCardDrawn OnCardDrawn;
    public event OnEmptied OnEmptied;

	public Option<Card> Draw() {
		if (Empty) return None;
		var card = Cards[0];
		Cards.RemoveAt(0);
        OnCardDrawn?.Invoke(card);
        if (Empty) OnEmptied?.Invoke();
		return Some(card);
	}

	public void InternalUndoDraw(Card card) => _cards.Insert(0, card);

	public void AddPack() {
		foreach (Suit suit in Enum.GetValues<Suit>()) foreach (Rank rank in Enum.GetValues<Rank>()) AddToBack(new Card(rank, suit));
	}
    
	public void Shuffle(Random random) => _cards.Sort(x => random.Next());
	public void Shuffle() => Shuffle(Random.Shared);

    public void Flip() => _cards.Replace(_cards.Reversed().DeepClone());
}
