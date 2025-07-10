
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Rummy.Game;

public class Game
{
    private readonly List<Player> _players = [];
    public ReadOnlyCollection<Player> Players => _players.AsReadOnly();

    public bool AddPlayer(Player player, int? index = null) {
        int trueIndex = index ?? _players.Count;
        if (_players.Contains(player) || trueIndex < 0 || trueIndex > _players.Count) return false;
        _players.Insert(trueIndex, player);
        return true;
    }
    public void RemovePlayer(Player player) => _players.Remove(player);

    public bool ReorderPlayer(Player player, int index) {
        if (!_players.Contains(player) || index < 0 || index >= _players.Count) return false;
        if (index >= _players.IndexOf(player)) index -= 1;
        _players.Remove(player); _players.Insert(index, player);
        return true;
    }

    public Round CurrentRound { get; private set; }

    private readonly List<Round> _roundHistory = [];

    public void BeginRound() {
        if (!(CurrentRound is null || CurrentRound.Finished)) return;
        CurrentRound = new Round(_players);
        _roundHistory.Add(CurrentRound);
    }
}