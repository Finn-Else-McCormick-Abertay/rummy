
using Godot;
using Rummy.Game;

namespace Rummy.Interface;

class UserPlayer : Player 
{
    public override string Name { get => "User"; }

    public override void BeginTurn(Round round) {

    }
}