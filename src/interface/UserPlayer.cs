
using Godot;
using Rummy.Game;

namespace Rummy.Interface;

class UserPlayer : Player 
{
    public UserPlayer() : base("User") {}

    public override void OnAddedToRound(Round round) {}
    public override void OnRemovedFromRound(Round round) {}

    public override void BeginTurn(Round round) {}
}