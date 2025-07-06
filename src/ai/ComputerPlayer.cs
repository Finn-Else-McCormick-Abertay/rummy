
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
public abstract partial class ComputerPlayer : Player
{
    public ComputerPlayer(string name) : base(name) { }
    public ComputerPlayer() : this(nameof(ComputerPlayer)) { }

    protected new HandInternal Hand => _hand;

    protected (List<Meld> Melds, List<NearMeld> NearMelds) FindPotentialMelds() => PotentialMoves.FindMelds(Hand.Cards);
    protected Dictionary<Card, List<Meld>> FindPotentialLayOffs() => PotentialMoves.FindLayOffs(Hand.Cards, Round);
    protected List<Meld> FindPotentialLayOffs(Card card) => PotentialMoves.FindLayOffs(card, Round);

    protected void Meld(Meld meld) {
        Say($"Melding {meld}.");
        meld.Cards.ForEach(card => Hand.Pop(card));
        Round.Meld(meld).InspectErr(err => Think($"Failed to meld {meld}: {err}"));
    }

    protected void LayOff(Card card, Meld meld) {
        Say($"Laying off {card} to {meld}.");
        Hand.Pop(card);
        meld.LayOff(card).InspectErr(_ => Think($"Failed to lay off {card} to {meld}."));
    }
    
    protected void Discard(Card card) {
        Say($"Discarding {card}.");
        Hand.Pop(card);
        Round.DiscardPile.Discard(card);
    }
}