using System.Collections.ObjectModel;
using GameOfCardsCsharp.Preferance.Sans;
using GameOfCardsCsharp.Preferance.Common;
using GameOfCardsCsharp.PreferanceBots.Models;
using Microsoft.Maui.Controls;

namespace GameOfCardsCsharp.PreferanceBots
{
    // AI Strategy enum - page-specific, stays here
    public enum AIStrategy
    {
        SuitControlOnly,
        PerfectInformation,
        PerfectGameTree,
        MonteCarlo100,
        MonteCarlo500,
        MonteCarloWithPerfectGame
    }

    public partial class SansPage : ContentPage
    {
        // Game state
        private List<Card> playerHand = new();
        private List<Card> botHand = new();
        private List<Card> cardsPlayed = new();
        private int playerTricks = 0;
        private int botTricks = 0;
        private int currentTrick = 0;
        private int currentLeader = 0; // 0 = player, 1 = bot
        private bool isPlayerTurn = true;
        private bool gameInProgress = false;

        // Game mode and AI settings
        private bool isTestMode = false;
        private AIStrategy currentAIStrategy = AIStrategy.SuitControlOnly;
        private int cardsPerPlayer = 10;

        // AI analyzer
        private Sans2TrickAnalyzer? analyzer;

        // UI collections
        private ObservableCollection<CardViewModel> playerHandDisplay = new();
        private ObservableCollection<CardViewModel> botHandDisplay = new();

        // Trick state
        private Card? playerPlayedCard = null;
        private Card? botPlayedCard = null;
        private Card? leadCard = null;

        public SansPage()
        {
            InitializeComponent();

            PlayerHandCollection.ItemsSource = playerHandDisplay;
            BotHandCollection.ItemsSource = botHandDisplay;

            // Set default selections
            GameModePicker.SelectedIndex = 1; // Test mode (All cards visible)
            AIStrategyPicker.SelectedIndex = 0; // Suit control
            CardsPerPlayerSlider.Value = 10; // Default 10 cards
            isTestMode = true;
            cardsPerPlayer = 10;

            LogMessage("Welcome to Sans! Click 'New Game' to start.");
        }

        private void OnCardsPerPlayerChanged(object sender, ValueChangedEventArgs e)
        {
            cardsPerPlayer = (int)e.NewValue;
            CardsPerPlayerLabel.Text = $"{cardsPerPlayer} card{(cardsPerPlayer == 1 ? "" : "s")}";
            
            if (gameInProgress)
            {
                LogMessage($"⚙️ Cards per player changed to {cardsPerPlayer}. Start a new game to apply.");
            }
        }

        private void OnNewGameClicked(object sender, EventArgs e)
        {
            StartNewGame();
        }

        private void OnGameModeChanged(object sender, EventArgs e)
        {
            isTestMode = GameModePicker.SelectedIndex == 1;
            
            if (gameInProgress)
            {
                LogMessage($"Mode changed to: {(isTestMode ? "Test Mode (cards visible)" : "Real Game (cards hidden)")}");
                UpdateBotHandDisplay();
            }
        }

        private void OnAIStrategyChanged(object sender, EventArgs e)
        {
            currentAIStrategy = AIStrategyPicker.SelectedIndex switch
            {
                0 => AIStrategy.SuitControlOnly,
                1 => AIStrategy.PerfectInformation,
                2 => AIStrategy.PerfectGameTree,
                3 => AIStrategy.MonteCarlo100,
                4 => AIStrategy.MonteCarlo500,
                5 => AIStrategy.MonteCarloWithPerfectGame,
                _ => AIStrategy.SuitControlOnly
            };

            LogMessage($"AI Strategy: {GetStrategyName(currentAIStrategy)}");
        }

        private void OnMakeAIMoveClicked(object sender, EventArgs e)
        {
            if (!gameInProgress || isPlayerTurn)
            {
                LogMessage("❌ It's not AI's turn!");
                return;
            }

            _ = MakeBotMove();
        }

