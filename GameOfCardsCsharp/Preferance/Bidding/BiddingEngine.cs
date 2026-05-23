using GameOfCardsCsharp.Tablic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.Bidding
{
    /// <summary>
    /// Synchronous bidding engine for Preferance.
    /// Manages bidding phase in a turn-based, command-driven manner.
    /// </summary>
    public class BiddingEngine
    {
        private BiddingStateMachine? _stateMachine;
        private readonly Dictionary<int, PreferancePlayer> _players;
        private bool _isRunning;
        private bool _waitingForBid;

        // ==================== EVENTS ====================

        /// <summary>
        /// Raised when bidding phase starts
        /// </summary>
        public event EventHandler<BiddingStartedEventArgs>? BiddingStarted;

        /// <summary>
        /// Raised when it's a player's turn to make a bid
        /// </summary>
        public event EventHandler<PlayerTurnEventArgs>? PlayerTurnToAct;

        /// <summary>
        /// Raised when a player places a bid
        /// </summary>
        public event EventHandler<BidPlacedEventArgs>? BidPlaced;

        /// <summary>
        /// Raised when bidding phase completes
        /// </summary>
        public event EventHandler<BiddingCompletedEventArgs>? BiddingCompleted;

        /// <summary>
        /// Raised when an error occurs
        /// </summary>
        public event EventHandler<BiddingErrorEventArgs>? Error;

        public BiddingEngine(Dictionary<int, PreferancePlayer> players)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
        }

        // ==================== PUBLIC COMMANDS ====================

        /// <summary>
        /// Starts the bidding phase
        /// </summary>
        public CommandResult StartBidding(List<int> playerIds, int startingPlayerId)
        {
            if (_isRunning)
                return CommandResult.Failure("Bidding already in progress");

            try
            {
                _stateMachine = new BiddingStateMachine(playerIds, startingPlayerId);
                _isRunning = true;
                _waitingForBid = false;

                LinkPlayerContexts();

                BiddingStarted?.Invoke(this, new BiddingStartedEventArgs(playerIds, startingPlayerId));

                // Notify first player's turn
                var currentPlayerId = _stateMachine.CurrentPlayerId;
                var availableActions = _stateMachine.GetAvailableActions();
                
                _waitingForBid = true;

                PlayerTurnToAct?.Invoke(this, new PlayerTurnEventArgs(currentPlayerId, availableActions));

                return CommandResult.SuccessResult("Bidding started");
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new BiddingErrorEventArgs(ex.Message));
                return CommandResult.Failure($"Failed to start bidding: {ex.Message}");
            }
        }

        /// <summary>
        /// Submits a bid action from a player
        /// </summary>
        public CommandResult SubmitBid(BidAction action)
        {
            var validationResult = ValidateBid(action);
            if (!validationResult.Success)
            {
                Error?.Invoke(this, new BiddingErrorEventArgs(validationResult.Message));
                return validationResult;
            }

            ProcessBid(action);

            return CommandResult.SuccessResult("Bid processed");
        }

        // ==================== PRIVATE LOGIC ====================

        private CommandResult ValidateBid(BidAction action)
        {
            if (_stateMachine == null)
                return CommandResult.Failure("Bidding not started");

            if (!_isRunning)
                return CommandResult.Failure("Bidding not in progress");

            if (!_waitingForBid)
                return CommandResult.Failure("Not waiting for a bid");

            var currentPlayerId = _stateMachine.CurrentPlayerId;
            var availableActions = _stateMachine.GetAvailableActions();

            // Validate action is available
            if (!availableActions.Contains(action))
                return CommandResult.Failure($"Invalid bid action for Player {currentPlayerId}");

            return CommandResult.SuccessResult();
        }

        private void ProcessBid(BidAction action)
        {
            if (_stateMachine == null)
                return;

            var currentPlayerId = _stateMachine.CurrentPlayerId;

            // Process the bid
            bool success = _stateMachine.ProcessBid(action);

            if (!success)
            {
                Error?.Invoke(this, new BiddingErrorEventArgs(
                    $"Player {currentPlayerId} attempted invalid bid: {action.GetDisplayName()}"));
                return;
            }

            _waitingForBid = false;

            // Raise bid placed event
            BidPlaced?.Invoke(this, new BidPlacedEventArgs(currentPlayerId, action));

            // Check if bidding is complete
            if (_stateMachine.IsBiddingComplete())
            {
                CompleteBidding();
            }
            else
            {
                // Move to next player
                MoveToNextPlayer();
            }
        }

        private void MoveToNextPlayer()
        {
            if (_stateMachine == null)
                return;

            var currentPlayerId = _stateMachine.CurrentPlayerId;
            var availableActions = _stateMachine.GetAvailableActions();

            _waitingForBid = true;

            PlayerTurnToAct?.Invoke(this, new PlayerTurnEventArgs(currentPlayerId, availableActions));
        }

        private void CompleteBidding()
        {
            if (_stateMachine == null)
                return;

            _isRunning = false;
            _waitingForBid = false;

            var result = _stateMachine.GetResult();

            BiddingCompleted?.Invoke(this, new BiddingCompletedEventArgs(result));
        }

        private void LinkPlayerContexts()
        {
            if (_stateMachine == null)
                return;

            var contexts = _stateMachine.GetAllContexts();

            foreach (var kvp in contexts)
            {
                var playerId = kvp.Key;
                var context = kvp.Value;

                if (_players.TryGetValue(playerId, out var player))
                {
                    player.SetBidContext(context);
                }
            }
        }

        // ==================== STATE QUERIES ====================

        public bool IsWaitingForBid() => _waitingForBid;

        public bool IsBiddingInProgress() => _isRunning;

        public bool IsBiddingComplete() => _stateMachine?.IsBiddingComplete() ?? false;

        public int GetCurrentPlayerId() => _stateMachine?.CurrentPlayerId ?? -1;

        public List<BidAction> GetAvailableActions()
        {
            return _stateMachine?.GetAvailableActions() ?? new List<BidAction>();
        }

        public string GetStateSnapshot()
        {
            return _stateMachine?.GetStateSnapshot() ?? "Bidding not started";
        }
    }

    // ==================== ERROR EVENT ====================

    public class BiddingErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; }

        public BiddingErrorEventArgs(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
