
using System;
using System.Collections.Generic;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Result;
using static Rummy.Util.Option;

namespace Rummy.Game;

public abstract class Player
{
    public delegate void NotifyScoreChangedAction();
    public event NotifyScoreChangedAction NotifyScoreChanged;
    
    public delegate void SayingMessageAction(string message);
    public event SayingMessageAction OnSayingMessage;
    protected void Say(string message) => OnSayingMessage?.Invoke(message);

    public delegate void ThinkingMessageAction(string message);
    public event ThinkingMessageAction OnThinkingMessage;
    protected void Think(string message) => OnThinkingMessage?.Invoke(message);

    public string Name { get; init; }

    private int _score;
    public int Score { get => _score; set { _score = value; NotifyScoreChanged?.Invoke(); } }
    
    protected Player(string name) {
        Name = name;
    }

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
        public new IList<Card> Cards => base.Cards;
	    public SortableObservableCollection<Card> CardsRaw => _cards;

        public void Add(Card card) => AddToBack(card);
        public void Add(List<Card> cards) => cards.ForEach(card => Add(card));
        
        public Option<Card> Pop(Card card) => Cards.Remove(card) ? card : None;
        public Option<Card> PopAt(int index) => (index < 0 || index >= Cards.Count) ? None : Pop(Cards.ElementAt(index));
        
        public void Reset() => _cards.Clear();

        public int Score() => _cards.ToList().Aggregate(0, (score, card) => score + card.Rank switch {
            Rank.King or Rank.Queen or Rank.Jack => 10,
            _ => (int)card.Rank
        });
        
        public IEnumerable<Card> Where(Func<Card, bool> pred) => Cards.Where(pred);
        public void ForEach(Action<Card> action) => Cards.ToList().ForEach(action);
    }

    protected HandInternal _hand = new();
    public IHand Hand => _hand;

	public List<Meld> Melds { get; set; } = new();

    public abstract void OnAddedToRound(Round round);
    public abstract void OnRemovedFromRound(Round round);

    public abstract void BeginTurn(Round game);
}