
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

[Tool]
[GlobalClass]
public partial class ComputerPlayer : Player 
{
    protected new HandInternal Hand => _hand;
    
    public ComputerPlayer() : this("ComputerPlayer") {}
    public ComputerPlayer(string name) : base(name) {}
    
    public override void OnAddedToRound(Round round) {}
    public override void OnRemovedFromRound(Round round) {}

    public override void BeginTurn(Round round) {
        round.Deck.Draw().Inspect(card => Hand.Add(card));
        Say("Drew from deck.");
        Hand.PopAt(0).Inspect(card => {
            round.DiscardPile.Discard(card);
            Say($"Discarded {card}.");
        });
        round.EndTurn();
    }

    protected (List<Meld> Melds, List<NearMeld> NearMelds) FindPotentialMelds() => PotentialMoves.FindMelds(Hand.Cards);
    protected Dictionary<Card, List<Meld>> FindPotentialLayOffs(Round round) => PotentialMoves.FindLayOffs(Hand.Cards, round);
}