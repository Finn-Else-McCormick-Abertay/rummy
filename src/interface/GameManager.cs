using Godot;
using Rummy.Game;
using System;
using System.Collections.Generic;

namespace Rummy.Interface;

public partial class GameManager : Node
{
    private Game.Game game;

    [Export]
    public PlayerHand PlayerHand { get; set; }

    public GameManager() {
        var players = new List<Game.Player> {
            new()
        };

        game = new Game.Game(players);
    }

    public override void _Ready() {
        foreach (Card card in game.Players[0].Hand) {
            PlayerHand.Add(card);
        }
    }
}
