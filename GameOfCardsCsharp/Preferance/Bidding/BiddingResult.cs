using System;

namespace GameOfCardsCsharp.Preferance.Bidding
{
    /// <summary>
    /// Represents the result of a completed bidding phase
    /// </summary>
    public class BiddingResult
    {
        /// <summary>
        /// The winning player ID (null if all players passed)
        /// </summary>
        public int? WinningPlayerId { get; }

        /// <summary>
        /// The winning bid action (null if all players passed)
        /// </summary>
        public BidAction? WinningBid { get; }

        /// <summary>
        /// True if all players passed (no winner)
        /// </summary>
        public bool AllPassed => !WinningPlayerId.HasValue;

        /// <summary>
        /// True if the winning bid was a Regular bid (requires talon exchange)
        /// </summary>
        public bool RequiresTalonExchange => WinningBid.HasValue && WinningBid.Value.IsRegularBid();

        /// <summary>
        /// True if the game can proceed directly to play phase (Trump/Betl/Sans or TrumpDeclaration)
        /// </summary>
        public bool ReadyForPlay => WinningBid.HasValue && !RequiresTalonExchange;

        private BiddingResult(int? winningPlayerId, BidAction? winningBid)
        {
            WinningPlayerId = winningPlayerId;
            WinningBid = winningBid;
        }

        /// <summary>
        /// Creates a result where all players passed
        /// </summary>
        public static BiddingResult AllPassedResult()
            => new BiddingResult(null, null);

        /// <summary>
        /// Creates a result with a winning player and bid
        /// </summary>
        public static BiddingResult Winner(int playerId, BidAction winningBid)
            => new BiddingResult(playerId, winningBid);

        public override string ToString()
        {
            if (AllPassed)
                return "Bidding Result: All players passed";

            return $"Bidding Result: Player {WinningPlayerId} won with {WinningBid?.GetDisplayName()}";
        }
    }
}
