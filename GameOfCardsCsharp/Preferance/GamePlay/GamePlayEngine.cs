using GameOfCardsCsharp.Tablic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.GamePlay
{
    /// <summary>
    /// Synchronous game play engine for Preferance.
    /// Manages trick-taking logic in a turn-based, command-driven manner.
    /// </summary>
    public class GamePlayEngine
    {
        private readonly Dictionary<int, PreferancePlayer> _players;
        private GamePlayStateMachine? _stateMachine;
        private RoleAssignmentResult? _currentRoles;
        private bool _isRunning;
        private bool _waitingForPlayerCard;
        private List<Card> _table; // Cards played in current trick

        // ==================== EVENTS ====================

        /// <summary>
        /// Raised when game play starts
        /// </summary>
        public event EventHandler<GamePlayStartedEventArgs>? GamePlayStarted;

        /// <summary>
        /// Raised when a new trick begins
        /// </summary>
        public event EventHandler<TrickStartedEventArgs>? TrickStarted;

        /// <summary>
        /// Raised when it's a player's turn to play a card
        /// </summary>
        public event EventHandler<PlayerTurnToPlayEventArgs>? PlayerTurnToPlay;

        /// <summary>
        /// Raised when a card is played
        /// </summary>
        public event EventHandler<CardPlayedEventArgs>? CardPlayed;

        /// <summary>
        /// Raised when a trick is completed
        /// </summary>
        public event EventHandler<TrickCompletedEventArgs>? TrickCompleted;

        /// <summary>
        /// Raised when the entire game is completed
        /// </summary>
        public event EventHandler<GamePlayCompletedEventArgs>? GamePlayCompleted;

        /// <summary>
        /// Raised when an error occurs
        /// </summary>
        public event EventHandler<GamePlayErrorEventArgs>? Error;

        public GamePlayEngine(Dictionary<int, PreferancePlayer> players)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _table = new List<Card>();
        }

        // ==================== PUBLIC COMMANDS ====================

        /// <summary>
        /// Starts a new game with the given roles
        /// </summary>
        public CommandResult StartGame(RoleAssignmentResult roles)
        {
            if (_isRunning)
                return CommandResult.Failure("Game already in progress");

            try
            {
                _currentRoles = roles;
                _stateMachine = CreateStateMachine(roles);
                _isRunning = true;
                _waitingForPlayerCard = false;
                _table.Clear();

                // Raise started event
                GamePlayStarted?.Invoke(this, new GamePlayStartedEventArgs(roles));

                // Start first trick
                return StartNewTrick();
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new GamePlayErrorEventArgs(ex.Message));
                return CommandResult.Failure($"Failed to start game: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts a new trick
        /// </summary>
        public CommandResult StartNewTrick()
        {
            if (_stateMachine == null)
                return CommandResult.Failure("Game not initialized");

            if (_stateMachine.IsGameComplete())
                return CommandResult.Failure("Game already complete");

            _table.Clear();
            _stateMachine.StartNewTrick();

            TrickStarted?.Invoke(this, new TrickStartedEventArgs(
                _stateMachine.CurrentTrick,
                _stateMachine.CurrentLeaderId));

            _waitingForPlayerCard = true;

            // Raise event for first player's turn
            var leaderId = _stateMachine.CurrentLeaderId;
            var legalMoves = _stateMachine.GetLegalMoves(leaderId);

            PlayerTurnToPlay?.Invoke(this, new PlayerTurnToPlayEventArgs(
                leaderId,
                legalMoves,
                null)); // No lead card yet

            return CommandResult.SuccessResult($"Trick {_stateMachine.CurrentTrick} started");
        }

        /// <summary>
        /// Submits a card play from a player
        /// </summary>
        public CommandResult SubmitMove(PreferanceMove move)
        {
            var validationResult = ValidateMove(move);
            if (!validationResult.Success)
            {
                Error?.Invoke(this, new GamePlayErrorEventArgs(validationResult.Message));
                return validationResult;
            }

            ProcessMove(move);

            return CommandResult.SuccessResult("Move processed");
        }

        public int GetCurrentPlayerTurn()
        {
            if (_stateMachine == null)
                return -1;

            var playersInTrick = _stateMachine.GetPlayersInTrick();

            // Current player is the one who hasn't played yet
            foreach (var playerId in playersInTrick)
            {
                var trickCards = _stateMachine.GetTrickCards(_stateMachine.CurrentTrick);
                if (!trickCards.ContainsKey(playerId))
                    return playerId;
            }

            return playersInTrick[0]; // Fallback to leader
        }

        // ==================== PRIVATE LOGIC ====================

        private CommandResult ValidateMove(PreferanceMove move)
        {
            if (_stateMachine == null)
                return CommandResult.Failure("Game not started");

            if (!_isRunning)
                return CommandResult.Failure("Game not in progress");

            if (!_waitingForPlayerCard)
                return CommandResult.Failure("Not waiting for a card");

            // Get expected player
            var expectedPlayerId = GetCurrentPlayerTurn();
            if (move.PlayerId != expectedPlayerId)
                return CommandResult.Failure($"Wrong player - it's Player {expectedPlayerId}'s turn");

            // Validate player has the card
            var playerHand = _players[move.PlayerId].GetHand().GetCards();
            if (!playerHand.Contains(move.Card))
                return CommandResult.Failure("Player doesn't have this card");

            // Validate card is legal
            var legalMoves = _stateMachine.GetLegalMoves(move.PlayerId);
            if (!legalMoves.Contains(move.Card))
                return CommandResult.Failure("Card is not a legal move (must follow suit)");

            return CommandResult.SuccessResult();
        }

        private void ProcessMove(PreferanceMove move)
        {
            if (_stateMachine == null)
                return;

            // Play the card
            _stateMachine.PlayCard(move.PlayerId, move.Card);
            _table.Add(move.Card);

            // Raise event
            CardPlayed?.Invoke(this, new CardPlayedEventArgs(
                move.PlayerId,
                move.Card,
                _stateMachine.CurrentTrick,
                _table.Count));

            // Check if trick is complete
            if (IsTrickComplete())
            {
                CompleteTrick();
            }
            else
            {
                // Move to next player
                MoveToNextPlayer();
            }
        }

        private bool IsTrickComplete()
        {
            if (_stateMachine == null)
                return false;

            var playersInTrick = _stateMachine.GetPlayersInTrick();
            return _table.Count >= playersInTrick.Count;
        }

        private void CompleteTrick()
        {
            if (_stateMachine == null)
                return;

            _waitingForPlayerCard = false;

            // Determine winner
            var winnerId = _stateMachine.CompleteTrick();
            var trickCards = _stateMachine.GetTrickCards(_stateMachine.CurrentTrick);

            TrickCompleted?.Invoke(this, new TrickCompletedEventArgs(
                _stateMachine.CurrentTrick,
                winnerId,
                trickCards));

            // Check if game is complete
            if (_stateMachine.IsGameComplete())
            {
                CompleteGame();
            }
            else
            {
                // Game continues - start next trick automatically
                StartNewTrick();
            }
        }

        private void MoveToNextPlayer()
        {
            if (_stateMachine == null)
                return;

            var playersInTrick = _stateMachine.GetPlayersInTrick();
            var currentPlayerId = GetCurrentPlayerTurn();
            
            var currentIndex = playersInTrick.IndexOf(currentPlayerId);
            var nextIndex = (currentIndex + 1) % playersInTrick.Count;
            var nextPlayerId = playersInTrick[nextIndex];

            var legalMoves = _stateMachine.GetLegalMoves(nextPlayerId);
            var leadCard = _stateMachine.GetLeadCard();

            _waitingForPlayerCard = true;

            PlayerTurnToPlay?.Invoke(this, new PlayerTurnToPlayEventArgs(
                nextPlayerId,
                legalMoves,
                leadCard));
        }

        private void CompleteGame()
        {
            if (_stateMachine == null)
                return;

            _isRunning = false;
            _waitingForPlayerCard = false;

            var result = _stateMachine.GetResult();

            GamePlayCompleted?.Invoke(this, new GamePlayCompletedEventArgs(result));
        }

        private GamePlayStateMachine CreateStateMachine(RoleAssignmentResult roles)
        {
            return roles.Contract switch
            {
                ContractType.Trump => new TrumpGameStateMachine(roles, _players),
                ContractType.Betl => new BetlGameStateMachine(roles, _players),
                ContractType.Sans => new SansGameStateMachine(roles, _players),
                _ => throw new InvalidOperationException($"Unknown contract type: {roles.Contract}")
            };
        }

        // ==================== STATE QUERIES ====================

        public bool IsWaitingForCard() => _waitingForPlayerCard;

        public bool IsGameInProgress() => _isRunning;

        public bool IsGameComplete() => _stateMachine?.IsGameComplete() ?? false;

        public int GetCurrentTrick() => _stateMachine?.CurrentTrick ?? 0;


        public List<Card> GetLegalMoves(int playerId)
        {
            return _stateMachine?.GetLegalMoves(playerId) ?? new List<Card>();
        }

        public List<Card> GetTableCards() => new List<Card>(_table);

        public Dictionary<int, int> GetCurrentTrickCounts()
        {
            if (_stateMachine == null)
                return new Dictionary<int, int>();

            return _stateMachine.GetTrickCounts();
        }
    }

    // ==================== MOVE COMMAND ====================

    /// <summary>
    /// Represents a card play move in Preferance
    /// </summary>
    public class PreferanceMove
    {
        public int PlayerId { get; set; }
        public Card Card { get; set; } = null!;
    }

    // ==================== ERROR EVENT ====================

    public class GamePlayErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; }

        public GamePlayErrorEventArgs(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
