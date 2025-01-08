using Godot;
using Rummy.Game;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Rummy.Interface;

public partial class PlayerHand : Control
{
    private readonly List<(Card, CardInput)> cards = new();

    private HBoxContainer cardContainer;

    public override void _Ready() {
        cardContainer = GetNode<HBoxContainer>("HBoxContainer");
        foreach (Node node in cardContainer.GetChildren()) {
            cardContainer.RemoveChild(node);
            node.QueueFree();
        }
        foreach ((Card card, CardInput input) in cards) {
            cardContainer.AddChild(input);
            input.Owner = cardContainer;
            input.Display.Rank = card.Rank;
            input.Display.Suit = card.Suit;
        }
    }

    public void Add(Card card) {
        var input = new CardInput();
        cards.Add((card, input));
        if (IsNodeReady()) {
            cardContainer.AddChild(input);
            input.Owner = cardContainer;
            input.Display.Rank = card.Rank;
            input.Display.Suit = card.Suit;
        }
    }
    public void Remove(Card card) {
        bool predicate((Card, CardInput) pair) => pair.Item1 == card;

        if (!cards.Exists(predicate)) { return; }
        var pair = cards.Find(predicate);
        var input = pair.Item2;

        cards.Remove(pair);
        input.QueueFree();
    }

    public void HookTo(CardPile hand) {
        hand.OnChanged += (obj, args) => {
            //GD.Print("OnChanged ", Enum.GetName(typeof(NotifyCollectionChangedAction), args.Action));
            if (args.Action == NotifyCollectionChangedAction.Remove || args.Action == NotifyCollectionChangedAction.Replace) {
                foreach (var item in args.OldItems) { Remove((Card)item); }
            }
            if (args.Action == NotifyCollectionChangedAction.Add || args.Action == NotifyCollectionChangedAction.Replace) {
                foreach (var item in args.NewItems) { Add((Card)item); }
            }
        };
    }

    public List<Card> SelectedSequence {
        get {
            var sequence = new List<Card>();
            foreach ((Card card, CardInput input) in cards) {
                if (input.Selected) {
                    sequence.Add(card);
                }
            }
            return sequence;
        }
    }
}
