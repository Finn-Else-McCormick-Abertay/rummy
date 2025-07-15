
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;
using Rummy.Interface;
using Rummy.Util;

namespace Rummy.Gameplay;

public partial class Game : GodotObject
{
    public Game() {
        RoundEnded += (round, index, _) => {
            if (round.Players.FirstOrDefault(x => x.Score > ScoreThreshold) is Player overallWinner)
                EmitSignalGameEnded(overallWinner);
        };
    }
    public Output Output { get; set; }

    public int ScoreThreshold { get; set; } = 300;

    public Round CurrentRound { get; private set; }
    public Player CurrentDealer => CurrentRound?.Players.ElementAtOrDefault(_currentDealerIndex);
    private int _currentDealerIndex = -1;

    public bool InRound => CurrentRound is not null && !CurrentRound.Finished && !CurrentRound.Failed;
    public bool GameFinished => _players.Any(x => x.Score > ScoreThreshold);

    [Signal] public delegate void RoundBeganEventHandler(Round round, int roundIndex, bool simulation);
    [Signal] public delegate void RoundEndedEventHandler(Round round, int roundIndex, bool simulation);
    [Signal] public delegate void GameEndedEventHandler(Player overallWinner);

    public Round BeginRound(bool force = false, bool simulation = false) {
        if (!(CurrentRound is null || CurrentRound.Finished) && !force) return null;

        // Increment dealer index
        if (++_currentDealerIndex >= _players.Count) _currentDealerIndex = 0;
        var reorderedPlayers = _players.Skip(_currentDealerIndex).Concat(_players.Take(_currentDealerIndex));

        var newRound = new Round([.. reorderedPlayers]) { Output = Output };
        int roundIndex = _roundHistory.Count;

        _roundHistory.Add(newRound);
        _roundAdditionalInfo[newRound] = new RoundInfo(index: roundIndex, dealer: _currentDealerIndex);
        newRound.NotifyRoundEnded += (winner, score, wasRummy) => {
            var roundInfo = _roundAdditionalInfo[newRound];
            roundInfo.WinnerScoreGain = score; roundInfo.EndedInRummy = wasRummy;
            roundInfo.ScoresAtRoundEnd = newRound.Players.Select(x => KeyValuePair.Create(x, x.Score)).ToDictionary();
            EmitSignalRoundEnded(newRound, roundIndex, simulation);
        };

        CurrentRound = newRound;
        EmitSignalRoundBegan(CurrentRound, roundIndex, simulation);
        return CurrentRound;
    }

    // Players (note - changes to order will only be applied at start of next round)
    private readonly List<Player> _players = [];
    public ReadOnlyCollection<Player> Players => _players.AsReadOnly();

    [Signal] public delegate void PlayerAddedEventHandler(Player player, int index);
    [Signal] public delegate void PlayerRemovedEventHandler(Player player);
    [Signal] public delegate void PlayerOrderChangedEventHandler();

    public bool AddPlayer(Player player, int? index = null) {
        if (player is null) return false;
        player.OnSayingMessage += OnPlayerSay;
        player.OnThinkingMessage += OnPlayerThink;
        int trueIndex = index ?? _players.Count;
        if (_players.Contains(player) || trueIndex < 0 || trueIndex > _players.Count) return false;
        _players.Insert(trueIndex, player);
        _playerOrderHistory.GetOrCreate(player).Add((_roundHistory.Count, true, trueIndex));
        EmitSignalPlayerAdded(player, trueIndex);
        EmitSignalPlayerOrderChanged();
        return true;
    }
    public void RemovePlayer(Player player) {
        if (player is null) return;
        player.OnSayingMessage -= OnPlayerSay;
        player.OnThinkingMessage -= OnPlayerThink;
        _players.Remove(player);
        _playerOrderHistory.GetOrCreate(player).Add((_roundHistory.Count, false, -1));
        EmitSignalPlayerRemoved(player);
        EmitSignalPlayerOrderChanged();
    }
    public bool ReplacePlayer(Player oldPlayer, Player newPlayer) {
        int playerIndex = _players.FindIndex(oldPlayer);
        if (playerIndex == -1) return false;
        RemovePlayer(oldPlayer); AddPlayer(newPlayer, playerIndex);
        return true;
    }

    public bool ReorderPlayer(Player player, int index) {
        if (!_players.Contains(player) || index < 0 || index >= _players.Count) return false;
        if (index >= _players.IndexOf(player)) index -= 1;
        _players.Remove(player); _players.Insert(index, player);
        _playerOrderHistory.GetOrCreate(player).Add((_roundHistory.Count, true, index));
        EmitSignalPlayerOrderChanged();
        return true;
    }

    public void SetPlayers(IEnumerable<Player> players) { ClearPlayers(); foreach (var player in players) AddPlayer(player); }
    public void ClearPlayers() { foreach (var player in _players) { RemovePlayer(player); } }
    
    private void OnPlayerSay(object obj, string message) => Output?.WriteLine(message, obj as Player, "say");
    private void OnPlayerThink(object obj, string message) => Output?.WriteLine(message, obj as Player, "think");

    // History tracking
    private struct RoundInfo(int index, int dealer)
    {
        public readonly int Index = index; public readonly int Dealer = dealer;
        public int? WinnerScoreGain { get; set; } = null; public bool? EndedInRummy { get; set; } = null;
        public Dictionary<Player, int> ScoresAtRoundEnd;
    }

    private readonly List<Round> _roundHistory = [];
    private readonly Dictionary<Round, RoundInfo> _roundAdditionalInfo = [];
    private readonly Dictionary<Player, List<(int Round, bool Added, int Index)>> _playerOrderHistory = [];
}