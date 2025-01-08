
using System;
using System.Collections.Generic;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Result;
using static Rummy.Util.Option;

namespace Rummy.Game;

public abstract class Player
{
    public abstract string Name { get; }
    public int Score { get; set; }

    public interface IHand : ICountable {
        public void Add(Card card);
        public void Add(List<Card> cards);
        public Option<Card> Pop(Card card);
        public Option<Card> PopAt(int index);
        public void Reset();
        public int Score();
    }

    protected class HandInternal : CardPile, IHand, IAccessibleCardPile
    {
        public new IList<Card> Cards { get => base.Cards; }

        public void Add(Card card) {
            AddToBack(card);
        }
        public void Add(List<Card> cards) {
            foreach (Card card in cards) { Add(card); }
        }
        
        public Option<Card> Pop(Card card) { return Cards.Remove(card) ? card : None; }

        public Option<Card> PopAt(int index) {
            if (index < 0 || index >= Cards.Count) { return None; }
            return Pop(Cards.ElementAt(index));
        }
        
        public void Reset() {
            _cards.Clear();
        }

        public int Score() {
            int score = 0;
            foreach (Card card in _cards) {
                score += card.Rank switch {
                    Rank.King or Rank.Queen or Rank.Jack => 10,
                    _ => (int)card.Rank,
                };
            }
            return score;
        }
    }

    protected HandInternal hand = new();
    public IHand Hand { get => hand; }

	public List<IMeld> Melds { get; set; } = new();

    public abstract void BeginTurn(Round game);
}