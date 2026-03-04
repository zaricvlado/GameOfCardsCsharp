using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameOfCardsCsharp;

namespace TablicGameConsole
{
    internal class TablicConsoleGame
    {
        private readonly TablicGameEngine engine;
        private readonly MoveRanker moveRanker;
        private bool userWantsToQuit;

        public TablicConsoleGame()
        {
            engine = new TablicGameEngine();
            // Initialize ranker with Greedy strategy for human assistance
            moveRanker = new MoveRanker(new GreedyStrategy(engine.GetRules()));
            userWantsToQuit = false;

            // Subscribe to events
            engine.GameStarted.Subscribe(OnGameStarted);
            engine.RoundStarted.Subscribe(OnRoundStarted);
            engine.CardPlayed.Subscribe(OnCardPlayed);
            engine.PlayerSwitched.Subscribe(OnPlayerSwitched);
            engine.RoundCompleted.Subscribe(OnRoundCompleted);
            engine.GameFinished.Subscribe(OnGameFinished);
            engine.Error.Subscribe(OnError);
        }

        public void Run()
        {
            ShowWelcome();
            SetupPlayers();

            engine.StartNewGame();

            // Main game loop - console controls everything
            while (!engine.IsGameFinished() && !userWantsToQuit)
            {
                // Check if we need to start a new round
                if (engine.NeedsNewRound())
                {
                    engine.StartNewRound();
                }

                // Handle player move if engine is waiting
                if (engine.IsWaitingForMove())
                {
                    var player = engine.GetPlayer(engine.GetCurrentPlayerId());
                    if (player != null)
                    {
                        if (player.GetPlayerType() == PlayerType.Human)
                        {
                            HandleHumanMove();
                        }
                        else if (player.GetPlayerType() == PlayerType.AI)
                        {
                            HandleAIMove();
                        }
                    }
                }
            }

            if (userWantsToQuit)
            {
                Console.WriteLine("\nGame cancelled by player.");
            }

            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }

        // Event handlers
        private void OnGameStarted(GameStartedEventArgs args)
        {
            ClearScreen();
            Console.WriteLine("\n=== GAME STARTED ===\n");
            Console.WriteLine("Initial talon cards:");
            foreach (var card in args.InitialTalon)
            {
                Console.WriteLine($"  {card}");
            }
            Console.WriteLine();
        }

        private void OnRoundStarted(RoundStartedEventArgs args)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine($"          ROUND {args.RoundNumber} STARTED");
            Console.WriteLine("========================================");
            Console.WriteLine("Each player dealt 6 cards\n");
            Pause();
        }

        private void OnCardPlayed(CardPlayedEventArgs args)
        {
            Console.WriteLine("\n--- Card Played ---");
            Console.WriteLine($"{GetPlayerName(args.PlayerId)} played: {args.PlayedCard}");

            if (args.WasPickup)
            {
                Console.WriteLine($"PICKUP! Collected {args.PickedCards.Count} cards:");
                foreach (var card in args.PickedCards)
                {
                    Console.WriteLine($"  {card}");
                }
            }
            else
            {
                Console.WriteLine("Card added to talon");
            }

            Console.WriteLine($"\nCurrent talon ({args.CurrentTalon.Count} cards):");
            foreach (var card in args.CurrentTalon)
            {
                Console.WriteLine($"  {card}");
            }
            Console.WriteLine();

            // Pause if AI played (so user can see)
            var player = engine.GetPlayer(args.PlayerId);
            if (player != null && player.GetPlayerType() == PlayerType.AI)
            {
                Pause();
            }
        }

        private void OnPlayerSwitched(PlayerSwitchedEventArgs args)
        {
            Console.WriteLine($"\n--- {GetPlayerName(args.CurrentPlayerId)}'s turn ---");
        }

        private void OnRoundCompleted(RoundCompletedEventArgs args)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine($"          ROUND {args.RoundNumber} COMPLETED");
            Console.WriteLine("========================================");
            Console.WriteLine($"{GetPlayerName(0)} pile: {args.Player1PileSize} cards");
            Console.WriteLine($"{GetPlayerName(1)} pile: {args.Player2PileSize} cards");
            DisplayScores();
            Console.WriteLine();

