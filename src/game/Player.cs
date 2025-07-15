
using System;
using System.Collections.Generic;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Result;
using static Rummy.Util.Option;
using Godot;
using System.Threading.Tasks;
using System.Text;
using System.Reflection;
using Rummy.Interface;

namespace Rummy.Gameplay;

[Tool, GlobalClass]
public abstract partial class Player : Resource
{
    protected Player(string name) {
        _defaultName = name;
        Name = name;
    }

    protected virtual void OnAddedToRound(Round round) { }
    protected virtual void OnRemovedFromRound(Round round) { }

    public abstract Task TakeTurn();

    public Round Round { get; set { if (Round is not null) OnRemovedFromRound(Round); field = value; if (Round is not null) OnAddedToRound(Round); } }

    public readonly List<Meld> Melds = [];

    public event Action NotifyNameChanged;
    public event Action NotifyScoreChanged;

    public int Score { get; set { field = value; NotifyScoreChanged?.Invoke(); } }

    private readonly string _defaultName;
    [Export] public string Name { get; set { field = value; NotifyNameChanged?.Invoke(); } }

    public override bool _PropertyCanRevert(StringName property) => property.ToString() switch {
        "Name" => true,
        _ => base._PropertyCanRevert(property)
    };
    public override Variant _PropertyGetRevert(StringName property) => property.ToString() switch {
        "Name" => _defaultName,
        _ => base._PropertyGetRevert(property)
    };

    public event EventHandler<string> OnSayingMessage, OnThinkingMessage;
    protected void Say(object message) => OnSayingMessage?.Invoke(this, message?.ToString());
    protected void Think(object message) => OnThinkingMessage?.Invoke(this, message?.ToString());
    protected void SayAndThink(object say, object think) { Say(say); Think(think); }

    protected HandInternal _hand = new();
    public IHand Hand => _hand;
    public interface IHand : ICountable
    {
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

    public Godot.Collections.Dictionary Serialize() => Serialize(this);

    public static Godot.Collections.Dictionary Serialize(Player player) {
        if (player.IsInvalid()) return [];
        Godot.Collections.Dictionary data = [];
        data["Type"] = player.GetType().Name;

        var exportedMembers =
            player.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty)
                .Where(x => x.MemberType == MemberTypes.Property || x.MemberType == MemberTypes.Field)
                .Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ExportAttribute)));

        foreach (var memberInfo in exportedMembers) data[memberInfo.Name] = player.Get(memberInfo.Name);
        return data;
    }

    public static Player Deserialize(Godot.Collections.Dictionary data) {
        if (!(data.TryGetValue("Type", out var typeNameVariant) && typeNameVariant.AsString() is string typeName)) return null;

        var playerType = ConfigPlayerEntry.PlayerTypes.FirstOrDefault(x => x.Name == typeName);
        var newPlayer = (Player)Activator.CreateInstance(playerType);

        var exportedMembers =
            playerType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty)
                .Where(x => x.MemberType == MemberTypes.Property || x.MemberType == MemberTypes.Field)
                .Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ExportAttribute)));

        foreach (var member in exportedMembers) if (data.TryGetValue(member.Name, out var variant)) newPlayer.Set(member.Name, variant);
        return newPlayer;
    }
}