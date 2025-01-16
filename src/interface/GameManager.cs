using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Result;
using static Rummy.Util.Option;
using System.Collections.Generic;
using System.Linq;
using Rummy.AI;
using System;
using System.Threading.Tasks;

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
    [Export] private PackedScene CardDisplayScene { get; set; }
    [Export] private PackedScene MeldScene { get; set; }
    [Export] private PackedScene ScoreDisplayScene { get; set; }
    private Theme CardInPileTheme = ResourceLoader.Load<Theme>("res://assets/themes/card/in_pile.tres");

    [ExportGroup("Animation")]
    [Export] public double AnimationSpeedMultiplier { get; set; } = 1f;
    [Export] private double _drawDuration = 0.4f;
    [Export] private double _discardDuration = 0.4f;
    [Export] private double _meldDuration = 0.4f;
    [Export] private double _layOffDurationPlayer = 0.15f;
    [Export] private double _layOffDurationBot = 0.4f;
    [Export] private double _deckTurnOverDuration = 0.3f;
    [Export] private double _initialDrawGapDuration = 0.1f;
    [Export] private double _discardPileStarterDuration = 0.2f;
    [Export] private double _shuffleDuration = 2f;

    public double DrawDuration => _drawDuration * AnimationSpeedMultiplier;
    public double DiscardDuration => _discardDuration * AnimationSpeedMultiplier;
    public double MeldDuration => _meldDuration * AnimationSpeedMultiplier;
    public double LayOffDurationPlayer => _layOffDurationPlayer * AnimationSpeedMultiplier;
    public double LayOffDurationBot => _layOffDurationBot * AnimationSpeedMultiplier;
    public double DeckTurnOverDuration => _deckTurnOverDuration * AnimationSpeedMultiplier;
    public double InitialDrawGapDuration => _initialDrawGapDuration * AnimationSpeedMultiplier;
    public double DiscardPileStarterDuration => _discardPileStarterDuration * AnimationSpeedMultiplier;
    public double ShuffleDuration => _shuffleDuration * AnimationSpeedMultiplier;

    [Signal] public delegate void DeckShuffleCompleteEventHandler();
    [Signal] public delegate void DeckTurnOverCompleteEventHandler();
    [Signal] public delegate void InitialDealCompleteEventHandler();

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
        PlayerHand.NotifyCardPileRebuilt += OnPlayerHandRebuilt;
        DiscardButton.Disabled = true;
        MeldButton.Disabled = true;
        NextTurnButton.Visible = false;
        RebuildMelds();

        BeginNewRound();
        //SimulateRoundWithoutDisplay();
    }

    Node FindPlayerScoreDisplayRoot(Player player) => ScoreDisplayRoot?.GetChildren().ToList()
        .Find(node => node.GetNode<PlayerScoreDisplay>("PlayerScoreDisplay")?.Player == player) as Node;
    CardPileContainer FindPlayerHandDisplay(Player player) => FindPlayerScoreDisplayRoot(player)?.GetNode<CardPileContainer>("HandDisplay");

    private void SimulateRoundWithoutDisplay() {
        if (Engine.IsEditorHint() || players.Any(player => player is UserPlayer)) { return; }

        Round = new Round(players);
        var result = Round.Simulate();
        result.InspectErr(GD.Print);
        result.AndThen(x => Ok($"{x.Win.Winner.Name} wins{(x.Win.WasRummy ? " by rummying" : "")}, scoring {x.Win.Score}"))
            .Inspect(msg => {
                GD.Print(msg);
                FailureMessage.DisplayMessage(msg);
            });
        
        result.AndThen(x => Ok(x.History)).Inspect(history => {
            GD.Print("\n --------------------- \n");
            history.ForEach(turn => GD.Print(turn));
        });
    }

    private async void BeginNewRound() {
        if (Engine.IsEditorHint()) { return; }

        Round = new Round(players);
        Deck.CardPile = Round.Deck; DiscardPile.CardPile = Round.DiscardPile;
        Round.NotifyTurnReset += RebuildMelds;
        Round.NotifyMelded += (player, cards) => RebuildMelds();
        Round.NotifyLaidOff += (player, card) => RebuildMelds();

        Round.NotifyTurnBegan += player => {
            if (player == UserPlayer) {
                Deck.AllowDraw = true;
                DiscardPile.AllowDraw = true;
                SetCanLayOff(false);
                Deck.GrabFocus();
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
                PlayerHand.MustUseCard = None;
                PlayerHand.CannotDiscardCard = None;
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
            Round.BeginTurn().Wait();
            Round.EndTurn();
        };

        Round.NotifyGameEnded += (winner, score, isRummy) => {
            FailureMessage.Message = $"{Round.Winner.Name} wins round{(isRummy ? " with a rummy" : "")}, scoring {score}.";
            GD.Print(FailureMessage.Message);
            FailureMessage.UseButton = false; FailureMessage.Show();
            NextTurnButton.Visible = false;
            SetCanLayOff(false);
        };

        Round.ImmediateDisplayNotifyDeckRanOut += async () => {
            if (DiscardPile.GetChildCount() == 0) { return; }
            var discardPileCards = DiscardPile.GetChildren().Cast<CardDisplay>();
            List<Vector2> startPositions = new();
            discardPileCards.ToList().ForEach(display => startPositions.Add(display.GlobalPosition));

            Deck.Hide(); DiscardPile.Hide();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var deckCards = Deck.GetChildren().Cast<Control>();
            var endPos = deckCards.Any() ? deckCards.Last().GlobalPosition : Deck.GlobalPosition;
            
            SignalAwaiter lastAwaiter = null;
            startPositions.ForEach(startPos => {
                lastAwaiter = CreateCardMoveTween(DeckTurnOverDuration, None, DiscardPile.CardSize, startPos, endPos, CardInPileTheme);
            });
            await lastAwaiter;
            Deck.Show(); //DiscardPile.Show();
            EmitSignal(SignalName.DeckTurnOverComplete);
        };
        Round.ImmediateDisplayNotifyInitialCardPlaceOnDiscard += async (card) => {
            await ToSignal(this, Round.Turn >= 0 ? SignalName.DeckTurnOverComplete : SignalName.InitialDealComplete);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var cardInDeck = Deck.GetChildCount() > 0 ? Deck.GetChildren().Last() as CardDisplay : null;
            var cardInDiscardPile = DiscardPile.GetChildCount() > 0 ? DiscardPile.GetChildren().Last() as CardDisplay : null;

            var startPos = cardInDeck?.GlobalPosition ?? Deck.GlobalPosition;
            var endPos = cardInDiscardPile?.GlobalPosition + new Vector2(0f, DiscardPile.CardSeparation) ?? DiscardPile.GlobalPosition;

            await CreateCardMoveTween(DiscardPileStarterDuration, card, Deck.CardSize, startPos, endPos, CardInPileTheme);

            DiscardPile.Show();
        };

        Round.ImmediateDisplayNotifyDiscarded += async (player, card) => {
            if (player == UserPlayer) { return; }
            var handDisplay = FindPlayerHandDisplay(player); if (handDisplay is null) { return; }
            var cardDisplayInHand = handDisplay.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card) ??
                (handDisplay.GetChildCount() > 0 ? handDisplay.GetChild<CardDisplay>(0) : null);
            var startPos = cardDisplayInHand?.GlobalPosition ?? handDisplay.GlobalPosition;

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var cardDisplayInDiscard = DiscardPile.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card);
            var endPos = cardDisplayInDiscard?.GlobalPosition ?? DiscardPile.GlobalPosition;

            cardDisplayInDiscard?.Hide();
            
            await CreateCardMoveScaleTween(DiscardDuration, card,
                handDisplay.CardSize, DiscardPile.CardSize, startPos, endPos, CardInPileTheme);
            
            if (IsInstanceValid(cardDisplayInDiscard)) { cardDisplayInDiscard?.Show(); }
        };
        Round.ImmediateDisplayNotifyDrewFromDeck += async (player) => {
            if (player == UserPlayer) { return; }
            var handDisplay = FindPlayerHandDisplay(player); if (handDisplay is null) { return; }

            var cardDisplayInHand = handDisplay.GetChildCount() > 0 ? handDisplay.GetChild<CardDisplay>(0) : null;
            var cardDisplayInDeck = Deck.GetChildCount() > 0 ? Deck.GetChildren().Cast<Control>().Last() : null;

            var startPos = cardDisplayInDeck?.GlobalPosition ?? Deck.GlobalPosition;
            var endPos = cardDisplayInHand?.GlobalPosition ?? handDisplay.GlobalPosition;

            cardDisplayInHand?.Hide();
            
            await CreateCardMoveScaleTween(DrawDuration, None, Deck.CardSize, handDisplay.CardSize, startPos, endPos, CardInPileTheme);
                
            if (IsInstanceValid(cardDisplayInHand)) { cardDisplayInHand?.Show(); }
        };
        Round.ImmediateDisplayNotifyDrewFromDiscardPile += async (player, card) => {
            if (player == UserPlayer) { return; }
            var handDisplay = FindPlayerHandDisplay(player); if (handDisplay is null) { return; }
            var cardDisplayInHand = handDisplay.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card) ??
                (handDisplay.GetChildCount() > 0 ? handDisplay.GetChild<CardDisplay>(0) : null);
            var cardDisplayInDiscardPile = DiscardPile.GetChildCount() > 0 ? DiscardPile.GetChildren().Cast<Control>().Last() : null;

            var startPos = cardDisplayInDiscardPile?.GlobalPosition ?? DiscardPile.GlobalPosition;
            var endPos = cardDisplayInHand?.GlobalPosition ?? handDisplay.GlobalPosition;

            cardDisplayInHand?.Hide();
            
            await CreateCardMoveScaleTween(DrawDuration, None,
                DiscardPile.CardSize, handDisplay.CardSize, startPos, endPos, CardInPileTheme);

            if (IsInstanceValid(cardDisplayInHand)) { cardDisplayInHand?.Show(); }
        };
        Round.ImmediateDisplayNotifyLaidOff += async (player, meld, card) => {
            if (player == UserPlayer) { return; }
            var handDisplay = FindPlayerHandDisplay(player); if (handDisplay is null) { return; }
            var handDisplayCardSize = handDisplay.CardSize;
            
            var cardDisplayInHand = handDisplay.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card) ??
                (handDisplay.GetChildCount() > 0 ? handDisplay.GetChild<CardDisplay>(0) : null);
                
            var startPos = cardDisplayInHand?.GlobalPosition ?? handDisplay.GlobalPosition;

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var meldContainer = MeldRoot.GetChildren().Cast<MeldContainer>().ToList().Find(child => child.CardPile as Meld == meld);
            if (meldContainer is null) { return; }

            var cardDisplayInMeld = meldContainer.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card);

            var endPos = cardDisplayInMeld?.GlobalPosition ?? meldContainer.GlobalPosition;
            int zIndex = cardDisplayInMeld.FindAbsoluteZIndex();

            cardDisplayInMeld?.Hide();
            
            var tempDisplay = CreateTempCard(card, handDisplayCardSize, startPos, CardInPileTheme);
            tempDisplay.ZAsRelative = false; tempDisplay.ZIndex = zIndex;
            await CreateCardMoveScaleTween(LayOffDurationBot, tempDisplay, meldContainer.CardSize, endPos);

            if (IsInstanceValid(cardDisplayInMeld)) { cardDisplayInMeld?.Show(); }
            RebuildMelds();
        };
        Round.ImmediateDisplayNotifyMelded += async (player, meld) => {
            RebuildMelds();
            if (player == UserPlayer) { return; }
            var handDisplay = FindPlayerHandDisplay(player); if (handDisplay is null) { return; }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var newMeld = MeldRoot.GetChildren().Cast<MeldContainer>().ToList().Find(child => child.CardPile as Meld == meld);

            SignalAwaiter finalSignalAwaiter = null;
            foreach (Card card in meld.Cards) {
                var cardDisplayInHand = handDisplay.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card) ??
                    (handDisplay.GetChildCount() > 0 ? handDisplay.GetChild<CardDisplay>(0) : null);
                var cardDisplayInMeld = newMeld.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card);

                var startPos = cardDisplayInHand?.GlobalPosition ?? handDisplay.GlobalPosition;
                var endPos = cardDisplayInMeld?.GlobalPosition ?? newMeld.GlobalPosition;
                int zIndex = cardDisplayInMeld.FindAbsoluteZIndex();
                
                var tempDisplay = CreateTempCard(card, handDisplay.CardSize, startPos, CardInPileTheme);
                tempDisplay.ZAsRelative = false; tempDisplay.ZIndex = zIndex;
                finalSignalAwaiter =  CreateCardMoveScaleTween(MeldDuration, tempDisplay, newMeld.CardSize, endPos);
            }
            newMeld?.Hide();
            await finalSignalAwaiter;
            if (IsInstanceValid(newMeld)) { newMeld?.Show(); }
        };

        RebuildMelds();
        RebuildPlayerDisplays(Round.Players);
        
        DiscardButton.Visible = UserPlayer is not null;
        MeldButton.Visible = UserPlayer is not null;
        PlayerHand.CardPile = UserPlayer?.Hand as CardPile;

        DiscardButton.Disabled = true;
        MeldButton.Disabled = true;
        NextTurnButton.Visible = false;

        Deck.AllowDraw = false;
        DiscardPile.AllowDraw = false;

        Round.ImmediateDisplayNotifyDeckShuffled += async () => {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            List<Tween> tweens = new();
            List<int> usedTargetIndices = new();
            foreach (CardDisplay display in Deck.GetChildren()) {
                var initialPosition = display.Position;
                var tween = GetTree().CreateTween();

                int nextIndex;
                do { nextIndex = Random.Shared.Next(Deck.GetChildCount()); } while (usedTargetIndices.Contains(nextIndex));
                usedTargetIndices.Add(nextIndex);
                int indexChange = display.GetIndex() - nextIndex;
                var endPosition = Deck.GetChild<Control>(nextIndex).Position;
                
                double waitTimeFront = Random.Shared.NextDouble() * ShuffleDuration;
                double moveTime = 0.1 * ShuffleDuration;
                double waitTimeBack = Math.Max(ShuffleDuration * 1.1 - waitTimeFront - moveTime, 0d);

                tween.TweenInterval(waitTimeFront);
                tween.TweenProperty(display, "position:x", display.Size.X * 1.15f, moveTime / 3d);
                tween.TweenProperty(display, "position:y", endPosition.Y, moveTime / 3d);
                tween.TweenProperty(display, "z_index", nextIndex, 0f);
                tween.TweenProperty(display, "position:x", initialPosition.X, moveTime / 3d);
                tween.TweenInterval(waitTimeBack);
                tween.SetTrans(Tween.TransitionType.Cubic);
                tweens.Add(tween);
            }
            await ToSignal(tweens.Last(), Tween.SignalName.Finished);
            await ToSignal(GetTree().CreateTimer(0.01f), Timer.SignalName.Timeout);
            EmitSignal(SignalName.DeckShuffleComplete);
        };
        Round.ImmediateDisplayNotifyDrewDuringInitialisation += async (player, cardIndex, handSize) => {
            int playerIndex = Round.Players.ToList().FindIndex(player);

            await ToSignal(GetTree().CreateTimer((Round.Players.Count * cardIndex + playerIndex) * InitialDrawGapDuration), Timer.SignalName.Timeout);

            var handDisplay = FindPlayerHandDisplay(player);
            var cardDisplayInHand = handDisplay.GetChild<CardDisplay>(cardIndex);
            var cardDisplayInPlayerHand = player == UserPlayer ? PlayerHand.GetChild<CardDisplay>(cardIndex) : null;

            var deckLastCard = Deck.GetChildCount() > 0 ? Deck.GetChildren().Cast<Control>().Last() : null;
            var startPos = deckLastCard?.GlobalPosition ?? Deck.GlobalPosition;
            var endPos = cardDisplayInPlayerHand?.GlobalPosition ?? cardDisplayInHand?.GlobalPosition ?? handDisplay.GlobalPosition;

            float endSize = player == UserPlayer ? PlayerHand.CardSize : handDisplay.CardSize;

            if (deckLastCard is not null) { Deck.RemoveChild(deckLastCard); deckLastCard.QueueFree(); }

            await CreateCardMoveScaleTween(DrawDuration, None, Deck.CardSize, endSize, startPos, endPos, CardInPileTheme);
            cardDisplayInHand.Show();
            cardDisplayInPlayerHand?.Show();

            if (playerIndex == Round.Players.Count - 1 && cardIndex == handSize - 1) { EmitSignal(SignalName.InitialDealComplete); }
        };

        // Create deck
        Round.CreateAndShuffleDeck();

        await ToSignal(this, SignalName.DeckShuffleComplete);

        // Deal cards
        Round.DealCardsAndInitialiseRound();
        
        stateInvalid = true;
        Players.ForEach(player =>
            FindPlayerHandDisplay(player)
                .GetChildren().Cast<Control>().ToList()
                .ForEach(child => child.Hide()));
        PlayerHand.GetChildren().Cast<Control>().ToList().ForEach(child => child.Hide());

        Util.Range.FromTo(Deck.GetChildCount(), 52).ForEach(_ => {
            var tempCard = CardDisplayScene.Instantiate() as CardDisplay;
            tempCard.Theme = CardInPileTheme;
            Deck.AddChild(tempCard); tempCard.Owner = Deck;
        });
        DiscardPile.Hide();

        await ToSignal(this, SignalName.InitialDealComplete);
        
        stateInvalid = false;
        // Make sure there are no temp cards left. (There shouldn't be, but just to be sure)
        Deck.CardPile = null;
        Deck.CardPile = Round.Deck;

        Callable.From(OnPlayerHandRebuilt).CallDeferred();
        OnReachTurnBoundary(Round.CurrentPlayer);
    }

    public override void _Process(double delta) {
        if (Engine.IsEditorHint() || Round is null || stateInvalid) { return; }

        if (NextTurnButton.Visible && !NextTurnButton.Disabled && Input.IsActionJustPressed(ActionName.Skip)) {
            NextTurnButton.Visible = false;
            Round.BeginTurn().Wait();
            Round.EndTurn();
        }
        if (!DiscardButton.Disabled && Input.IsActionJustPressed(ActionName.Discard)) { OnDiscardButtonPressed(); }
        if (!MeldButton.Disabled && Input.IsActionJustPressed(ActionName.Meld)) { OnMeldButtonPressed(); }

        if (Round.CurrentPlayer == UserPlayer && !Round.MidTurn && !Round.Finished && Round.Turn >= 0) {
            Round.BeginTurn().Wait();
        }
        if (Round.CurrentPlayer == UserPlayer && Round.MidTurn) {
            if (Round.HasDrawn) {
                Deck.AllowDraw = false;
                DiscardPile.AllowDraw = false;
                SetCanLayOff(true);

                var selected = PlayerHand.SelectedSequence;
                DiscardButton.Disabled = !(selected.Count == 1 &&
                    (PlayerHand.CannotDiscardCard.IsNone ||
                    selected.First() != PlayerHand.CannotDiscardCard.Value) ||
                    PlayerHand.GetChildCount() <= 1);
                MeldButton.Disabled = !(new Run(selected).Valid || new Set(selected).Valid);
                
                if (PlayerHand.GetChildCount() == 0 && !Round.Finished) { Round.EndTurn(); }
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
            meldContainer.CardPile = meld;
            meldContainer.PlayerHand = PlayerHand;
            meldContainer.NotifyLaidOff += async card => {
                if (meldContainer.CardPile is Meld) {
                    var displayInHand = PlayerHand.FindCard(card);
                    var startPos = displayInHand.AndThen(display => Some(display.GlobalPosition)).Or(PlayerHand.GlobalPosition);

                    UserPlayer.Hand.Pop(card);
                    (meldContainer.CardPile as Meld).LayOff(card);

                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var displayInMeld = meldContainer.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card);

                    if (displayInMeld is null) { GD.Print("USER Layoff display is null"); }

                    var endPos = displayInMeld?.GlobalPosition ?? meldContainer.GlobalPosition;
                    //int? zIndex = displayInMeld?.FindAbsoluteZIndex();

                    displayInMeld?.Hide();

                    var tempDisplay = CreateTempCard(card, PlayerHand.CardSize, startPos);
                    //if (zIndex is not null) { tempDisplay.ZAsRelative = false; tempDisplay.ZIndex = zIndex ?? 0; }
                    await CreateCardMoveScaleTween(LayOffDurationPlayer, tempDisplay, meldContainer.CardSize, endPos);

                    displayInMeld?.Show();                                        
                }
            };
        });
    }

    private void SetCanLayOff(bool canLayOff) {
        if (!IsNodeReady() || Engine.IsEditorHint()) { return; }
        foreach (MeldContainer container in MeldRoot.GetChildren().Cast<MeldContainer>()) { container.CanLayOff = canLayOff; }
    }

    private void OnReachTurnBoundary(Player nextPlayer) {
        if (!IsNodeReady() || Round is null) { return; }
        if (nextPlayer != UserPlayer && !Round.Finished) { NextTurnButton.Visible = true; NextTurnButton.GrabFocus(); }
    }

    private void OnPlayerHandRebuilt() {
        if (!IsNodeReady() || Round is null) { return; }
        var firstCard = PlayerHand.GetChildCount() > 0 ? PlayerHand.GetChild<Control>(0) : null;
        var firstCardPath = firstCard?.GetPath() ?? new NodePath();
        DiscardPile.FocusNext = firstCardPath;
        NextTurnButton.FocusNext = firstCardPath;
        // These may be overridden later on if a card is directly below the deck
        Deck.FocusNeighborBottom = firstCardPath; DiscardPile.FocusNeighborBottom = firstCardPath;

        if (firstCard is not null) { firstCard.FocusPrevious = DiscardPile.GetPath(); }
        NextTurnButton.FocusNeighborBottom =
            (PlayerHand.GetChildCount() > 0 ? PlayerHand.GetChildren().Last() : null)?.GetPath()?? new NodePath();
        NodePath deckPath = Deck.GetPath(), discardPath = DiscardPile.GetPath(), nextTurnPath = NextTurnButton.GetPath();

        var deckCentre = Deck.GetGlobalRect().GetCenter();

        Rect2 discardPileRect = DiscardPile.GetChildCount() > 0 ? DiscardPile.GetChildren().Cast<Control>().Last().GetGlobalRect() : new Rect2();
        var discardPileCentre = discardPileRect.GetCenter();
        float discardPileLeftBound = discardPileRect.Position.X, discardPileRightBound = discardPileRect.End.X;
        PlayerHand.GetChildren().Cast<Control>().ToList().ForEach(card => {
            var cardRect = card.GetGlobalRect();
            var cardCentre = cardRect.GetCenter();

            if (cardRect.Position.X < deckCentre.X && cardRect.End.X > deckCentre.X) {
                Deck.FocusNeighborBottom = card.GetPath();
            }
            if (cardRect.Position.X < discardPileCentre.X && cardRect.End.X > discardPileCentre.X) {
                DiscardPile.FocusNeighborBottom = card.GetPath();
            }

            if (cardCentre.X < discardPileLeftBound && DiscardPile.GetChildCount() > 0) {
                card.FocusNeighborTop = deckPath;
            }
            else if (cardCentre.X < discardPileRightBound + discardPileRect.Size.X * 2f && DiscardPile.GetChildCount() > 0) {
                card.FocusNeighborTop = discardPath;
            }
            else if (cardCentre.X > NextTurnButton.GlobalPosition.X - NextTurnButton.GetGlobalRect().Size.X * 1.5f) {
                card.FocusNeighborTop = nextTurnPath;
            }
            else { card.FocusNeighborTop = new NodePath(); }
        });
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

    private CardDisplay CreateTempCard(Option<Card> cardOpt, float cardSize, Vector2 pos, Theme theme = null) {
        var display = CardDisplayScene.Instantiate() as CardDisplay;
        display.SetAnchorsPreset(Control.LayoutPreset.Center); display.Size = new Vector2(cardSize, 0f);
        display.FaceDown = cardOpt.IsNone; cardOpt.Inspect(card => display.Card = card);
        if (theme is not null) { display.Theme = theme; }
        GetTree().Root.AddChild(display);
        display.Position = pos;
        return display;
    }

    private SignalAwaiter CreateCardMoveTween(double duration, CardDisplay display, Vector2 endPos) {
        var tween = GetTree().CreateTween();
        tween.TweenProperty(display, "position", endPos, duration).SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(display.QueueFree));

        return ToSignal(tween, Tween.SignalName.Finished);
    }

    private SignalAwaiter CreateCardMoveTween(
            double duration, Option<Card> cardOpt, float cardSize, Vector2 startPos, Vector2 endPos, Theme theme = null
        ) => CreateCardMoveTween(duration, CreateTempCard(cardOpt, cardSize, startPos, theme), endPos);

    private SignalAwaiter CreateCardMoveScaleTween(double duration, CardDisplay display, float endSize, Vector2 endPos) {
        var tween = GetTree().CreateTween();
        tween.TweenProperty(display, "position", endPos, duration).SetEase(Tween.EaseType.InOut);
        tween.Parallel().TweenProperty(display, "size", new Vector2(endSize, 0f), duration).SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(display.QueueFree));

        return ToSignal(tween, Tween.SignalName.Finished);
    }

    private SignalAwaiter CreateCardMoveScaleTween(
            double duration, Option<Card> cardOpt, float startSize, float endSize,
            Vector2 startPos, Vector2 endPos, Theme theme = null
        ) => CreateCardMoveScaleTween(duration, CreateTempCard(cardOpt, startSize, startPos, theme), endSize, endPos);

    private void OnUserDrewFromDeck(int _) {
        Vector2 lastCardPos = Deck.GetChildOrNull<Control>(Deck.GetChildCount() - 1)?.GlobalPosition ?? Deck.GlobalPosition;
        var endPos = PlayerHand.GetChildOrNull<Control>(PlayerHand.GetChildCount() - 1)?.GlobalPosition ?? PlayerHand.GlobalPosition;
        Round.Deck.Draw().Inspect(async card => {
            UserPlayer.Hand.Add(card);
            var display = PlayerHand.FindCard(card);
            display.Inspect(display => display.Visible = false);

            await CreateCardMoveTween(DrawDuration, None, Deck.CardSize, lastCardPos, endPos);

            display.Inspect(display => display.Visible = true);
            PlayerHand.HoveredCard = card;
            PlayerHand.SendFocusToCards();
        });
    }

    private void OnUserDrewFromDiscardPile(int count) {
        for (int i = 0; i < count; ++i) {
            var lastChild = DiscardPile.GetChildCount() > 0 ? DiscardPile.GetChildren().Last() as Control : null;
            var beginPos = lastChild?.GlobalPosition ?? DiscardPile.GlobalPosition;
            if (i > 0 && lastChild is not null) {
                if (DiscardPile.HighlightedIndex == lastChild?.GetIndex()) { beginPos -= DiscardPile.HighlightOffset; }
            }
            var drawnCard = Round.DiscardPile.Draw();
            drawnCard.Inspect(card => UserPlayer.Hand.Add(card));
            var display = drawnCard.AndThen(card => PlayerHand.FindCard(card));

            drawnCard.AndThen<(Card card, int index)>(card => (card, i)).Inspect(async pair => {
                display.Inspect(display => display.Visible = false);

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var endPos = display.AndThen(display => Some(display.GlobalPosition)).Or(PlayerHand.GlobalPosition);

                await CreateCardMoveTween(DrawDuration, pair.card, DiscardPile.CardSize, beginPos, endPos);

                display.Inspect(display => display.Visible = true);
                if (pair.index == 0) { PlayerHand.CannotDiscardCard = pair.card; }
                if (count > 1 && pair.index == count - 1) {
                    PlayerHand.MustUseCard = pair.card;
                    PlayerHand.Select(pair.card);
                    PlayerHand.HoveredCard = pair.card;
                }
                
                if (pair.index == count - 1) { PlayerHand.SendFocusToCards(); }
            });
        }
    }

    private async void OnDiscardButtonPressed() {
        if (!IsNodeReady() || UserPlayer is null || Round is null || PlayerHand.SelectedSequence.Count != 1) { return; }

        var selected = PlayerHand.SelectedSequence.Single();
        
        var startPos = PlayerHand.FindCard(selected).AndThen(card => Some(card.GlobalPosition)).Or(PlayerHand.GlobalPosition);
        
        (UserPlayer.Hand as IAccessibleCardPile).Cards.Remove(selected);
        Round.DiscardPile.Discard(selected);

        var discardDisplay = DiscardPile.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == selected);
        discardDisplay?.Hide();

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var endPos = discardDisplay?.GlobalPosition + new Vector2(0f, DiscardPile.CardSeparation / 2f) ?? DiscardPile.GlobalPosition;

        var distance = (endPos - startPos).Length();
        double duration = DiscardDuration / (900.0 / distance);

        await CreateCardMoveTween(duration, selected, PlayerHand.CardSize, startPos, endPos);
        
        if (IsInstanceValid(discardDisplay)) { discardDisplay?.Show(); }
        if (!Round.Finished) { Round?.EndTurn(); }
    }
    
    private async void OnMeldButtonPressed() {
        if (!IsNodeReady() || UserPlayer is null || Round is null) { return; }

        var sequence = PlayerHand.SelectedSequence;
        var set = new Set(sequence); var run = new Run(sequence);
        
        Result<Meld, string> melded = Err("Invalid meld");
        if (set.Valid)      { melded = Round.Meld(set).And(set as Meld); }
        else if (run.Valid) { melded = Round.Meld(run).And(run as Meld); }

        if (melded.IsOk) {
            RebuildMelds();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var newMeld = MeldRoot.GetChildren().Cast<MeldContainer>().ToList().Find(child => child.CardPile as Meld == melded.Value);

            SignalAwaiter finalSignalAwaiter = null;
            foreach (Card card in melded.Value.Cards) {
                var cardDisplayInMeld = newMeld.GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card);

                var startPos = PlayerHand.FindCard(card).AndThen(card => Some(card.GlobalPosition)).Or(PlayerHand.GlobalPosition);
                var endPos = cardDisplayInMeld?.GlobalPosition ?? newMeld.GlobalPosition;
                //int zIndex = cardDisplayInMeld.FindAbsoluteZIndex();
                
                var tempDisplay = CreateTempCard(card, PlayerHand.CardSize, startPos);
                //tempDisplay.ZAsRelative = false; tempDisplay.ZIndex = zIndex;

                UserPlayer.Hand.Pop(card);
                finalSignalAwaiter =  CreateCardMoveScaleTween(MeldDuration, tempDisplay, newMeld.CardSize, endPos);
            }
            newMeld?.Hide();
            await finalSignalAwaiter;
            if (IsInstanceValid(newMeld)) { newMeld?.Show(); }
            PlayerHand.SendFocusToCards();
        }
    }

    private void OnResetButtonPressed() {
        if (Round is null || !IsNodeReady()) { return; }
        Round.ResetTurn();
        stateInvalid = false;
        FailureMessage.Hide();
        PlayerHand.MustUseCard = None;
        PlayerHand.CannotDiscardCard = None;
    }
}
