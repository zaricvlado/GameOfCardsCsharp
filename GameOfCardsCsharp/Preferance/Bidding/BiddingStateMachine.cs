using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.Bidding
{
    /// <summary>
    /// Internal state machine that manages bidding logic, turn order, and action filtering.
    /// Does NOT handle player interaction or events - that's BiddingEngine's job.
    /// </summary>
    internal class BiddingStateMachine
    {
        private readonly Dictionary<int, PlayerBidContext> _playerContexts;
        private readonly List<int> _playerIds;
        private readonly int _startingPlayerId;
        
        private int _currentPlayerIndex;
        private BidAction? _lastBid;
        private int? _lastBidderId;

        public BiddingStateMachine(List<int> playerIds, int startingPlayerId)
        {
            if (playerIds == null || playerIds.Count < 2)
                throw new ArgumentException("At least 2 players required for bidding", nameof(playerIds));

            if (!playerIds.Contains(startingPlayerId))
                throw new ArgumentException("Starting player must be in player list", nameof(startingPlayerId));

            _playerIds = new List<int>(playerIds);
            _startingPlayerId = startingPlayerId;
            _currentPlayerIndex = _playerIds.IndexOf(startingPlayerId);
            
            _playerContexts = new Dictionary<int, PlayerBidContext>();
            foreach (var playerId in playerIds)
            {
                _playerContexts[playerId] = new PlayerBidContext(playerId);
            }

            _lastBid = null;
            _lastBidderId = null;

            // Initialize available actions for starting player
            UpdateCurrentPlayerActions();
        }

        /// <summary>
        /// Gets the current player who needs to make a bid
        /// </summary>
        public int CurrentPlayerId => _playerIds[_currentPlayerIndex];

        /// <summary>
        /// Gets the player bid context for the current player
        /// </summary>
        public PlayerBidContext CurrentPlayerContext => _playerContexts[CurrentPlayerId];

        /// <summary>
        /// Gets available actions for the current player
        /// </summary>
        public List<BidAction> GetAvailableActions() => CurrentPlayerContext.AvailableActions;

        /// <summary>
        /// Gets all player contexts (for debugging/display)
        /// </summary>
        public IReadOnlyDictionary<int, PlayerBidContext> GetAllContexts() => _playerContexts;

        /// <summary>
        /// Checks if bidding is complete (only one active player or all passed)
        /// </summary>
        public bool IsBiddingComplete()
        {
            var activePlayers = _playerContexts.Values.Where(ctx => ctx.IsActive).ToList();
            
            // All players passed
            if (activePlayers.Count == 0)
                return true;

            // Only one player left who made a bid
            if (activePlayers.Count == 1 && _lastBid.HasValue)
                return true;

            return false;
        }

        /// <summary>
        /// Gets the bidding result (only call when IsBiddingComplete returns true)
        /// </summary>
        public BiddingResult GetResult()
        {
            if (!IsBiddingComplete())
                throw new InvalidOperationException("Bidding is not complete yet");

            // Check if all players passed
            if (!_lastBid.HasValue || !_lastBidderId.HasValue)
                return BiddingResult.AllPassedResult();

            return BiddingResult.Winner(_lastBidderId.Value, _lastBid.Value);
        }

        /// <summary>
        /// Processes a bid action from the current player and advances state
        /// </summary>
        /// <returns>True if the action was valid and processed</returns>
        public bool ProcessBid(BidAction action)
        {
            // Validate action is available
            if (!CurrentPlayerContext.AvailableActions.Contains(action))
                return false;

            var currentPlayerId = CurrentPlayerId;
            var context = CurrentPlayerContext;

            // Record the action
            context.RecordAction(action);

            // Update last bid if not a pass
            if (!action.IsPass)
            {
                _lastBid = action;
                _lastBidderId = currentPlayerId;

                // Eliminate Regular track players if Trump track bid was made
                if (action.Track == BiddingTrack.Trump && action.IsTrumpBid())
                {
                    EliminateRegularBidders();
                }
            }

            // Move to next active player
            MoveToNextActivePlayer();

            // Update available actions for new current player (if bidding not complete)
            if (!IsBiddingComplete())
            {
                UpdateCurrentPlayerActions();
            }

            return true;
        }

        /// <summary>
        /// Eliminates all players who committed to Regular track when Trump track becomes active
        /// </summary>
        private void EliminateRegularBidders()
        {
            foreach (var context in _playerContexts.Values)
            {
                if (context.CommittedTrack == BiddingTrack.Regular)
                {
                    context.Eliminate();
                }
            }
        }

        /// <summary>
        /// Moves to the next active player (skips eliminated/passed players)
        /// </summary>
        private void MoveToNextActivePlayer()
        {
            int startIndex = _currentPlayerIndex;
            
            do
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _playerIds.Count;
                
                // Prevent infinite loop if all players are inactive
                if (_currentPlayerIndex == startIndex)
                {
                    // If we've cycled back and current player is inactive, we're done
                    if (!CurrentPlayerContext.IsActive)
                        break;
                }

                if (CurrentPlayerContext.IsActive)
                    return;

            } while (true);
        }

        /// <summary>
        /// Updates available actions for the current player based on last bid
        /// </summary>
        private void UpdateCurrentPlayerActions()
        {
            var context = CurrentPlayerContext;
            
            // Determine if current player can match the last bid
            // (only if they haven't bid yet or are in trump declaration phase)
            bool canMatch = context.CurrentBidAction == null;

            context.UpdateAvailableActions(_lastBid, canMatch);
        }

        /// <summary>
        /// Gets current bidding state summary for debugging
        /// </summary>
        public string GetStateSnapshot()
        {
            var activePlayers = _playerContexts.Values.Count(ctx => ctx.IsActive);
            var lastBidInfo = _lastBid.HasValue 
                ? $"Last Bid: {_lastBid.Value.GetDisplayName()} by Player {_lastBidderId}" 
                : "No bids yet";
            
            return $"Current Player: {CurrentPlayerId}, Active Players: {activePlayers}, {lastBidInfo}";
        }
    }
}