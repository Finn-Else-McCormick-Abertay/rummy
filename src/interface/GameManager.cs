using Godot;
using Rummy.Game;
using System;
using System.Collections.Generic;

namespace Rummy.Interface;

public partial class GameManager : Node
{
    private Round round;
    private List<Player> players;

    [Export]
    public PlayerHand PlayerHand { get; set; }

    public GameManager() {
        var userPlayer = new UserPlayer();
        (userPlayer.Hand as CardPile).OnCardAdded += (card) => {
            if (PlayerHand == null) { return; }
            PlayerHand.Add(card);
        };

        players = new List<Player> {
            userPlayer
        };
    }

    public override void _Ready() {
        round = new Round(players);
        round.ProgressTurn();
    }

    public override void _Process(double delta) {
    }
}
