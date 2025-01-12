using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Result;
using static Rummy.Util.Option;
using System.Collections.Generic;
using System.Linq;
using Rummy.AI;
using System;

namespace Rummy.Interface;

[Tool]
public partial class GameManager : Node
{
    public Round Round { get; private set; }
    private bool stateInvalid = false;

    private List<Player> players = new();
    private UserPlayer _userPlayer;
    public UserPlayer UserPlayer {
        get => _userPlayer;
        private set {
            _userPlayer = value;
            if (IsNodeReady() && DiscardButton is not null && MeldButton is not null && PlayerHand is not null) {
                DiscardButton.Visible = UserPlayer is not null;
                MeldButton.Visible = UserPlayer is not null;
                PlayerHand.CardPile = UserPlayer?.Hand as CardPile;
            }
        }
    }

    [Export] public Godot.Collections.Array<Player> Players {
        get => new(players);
        set {
            players.ForEach(player => {
                if (player is null) { return; }
                player.OnSayingMessage -= OnPlayerSay;
                player.OnThinkingMessage -= OnPlayerThink;
            });
            players = value.Cast<Player>().ToList();
            players.ForEach(player => {
                if (player is null) { return; }
                player.OnSayingMessage += OnPlayerSay;
                player.OnThinkingMessage += OnPlayerThink;
            });
            _userPlayer = (UserPlayer)players.Find(x => x is UserPlayer);
            RebuildPlayerDisplays(players);
        }
    }

    [ExportGroup("Nodes")]
    [Export] private DrawableCardPileContainer Deck { get; set; }
    [Export] private DrawableCardPileContainer DiscardPile { get; set; }
    [Export] private PlayerHand PlayerHand { get; set; }
    [Export] private Control ScoreDisplayRoot { get; set; }
    [Export] private Control MeldRoot { get; set; }
    [Export] private Button DiscardButton { get; set; }
    [Export] private Button MeldButton { get; set; }
    [Export] private Button NextTurnButton { get; set; }
    [Export] private FailureMessage FailureMessage { get; set; }

    [ExportGroup("Scenes")]
    [Export] private PackedScene MeldScene { get; set; }
    [Export] private PackedScene ScoreDisplayScene { get; set; }

    public override void _Ready() {
        if (Engine.IsEditorHint()) {
            RebuildPlayerDisplays(players);
            return;
        }
        
        Deck.NotifyDrew += OnUserDrewFromDeck;
        DiscardPile.NotifyDrew += OnUserDrewFromDiscardPile;
        DiscardButton.Pressed += OnDiscardButtonPressed;
        MeldButton.Pressed += OnMeldButtonPressed;
        FailureMessage.Button.Pressed += OnResetButtonPressed;
        RebuildMelds();

        BeginNewRound();
    }

    private void BeginNewRound() {
        if (Engine.IsEditorHint()) { return; }

        Round = new Round(players);
        Deck.CardPile = Round.Deck; DiscardPile.CardPile = Round.DiscardPile;
        Round.NotifyTurnReset += RebuildMelds;
        Round.NotifyMelded += (player, cards) => RebuildMelds();
        Round.NotifyLaidOff += (player, card) => RebuildMelds();

        RebuildMelds();
        RebuildPlayerDisplays(Round.Players);

        Round.NotifyTurnBegan += player => {
            if (player == UserPlayer) {
                Deck.AllowDraw = true;
                DiscardPile.AllowDraw = true;
                SetCanLayOff(false);
            }
        };
        Round.NotifyTurnEnded += (player, result) => {
            Deck.AllowDraw = false;
            DiscardPile.AllowDraw = false;
            SetCanLayOff(false);
            OnReachTurnBoundary(Round.NextPlayer);
            result.Inspect(_ => {
                DiscardButton.Disabled = true;
                MeldButton.Disabled = true;
                FailureMessage.Hide();
            }).InspectErr(err => {
                FailureMessage.Message = err.Replace(". ", ".\n");
                FailureMessage.UseButton = true; FailureMessage.Show();
                stateInvalid = true;
                DiscardButton.Disabled = true;
                MeldButton.Disabled = true;
                NextTurnButton.Visible = false;
            });
        };
        Round.NotifyTurnReset += () => OnReachTurnBoundary(Round.CurrentPlayer);

        NextTurnButton.Pressed += () => {
            NextTurnButton.Visible = false;
            Round.BeginTurn();
        };

        Round.NotifyGameEnded += (winner, score, isRummy) => {
            FailureMessage.Message = $"{Round.Winner.Name} wins round{(isRummy ? " with a rummy" : "")}, scoring {score}.";
            GD.Print(FailureMessage.Message);
            FailureMessage.UseButton = false; FailureMessage.Show();
            NextTurnButton.Visible = false;
            SetCanLayOff(false);
        };
        
        DiscardButton.Visible = UserPlayer is not null;
        MeldButton.Visible = UserPlayer is not null;
        PlayerHand.CardPile = UserPlayer?.Hand as CardPile;

        DiscardButton.Disabled = true;
        MeldButton.Disabled = true;
        NextTurnButton.Visible = false;

        OnReachTurnBoundary(Round.CurrentPlayer);
    }

