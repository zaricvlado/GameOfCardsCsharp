using System.Collections.ObjectModel;
using GameOfCardsCsharp.PreferanceBots.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using GameOfCardsCsharp.Preferance.GamePlay;
using GameOfCardsCsharp.Preferance;
using GameOfCardsCsharp.Preferance.Sans;
using GameOfCardsCsharp.Preferance.Trump;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.PreferanceBots
{
    public partial class PreferancePage : ContentPage
    {
        // ==================== PLAYER IDS ====================
        private const int BOTTOM_PLAYER_ID = 0; // Human player
        private const int LEFT_PLAYER_ID = 1;   // AI player
        private const int RIGHT_PLAYER_ID = 2;  // AI player

        // ==================== GAME ENGINE ====================
        private GamePlayEngine? _gamePlayEngine;
        private Dictionary<int, PreferancePlayer> _players;

        // ==================== GAME STATE ====================
        private List<Card> talonCards = new(); // 2 extra cards for display only
        private int totalTricks = 10;

        // ==================== GAME SETTINGS ====================
        private int cardsPerPlayer = 10;
        private bool isPerfectPlayMode = true; // Default: all cards visible
        private PlayerRole bottomPlayerRole = PlayerRole.Declarer;
        private PlayerRole leftPlayerRole = PlayerRole.Spectator;
        private PlayerRole rightPlayerRole = PlayerRole.Defender;

        // ==================== CAPTURED STATE FOR PERFECT PLAY ====================
        private PerfPerfectGameState? _currentTrickGameState;
        private int _currentTrickDeclarerIndex;

        // ==================== UI COLLECTIONS ====================
        private ObservableCollection<PreferanceCardViewModel> bottomHandDisplay = new();
        private ObservableCollection<PreferanceCardViewModel> leftHandDisplay = new();
        private ObservableCollection<PreferanceCardViewModel> rightHandDisplay = new();

        // ==================== LOG ====================
        private List<string> logMessages = new();

        public PreferancePage()
        {
            InitializeComponent();

            // Bottom player: CollectionView - use ItemsSource property
            BottomHandCollection.ItemsSource = bottomHandDisplay;
            
            // Left & Right players: VerticalStackLayout - use BindableLayout
            BindableLayout.SetItemsSource(LeftHandCollection, leftHandDisplay);
            BindableLayout.SetItemsSource(RightHandCollection, rightHandDisplay);

            // Initialize players
            _players = new Dictionary<int, PreferancePlayer>
            {
                { BOTTOM_PLAYER_ID, new PreferancePlayer(BOTTOM_PLAYER_ID) },
                { LEFT_PLAYER_ID, new PreferancePlayer(LEFT_PLAYER_ID) },
                { RIGHT_PLAYER_ID, new PreferancePlayer(RIGHT_PLAYER_ID) }
            };

            // Ensure default selections are set
            ContractTypePicker.SelectedIndex = 2;  // Sans
            TrumpSuitPicker.SelectedIndex = 0;     // Spades
            BottomRolePicker.SelectedIndex = 0;    // Declarer
            LeftRolePicker.SelectedIndex = 3;      // Spectator
            RightRolePicker.SelectedIndex = 1;     // Defender
            CardsPerPlayerSlider.Value = 10;       // 10 cards
            VisibilityModePicker.SelectedIndex = 0; // Perfect Play

            LogMessage("Welcome to Preferance! Configure settings and click 'Start'.");
        }

        // ==================== EVENT HANDLERS ====================

        private void OnStartClicked(object sender, EventArgs e)
        {
            StartNewGame();
        }

        private void OnNextClicked(object sender, EventArgs e)
        {
            if (_gamePlayEngine == null || !_gamePlayEngine.IsWaitingForCard())
                return;

            var currentPlayerId = _gamePlayEngine.GetCurrentPlayerTurn();

            // If it's human player's turn, they need to click a card
            if (currentPlayerId == BOTTOM_PLAYER_ID)
            {
                LogMessage("⚠️ Click a card to play!");
                return;
            }

            // AI player - use perfect play or random card
            PlayAICard(currentPlayerId);
        }

        private void OnBottomPlayerCardTapped(object sender, TappedEventArgs e)
        {
            if (_gamePlayEngine == null || !_gamePlayEngine.IsWaitingForCard())
                return;

            var currentPlayerId = _gamePlayEngine.GetCurrentPlayerTurn();
            if (currentPlayerId != BOTTOM_PLAYER_ID)
            {
                LogMessage("⚠️ Not your turn!");
                return;
            }

            if (e.Parameter is PreferanceCardViewModel cardViewModel)
            {
                PlayHumanCard(cardViewModel.Card);
            }
        }

        private void OnCardsPerPlayerChanged(object sender, ValueChangedEventArgs e)
        {
            cardsPerPlayer = (int)e.NewValue;
            CardsPerPlayerLabel.Text = $"{cardsPerPlayer} card{(cardsPerPlayer == 1 ? "" : "s")}";
            totalTricks = cardsPerPlayer;

            if (_gamePlayEngine?.IsGameInProgress() == true)
            {
                LogMessage($"⚙️ Cards changed to {cardsPerPlayer}. Start new game to apply.");
            }
        }

        private void OnVisibilityModeChanged(object sender, EventArgs e)
        {
            isPerfectPlayMode = VisibilityModePicker.SelectedIndex == 0;
            
            if (_gamePlayEngine?.IsGameInProgress() == true)
            {
                LogMessage($"Mode: {(isPerfectPlayMode ? "Perfect Play (all visible)" : "Regular (opponents hidden)")}");
                UpdateAllHandDisplays();
            }
        }

        // ==================== GAME FLOW ====================

        private void StartNewGame()
        {
            // Validate roles
            if (!ValidateRoles())
            {
                DisplayAlert("Invalid Roles", 
                    "Invalid role configuration!\n\n" +
                    "Rules:\n" +
                    "- Exactly one Declarer required\n" +
                    "- At least one Defender required\n" +
                    "- Valid: 1 Declarer + 2 Defenders\n" +
                    "- Valid: 1 Declarer + 1 Defender + 1 Spectator\n" +
                    "- Valid: 1 Declarer + 1 Defender + 1 Partner", 
                    "OK");
                return;
            }

            // Unsubscribe from old engine events if exists
            if (_gamePlayEngine != null)
            {
                _gamePlayEngine.GamePlayStarted -= OnGamePlayStarted;
                _gamePlayEngine.TrickStarted -= OnTrickStarted;
                _gamePlayEngine.PlayerTurnToPlay -= OnPlayerTurnToPlay;
                _gamePlayEngine.CardPlayed -= OnCardPlayed;
                _gamePlayEngine.TrickCompleted -= OnTrickCompleted;
                _gamePlayEngine.GamePlayCompleted -= OnGamePlayCompleted;
                _gamePlayEngine.Error -= OnGamePlayError;
            }

            // Reset game state
            ResetGame();

            // Deal cards to players
            DealCards();

            // Update UI
            UpdateAllHandDisplays();
            UpdateRoleLabels();
            UpdateTalonDisplay();

            // Create role assignment result
            var roles = CreateRoleAssignmentResult();

            // Create and initialize NEW engine
            _gamePlayEngine = new GamePlayEngine(_players);

            // Subscribe to events
            _gamePlayEngine.GamePlayStarted += OnGamePlayStarted;
            _gamePlayEngine.TrickStarted += OnTrickStarted;
            _gamePlayEngine.PlayerTurnToPlay += OnPlayerTurnToPlay;
            _gamePlayEngine.CardPlayed += OnCardPlayed;
            _gamePlayEngine.TrickCompleted += OnTrickCompleted;
            _gamePlayEngine.GamePlayCompleted += OnGamePlayCompleted;
            _gamePlayEngine.Error += OnGamePlayError;

            // Start the game
            var result = _gamePlayEngine.StartGame(roles);
            
            if (!result.Success)
            {
                LogMessage($"❌ Start failed: {result.Message}");
                DisplayAlert("Error", result.Message, "OK");
                return;
            }

            // Enable Next button
            NextButton.IsEnabled = true;
            StartButton.IsEnabled = false;

            LogMessage("═══════════════════════════════════");
            LogMessage($"🎮 Game Started! {cardsPerPlayer} cards per player");
            LogMessage($"Contract: {GetContractTypeText()}");
            LogMessage($"Roles: {GetRolesText()}");
            LogMessage($"Declarer leads first trick");
            LogMessage("═══════════════════════════════════");
        }

        private void PlayHumanCard(Card card)
        {
            if (_gamePlayEngine == null)
                return;

            var move = new PreferanceMove
            {
                PlayerId = BOTTOM_PLAYER_ID,
                Card = card
            };

            var result = _gamePlayEngine.SubmitMove(move);
            
            if (!result.Success)
            {
                LogMessage($"❌ {result.Message}");
            }
        }

        private void PlayAICard(int playerId)
        {
            if (_gamePlayEngine == null)
                return;

            var legalMoves = _gamePlayEngine.GetLegalMoves(playerId);
            
            if (legalMoves.Count == 0)
            {
                LogMessage($"⚠️ No legal moves for Player {playerId}");
                return;
            }

            Card selectedCard;

            // Try to use perfect play
            if (CanUsePerfectPlay())
            {
                try
                {
                    selectedCard = GetPerfectPlayCard(playerId);
                    LogMessage($"🧠 {GetPlayerName(playerId)} uses perfect play");
                }
                catch (Exception ex)
                {
                    LogMessage($"⚠️ Perfect play error: {ex.Message}");
                    LogMessage($"🎲 {GetPlayerName(playerId)} falls back to random play");
                    selectedCard = GetRandomCard(legalMoves);
                }
            }
            else
            {
                // Fallback: random card selection
                selectedCard = GetRandomCard(legalMoves);
            }

            var move = new PreferanceMove
            {
                PlayerId = playerId,
                Card = selectedCard
            };

            var result = _gamePlayEngine.SubmitMove(move);
            
            if (!result.Success)
            {
                LogMessage($"❌ AI error: {result.Message}");
            }
        }

        private void ResetGame()
        {
            talonCards.Clear();
            bottomHandDisplay.Clear();
            leftHandDisplay.Clear();
            rightHandDisplay.Clear();

            foreach (var player in _players.Values)
            {
                player.Reset();
            }

            ClearPlayedCards();
            UpdateTrickCountsDisplay(new Dictionary<int, int>
            {
                { BOTTOM_PLAYER_ID, 0 },
                { LEFT_PLAYER_ID, 0 },
                { RIGHT_PLAYER_ID, 0 }
            });

            logMessages.Clear();
            GameLogLabel.Text = "";
            
            // Clear captured state
            _currentTrickGameState = null;
            _currentTrickDeclarerIndex = 0;
        }

        private void DealCards()
        {
            var deck = new ShortDeck();
            deck.Shuffle();

            foreach (var player in _players.Values)
            {
                player.GetHand().Clear();
                for (int i = 0; i < cardsPerPlayer; i++)
                {
                    if (!deck.IsEmpty())
                    {
                        player.GetHand().AddCard(deck.DrawCard());
                    }
                }
                player.GetHand().SortBySuitThenRankDescending();
            }

            // Remaining 2 cards to talon (display only)
            if (!deck.IsEmpty()) talonCards.Add(deck.DrawCard());
            if (!deck.IsEmpty()) talonCards.Add(deck.DrawCard());

            LogMessage($"Cards dealt: {cardsPerPlayer} per player, 2 in talon");
        }

        private RoleAssignmentResult CreateRoleAssignmentResult()
        {
            var declarerId = GetDeclarerPlayerId();
            var defenderIds = new List<int>();
            var spectatorIds = new List<int>();
            int? partnerId = null;

            for (int i = 0; i < 3; i++)
            {
                var role = GetRoleForPlayer(i);
                if (role == PlayerRole.Defender)
                    defenderIds.Add(i);
                else if (role == PlayerRole.Spectator)
                    spectatorIds.Add(i);
                else if (role == PlayerRole.Partner)
                    partnerId = i;
            }

            var contract = GetContractType();
            var trumpSuit = GetTrumpSuitEnum();

            return new RoleAssignmentResult(
                declarerId,
                contract,
                trumpSuit,
                defenderIds,
                spectatorIds,
                partnerId
            );
        }

        // ==================== ENGINE EVENT HANDLERS ====================

        private void OnGamePlayStarted(object? sender, GamePlayStartedEventArgs e)
        {
            LogMessage("🎮 Game play started!");
        }

        private void OnTrickStarted(object? sender, TrickStartedEventArgs e)
        {
            // Capture game state BEFORE any cards are played in this trick
            _currentTrickGameState = CaptureCurrentGameState();
            _currentTrickDeclarerIndex = GetDeclarerMappedIndex();
            
            ClearPlayedCards();
            
            TrickStatusLabel.Text = $"Trick {e.TrickNumber} / {totalTricks}";
            
            var leaderName = GetPlayerName(e.LeaderId);
            LogMessage($"─────────────────────────────────");
            LogMessage($"Trick {e.TrickNumber} - {leaderName} leads");
        }

        private void OnPlayerTurnToPlay(object? sender, PlayerTurnToPlayEventArgs e)
        {
            var playerName = GetPlayerName(e.PlayerId);
            CurrentTurnLabel.Text = $"Current turn: {playerName}";
            
            if (e.PlayerId == BOTTOM_PLAYER_ID)
            {
                LogMessage($"► Your turn! Click a card to play. ({e.LegalMoves.Count} legal moves)");
            }
            else
            {
                LogMessage($"🤖 {playerName}'s turn. Click 'Next' to make AI play.");
            }
        }

        private void OnCardPlayed(object? sender, Preferance.GamePlay.CardPlayedEventArgs e)
        {
            var playerName = GetPlayerName(e.PlayerId);
            var cardText = GetCardText(e.Card);
            
            LogMessage($"✓ {playerName} plays {cardText}");
            
            // Display card in play area
            DisplayPlayedCard(e.PlayerId, e.Card);
            
            // Update hand display
            UpdateHandDisplay(e.PlayerId);
        }

        private void OnTrickCompleted(object? sender, TrickCompletedEventArgs e)
        {
            var winnerName = GetPlayerName(e.WinnerId);
            LogMessage($"🏆 {winnerName} wins trick {e.TrickNumber}!");
            
            // Highlight winner's card
            HighlightWinningCard(e.WinnerId);
            
            // Update trick counts
            if (_gamePlayEngine != null)
            {
                var counts = _gamePlayEngine.GetCurrentTrickCounts();
                UpdateTrickCountsDisplay(counts);
            }
        }

        private void OnGamePlayCompleted(object? sender, GamePlayCompletedEventArgs e)
        {
            NextButton.IsEnabled = false;
            StartButton.IsEnabled = true;
            
            LogMessage("═══════════════════════════════════");
            LogMessage("🎉 GAME COMPLETE!");
            
            // Update final trick counts
            UpdateTrickCountsDisplay(e.Result.ActualTricksByPlayer);
            
            // Show results
            foreach (var kvp in e.Result.ActualTricksByPlayer.OrderBy(x => x.Key))
            {
                var name = GetPlayerName(kvp.Key);
                LogMessage($"{name}: {kvp.Value} tricks");
            }
            
            LogMessage("═══════════════════════════════════");
            
            var result = e.Result.DeclarerWon ? "WON" : "LOST";
            LogMessage($"Declarer {result} the {e.Result.Contract} contract!");
        }

        private void OnGamePlayError(object? sender, GamePlayErrorEventArgs e)
        {
            LogMessage($"❌ Error: {e.ErrorMessage}");
        }

        // ==================== UI UPDATES ====================

        private void DisplayPlayedCard(int playerId, Card card)
        {
            var suitSymbol = card.Suit switch
            {
                Suit.Hearts => "♥",
                Suit.Diamonds => "♦",
                Suit.Clubs => "♣",
                Suit.Spades => "♠",
                _ => ""
            };

            var rankText = card.Rank switch
            {
                Rank.Seven => "7",
                Rank.Eight => "8",
                Rank.Nine => "9",
                Rank.Ten => "10",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                Rank.Ace => "A",
                _ => "?"
            };

            var color = (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds) 
                ? Color.FromArgb("#DC143C") 
                : Colors.Black;

            var displayText = $"{rankText}\n{suitSymbol}";

            switch (playerId)
            {
                case BOTTOM_PLAYER_ID:
                    BottomPlayedCardLabel.Text = displayText;
                    BottomPlayedCardLabel.TextColor = color;
                    BottomPlayedCardBorder.Stroke = Colors.Blue;
                    BottomPlayedCardBorder.IsVisible = true;
                    break;
                case LEFT_PLAYER_ID:
                    LeftPlayedCardLabel.Text = displayText;
                    LeftPlayedCardLabel.TextColor = color;
                    LeftPlayedCardBorder.Stroke = Colors.Gray;
                    LeftPlayedCardBorder.IsVisible = true;
                    break;
                case RIGHT_PLAYER_ID:
                    RightPlayedCardLabel.Text = displayText;
                    RightPlayedCardLabel.TextColor = color;
                    RightPlayedCardBorder.Stroke = Colors.Gray;
                    RightPlayedCardBorder.IsVisible = true;
                    break;
            }
        }

        private void HighlightWinningCard(int winnerId)
        {
            var goldColor = Color.FromArgb("#FFD700");
            switch (winnerId)
            {
                case BOTTOM_PLAYER_ID:
                    BottomPlayedCardBorder.Stroke = goldColor;
                    BottomPlayedCardBorder.StrokeThickness = 4;
                    break;
                case LEFT_PLAYER_ID:
                    LeftPlayedCardBorder.Stroke = goldColor;
                    LeftPlayedCardBorder.StrokeThickness = 4;
                    break;
                case RIGHT_PLAYER_ID:
                    RightPlayedCardBorder.Stroke = goldColor;
                    RightPlayedCardBorder.StrokeThickness = 4;
                    break;
            }
        }

        private void ClearPlayedCards()
        {
            BottomPlayedCardBorder.IsVisible = false;
            LeftPlayedCardBorder.IsVisible = false;
            RightPlayedCardBorder.IsVisible = false;
            
            BottomPlayedCardBorder.StrokeThickness = 2;
            LeftPlayedCardBorder.StrokeThickness = 2;
            RightPlayedCardBorder.StrokeThickness = 2;
        }

        private void UpdateAllHandDisplays()
        {
            UpdateHandDisplay(BOTTOM_PLAYER_ID);
            UpdateHandDisplay(LEFT_PLAYER_ID);
            UpdateHandDisplay(RIGHT_PLAYER_ID);
        }

        private void UpdateHandDisplay(int playerId)
        {
            ObservableCollection<PreferanceCardViewModel> display;
            List<Card> hand;

            switch (playerId)
            {
                case BOTTOM_PLAYER_ID:
                    display = bottomHandDisplay;
                    hand = _players[BOTTOM_PLAYER_ID].GetHand().GetCards().ToList();
                    break;
                case LEFT_PLAYER_ID:
                    display = leftHandDisplay;
                    hand = _players[LEFT_PLAYER_ID].GetHand().GetCards().ToList();
                    break;
                case RIGHT_PLAYER_ID:
                    display = rightHandDisplay;
                    hand = _players[RIGHT_PLAYER_ID].GetHand().GetCards().ToList();
                    break;
                default:
                    return;
            }

            display.Clear();

            bool isBottomPlayer = playerId == BOTTOM_PLAYER_ID;
            bool showCards = isBottomPlayer || isPerfectPlayMode;

            foreach (var card in hand)
            {
                display.Add(PreferanceCardViewModel.FromCard(card, showCards));
            }
        }

        private void UpdateRoleLabels()
        {
            BottomPlayerRoleLabel.Text = $"({bottomPlayerRole})";
            LeftPlayerRoleLabel.Text = $"({leftPlayerRole})";
            RightPlayerRoleLabel.Text = $"({rightPlayerRole})";
        }

        private void UpdateTrickCountsDisplay(Dictionary<int, int> trickCounts)
        {
            BottomPlayerTricksLabel.Text = $"Tricks: {trickCounts.GetValueOrDefault(BOTTOM_PLAYER_ID, 0)}";
            LeftPlayerTricksLabel.Text = $"Tricks: {trickCounts.GetValueOrDefault(LEFT_PLAYER_ID, 0)}";
            RightPlayerTricksLabel.Text = $"Tricks: {trickCounts.GetValueOrDefault(RIGHT_PLAYER_ID, 0)}";

            BottomTrickCountLabel.Text = $"{trickCounts.GetValueOrDefault(BOTTOM_PLAYER_ID, 0)} tricks";
            LeftTrickCountLabel.Text = $"{trickCounts.GetValueOrDefault(LEFT_PLAYER_ID, 0)} tricks";
            RightTrickCountLabel.Text = $"{trickCounts.GetValueOrDefault(RIGHT_PLAYER_ID, 0)} tricks";
        }

        private void UpdateTalonDisplay()
        {
            TalonCardsStack.Children.Clear();

            bool showTalon = isPerfectPlayMode;

            foreach (var card in talonCards)
            {
                var cardBorder = new Border
                {
                    BackgroundColor = showTalon ? Colors.White : Color.FromArgb("#DDD"),
                    Stroke = Color.FromArgb("#888"),
                    StrokeThickness = 2,
                    WidthRequest = 55,
                    HeightRequest = 75,
                    Margin = new Thickness(2)
                };

                var label = new Label
                {
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                if (showTalon)
                {
                    var suitSymbol = card.Suit switch
                    {
                        Suit.Hearts => "♥",
                        Suit.Diamonds => "♦",
                        Suit.Clubs => "♣",
                        Suit.Spades => "♠",
                        _ => ""
                    };

                    var rankText = card.Rank switch
                    {
                        Rank.Seven => "7",
                        Rank.Eight => "8",
                        Rank.Nine => "9",
                        Rank.Ten => "10",
                        Rank.Jack => "J",
                        Rank.Queen => "Q",
                        Rank.King => "K",
                        Rank.Ace => "A",
                        _ => "?"
                    };

                    var color = (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds) 
                        ? Color.FromArgb("#DC143C") 
                        : Colors.Black;

                    label.Text = $"{rankText}\n{suitSymbol}";
                    label.TextColor = color;
                }
                else
                {
                    label.Text = "🂠";
                    label.TextColor = Color.FromArgb("#444");
                }

                cardBorder.Content = label;
                TalonCardsStack.Children.Add(cardBorder);
            }
        }

        // ==================== ROLE MANAGEMENT ====================

        private bool ValidateRoles()
        {
            bottomPlayerRole = GetRoleFromPickerIndex(BottomRolePicker.SelectedIndex);
            leftPlayerRole = GetRoleFromPickerIndex(LeftRolePicker.SelectedIndex);
            rightPlayerRole = GetRoleFromPickerIndex(RightRolePicker.SelectedIndex);

            var roles = new[] { bottomPlayerRole, leftPlayerRole, rightPlayerRole };

            if (roles.Count(r => r == PlayerRole.Declarer) != 1)
                return false;

            if (roles.Count(r => r == PlayerRole.Defender) < 1)
                return false;

            return true;
        }

        private PlayerRole GetRoleFromPickerIndex(int index)
        {
            return index switch
            {
                0 => PlayerRole.Declarer,
                1 => PlayerRole.Defender,
                2 => PlayerRole.Partner,
                3 => PlayerRole.Spectator,
                _ => PlayerRole.Spectator
            };
        }

        private PlayerRole GetRoleForPlayer(int playerId)
        {
            return playerId switch
            {
                BOTTOM_PLAYER_ID => bottomPlayerRole,
                LEFT_PLAYER_ID => leftPlayerRole,
                RIGHT_PLAYER_ID => rightPlayerRole,
                _ => PlayerRole.Spectator
            };
        }

        private int GetDeclarerPlayerId()
        {
            if (bottomPlayerRole == PlayerRole.Declarer) return BOTTOM_PLAYER_ID;
            if (leftPlayerRole == PlayerRole.Declarer) return LEFT_PLAYER_ID;
            if (rightPlayerRole == PlayerRole.Declarer) return RIGHT_PLAYER_ID;
            return BOTTOM_PLAYER_ID;
        }

        private ContractType GetContractType()
        {
            return ContractTypePicker.SelectedIndex switch
            {
                0 => ContractType.Trump,
                1 => ContractType.Betl,
                2 => ContractType.Sans,
                _ => ContractType.Sans
            };
        }

        private TrumpSuit? GetTrumpSuitEnum()
        {
            if (GetContractType() != ContractType.Trump)
                return null;

            return TrumpSuitPicker.SelectedIndex switch
            {
                0 => TrumpSuit.Spades,
                1 => TrumpSuit.Diamonds,
                2 => TrumpSuit.Hearts,
                3 => TrumpSuit.Clubs,
                _ => TrumpSuit.Spades
            };
        }

        private string GetRolesText()
        {
            return $"You: {bottomPlayerRole}, Left: {leftPlayerRole}, Right: {rightPlayerRole}";
        }

        private string GetPlayerName(int playerId)
        {
            return playerId switch
            {
                BOTTOM_PLAYER_ID => "You (Bottom)",
                LEFT_PLAYER_ID => "Left Player",
                RIGHT_PLAYER_ID => "Right Player",
                _ => "Unknown"
            };
        }

        private string GetContractTypeText()
        {
            var contract = GetContractType().ToString();
            if (GetContractType() == ContractType.Trump)
            {
                var suit = GetTrumpSuitEnum()?.ToString() ?? "Spades";
                return $"{contract} ({suit})";
            }
            return contract;
        }

        private string GetCardText(Card card)
        {
            var suit = card.Suit switch
            {
                Suit.Hearts => "♥",
                Suit.Diamonds => "♦",
                Suit.Clubs => "♣",
                Suit.Spades => "♠",
                _ => ""
            };

            var rank = card.Rank switch
            {
                Rank.Seven => "7",
                Rank.Eight => "8",
                Rank.Nine => "9",
                Rank.Ten => "10",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                Rank.Ace => "A",
                _ => "?"
            };

            return $"{rank}{suit}";
        }

        // ==================== LOGGING ====================

        private void LogMessage(string message)
        {
            logMessages.Add(message);
            GameLogLabel.Text = string.Join("\n", logMessages);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(50);
                await LogScrollView.ScrollToAsync(GameLogLabel, ScrollToPosition.End, false);
            });
        }

        // ==================== PERFECT PLAY HELPERS ====================

        /// <summary>
        /// Checks if perfect play can be used (2-player Sans/Trump or 3-player Trump game)
        /// </summary>
        private bool CanUsePerfectPlay()
        {
            if (_gamePlayEngine == null || !_gamePlayEngine.IsGameInProgress())
                return false;

            var contractType = GetContractType();
            var activePlayers = GetActivePlayerIds();

            // 2-player Sans or Trump
            if (activePlayers.Count == 2 && (contractType == ContractType.Sans || contractType == ContractType.Trump))
                return true;

            // 3-player Trump
            if (activePlayers.Count == 3 && contractType == ContractType.Trump)
                return true;

            return false;
        }

        /// <summary>
        /// Gets the optimal card using Sans2PerfectGame, Trump2PerfectGame, or Trump3PerfectGame
        /// </summary>
        private Card GetPerfectPlayCard(int playerId)
        {
            if (_gamePlayEngine == null)
                throw new InvalidOperationException("Game engine not initialized");

            var contractType = GetContractType();
            var activePlayers = GetActivePlayerIds();

            // Route to appropriate perfect game implementation
            if (contractType == ContractType.Sans && activePlayers.Count == 2)
            {
                return GetSansPerfectPlayCard(playerId);
            }
            else if (contractType == ContractType.Trump && activePlayers.Count == 2)
            {
                return GetTrumpPerfectPlayCard(playerId);
            }
            else if (contractType == ContractType.Trump && activePlayers.Count == 3)
            {
                return GetTrump3PerfectPlayCard(playerId);
            }
            else
            {
                throw new InvalidOperationException($"Perfect play not supported for {contractType} with {activePlayers.Count} players");
            }
        }

        /// <summary>
        /// Gets the optimal card for Sans games using Sans2PerfectGame
        /// </summary>
        private Card GetSansPerfectPlayCard(int playerId)
        {
            if (_gamePlayEngine == null)
                throw new InvalidOperationException("Game engine not initialized");
            
            if (_currentTrickGameState == null)
                throw new InvalidOperationException("Game state not available. Cannot use perfect play.");

            var activePlayers = GetActivePlayerIds();
            var tableCards = _gamePlayEngine.GetTableCards();
            
            var playerIdMap = new Dictionary<int, int>();
            for (int i = 0; i < activePlayers.Count; i++)
            {
                playerIdMap[activePlayers[i]] = i;
            }

            // Clone captured state
            var gameState = CloneGameState(_currentTrickGameState);
            gameState.CurrentPlayerIndex = playerIdMap[playerId];
            
            // Mark table cards as played
            foreach (var tableCard in tableCards)
            {
                MarkCardAsPlayed(gameState, tableCard);
            }
            
            var sansGame = new Sans2PerfectGame(gameState);

            PerfectCardMove bestMove;
            if (tableCards.Count == 0)
            {
                var bestMoves = sansGame.BestLeadCard();
                bestMove = bestMoves[0];
            }
            else
            {
                var leadMove = FindCardInGameState(gameState, tableCards[0]);
                bestMove = sansGame.BestFollowCard(leadMove);
            }

            return bestMove.Card;
        }

        /// <summary>
        /// Gets the optimal card for 2-player Trump games using Trump2PerfectGame
        /// </summary>
        private Card GetTrumpPerfectPlayCard(int playerId)
        {
            if (_gamePlayEngine == null)
                throw new InvalidOperationException("Game engine not initialized");
            
            if (_currentTrickGameState == null)
                throw new InvalidOperationException("Game state not available. Cannot use perfect play.");

            var trumpSuit = GetTrumpSuitEnum();
            if (trumpSuit == null)
                throw new InvalidOperationException("Trump suit not specified for Trump game");

            var activePlayers = GetActivePlayerIds();
            var tableCards = _gamePlayEngine.GetTableCards();
            
            var playerIdMap = new Dictionary<int, int>();
            for (int i = 0; i < activePlayers.Count; i++)
            {
                playerIdMap[activePlayers[i]] = i;
            }

            // Clone captured state
            var gameState = CloneGameState(_currentTrickGameState);
            gameState.CurrentPlayerIndex = playerIdMap[playerId];
            
            // Mark table cards as played
            foreach (var tableCard in tableCards)
            {
                MarkCardAsPlayed(gameState, tableCard);
            }
            
            var trumpGame = new Trump2PerfectGame(gameState);

            PerfectCardMove bestMove;
            if (tableCards.Count == 0)
            {
                var bestMoves = trumpGame.BestLeadCard();
                bestMove = bestMoves[0];
            }
            else
            {
                var leadMove = FindCardInGameState(gameState, tableCards[0]);
                bestMove = trumpGame.BestFollowCard(leadMove);
            }

            return bestMove.Card;
        }

        /// <summary>
        /// Gets the optimal card for 3-player Trump games using Trump3PerfectGame
        /// </summary>
        private Card GetTrump3PerfectPlayCard(int playerId)
        {
            if (_gamePlayEngine == null)
                throw new InvalidOperationException("Game engine not initialized");
            
            if (_currentTrickGameState == null)
                throw new InvalidOperationException("Game state not available. Cannot use perfect play.");

            var trumpSuit = GetTrumpSuitEnum();
            if (trumpSuit == null)
                throw new InvalidOperationException("Trump suit not specified for Trump game");

            var activePlayers = GetActivePlayerIds();
            var tableCards = _gamePlayEngine.GetTableCards();
            
            if (activePlayers.Count != 3)
                throw new InvalidOperationException($"Trump3PerfectGame requires 3 active players, but found {activePlayers.Count}");

            var playerIdMap = new Dictionary<int, int>();
            for (int i = 0; i < activePlayers.Count; i++)
            {
                playerIdMap[activePlayers[i]] = i;
            }

            // Clone captured state
            var gameState = CloneGameState(_currentTrickGameState);
            gameState.CurrentPlayerIndex = playerIdMap[playerId];
            
            // Mark table cards as played
            foreach (var tableCard in tableCards)
            {
                MarkCardAsPlayed(gameState, tableCard);
            }

            var trumpGame = new Trump3PerfectGame(gameState, _currentTrickDeclarerIndex);
            
            PerfectCardMove bestMove;
            if (tableCards.Count == 0)
            {
                var bestMoves = trumpGame.BestLeadCard();
                bestMove = bestMoves[0];
            }
            else if (tableCards.Count == 1)
            {
                var leadMove = FindCardInGameState(gameState, tableCards[0]);
                bestMove = trumpGame.BestFollowCard(leadMove, null);
            }
            else
            {
                var leadMove = FindCardInGameState(gameState, tableCards[0]);
                var firstFollowMove = FindCardInGameState(gameState, tableCards[1]);
                bestMove = trumpGame.BestFollowCard(leadMove, firstFollowMove);
            }

            return bestMove.Card;
        }

        /// <summary>
        /// Captures the current game state at the start of a trick (before any cards played)
        /// </summary>
        private PerfPerfectGameState CaptureCurrentGameState()
        {
            var contractType = GetContractType();
            var trumpSuit = GetTrumpSuitEnum() ?? TrumpSuit.None;
            var activePlayers = GetActivePlayerIds();
            
            var gameMode = contractType switch
            {
                ContractType.Sans => PreferanceGameMode.Sans,
                ContractType.Trump => PreferanceGameMode.Trump,
                ContractType.Betl => PreferanceGameMode.Betl,
                _ => PreferanceGameMode.Sans
            };

            var playerNames = activePlayers.Count == 2 
                ? new List<string> { "Player0", "Player1" }
                : new List<string> { "Player0", "Player1", "Player2" };
            
            var gameState = new PerfPerfectGameState(
                gameMode,
                playerNames,
                trumpSuit,
                0,
                0
            );

            // Capture all players' hands as they are RIGHT NOW (at trick start)
            var playerHands = new Dictionary<int, List<Card>>();
            for (int i = 0; i < activePlayers.Count; i++)
            {
                var playerId = activePlayers[i];
                var hand = _players[playerId].GetHand().GetCards().ToList();
                playerHands[i] = hand;
            }
            
            gameState.SetupPlayerHands(playerHands);
            
            return gameState;
        }

        /// <summary>
        /// Gets the declarer's mapped index (0-2) in the active players list
        /// </summary>
        private int GetDeclarerMappedIndex()
        {
            var activePlayers = GetActivePlayerIds();
            var declarerPlayerId = GetDeclarerPlayerId();
            
            for (int i = 0; i < activePlayers.Count; i++)
            {
                if (activePlayers[i] == declarerPlayerId)
                    return i;
            }
            
            return 0;
        }

        /// <summary>
        /// Clones a game state for analysis without mutating the original
        /// </summary>
        private PerfPerfectGameState CloneGameState(PerfPerfectGameState original)
        {
            var clone = new PerfPerfectGameState(
                original.GameMode,
                new List<string>(original.Players),
                original.TrumpSuit,
                original.CurrentPlayerIndex,
                original.LeaderPlayerIndex
            );

            // Rebuild hands from available moves
            var playerHands = new Dictionary<int, List<Card>>();
            
            for (int playerIdx = 0; playerIdx < original.Players.Count; playerIdx++)
            {
                playerHands[playerIdx] = new List<Card>();
            }
            
            // Collect all available cards
            for (int suitIdx = 0; suitIdx < 4; suitIdx++)
            {
                foreach (var move in original.Moves[suitIdx])
                {
                    if (move.Available)
                    {
                        playerHands[move.PlayerIndex].Add(move.Card);
                    }
                }
            }

            clone.SetupPlayerHands(playerHands);
            
            return clone;
        }

        /// <summary>
        /// Marks a card as played (unavailable) in the game state
        /// </summary>
        private void MarkCardAsPlayed(PerfPerfectGameState gameState, Card card)
        {
            var suitMoves = gameState.Moves[(int)card.Suit];
            for (int i = 0; i < suitMoves.Count; i++)
            {
                if (suitMoves[i].Card.Suit == card.Suit && suitMoves[i].Card.Rank == card.Rank && suitMoves[i].Available)
                {
                    gameState.Moves[(int)card.Suit][i].Available = false;
                    return;
                }
            }
        }

        /// <summary>
        /// Finds a card in the game state
        /// </summary>
        private PerfectCardMove FindCardInGameState(PerfPerfectGameState gameState, Card card)
        {
            var suitMoves = gameState.Moves[(int)card.Suit];
            var move = suitMoves.FirstOrDefault(m => 
                m.Card.Suit == card.Suit && 
                m.Card.Rank == card.Rank);

            if (move.Card == null)
                throw new InvalidOperationException($"Could not find card {GetCardText(card)} in game state");

            return move;
        }

        /// <summary>
        /// Selects a random card from legal moves
        /// </summary>
        private Card GetRandomCard(List<Card> legalMoves)
        {
            var random = new Random();
            return legalMoves[random.Next(legalMoves.Count)];
        }

        private List<int> GetActivePlayerIds()
        {
            var activeIds = new List<int>();

            for (int i = 0; i < 3; i++)
            {
                var role = GetRoleForPlayer(i);
                if (role != PlayerRole.Spectator)
                {
                    activeIds.Add(i);
                }
            }

            return activeIds;
        }

        // ==================== ANALYZE MODE ====================

        private void OnAnalyzeClicked(object sender, EventArgs e)
        {
            AnalysisResultsPanel.IsVisible = true;
            AnalysisRecommendedCardLabel.IsVisible = false;
            AnalysisExpectedTricksLabel.IsVisible = false;
            AnalysisPerformanceLabel.IsVisible = false;

            if (_gamePlayEngine == null || !_gamePlayEngine.IsGameInProgress())
            {
                AnalysisStatusLabel.Text = "⚠️ Game not started. Please start a game first.";
                return;
            }

            if (!CanUsePerfectPlay())
            {
                var contractType = GetContractType();
                var activePlayers = GetActivePlayerIds();

                if (contractType != ContractType.Sans && contractType != ContractType.Trump)
                {
                    AnalysisStatusLabel.Text = $"⚠️ Analysis is currently only supported for Sans and Trump games.";
                }
                else
                {
                    AnalysisStatusLabel.Text = $"⚠️ Perfect play supports:\n- 2-player Sans/Trump\n- 3-player Trump\n\nCurrent: {activePlayers.Count}-player {contractType}";
                }
                return;
            }

            try
            {
                var contractType = GetContractType();
                var activePlayers = GetActivePlayerIds();
                AnalysisResult analysisResult;

                // Start timing
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                if (contractType == ContractType.Sans && activePlayers.Count == 2)
                {
                    analysisResult = PerformSans2Analysis();
                }
                else if (contractType == ContractType.Trump && activePlayers.Count == 2)
                {
                    analysisResult = PerformTrump2Analysis();
                }
                else if (contractType == ContractType.Trump && activePlayers.Count == 3)
                {
                    analysisResult = PerformTrump3Analysis();
                }
                else
                {
                    AnalysisStatusLabel.Text = $"⚠️ Unsupported configuration: {contractType} with {activePlayers.Count} players";
                    return;
                }

                // Stop timing
                stopwatch.Stop();
                analysisResult.ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

                DisplayAnalysisResult(analysisResult);
            }
            catch (Exception ex)
            {
                AnalysisStatusLabel.Text = $"❌ Error during analysis:\n{ex.Message}";
            }
        }

        private AnalysisResult PerformSans2Analysis()
        {
            if (_gamePlayEngine == null)
                throw new InvalidOperationException("Game engine not initialized");
            
            if (_currentTrickGameState == null)
                throw new InvalidOperationException("Game state not captured. Analysis only works during active tricks.");

            var activePlayers = GetActivePlayerIds();
            var currentPlayerId = _gamePlayEngine.GetCurrentPlayerTurn();
            var tableCards = _gamePlayEngine.GetTableCards();

            var playerIdMap = new Dictionary<int, int>();
            for (int i = 0; i < activePlayers.Count; i++)
            {
                playerIdMap[activePlayers[i]] = i;
            }

            // Clone the captured state
            var gameState = CloneGameState(_currentTrickGameState);
            gameState.CurrentPlayerIndex = playerIdMap[currentPlayerId];
            
            // Mark table cards as played
            foreach (var tableCard in tableCards)
            {
                MarkCardAsPlayed(gameState, tableCard);
            }

            var sansGame = new Sans2PerfectGame(gameState);
            
            List<PerfectCardMove> bestMoves;
            if (tableCards.Count == 0)
            {
                bestMoves = sansGame.BestLeadCard();
            }
            else
            {
                var leadMove = FindCardInGameState(gameState, tableCards[0]);
                var bestMove = sansGame.BestFollowCard(leadMove);
                bestMoves = new List<PerfectCardMove> { bestMove };
            }

            var firstMove = bestMoves[0];
            var currentPlayerMappedIndex = playerIdMap[currentPlayerId];
            var opponentId = activePlayers.First(id => id != currentPlayerId);
            var opponentMappedIndex = playerIdMap[opponentId];

            return new AnalysisResult
            {
                BestMoves = bestMoves,
                IsLeader = (tableCards.Count == 0),
                CurrentPlayerId = currentPlayerId,
                CurrentPlayerExpectedTricks = firstMove.ExpectedTricks![currentPlayerMappedIndex],
                OpponentId = opponentId,
                OpponentExpectedTricks = firstMove.ExpectedTricks![opponentMappedIndex]
            };
        }

        private AnalysisResult PerformTrump2Analysis()
        {
            if (_gamePlayEngine == null)
                throw new InvalidOperationException("Game engine not initialized");
            
            if (_currentTrickGameState == null)
                throw new InvalidOperationException("Game state not captured. Analysis only works during active tricks.");

            var trumpSuit = GetTrumpSuitEnum();
            if (trumpSuit == null)
                throw new InvalidOperationException("Trump suit not specified for Trump game");

            var activePlayers = GetActivePlayerIds();
            var currentPlayerId = _gamePlayEngine.GetCurrentPlayerTurn();
            var tableCards = _gamePlayEngine.GetTableCards();

            var playerIdMap = new Dictionary<int, int>();
            for (int i = 0; i < activePlayers.Count; i++)
            {
                playerIdMap[activePlayers[i]] = i;
            }

            // Clone the captured state
            var gameState = CloneGameState(_currentTrickGameState);
            gameState.CurrentPlayerIndex = playerIdMap[currentPlayerId];
            
            // Mark table cards as played
            foreach (var tableCard in tableCards)
            {
                MarkCardAsPlayed(gameState, tableCard);
            }

            var trumpGame = new Trump2PerfectGame(gameState);

            List<PerfectCardMove> bestMoves;
            if (tableCards.Count == 0)
            {
                bestMoves = trumpGame.BestLeadCard();
            }
            else
            {
                var leadMove = FindCardInGameState(gameState, tableCards[0]);
                var bestMove = trumpGame.BestFollowCard(leadMove);
                bestMoves = new List<PerfectCardMove> { bestMove };
            }

            var firstMove = bestMoves[0];
            var currentPlayerMappedIndex = playerIdMap[currentPlayerId];
            var opponentId = activePlayers.First(id => id != currentPlayerId);
            var opponentMappedIndex = playerIdMap[opponentId];

            return new AnalysisResult
            {
                BestMoves = bestMoves,
                IsLeader = (tableCards.Count == 0),
                CurrentPlayerId = currentPlayerId,
                CurrentPlayerExpectedTricks = firstMove.ExpectedTricks![currentPlayerMappedIndex],
                OpponentId = opponentId,
                OpponentExpectedTricks = firstMove.ExpectedTricks![opponentMappedIndex]
            };
        }

        private AnalysisResult PerformTrump3Analysis()
        {
            if (_gamePlayEngine == null)
                throw new InvalidOperationException("Game engine not initialized");
            
            if (_currentTrickGameState == null)
                throw new InvalidOperationException("Game state not captured. Analysis only works during active tricks.");

            var trumpSuit = GetTrumpSuitEnum();
            if (trumpSuit == null)
                throw new InvalidOperationException("Trump suit not specified for Trump game");

            var activePlayers = GetActivePlayerIds();
            var currentPlayerId = _gamePlayEngine.GetCurrentPlayerTurn();
            var tableCards = _gamePlayEngine.GetTableCards();

            var playerIdMap = new Dictionary<int, int>();
            for (int i = 0; i < activePlayers.Count; i++)
            {
                playerIdMap[activePlayers[i]] = i;
            }

            // Clone the captured state
            var gameState = CloneGameState(_currentTrickGameState);
            gameState.CurrentPlayerIndex = playerIdMap[currentPlayerId];
            
            // Mark table cards as played
            foreach (var tableCard in tableCards)
            {
                MarkCardAsPlayed(gameState, tableCard);
            }

            var trumpGame = new Trump3PerfectGame(gameState, _currentTrickDeclarerIndex);
            
            List<PerfectCardMove> bestMoves;
            if (tableCards.Count == 0)
            {
                bestMoves = trumpGame.BestLeadCard();
            }
            else if (tableCards.Count == 1)
            {
                var leadMove = FindCardInGameState(gameState, tableCards[0]);
                var bestMove = trumpGame.BestFollowCard(leadMove, null);
                bestMoves = new List<PerfectCardMove> { bestMove };
            }
            else
            {
                var leadMove = FindCardInGameState(gameState, tableCards[0]);
                var firstFollowMove = FindCardInGameState(gameState, tableCards[1]);
                var bestMove = trumpGame.BestFollowCard(leadMove, firstFollowMove);
                bestMoves = new List<PerfectCardMove> { bestMove };
            }

            var firstMove = bestMoves[0];
            var currentPlayerMappedIndex = playerIdMap[currentPlayerId];
            int declarerPlayerId = GetDeclarerPlayerId();
            int declarerMappedIndex = playerIdMap[declarerPlayerId];

            return new AnalysisResult
            {
                BestMoves = bestMoves,
                IsLeader = (tableCards.Count == 0),
                CurrentPlayerId = currentPlayerId,
                CurrentPlayerExpectedTricks = firstMove.ExpectedTricks![currentPlayerMappedIndex],
                OpponentId = declarerPlayerId,
                OpponentExpectedTricks = firstMove.ExpectedTricks![declarerMappedIndex],
                IsThreePlayer = true,
                AllPlayerTricks = firstMove.ExpectedTricks
            };
        }

        private void DisplayAnalysisResult(AnalysisResult result)
        {
            var contractType = GetContractType();
            var moveType = result.IsLeader ? "Lead" : "Follow";
            var currentPlayerName = GetPlayerName(result.CurrentPlayerId);

            AnalysisStatusLabel.Text = $"✓ {contractType} Analysis complete for {currentPlayerName}";
            AnalysisRecommendedCardLabel.IsVisible = true;
            
            if (result.BestMoves.Count == 1)
            {
                AnalysisRecommendedCardLabel.Text = 
                    $"Recommended {moveType}: {GetCardText(result.BestMoves[0].Card)}";
            }
            else
            {
                var cardsList = string.Join(", ", result.BestMoves.Select(m => GetCardText(m.Card)));
                AnalysisRecommendedCardLabel.Text = 
                    $"Best {moveType} moves ({result.BestMoves.Count}): {cardsList}";
            }

            AnalysisExpectedTricksLabel.IsVisible = true;
            
            if (result.IsThreePlayer && result.AllPlayerTricks != null)
            {
                var activePlayers = GetActivePlayerIds();
                AnalysisExpectedTricksLabel.Text = 
                    $"Expected Tricks:\n" +
                    $"  {GetPlayerName(activePlayers[0])}: {result.AllPlayerTricks[0]}\n" +
                    $"  {GetPlayerName(activePlayers[1])}: {result.AllPlayerTricks[1]}\n" +
                    $"  {GetPlayerName(activePlayers[2])}: {result.AllPlayerTricks[2]}";
            }
            else
            {
                var opponentName = GetPlayerName(result.OpponentId);
                AnalysisExpectedTricksLabel.Text = 
                    $"Expected Tricks:\n" +
                    $"  {currentPlayerName}: {result.CurrentPlayerExpectedTricks}\n" +
                    $"  {opponentName}: {result.OpponentExpectedTricks}";
            }

            // Display performance metrics
            AnalysisPerformanceLabel.IsVisible = true;
            AnalysisPerformanceLabel.Text = $"⏱️ Time: {result.ElapsedMilliseconds:F2} ms";
        }

        private class AnalysisResult
        {
            public List<PerfectCardMove> BestMoves { get; set; } = new();
            public bool IsLeader { get; set; }
            public int CurrentPlayerId { get; set; }
            public int CurrentPlayerExpectedTricks { get; set; }
            public int OpponentId { get; set; }
            public int OpponentExpectedTricks { get; set; }
            public bool IsThreePlayer { get; set; } = false;
            public int[]? AllPlayerTricks { get; set; }
            public double ElapsedMilliseconds { get; set; }
        }
    }
}