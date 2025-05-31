
using System;
using System.Collections.Generic;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Result;
using static Rummy.Util.Option;
using Godot;
using System.Threading.Tasks;

namespace Rummy.Game;

[Tool]
[GlobalClass]
public abstract partial class Player : Resource
{
    protected Player(string name) {
        _defaultName = name;
        Name = name;
    }

    protected virtual void OnAddedToRound(Round round) { }
    protected virtual void OnRemovedFromRound(Round round) {}

    public abstract Task TakeTurn();

    public Round Round { get; set { if (Round is not null) OnRemovedFromRound(Round); field = value; if (Round is not null) OnAddedToRound(Round); } }

    public List<Meld> Melds { get; set; } = [];

    public event Action NotifyNameChanged;
    public event Action NotifyScoreChanged;
    
    public int Score { get; set { field = value; NotifyScoreChanged?.Invoke(); } }

    private readonly string _defaultName;
    [Export] public string Name { get; private set { field = value; NotifyNameChanged?.Invoke(); } }

    public override bool _PropertyCanRevert(StringName property) => property.ToString() switch {
        "Name" => true,
        _ => base._PropertyCanRevert(property)
    };
    public override Variant _PropertyGetRevert(StringName property) => property.ToString() switch {
        "Name" => _defaultName,
        _ => base._PropertyGetRevert(property)
    };
    
    public event EventHandler<string> OnSayingMessage, OnThinkingMessage;
    protected void Say(string message) => OnSayingMessage?.Invoke(this, message);
    protected void Think(string message) => OnThinkingMessage?.Invoke(this, message);
    
    protected HandInternal _hand = new();
    public IHand Hand => _hand;
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
}