    public override void _Process(double delta) {
        if (Engine.IsEditorHint() || Round is null || stateInvalid || Round.CurrentPlayer != UserPlayer) { return; }

        if (!Round.MidTurn && !Round.Finished) {
            Round.BeginTurn();
        }
        if (Round.MidTurn) {
            if (Round.HasDrawn) {
                Deck.AllowDraw = false;
                DiscardPile.AllowDraw = false;
                SetCanLayOff(true);

                var selected = PlayerHand.SelectedSequence;
                DiscardButton.Disabled = selected.Count != 1;
                MeldButton.Disabled = !(new Run(selected).Valid || new Set(selected).Valid);
            }
            else {
                Deck.AllowDraw = true;
                DiscardPile.AllowDraw = true;
                DiscardButton.Disabled = true;
                MeldButton.Disabled = true;
                SetCanLayOff(false);
            }
        }
    }

    private void RebuildMelds() {
        if (!IsNodeReady() || Engine.IsEditorHint()) { return; }
        foreach (Node node in MeldRoot.GetChildren()) { MeldRoot.RemoveChild(node); node.QueueFree(); }
        Round?.Melds.ToList().ForEach(meld => {
            var meldContainer = MeldScene.Instantiate() as MeldContainer;
            MeldRoot.AddChild(meldContainer); meldContainer.SetOwner(MeldRoot);
            meldContainer.CardPile = meld as CardPile;
            meldContainer.PlayerHand = PlayerHand;
            meldContainer.NotifyLaidOff += card => {
                if (meldContainer.CardPile is Meld) { UserPlayer.Hand.Pop(card); (meldContainer.CardPile as Meld).LayOff(card); }
            };
        });
    }

    private void SetCanLayOff(bool canLayOff) {
        if (!IsNodeReady() || Engine.IsEditorHint()) { return; }
        foreach (MeldContainer container in MeldRoot.GetChildren().Cast<MeldContainer>()) { container.CanLayOff = canLayOff; }
    }

    private void OnReachTurnBoundary(Player nextPlayer) {
        if (!IsNodeReady() || Round is null) { return; }
        NextTurnButton.Visible = nextPlayer != UserPlayer && !Round.Finished;
    }

    private void OnPlayerSay(object obj, string message) => GD.Print($"{(obj as Player)?.Name}: {message}");
    private void OnPlayerThink(object obj, string message) => GD.Print($"{(obj as Player)?.Name}(Think): {message}");

    private void RebuildPlayerDisplays(IEnumerable<Player> players) {
        if (!IsNodeReady() || ScoreDisplayRoot is null) { return; }

        foreach (Node node in ScoreDisplayRoot.GetChildren()) { ScoreDisplayRoot.RemoveChild(node); node.QueueFree(); }
        players.Where(x => x is not null).ToList().ForEach(player => {
            var root = ScoreDisplayScene.Instantiate();
            ScoreDisplayRoot.AddChild(root); if (!Engine.IsEditorHint()) { root.SetOwner(ScoreDisplayRoot); }
            var display = root.GetNode("PlayerScoreDisplay") as PlayerScoreDisplay;
            display.Player = player; display.Round = Round;
            var hand = root.FindChild("HandDisplay") as CardPileContainer;
            hand.CardPile = player.Hand as CardPile;
        });
    }

    private void OnUserDrewFromDeck(int _) => Round.Deck.Draw().Inspect(card => UserPlayer.Hand.Add(card));
    private void OnUserDrewFromDiscardPile(int count) {
        var drawnCards = Round.DiscardPile.Draw(count);
        drawnCards.ForEach(card => UserPlayer.Hand.Add(card));
        if (count > 1) { PlayerHand.Select(drawnCards.Last()); }
        // Should display in some way that it was the top card drawn
        //drawnCards.First();
    }

    private void OnDiscardButtonPressed() {
        if (!IsNodeReady() || UserPlayer is null || Round is null) { return; }

        var selected = PlayerHand.SelectedSequence.Single();
        (UserPlayer.Hand as IAccessibleCardPile).Cards.Remove(selected);
        Round.DiscardPile.Discard(selected);
        Round.EndTurn();
    }
    
    private void OnMeldButtonPressed() {
        if (!IsNodeReady() || UserPlayer is null || Round is null) { return; }

        var sequence = PlayerHand.SelectedSequence;
        var set = new Set(sequence); var run = new Run(sequence);
        
        Result<Meld, string> melded = Err("Invalid meld");
        if (set.Valid)      { melded = Round.Meld(set).And(set as Meld); }
        else if (run.Valid) { melded = Round.Meld(run).And(run as Meld); }

        melded.Inspect(meld => meld.Cards.ToList().ForEach(card => UserPlayer.Hand.Pop(card)));
        if (melded.IsOk) { RebuildMelds(); }
    }

    private void OnResetButtonPressed() {
        if (Round is null || !IsNodeReady()) { return; }
        Round.ResetTurn();
        stateInvalid = false;
        FailureMessage.Hide();
    }
}