        private async void OnAnalyzePositionClicked(object sender, EventArgs e)
        {
            if (!gameInProgress)
            {
                LogMessage("❌ No game in progress!");
                return;
            }

            try
            {
                UpdateStatus("Analyzing current position...");
                AIAnalysisLabel.Text = "Running position analysis...";

                // Disable button during analysis
                AnalyzePositionButton.IsEnabled = false;

                await Task.Delay(100); // UI update delay

                var startTime = DateTime.Now;

                // Check if current player is following or leading
                bool playerIsFollowing = leadCard != null && isPlayerTurn;
                bool botIsFollowing = leadCard != null && !isPlayerTurn;

                // Analyze for both players
                var (botBestMove, playerBestMove) = await Task.Run(() =>
                {
                    // Get AI's best move
                    RankedMove botMove;
                    if (botIsFollowing && leadCard != null)
                    {
                        // Bot is following
                        botMove = analyzer!.GetBestFollowingMove(botHand, playerHand, leadCard, currentLeader);
                    }
                    else
                    {
                        // Bot is leading
                        botMove = GetAnalysisMove(botHand, playerHand, currentLeader);
                    }

                    // Get player's best move
                    RankedMove playerMove;
                    if (playerIsFollowing && leadCard != null)
                    {
                        // Player is following
                        playerMove = analyzer!.GetBestFollowingMove(playerHand, botHand, leadCard, currentLeader == 0 ? 0 : 1);
                    }
                    else
                    {
                        // Player is leading
                        playerMove = GetAnalysisMove(playerHand, botHand, currentLeader == 0 ? 0 : 1);
                    }

                    return (botMove, playerMove);
                });

                var elapsed = DateTime.Now - startTime;

                // Display comprehensive analysis
                var analysisText = $"═══ POSITION ANALYSIS ═══\n\n";
                
                analysisText += $"🤖 AI BEST MOVE:\n";
                analysisText += $"Card: {botBestMove.Card}\n";
                analysisText += $"Est. Final: Bot={botBestMove.PredictedMyTricks:F1}, You={botBestMove.PredictedOpponentTricks:F1}\n";
                analysisText += $"Advantage: {botBestMove.ExpectedTricks:F2}\n";
                analysisText += $"Confidence: {botBestMove.Confidence:P0}\n";
                analysisText += $"{botBestMove.Reasoning}\n\n";

                analysisText += $"👤 YOUR BEST MOVE:\n";
                analysisText += $"Card: {playerBestMove.Card}\n";
                analysisText += $"Est. Final: You={playerBestMove.PredictedMyTricks:F1}, Bot={playerBestMove.PredictedOpponentTricks:F1}\n";
                analysisText += $"Advantage: {playerBestMove.ExpectedTricks:F2}\n";
                analysisText += $"Confidence: {playerBestMove.Confidence:P0}\n";
                analysisText += $"{playerBestMove.Reasoning}\n\n";

                analysisText += $"Analysis time: {elapsed.TotalMilliseconds:F0}ms";

                AIAnalysisLabel.Text = analysisText;
                LogMessage($"📊 Position analyzed - check AI Analysis panel");

                UpdateStatus(isPlayerTurn ? "Your turn" : "Bot's turn (click 'AI Move')");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Analysis error: {ex.Message}");
                AIAnalysisLabel.Text = $"Analysis failed: {ex.Message}";
            }
            finally
            {
                AnalyzePositionButton.IsEnabled = true;
            }
        }

        private RankedMove GetAnalysisMove(List<Card> myHand, List<Card> oppHand, int leader)
        {
            return currentAIStrategy switch
            {
                AIStrategy.SuitControlOnly => analyzer!.GetBestMoveSuitControl(myHand, oppHand, leader),
                AIStrategy.PerfectInformation => analyzer!.GetBestMovePerfectInfoLegacy(myHand, oppHand, leader),
                AIStrategy.PerfectGameTree when isTestMode => analyzer!.GetBestMovePerfectInfo(myHand, oppHand, leader),
                AIStrategy.MonteCarloWithPerfectGame => analyzer!.GetBestMoveMonteCarlo(myHand, cardsPlayed, leader, usePerfectGameAnalyzer: true),
                _ when isTestMode => analyzer!.GetBestMovePerfectInfo(myHand, oppHand, leader),
                _ => analyzer!.GetBestMoveMonteCarlo(myHand, cardsPlayed, leader)
            };
        }

