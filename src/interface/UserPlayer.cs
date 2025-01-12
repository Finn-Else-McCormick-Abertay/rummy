
using Godot;
using Rummy.Game;

namespace Rummy.Interface;

[Tool]
[GlobalClass]
public partial class UserPlayer : Player 
{
    public UserPlayer() : base("User") {}

    public override void OnAddedToRound(Round round) {}
    public override void OnRemovedFromRound(Round round) {}

    public override void BeginTurn(Round round) {}
}