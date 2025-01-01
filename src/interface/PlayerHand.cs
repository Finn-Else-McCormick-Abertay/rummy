using Godot;
using Rummy.Game;
using System;
using System.Collections.Generic;

namespace Rummy.Interface;

public partial class PlayerHand : Control
{
    private readonly List<(Card, CardInput)> cards = new();

    private HBoxContainer cardContainer;

    public override void _Ready() {
        cardContainer = GetNode<HBoxContainer>("HBoxContainer");
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
}