        private void StartNewGame()
        {
            // Reset game state
            playerHand.Clear();
            botHand.Clear();
            cardsPlayed.Clear();
            playerTricks = 0;
            botTricks = 0;
            currentTrick = 0;
            playerPlayedCard = null;
            botPlayedCard = null;
            leadCard = null;

            // Use ShortDeck (32 cards: 7-Ace in all suits)
            var deck = new ShortDeck();
            deck.Shuffle();

            // Deal cards to each player based on the selected number of cards
            for (int i = 0; i < cardsPerPlayer; i++)
            {
                playerHand.Add(deck.DrawCard());
                botHand.Add(deck.DrawCard());
            }

            // Sort hands
            playerHand = playerHand.OrderBy(c => c.Suit).ThenByDescending(c => c.Rank).ToList();
            botHand = botHand.OrderBy(c => c.Suit).ThenByDescending(c => c.Rank).ToList();

            // Determine starting player (random)
            currentLeader = Random.Shared.Next(2);
            isPlayerTurn = currentLeader == 0;

            // Initialize AI analyzer based on mode
            var analysisMode = isTestMode ? AnalysisMode.PerfectInformation : AnalysisMode.MonteCarloSimulation;
            var simCount = currentAIStrategy switch
            {
                AIStrategy.MonteCarlo100 => 100,
                AIStrategy.MonteCarlo500 => 500,
                AIStrategy.MonteCarloWithPerfectGame => 100,
                _ => 100
            };

            analyzer = new Sans2TrickAnalyzer(analysisMode, simCount, minimaxDepth: 3);

            gameInProgress = true;

            // Update UI
            UpdateUI();
            UpdateButtonStates();
            
            LogMessage($"════════════════════════════");
            LogMessage($"🎮 NEW GAME STARTED ({cardsPerPlayer} cards each)");
            LogMessage($"Mode: {(isTestMode ? "Test (All Visible)" : "Real Game")}");
            LogMessage($"AI: {GetStrategyName(currentAIStrategy)}");
            LogMessage($"Starting player: {(currentLeader == 0 ? "You" : "Bot")}");
            LogMessage($"════════════════════════════");

            AIAnalysisLabel.Text = "Game started. Use 'Analyze Position' to see best moves.";
        }

        private async void OnPlayerCardTapped(object sender, TappedEventArgs e)
        {
            if (!gameInProgress || !isPlayerTurn)
            {
                LogMessage("It's not your turn!");
                return;
            }

            if (sender is not Border border || border.BindingContext is not CardViewModel cardModel)
                return;

            // Find the card in player's hand
            var card = playerHand.FirstOrDefault(c =>
                c.Suit.ToString() == cardModel.Suit && c.Rank.ToString() == cardModel.Rank);

            if (card == null)
                return;

            // Validate if player must follow suit
            if (leadCard != null && !CanPlayCard(card, playerHand, leadCard))
            {
                LogMessage($"❌ You must follow suit ({leadCard.Suit}) if you have it!");
                return;
            }

            // Play the card
            await PlayPlayerCard(card);
        }

        private bool CanPlayCard(Card cardToPlay, List<Card> hand, Card leadCard)
        {
            // If leading, any card is valid
            if (leadCard == null)
                return true;

            // Check if player has cards in the lead suit
            var sameSuitCards = hand.Where(c => c.Suit == leadCard.Suit).ToList();

            // If player has cards in lead suit, must play one of them
            if (sameSuitCards.Any())
            {
                return cardToPlay.Suit == leadCard.Suit;
            }

            // If no cards in lead suit, can play anything
            return true;
        }

        private async Task PlayPlayerCard(Card card)
        {
            playerPlayedCard = card;
            playerHand.Remove(card);
            cardsPlayed.Add(card);

            // Set lead card if this is the first card played
            if (leadCard == null)
            {
                leadCard = card;
                LogMessage($"You led with: {card}");
            }
            else
            {
                LogMessage($"You followed with: {card}");
            }

            // Display played card with correct color
            PlayerPlayedCardLabel.Text = CardViewModel.GetCardSymbol(card);
            PlayerPlayedCardLabel.TextColor = CardViewModel.GetSuitColor(card.Suit);
            PlayerPlayedCardBorder.IsVisible = true;

            UpdatePlayerHandDisplay();

            // Wait for visual effect
            await Task.Delay(1000);

            // Check if this completes the trick
            if (botPlayedCard != null)
            {
                await CompleteTrick();
            }
            else
            {
                // Bot's turn to play - manual mode
                isPlayerTurn = false;
                UpdateStatus("Bot's turn - click 'AI Move' button");
                UpdateButtonStates();
                AIAnalysisLabel.Text = "Waiting for AI move...\nClick 'AI Move' or 'Analyze Position'";
            }
        }

