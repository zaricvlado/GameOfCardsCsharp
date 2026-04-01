using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.Bidding
{
    /// <summary>
    /// Tracks bidding state and available actions for a single player
    /// </summary>
    public class PlayerBidContext
    {
        public int PlayerId { get; }
        public BiddingTrack CommittedTrack { get; private set; }
        public BidAction? CurrentBidAction { get; private set; }
        public BidAction? LastActiveBidAction { get; private set; }
        public bool IsPassed { get; private set; }
        public bool IsEliminated { get; private set; }
        public List<BidAction> AvailableActions { get; private set; }

        /// <summary>
        /// True if player is still active in bidding (not passed and not eliminated)
        /// </summary>
        public bool IsActive => !IsPassed && !IsEliminated;

        public PlayerBidContext(int playerId)
        {
            PlayerId = playerId;
            CommittedTrack = BiddingTrack.None;
            CurrentBidAction = null;
            LastActiveBidAction = null;
            IsPassed = false;
            IsEliminated = false;
            AvailableActions = new List<BidAction>();
        }

        /// <summary>
        /// Records a bid action taken by this player
        /// </summary>
        public void RecordAction(BidAction action)
        {
            CurrentBidAction = action;

            if (action.IsPass)
            {
                IsPassed = true;
                IsEliminated = true; // Passing eliminates the player
                AvailableActions.Clear();
            }
            else
            {
                LastActiveBidAction = action;
                
                if (CommittedTrack == BiddingTrack.None)
                {
                    CommittedTrack = action.Track;
                }
            }
        }

        /// <summary>
        /// Eliminates this player from bidding (e.g., Regular track players when Trump bid is made)
        /// </summary>
        public void Eliminate()
        {
            IsEliminated = true;
            AvailableActions.Clear();
        }

        /// <summary>
        /// Updates available actions based on the current highest bid.
        /// The lastBid contains all necessary information (track, values, etc.)
        /// </summary>
        public void UpdateAvailableActions(
            BidAction? lastBid,
            bool canMatchLastBid)
        {
            AvailableActions.Clear();

            // If eliminated or passed, no actions available
            if (IsEliminated || IsPassed)
            {
                return;
            }

            // No bid yet - starting the bidding
            if (!lastBid.HasValue)
            {
                // Can start with any initial bid
                AvailableActions.Add(BidActions.Pass);
                AvailableActions.Add(BidActions.Bid2);
                AvailableActions.Add(BidActions.Trump);
                AvailableActions.Add(BidActions.Betl);
                AvailableActions.Add(BidActions.Sans);
                return;
            }

            var highestBid = lastBid.Value;

            // Temporarily add Pass - we'll decide if it stays at the end
            AvailableActions.Add(BidActions.Pass);

            // Handle different tracks based on the highest bid
            if (highestBid.Track == BiddingTrack.Regular)
            {
                HandleRegularBidding(highestBid, canMatchLastBid);
            }
            else if (highestBid.Track == BiddingTrack.Trump)
            {
                HandleTrumpBidding(highestBid);
            }
            else if (highestBid.Track == BiddingTrack.TrumpDeclaration)
            {
                HandleTrumpDeclaration(highestBid);
            }

            // CRITICAL: If only Pass is available, clear the list entirely
            if (AvailableActions.Count == 1 && AvailableActions[0].IsPass)
            {
                AvailableActions.Clear();
            }
        }

        /// <summary>
        /// Handles available actions when highest bid is on Regular track
        /// </summary>
        private void HandleRegularBidding(BidAction highestBid, bool canMatch)
        {
            // Check if player is uncommitted or on Regular track
            if (CommittedTrack == BiddingTrack.None)
            {
                // Uncommitted: can bid Regular or switch to Trump track
                AddNextRegularBid(highestBid, canMatch);
                
                // Can also start Trump track
                AvailableActions.Add(BidActions.Trump);
                AvailableActions.Add(BidActions.Betl);
                AvailableActions.Add(BidActions.Sans);
            }
            else if (CommittedTrack == BiddingTrack.Regular)
            {
                // Committed to Regular: can only bid higher Regular
                AddNextRegularBid(highestBid, canMatch);
            }
            else if (CommittedTrack == BiddingTrack.Trump)
            {
                // Already on Trump track: Regular bids don't matter
                // Can only bid higher Trump bids (but no Regular bid exists yet on Trump track)
                // This shouldn't happen in normal flow
            }
        }

        /// <summary>
        /// Handles available actions when highest bid is on Trump track
        /// </summary>
        private void HandleTrumpBidding(BidAction highestBid)
        {
            // Once Trump track is active, Regular bidders are eliminated
            if (CommittedTrack == BiddingTrack.Regular)
            {
                // Eliminated: no actions available
                AvailableActions.Clear();
                return;
            }

            // Uncommitted or Trump committed: can bid higher Trump
            if (CommittedTrack == BiddingTrack.None || CommittedTrack == BiddingTrack.Trump)
            {
                AddTrumpBidsHigherThan(highestBid);
            }
        }

        /// <summary>
        /// Handles available actions when in Trump suit declaration phase.
        /// Players must declare trump suits in conflict resolution.
        /// Rule: Can only declare suits HIGHER than the current highest.
        /// Example: If highest is Diamonds (3), can only declare Hearts (4) or Clubs (5)
        /// </summary>
        private void HandleTrumpDeclaration(BidAction highestBid)
        {
            // Only players in trump declaration phase can bid here
            if (CommittedTrack != BiddingTrack.Trump && CommittedTrack != BiddingTrack.TrumpDeclaration)
            {
                AvailableActions.Clear();
                return;
            }

            // Add trump suits higher than the current highest
            var currentSuitPriority = (int)highestBid.TrumpSuitValue;

            foreach (var declaration in BidActions.AllTrumpDeclarations)
            {
                if ((int)declaration.TrumpSuitValue > currentSuitPriority)
                {
                    AvailableActions.Add(declaration);
                }
            }

            // If no higher suits available (e.g., Clubs (5) is highest), only Pass remains
            // The check at the end will clear the list
        }

        /// <summary>
        /// Adds the next available regular bid (only ONE bid at a time)
        /// </summary>
        private void AddNextRegularBid(BidAction highestBid, bool canMatch)
        {
            if (!highestBid.IsRegularBid())
            {
                return;
            }

            var currentPriority = highestBid.GetPriority();
            var nextPriority = canMatch ? currentPriority : currentPriority + 1;

            var nextBid = BidActions.AllRegularBids.FirstOrDefault(b => b.GetPriority() == nextPriority);
            if (nextBid != default)
            {
                AvailableActions.Add(nextBid);
            }
        }

        /// <summary>
        /// Adds Trump track bids higher than the current highest
        /// </summary>
        private void AddTrumpBidsHigherThan(BidAction highestBid)
        {
            if (!highestBid.IsTrumpBid())
            {
                return;
            }

            if (highestBid == BidActions.Trump)
            {
                // Can bid Trump (same level - triggers conflict), Betl, or Sans
                AvailableActions.Add(BidActions.Trump);
                AvailableActions.Add(BidActions.Betl);
                AvailableActions.Add(BidActions.Sans);
            }
            else if (highestBid == BidActions.Betl)
            {
                // Can only bid Sans (higher than Betl)
                AvailableActions.Add(BidActions.Sans);
            }
            // If highestBid == Sans, no higher bids possible
            // Only Pass remains, which will be cleared by the check at the end
        }

        public override string ToString()
        {
            var status = IsEliminated ? "Eliminated" : IsPassed ? "Passed" : "Active";
            var track = CommittedTrack != BiddingTrack.None ? $" [{CommittedTrack}]" : "";
            var currentBid = CurrentBidAction.HasValue ? $" Current: {CurrentBidAction.Value.GetDisplayName()}" : "";
            var lastActive = LastActiveBidAction.HasValue && LastActiveBidAction != CurrentBidAction 
                ? $" (Last Active: {LastActiveBidAction.Value.GetDisplayName()})" : "";
            return $"Player {PlayerId}: {status}{track}{currentBid}{lastActive}";
        }
    }
}
