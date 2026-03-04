using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// Command result
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public static CommandResult SuccessResult(string msg = "") =>
            new() { Success = true, Message = msg };

        public static CommandResult Failure(string msg) =>
            new() { Success = false, Message = msg };
    }

    /// <summary>
    /// Main game engine
    /// </summary>
    public class TablicGameEngine
    {
        private readonly TablicGameState state;
        private readonly Deck deck;
        private readonly TablicRules rules;
        private readonly TablicMoveValidator validator;
        private readonly TablicScoreCalculator scoreCalculator;

        private readonly IPlayer?[] players = new IPlayer?[2];
        private bool waitingForPlayerMove;

        // Events
        public EventPublisher<GameStartedEventArgs> GameStarted { get; } = new();
        public EventPublisher<RoundStartedEventArgs> RoundStarted { get; } = new();
        public EventPublisher<CardPlayedEventArgs> CardPlayed { get; } = new();
        public EventPublisher<PlayerSwitchedEventArgs> PlayerSwitched { get; } = new();
        public EventPublisher<RoundCompletedEventArgs> RoundCompleted { get; } = new();
        public EventPublisher<GameFinishedEventArgs> GameFinished { get; } = new();
        public EventPublisher<GameErrorEventArgs> Error { get; } = new();

        public TablicGameEngine()
        {
            state = new TablicGameState();
            deck = new FullDeck();
            rules = new TablicRules();
            validator = new TablicMoveValidator(rules);
            scoreCalculator = new TablicScoreCalculator(rules);
            waitingForPlayerMove = false;

            // Default players
            players[0] = new HumanPlayer(0, "Player 1");
            players[1] = new HumanPlayer(1, "Player 2");
        }

        // Player management
        public void SetPlayer(int playerId, IPlayer player)
        {
            if (playerId >= 0 && playerId < 2)
            {
                players[playerId] = player;
            }
        }

        public IPlayer? GetPlayer(int playerId)
        {
            return playerId >= 0 && playerId < 2 ? players[playerId] : null;
        }

        // Game commands
        public CommandResult StartNewGame()
        {
            if (state.GetPhase() == GamePhase.InProgress)
            {
                return CommandResult.Failure("Game already in progress");
            }

            state.Reset();
            deck.Reset();
            deck.Shuffle();

            state.SetPhase(GamePhase.InProgress);
            state.SetCurrentRound(1);
            state.SetCurrentPlayerId(0);

            DealInitialTalon();

            // Notify players
            for (int i = 0; i < 2; i++)
            {
                players[i]?.OnGameStarted();
            }

            // Publish event
            GameStarted.Publish(new GameStartedEventArgs
            {
                InitialTalon = state.GetTalon().ToList(),
                Player1InitialCards = 0,
                Player2InitialCards = 0
            });

            return StartNewRound();
        }

        private void DealInitialTalon()
        {
            state.ClearTalon();
            for (int i = 0; i < 4; i++)
            {
                if (!deck.IsEmpty())
                {
                    state.AddToTalon(deck.DrawCard());
                }
            }
        }

        public CommandResult StartNewRound()
        {
            if (state.GetCurrentRound() > 4)
            {
                return CommandResult.Failure("All rounds completed");
            }

            // Deal 6 cards to each player
            for (int i = 0; i < 6; i++)
            {
                if (!deck.IsEmpty())
                    state.GetPlayer(0).GetHand().AddCard(deck.DrawCard());
                if (!deck.IsEmpty())
                    state.GetPlayer(1).GetHand().AddCard(deck.DrawCard());
            }

            // Notify players
            for (int i = 0; i < 2; i++)
            {
                players[i]?.OnRoundStarted(state.GetCurrentRound());
            }

            // Publish event
            RoundStarted.Publish(new RoundStartedEventArgs
            {
                RoundNumber = state.GetCurrentRound(),
                CurrentPlayerId = state.GetCurrentPlayerId(),
                CardsDealtToPlayer1 = 6,
                CardsDealtToPlayer2 = 6
            });

            waitingForPlayerMove = true;

            return CommandResult.SuccessResult("Round started");
        }

        public CommandResult SubmitMove(PlayerMove move)
        {
            var validationResult = ValidateMove(move);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            waitingForPlayerMove = false;
            ProcessMove(move);

            return CommandResult.SuccessResult("Move processed");
        }

        private CommandResult ValidateMove(PlayerMove move)
        {
            if (state.GetPhase() != GamePhase.InProgress)
                return CommandResult.Failure("Game not in progress");

            if (!waitingForPlayerMove)
                return CommandResult.Failure("Not waiting for a move");

            if (move.PlayerId != state.GetCurrentPlayerId())
                return CommandResult.Failure($"Invalid player - it is player {state.GetCurrentPlayerId()}'s turn");

            if (move.PlayerId < 0 || move.PlayerId >= 2 || players[move.PlayerId] == null)
                return CommandResult.Failure("Invalid player ID");

            var currentPlayer = state.GetPlayer(move.PlayerId);
            if (move.HandIndex < 0 || move.HandIndex >= currentPlayer.GetHand().CardCount())
                return CommandResult.Failure("Invalid hand index");

            if (move.AttemptPickup)
            {
                if (move.TalonIndices.Count == 0)
                    return CommandResult.Failure("Pickup attempted but no talon cards specified");

                foreach (int idx in move.TalonIndices)
                {
                    if (idx < 0 || idx >= state.GetTalon().Count)
                        return CommandResult.Failure($"Invalid talon index: {idx}");
                }

                if (move.TalonIndices.Distinct().Count() != move.TalonIndices.Count)
                    return CommandResult.Failure("Duplicate talon index in pickup");

                var playedCard = currentPlayer.GetHand().GetCards()[move.HandIndex];
                var talonCards = move.TalonIndices.Select(idx => state.GetTalon()[idx]).ToList();

                var pickupValidation = validator.ValidatePickup(playedCard, talonCards);
                if (!pickupValidation.IsValid)
                    return CommandResult.Failure($"Invalid pickup: {pickupValidation.ErrorMessage}");
            }

            return CommandResult.SuccessResult();
        }

        private void ProcessMove(PlayerMove move)
        {
            int currentPlayerId = move.PlayerId;
            var currentPlayer = state.GetPlayer(currentPlayerId);

            var playedCard = currentPlayer.GetHand().GetCards()[move.HandIndex];
            currentPlayer.GetHand().RemoveCard(move.HandIndex);

            bool wasPickup = false;
            var pickedCards = new List<Card>();

            if (move.AttemptPickup && move.TalonIndices.Count > 0)
            {
                foreach (int idx in move.TalonIndices)
                {
                    if (idx >= 0 && idx < state.GetTalon().Count)
                    {
                        pickedCards.Add(state.GetTalon()[idx]);
                    }
                }

                currentPlayer.AddToPile(playedCard);
                currentPlayer.AddToPile(pickedCards);

                state.RemoveFromTalon(move.TalonIndices);
                
                // Award talon point if player cleared the talon
                if (state.GetTalon().Count == 0)
                {
                    currentPlayer.IncrementTalonClearCount();
                }
                
                state.SetLastPickupPlayerId(currentPlayerId);

                wasPickup = true;
            }
            else
            {
                state.AddToTalon(playedCard);
            }

            // Notify players
            for (int i = 0; i < 2; i++)
            {
                players[i]?.OnCardPlayed(currentPlayerId, playedCard, wasPickup);
            }

            // Publish event - now includes HandIndex
            CardPlayed.Publish(new CardPlayedEventArgs
            {
                PlayerId = currentPlayerId,
                HandIndex = move.HandIndex,
                PlayedCard = playedCard,
                WasPickup = wasPickup,
                PickedCards = pickedCards,
                CurrentTalon = state.GetTalon().ToList()
            });

            CheckRoundCompletion();
        }

        private void CheckRoundCompletion()
        {
            bool roundComplete =
                state.GetPlayer(0).GetHand().CardCount() == 0 &&
                state.GetPlayer(1).GetHand().CardCount() == 0;

            // ALWAYS switch player after every move (before checking round completion)
            int previousPlayerId = state.GetCurrentPlayerId();
            state.SwitchPlayer();

            PlayerSwitched.Publish(new PlayerSwitchedEventArgs
            {
                PreviousPlayerId = previousPlayerId,
                CurrentPlayerId = state.GetCurrentPlayerId()
            });

            if (roundComplete)
            {

                RoundCompleted.Publish(new RoundCompletedEventArgs
                {
                    RoundNumber = state.GetCurrentRound(),
                    Player1PileSize = state.GetPlayer(0).GetPileSize(),
                    Player2PileSize = state.GetPlayer(1).GetPileSize()
                });

                state.IncrementRound();

                if (state.GetCurrentRound() > 4)
                {
                    // Award remaining talon to last pickup player if the game ended without a new round starting
                    if (state.GetTalon().Count > 0 && state.GetLastPickupPlayerId() >= 0)
                    {
                        state.GetPlayer(state.GetLastPickupPlayerId()).AddToPile(state.GetTalon());
                        state.ClearTalon();
                    }
                    FinalizeGame();
                }
                else
                {
                    // Round is done, waiting for UI/console to call StartNewRound()
                    waitingForPlayerMove = false;
                }
            }
            else
            {
                // Continue round - wait for next move
                waitingForPlayerMove = true;
            }
        }

        private void FinalizeGame()
        {
            state.SetPhase(GamePhase.Finished);
            waitingForPlayerMove = false;

            var (score1, score2) = scoreCalculator.CalculateFinalScores(
                state.GetPlayer(0),
                state.GetPlayer(1));

            int winnerId = scoreCalculator.DetermineWinner(score1, score2);

            for (int i = 0; i < 2; i++)
            {
                players[i]?.OnGameFinished(winnerId);
            }

            GameFinished.Publish(new GameFinishedEventArgs
            {
                Player1Score = score1,
                Player2Score = score2,
                WinnerId = winnerId
            });
        }

        // State queries
        public TablicGameState GetState() => state;

        public bool IsGameFinished() => state.GetPhase() == GamePhase.Finished;

        public bool IsRoundComplete() =>
            state.GetPlayer(0).GetHand().CardCount() == 0 &&
            state.GetPlayer(1).GetHand().CardCount() == 0;

        public bool NeedsNewRound() =>
            state.GetPhase() == GamePhase.InProgress &&
            IsRoundComplete() &&
            state.GetCurrentRound() < 4;

        public bool IsWaitingForMove() => waitingForPlayerMove;

        public int GetCurrentPlayerId() => state.GetCurrentPlayerId();

        public TablicRules GetRules() => rules;

        public List<PossibleMove> GetPossibleMoves()
        {
            var moves = new List<PossibleMove>();
            var currentPlayer = state.GetCurrentPlayer();
            var hand = currentPlayer.GetHand().GetCards();

            for (int i = 0; i < hand.Count; i++)
            {
                var move = new PossibleMove
                {
                    HandIndex = i,
                    Card = hand[i],
                    CanPickup = false,
                    PossibleCombinations = new List<List<int>>()
                };

                var matches = validator.FindMatches(hand[i], state.GetTalon());
                move.CanPickup = matches.Count > 0;

                foreach (var match in matches)
                {
                    if (match.HasMatch)
                    {
                        move.PossibleCombinations.Add(match.TalonIndices);
                    }
                }

                moves.Add(move);
            }

            return moves;
        }

        public (PlayerScore, PlayerScore) GetCurrentScores()
        {
            return scoreCalculator.CalculateFinalScores(
                state.GetPlayer(0),
                state.GetPlayer(1));
        }

        public int GetCurrentWinner()
        {
            var (score1, score2) = GetCurrentScores();
            return scoreCalculator.DetermineWinner(score1, score2);
        }
    }
}