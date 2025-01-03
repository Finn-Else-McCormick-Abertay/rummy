
using Godot;
using Rummy.Game;

namespace Rummy.Interface;

class UserPlayer : Player 
{
    public override string Name { get => "User"; }

    public delegate void TurnBeginAction(Player player, Round round);
    public event TurnBeginAction TurnBegin;

    public override void BeginTurn(Round round) {
        TurnBegin?.Invoke(this, round);
    }
}