
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
public abstract partial class ComputerPlayer(string name = "ComputerPlayer") : Player(name) 
{
    protected new HandInternal Hand => _hand;

    protected (List<Meld> Melds, List<NearMeld> NearMelds) FindPotentialMelds() => PotentialMoves.FindMelds(Hand.Cards);
    protected Dictionary<Card, List<Meld>> FindPotentialLayOffs() => PotentialMoves.FindLayOffs(Hand.Cards, Round);
    protected List<Meld> FindPotentialLayOffs(Card card) => PotentialMoves.FindLayOffs(card, Round);
}