            // Ask if player wants to continue
            if (!AskToContinue())
            {
                userWantsToQuit = true;
            }
        }

        private void OnGameFinished(GameFinishedEventArgs args)
        {
            ClearScreen();
            Console.WriteLine("\n========================================");
            Console.WriteLine("          GAME FINISHED!");
            Console.WriteLine("========================================\n");

            Console.WriteLine("Final Scores:\n");

            Console.WriteLine($"{GetPlayerName(0)}:");
            Console.WriteLine($"  Cards collected: {engine.GetState().GetPlayer(0).GetPileSize()}");
            Console.WriteLine($"  Trick points: {args.Player1Score.Tricks}");
            Console.WriteLine($"  Total score: {args.Player1Score.TotalPoints}\n");

            Console.WriteLine($"{GetPlayerName(1)}:");
            Console.WriteLine($"  Cards collected: {engine.GetState().GetPlayer(1).GetPileSize()}");
            Console.WriteLine($"  Trick points: {args.Player2Score.Tricks}");
            Console.WriteLine($"  Total score: {args.Player2Score.TotalPoints}\n");

            if (args.WinnerId >= 0)
            {
                Console.WriteLine($"*** WINNER: {GetPlayerName(args.WinnerId)} ***");
            }
            else
            {
                Console.WriteLine("*** IT'S A TIE! ***");
            }
            Console.WriteLine();
        }

        private void OnError(GameErrorEventArgs args)
        {
            Console.Error.WriteLine($"\nERROR: {args.ErrorMessage}");
            if (!string.IsNullOrEmpty(args.Context))
            {
                Console.Error.WriteLine($"Context: {args.Context}");
            }
            Console.Error.WriteLine();
        }

        // Game flow
        private void HandleHumanMove()
        {
            DisplayGameState();
            DisplayPossibleMoves();

            if (!GetPlayerChoice(out int choice))
            {
                return;
            }

            // Build PlayerMove structure
            var move = new PlayerMove
            {
                PlayerId = engine.GetCurrentPlayerId(),
                HandIndex = choice,
                AttemptPickup = false,
                TalonIndices = new List<int>()
            };

            // Check for possible pickups
            var moves = engine.GetPossibleMoves();
            if (choice >= 0 && choice < moves.Count)
            {
                var possibleMove = moves[choice];

                if (possibleMove.CanPickup && possibleMove.PossibleCombinations.Count > 0)
                {
                    if (AskForPickup())
                    {
                        DisplayPickupCombinations(possibleMove);

                        int comboChoice = GetCombinationChoice(possibleMove.PossibleCombinations.Count);

                        move.AttemptPickup = true;
                        move.TalonIndices = possibleMove.PossibleCombinations[comboChoice];
                    }
                }
            }

            // Submit the move to the engine
            var result = engine.SubmitMove(move);
            if (!result.Success)
            {
                Console.WriteLine($"ERROR: {result.Message}");
            }
        }

        private void HandleAIMove()
        {
            var player = engine.GetPlayer(engine.GetCurrentPlayerId());
            if (player == null)
            {
                return;
            }

            // Request move from AI player
            var move = player.RequestMove(engine);

            // Submit the AI move to the engine
            if (move != null)
            {
                var result = engine.SubmitMove(move);
                if (!result.Success)
                {
                    Console.WriteLine($"AI ERROR: {result.Message}");
                }
            }
        }

        // UI helpers
        private void ShowWelcome()
        {
            ClearScreen();
            Console.WriteLine("========================================");
            Console.WriteLine("          TABLIC CARD GAME");
            Console.WriteLine("========================================\n");
            Console.WriteLine("Rules:");
            Console.WriteLine("- 2 players, 4 rounds per game");
            Console.WriteLine("- Each round: 6 cards per player");
            Console.WriteLine("- Match card values to pick up from talon");
            Console.WriteLine("- Score points for trick cards");
            Console.WriteLine("- Bonus point for most cards collected\n");
        }

        private void SetupPlayers()
        {
            Console.WriteLine("Choose game mode:");
            Console.WriteLine("1. Human vs Human");
            Console.WriteLine("2. Human vs AI (Random)");
            Console.WriteLine("3. Human vs AI (Greedy)");
            Console.WriteLine("4. AI vs AI (Random vs Greedy)");
            Console.Write("\nYour choice: ");

            if (!int.TryParse(Console.ReadLine(), out int choice))
            {
                choice = 3; // Default
            }

            var rules = engine.GetRules();

            switch (choice)
            {
                case 1:
                    engine.SetPlayer(0, new HumanPlayer(0, "Player 1"));
                    engine.SetPlayer(1, new HumanPlayer(1, "Player 2"));
                    break;

                case 2:
                    engine.SetPlayer(0, new HumanPlayer(0, "You"));
                    engine.SetPlayer(1, new AIPlayer(1, new RandomStrategy(rules), "Computer"));
                    break;

                case 3:
                    engine.SetPlayer(0, new HumanPlayer(0, "You"));
                    engine.SetPlayer(1, new AIPlayer(1, new GreedyStrategy(rules), "Computer"));
                    break;

                case 4:
                    engine.SetPlayer(0, new AIPlayer(0, new RandomStrategy(rules), "Random Bot"));
                    engine.SetPlayer(1, new AIPlayer(1, new GreedyStrategy(rules), "Greedy Bot"));
                    break;

                default:
                    Console.WriteLine("Invalid choice. Defaulting to Human vs AI (Greedy)");
                    engine.SetPlayer(0, new HumanPlayer(0, "You"));
                    engine.SetPlayer(1, new AIPlayer(1, new GreedyStrategy(rules), "Computer"));
                    break;
            }

            Console.WriteLine("\nPress Enter to start...");
            Console.ReadLine();
        }

        private void DisplayGameState()
        {
            Console.WriteLine("\n=== Current Game State ===");
            DisplayTalon();
            DisplayPlayerHand();
            DisplayScores();
        }

        private void DisplayTalon()
        {
            var talon = engine.GetState().GetTalon();
            Console.WriteLine($"\nTalon ({talon.Count} cards):");
            for (int i = 0; i < talon.Count; i++)
            {
                Console.WriteLine($"  [{i}] {talon[i]}");
            }
        }

        private void DisplayPlayerHand()
        {
            var player = engine.GetState().GetCurrentPlayer();
            var cards = player.GetHand().GetCards();

            Console.WriteLine($"\nYour hand ({cards.Count} cards):");
            for (int i = 0; i < cards.Count; i++)
            {
                Console.WriteLine($"  [{i}] {cards[i]}");
            }
        }

        private void DisplayScores()
        {
            var (score1, score2) = engine.GetCurrentScores();

            Console.WriteLine("\nCurrent Scores:");
            Console.WriteLine($"  {GetPlayerName(0)}: " +
                $"{engine.GetState().GetPlayer(0).GetPileSize()} cards, " +
                $"{score1.Tricks} points");
            Console.WriteLine($"  {GetPlayerName(1)}: " +
                $"{engine.GetState().GetPlayer(1).GetPileSize()} cards, " +
                $"{score2.Tricks} points");
        }

        private void DisplayPossibleMoves()
        {
            var allMoves = engine.GetPossibleMoves();

            if (allMoves.Count == 0)
            {
                Console.WriteLine("\nNo moves available!");
                return;
            }

            // Show AI recommendations for human player
            var bestMoves = moveRanker.GetBestMoves(engine.GetState(), allMoves, 5);

            Console.WriteLine("\n╔══════════════════════════════════════╗");
            Console.WriteLine("║   AI RECOMMENDED MOVES (Top 5)       ║");
            Console.WriteLine("╚══════════════════════════════════════╝");

            foreach (var rankedMove in bestMoves)
            {
                var move = rankedMove.Move;

                // Format: [Index] Card (Score: XX)
                Console.Write($"  [{move.HandIndex}] {move.Card,-15}");

                // Show score with color
                Console.ForegroundColor = GetScoreColor(rankedMove.Score);
                Console.Write($"Score: {rankedMove.Score,4}");
                Console.ResetColor();

                // Show pickup indicator
                if (move.CanPickup)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($" ★ PICKUP");
                    Console.ResetColor();
                    Console.Write($" ({move.PossibleCombinations.Count} combo{(move.PossibleCombinations.Count > 1 ? "s" : "")})");
                }

                Console.WriteLine();
            }

            // Show other available moves
            var otherMoves = allMoves
                .Where(m => !bestMoves.Any(rm => rm.Move.HandIndex == m.HandIndex))
                .ToList();

            if (otherMoves.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  ... and {otherMoves.Count} other move(s) available");
                Console.ResetColor();
            }
        }

        private void DisplayPickupCombinations(PossibleMove move)
        {
            Console.WriteLine("\n╔══════════════════════════════════════╗");
            Console.WriteLine("║   AVAILABLE PICKUP COMBINATIONS      ║");
            Console.WriteLine("╚══════════════════════════════════════╝");

            // Rank combinations by their trick value + card count
            var rules = engine.GetRules();
            var talon = engine.GetState().GetTalon();

            var rankedCombos = move.PossibleCombinations
                .Select((combo, index) => new
                {
                    Index = index,
                    Combo = combo,
                    TrickValue = combo.Sum(idx => rules.GetTrickValue(talon[idx])),
                    CardCount = combo.Count,
                    Score = (combo.Sum(idx => rules.GetTrickValue(talon[idx])) * 10) + combo.Count
                })
                .OrderByDescending(c => c.Score)
                .ToList();

            foreach (var ranked in rankedCombos)
            {
                Console.Write($"  [{ranked.Index}] ");

                // Show cards
                foreach (var idx in ranked.Combo)
                {
                    Console.Write($"{talon[idx]} ");
                }

                // Show evaluation
                Console.Write("  ");
                if (ranked.TrickValue > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"(Tricks: {ranked.TrickValue}, ");
                    Console.ResetColor();
                }
                else
                {
                    Console.Write("(");
                }

                Console.Write($"Cards: {ranked.CardCount}, Score: {ranked.Score})");

                // Recommend best
                if (ranked.Index == rankedCombos[0].Index)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(" ← BEST");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
        }

        private ConsoleColor GetScoreColor(int score)
        {
            if (score >= 1030) return ConsoleColor.Green;      // Excellent pickup
            if (score >= 1010) return ConsoleColor.Cyan;       // Good pickup
            if (score >= 1000) return ConsoleColor.Yellow;     // Regular pickup
            if (score >= 0) return ConsoleColor.White;         // Discard low-value card
            return ConsoleColor.Red;                           // Discard valuable card
        }

        private bool GetPlayerChoice(out int choice)
        {
            Console.Write("\n➤ Enter card number to play: ");
            var input = Console.ReadLine();

            if (int.TryParse(input, out choice))
            {
                return true;
            }

            Console.WriteLine("Invalid input. Please enter a number.");
            choice = 0;
            return false;
        }

        private bool AskForPickup()
        {
            Console.Write("\n➤ Pickup available. Pick up cards? (y/n): ");
            var response = Console.ReadLine();

            return response?.ToLower() == "y";
        }

        private int GetCombinationChoice(int maxCount)
        {
            if (maxCount == 1)
            {
                return 0;
            }

            Console.Write($"\n➤ Choose combination (0-{maxCount - 1}): ");
            if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 0 && choice < maxCount)
            {
                return choice;
            }

            Console.WriteLine("Invalid choice, using best combination (0).");
            return 0;
        }

        private bool AskToContinue()
        {
            Console.Write("\n➤ Continue to next round? (y/n): ");
            var response = Console.ReadLine();

            return response?.ToLower() == "y";
        }

        private void Pause()
        {
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }

        private void ClearScreen()
        {
            Console.Clear();
        }

        private string GetPlayerName(int playerId)
        {
            var player = engine.GetPlayer(playerId);
            if (player != null)
            {
                return player.GetPlayerName();
            }
            return $"Player {playerId}";
        }
    }
}