
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rummy.AI;
using Rummy.Game;

namespace Rummy.Interface;

[Tool]
[GlobalClass]
public partial class UserPlayer : Player 
{
    public UserPlayer() : base("User") {}

    public override Task TakeTurn() {
        var (melds, nearMelds) = PotentialMoves.FindMelds(_hand.Cards);
        var layoffs = PotentialMoves.FindLayOffs(_hand.Cards, Round);
        
        if (melds.Count > 0) { Think($"Potential Melds: {string.Join(", ", melds)}"); }
        if (nearMelds.Count > 0) { Think($"Near Melds: {string.Join(", ", nearMelds.Select(x => string.Join(", ", x)))}"); }
        if (layoffs.Count > 0) { Think($"Potential Layoffs: {(Melds.Count == 0 ? "(cannot lay off)" : "")} {string.Join(", ", layoffs.Select(kvp => $"{kvp.Key} -> {(kvp.Value.Count > 1 ? "{" : "")}{string.Join(", ", kvp.Value)}{(kvp.Value.Count > 1 ? "}" : "")}"))}");  }
        
        return Task.CompletedTask;
    }
}