        private async Task MakeBotMove()
        {
            if (!gameInProgress)
                return;

            UpdateStatus("Bot is thinking...");
            AIAnalysisLabel.Text = "Analyzing position...";
            UpdateButtonStates();

            await Task.Delay(100); // Small delay for UI update

            try
            {
                // Get bot's best move using AI
                var startTime = DateTime.Now;
                var bestMove = await Task.Run(() => GetBotBestMove());
                var elapsed = DateTime.Now - startTime;

                // Validate bot must follow suit
                var card = bestMove.Card;
                
                // Double-check bot's move is legal
                if (leadCard != null && !CanPlayCard(card, botHand, leadCard))
                {
                    // AI made illegal move - find legal card
                    LogMessage($"⚠️ AI tried illegal move, correcting...");
                    card = GetLegalBotCard(leadCard);
                }

                botPlayedCard = card;
                botHand.Remove(card);
                cardsPlayed.Add(card);

                // Set lead card if this is the first card played
                if (leadCard == null)
                {
                    leadCard = card;
                    LogMessage($"Bot led with: {card}");
                }
                else
                {
                    LogMessage($"Bot followed with: {card}");
                }

                // Display played card with correct color
                BotPlayedCardLabel.Text = CardViewModel.GetCardSymbol(card);
                BotPlayedCardLabel.TextColor = CardViewModel.GetSuitColor(card.Suit);
                BotPlayedCardBorder.IsVisible = true;

                // Update AI analysis display with predictions
                AIAnalysisLabel.Text = $"Bot played: {card}\n" +
                    $"Predicted final: Bot={bestMove.PredictedMyTricks:F1}, You={bestMove.PredictedOpponentTricks:F1}\n" +
                    $"Advantage: {bestMove.ExpectedTricks:F2}\n" +
                    $"Confidence: {bestMove.Confidence:P0}\n" +
                    $"Time: {elapsed.TotalMilliseconds:F0}ms\n" +
                    $"{bestMove.Reasoning}";

                LogMessage($"  → Bot expects to win {bestMove.PredictedMyTricks:F1} tricks total");
                
                // Update bot score label with prediction
                BotScoreLabel.Text = $"{botTricks} tricks (Est: {bestMove.PredictedMyTricks:F1})";

                UpdateBotHandDisplay();

                // Wait for visual effect
                await Task.Delay(500);

                // Check if this completes the trick
                if (playerPlayedCard != null)
                {
                    await CompleteTrick();
                }
                else
                {
                    // Player's turn
                    isPlayerTurn = true;
                    UpdateStatus("Your turn to play");
                    UpdateButtonStates();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error: {ex.Message}");
                AIAnalysisLabel.Text = $"Error: {ex.Message}";
                UpdateStatus("Error occurred");
                UpdateButtonStates();
            }
        }

        private Card GetLegalBotCard(Card leadCard)
        {
            // Find cards in the lead suit
            var sameSuit = botHand.Where(c => c.Suit == leadCard.Suit).ToList();
            
            if (sameSuit.Any())
            {
                // Must follow suit - play lowest
                return sameSuit.OrderBy(c => c.Rank).First();
            }
            
            // No cards in lead suit - play lowest card overall
            return botHand.OrderBy(c => c.Rank).First();
        }

        private RankedMove GetBotBestMove()
        {
            if (analyzer == null)
                throw new InvalidOperationException("Analyzer not initialized");

            // Check if bot is following (not leading)
            bool isFollowing = leadCard != null;

            if (isFollowing)
            {
                // Bot is responding to a lead
                LogMessage($"  → Bot analyzing response to {leadCard}...");
                return analyzer.GetBestFollowingMove(botHand, playerHand, leadCard!, currentLeader);
            }
            else
            {
                // Bot is leading - use strategy-specific analysis
                return currentAIStrategy switch
                {
                    AIStrategy.SuitControlOnly => 
                        analyzer.GetBestMoveSuitControl(botHand, playerHand, currentLeader),
                    
                    AIStrategy.PerfectInformation => 
                        analyzer.GetBestMovePerfectInfoLegacy(botHand, playerHand, currentLeader),
                    
                    AIStrategy.PerfectGameTree when isTestMode =>
                        analyzer.GetBestMovePerfectInfo(botHand, playerHand, currentLeader),
                    
                    AIStrategy.MonteCarloWithPerfectGame => 
                        analyzer.GetBestMoveMonteCarlo(botHand, cardsPlayed, currentLeader, usePerfectGameAnalyzer: true),
                    
                    AIStrategy.MonteCarlo100 or AIStrategy.MonteCarlo500 => 
                        analyzer.GetBestMoveMonteCarlo(botHand, cardsPlayed, currentLeader, usePerfectGameAnalyzer: false),
                    
                    _ when isTestMode => 
                        analyzer.GetBestMovePerfectInfo(botHand, playerHand, currentLeader),
                    
                    _ => 
                        analyzer.GetBestMoveMonteCarlo(botHand, cardsPlayed, currentLeader)
                };
            }
        }

        private async Task CompleteTrick()
        {
            if (playerPlayedCard == null || botPlayedCard == null || leadCard == null)
                return;

            currentTrick++;

            // Determine trick winner based on lead card
            int winner = DetermineTrickWinner(leadCard, playerPlayedCard, botPlayedCard, currentLeader);

            // Update trick counts
            if (winner == 0)
            {
                playerTricks++;
                LogMessage($"✓ You won trick #{currentTrick}!");
                currentLeader = 0; // Winner leads next
            }
            else
            {
                botTricks++;
                LogMessage($"✗ Bot won trick #{currentTrick}");
                currentLeader = 1; // Winner leads next
            }

            // Update scores
            UpdateScoreDisplay();

            // Wait to show result
            await Task.Delay(1500);

            // Clear played cards and lead card
            PlayerPlayedCardBorder.IsVisible = false;
            BotPlayedCardBorder.IsVisible = false;
            playerPlayedCard = null;
            botPlayedCard = null;
            leadCard = null; // Reset lead card for next trick

            // Check if game is over
            if (currentTrick >= cardsPerPlayer)
            {
                EndGame();
                return;
            }

            // Winner leads next trick
            isPlayerTurn = currentLeader == 0;

            UpdateStatus(isPlayerTurn ? "Your turn to lead" : "Bot's turn to lead - click 'AI Move'");
            UpdateButtonStates();
            TrickStatusLabel.Text = $"Trick {currentTrick + 1} / {cardsPerPlayer}";
            CurrentLeaderLabel.Text = isPlayerTurn ? "You lead" : "Bot leads";
        }

        private int DetermineTrickWinner(Card leadCard, Card playerCard, Card botCard, int whoLed)
        {
            Card followCard = whoLed == 0 ? botCard : playerCard;

            // Rule 1: If follower didn't follow suit, leader wins automatically
            if (followCard.Suit != leadCard.Suit)
            {
                LogMessage($"  → {(whoLed == 0 ? "Bot" : "You")} couldn't follow suit, leader wins");
                return whoLed;
            }

            // Rule 2: Both cards are same suit - highest rank wins
            if (whoLed == 0) // Player led
            {
                if (botCard.Rank > playerCard.Rank)
                {
                    LogMessage($"  → Bot's {botCard.Rank} beats your {playerCard.Rank}");
                    return 1; // Bot wins
                }
                else
                {
                    LogMessage($"  → Your {playerCard.Rank} beats bot's {botCard.Rank}");
                    return 0; // Player wins
                }
            }
            else // Bot led
            {
                if (playerCard.Rank > botCard.Rank)
                {
                    LogMessage($"  → Your {playerCard.Rank} beats bot's {botCard.Rank}");
                    return 0; // Player wins
                }
                else
                {
                    LogMessage($"  → Bot's {botCard.Rank} beats your {playerCard.Rank}");
                    return 1; // Bot wins
                }
            }
        }

        private void EndGame()
        {
            gameInProgress = false;
            isPlayerTurn = false;
            UpdateButtonStates();

            string result = playerTricks > botTricks ? "🏆 You win!" :
                           botTricks > playerTricks ? "🤖 Bot wins!" :
                           "🤝 It's a tie!";

            UpdateStatus($"Game Over! {result}");
            LogMessage($"=== GAME OVER ===");
            LogMessage($"Final Score: You {playerTricks} - Bot {botTricks}");
            LogMessage($"{result}");
        }

        private void UpdateUI()
        {
            UpdatePlayerHandDisplay();
            UpdateBotHandDisplay();
            UpdateScoreDisplay();
            UpdateStatus(isPlayerTurn ? "Your turn" : "Bot's turn");
            TrickStatusLabel.Text = $"Trick {currentTrick + 1} / {cardsPerPlayer}";
            CurrentLeaderLabel.Text = currentLeader == 0 ? "You lead" : "Bot leads";
        }

        private void UpdateButtonStates()
        {
            // AI Move button: enabled only when it's AI's turn and game is in progress
            MakeAIMoveButton.IsEnabled = gameInProgress && !isPlayerTurn;
            
            // Analyze Position button: enabled when game is in progress
            AnalyzePositionButton.IsEnabled = gameInProgress;
        }

        private void UpdatePlayerHandDisplay()
        {
            playerHandDisplay.Clear();
            foreach (var card in playerHand)
            {
                playerHandDisplay.Add(new CardViewModel
                {
                    Suit = card.Suit.ToString(),
                    Rank = card.Rank.ToString(),
                    DisplayText = CardViewModel.GetCardSymbol(card),
                    BorderColor = Colors.Blue,
                    BorderThickness = 2,
                    BackgroundColor = Colors.White,
                    TextColor = CardViewModel.GetSuitColor(card.Suit)
                });
            }
        }

        private void UpdateBotHandDisplay()
        {
            botHandDisplay.Clear();
            foreach (var card in botHand)
            {
                if (isTestMode)
                {
                    // Show bot's cards face up
                    botHandDisplay.Add(new CardViewModel
                    {
                        Suit = card.Suit.ToString(),
                        Rank = card.Rank.ToString(),
                        DisplayText = CardViewModel.GetCardSymbol(card),
                        BorderColor = Colors.Gray,
                        BorderThickness = 2,
                        BackgroundColor = Colors.White,
                        TextColor = CardViewModel.GetSuitColor(card.Suit)
                    });
                }
                else
                {
                    // Show card backs
                    botHandDisplay.Add(new CardViewModel
                    {
                        Suit = "Hidden",
                        Rank = "Hidden",
                        DisplayText = "🂠", // Card back symbol
                        BorderColor = Colors.DarkBlue,
                        BorderThickness = 2,
                        BackgroundColor = Color.FromArgb("#0D47A1"),
                        TextColor = Colors.White
                    });
                }
            }
        }

        private void UpdateScoreDisplay()
        {
            PlayerScoreLabel.Text = $"{playerTricks} tricks";
            BotScoreLabel.Text = $"{botTricks} tricks";
            PlayerTricksLabel.Text = $"Tricks: {playerTricks}";
            BotTricksLabel.Text = $"Tricks: {botTricks}";
        }

        private void UpdateStatus(string message)
        {
            GameStatusLabel.Text = message;
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\n";
            
            // MUST update UI on main thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                GameLogLabel.Text += logEntry;
                
                // Auto-scroll to bottom
                await Task.Delay(50);
                await LogScrollView.ScrollToAsync(0, GameLogLabel.Height, false);
            });
        }

        private string GetStrategyName(AIStrategy strategy)
        {
            return strategy switch
            {
                AIStrategy.SuitControlOnly => "Suit Control Only",
                AIStrategy.PerfectInformation => "Perfect Info (Legacy)",
                AIStrategy.PerfectGameTree => "Perfect Game Tree",
                AIStrategy.MonteCarlo100 => "Monte Carlo (100 sims)",
                AIStrategy.MonteCarlo500 => "Monte Carlo (500 sims)",
                AIStrategy.MonteCarloWithPerfectGame => "Monte Carlo + Perfect Game",
                _ => "Unknown"
            };
        }
    }
}