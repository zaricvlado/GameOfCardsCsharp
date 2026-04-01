using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Preferance.Bidding
{
    /// <summary>
    /// Represents a discrete bidding action as a union of bidding-related enums.
    /// Each action type sets specific enum values and leaves others as None.
    /// </summary>
    public readonly struct BidAction
    {
        public BiddingTrack Track { get; }
        public RegularBid RegularBidValue { get; }
        public TrumpBid TrumpBidValue { get; }
        public TrumpSuit TrumpSuitValue { get; }
        public bool IsPass { get; }

        private BidAction(BiddingTrack track, RegularBid regularBid, TrumpBid trumpBid, TrumpSuit trumpSuit, bool isPass)
        {
            Track = track;
            RegularBidValue = regularBid;
            TrumpBidValue = trumpBid;
            TrumpSuitValue = trumpSuit;
            IsPass = isPass;
        }

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates a Pass action
        /// Track: None, All enums: None
        /// </summary>
        public static BidAction Pass()
            => new(BiddingTrack.None, RegularBid.None, TrumpBid.None, TrumpSuit.None, isPass: true);

        /// <summary>
        /// Creates a Regular bid action (2-7)
        /// Track: Regular, RegularBid: specified, Others: None
        /// </summary>
        public static BidAction Regular(RegularBid bid)
            => new(BiddingTrack.Regular, bid, TrumpBid.None, TrumpSuit.None, isPass: false);

        /// <summary>
        /// Creates a Trump track bid action (Trump/Betl/Sans)
        /// Track: Trump, TrumpBid: specified, Others: None
        /// </summary>
        public static BidAction TrumpTrack(TrumpBid bid)
            => new(BiddingTrack.Trump, RegularBid.None, bid, TrumpSuit.None, isPass: false);

        /// <summary>
        /// Creates a Trump suit declaration action
        /// Track: TrumpDeclaration, TrumpSuit: specified, TrumpBid: Trump, RegularBid: None
        /// </summary>
        public static BidAction TrumpDeclaration(TrumpSuit suit)
            => new(BiddingTrack.TrumpDeclaration, RegularBid.None, TrumpBid.Trump, suit, isPass: false);

        // ==================== QUERY METHODS ====================

        public bool IsRegularBid() => RegularBidValue != RegularBid.None;
        public bool IsTrumpBid() => TrumpBidValue != TrumpBid.None && Track == BiddingTrack.Trump;
        public bool IsTrumpDeclaration() => Track == BiddingTrack.TrumpDeclaration;

        /// <summary>
        /// Gets the bidding priority for comparison (higher beats lower)
        /// </summary>
        public int GetPriority()
        {
            if (IsPass) return 0;
            if (IsRegularBid()) return (int)RegularBidValue;
            if (IsTrumpBid()) return 100 + (int)TrumpBidValue;
            if (IsTrumpDeclaration()) return (int)TrumpSuitValue;
            return 0;
        }

        /// <summary>
        /// Creates a PlayerBid from this action for a specific player
        /// </summary>
        public PlayerBid CreatePlayerBid(int playerId)
        {
            if (IsPass)
                return PlayerBid.Pass(playerId);

            if (IsRegularBid())
                return PlayerBid.Regular(playerId, RegularBidValue);

            if (IsTrumpDeclaration())
                return PlayerBid.Trump(playerId, TrumpSuitValue);

            if (IsTrumpBid())
            {
                return TrumpBidValue switch
                {
                    TrumpBid.Trump => PlayerBid.Trump(playerId),
                    TrumpBid.Betl => PlayerBid.TrumpBetl(playerId),
                    TrumpBid.Sans => PlayerBid.TrumpSans(playerId),
                    _ => PlayerBid.Pass(playerId)
                };
            }

            return PlayerBid.Pass(playerId);
        }

        public string GetDisplayName()
        {
            if (IsPass) return "Pass";

            if (IsRegularBid())
            {
                return RegularBidValue switch
                {
                    RegularBid.Two => "2 (Spades)",
                    RegularBid.Three => "3 (Diamonds)",
                    RegularBid.Four => "4 (Hearts)",
                    RegularBid.Five => "5 (Clubs)",
                    RegularBid.Six => "6 (Betl)",
                    RegularBid.Seven => "7 (Sans)",
                    _ => "Unknown"
                };
            }

            if (IsTrumpBid())
            {
                return TrumpBidValue switch
                {
                    TrumpBid.Trump => "Trump",
                    TrumpBid.Betl => "Betl",
                    TrumpBid.Sans => "Sans",
                    _ => "Unknown"
                };
            }

            if (IsTrumpDeclaration())
            {
                return TrumpSuitValue switch
                {
                    TrumpSuit.Spades => "Trump: Spades",
                    TrumpSuit.Diamonds => "Trump: Diamonds",
                    TrumpSuit.Hearts => "Trump: Hearts",
                    TrumpSuit.Clubs => "Trump: Clubs",
                    _ => "Unknown"
                };
            }

            return "Unknown";
        }

        public override string ToString() => GetDisplayName();

        public override bool Equals(object? obj)
        {
            return obj is BidAction action &&
                   Track == action.Track &&
                   RegularBidValue == action.RegularBidValue &&
                   TrumpBidValue == action.TrumpBidValue &&
                   TrumpSuitValue == action.TrumpSuitValue &&
                   IsPass == action.IsPass;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Track, RegularBidValue, TrumpBidValue, TrumpSuitValue, IsPass);
        }

        public static bool operator ==(BidAction left, BidAction right) => left.Equals(right);
        public static bool operator !=(BidAction left, BidAction right) => !left.Equals(right);
    }

    /// <summary>
    /// Static factory for all predefined bid actions
    /// </summary>
    public static class BidActions
    {
        // ==================== PASS ====================
        public static readonly BidAction Pass = BidAction.Pass();

        // ==================== REGULAR BIDS (2-7) ====================
        public static readonly BidAction Bid2 = BidAction.Regular(RegularBid.Two);
        public static readonly BidAction Bid3 = BidAction.Regular(RegularBid.Three);
        public static readonly BidAction Bid4 = BidAction.Regular(RegularBid.Four);
        public static readonly BidAction Bid5 = BidAction.Regular(RegularBid.Five);
        public static readonly BidAction Bid6 = BidAction.Regular(RegularBid.Six);
        public static readonly BidAction Bid7 = BidAction.Regular(RegularBid.Seven);

        // ==================== TRUMP TRACK BIDS ====================
        public static readonly BidAction Trump = BidAction.TrumpTrack(TrumpBid.Trump);
        public static readonly BidAction Betl = BidAction.TrumpTrack(TrumpBid.Betl);
        public static readonly BidAction Sans = BidAction.TrumpTrack(TrumpBid.Sans);

        // ==================== TRUMP DECLARATIONS ====================
        public static readonly BidAction TrumpSpades = BidAction.TrumpDeclaration(TrumpSuit.Spades);
        public static readonly BidAction TrumpDiamonds = BidAction.TrumpDeclaration(TrumpSuit.Diamonds);
        public static readonly BidAction TrumpHearts = BidAction.TrumpDeclaration(TrumpSuit.Hearts);
        public static readonly BidAction TrumpClubs = BidAction.TrumpDeclaration(TrumpSuit.Clubs);

        // ==================== COLLECTIONS ====================
        public static readonly List<BidAction> AllRegularBids = new()
        {
            Bid2, Bid3, Bid4, Bid5, Bid6, Bid7
        };

        public static readonly List<BidAction> AllTrumpBids = new()
        {
            Trump, Betl, Sans
        };

        public static readonly List<BidAction> AllTrumpDeclarations = new()
        {
            TrumpSpades, TrumpDiamonds, TrumpHearts, TrumpClubs
        };

        public static readonly List<BidAction> AllBids = new List<BidAction>
        {
            Pass,
            Bid2, Bid3, Bid4, Bid5, Bid6, Bid7,
            Trump, Betl, Sans,
            TrumpSpades, TrumpDiamonds, TrumpHearts, TrumpClubs
        };

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Gets a regular bid action by enum value
        /// </summary>
        public static BidAction GetRegularBid(RegularBid bid)
        {
            return bid switch
            {
                RegularBid.Two => Bid2,
                RegularBid.Three => Bid3,
                RegularBid.Four => Bid4,
                RegularBid.Five => Bid5,
                RegularBid.Six => Bid6,
                RegularBid.Seven => Bid7,
                _ => throw new ArgumentException($"Invalid regular bid: {bid}")
            };
        }

        /// <summary>
        /// Gets a trump declaration action by suit
        /// </summary>
        public static BidAction GetTrumpDeclaration(TrumpSuit suit)
        {
            return suit switch
            {
                TrumpSuit.Spades => TrumpSpades,
                TrumpSuit.Diamonds => TrumpDiamonds,
                TrumpSuit.Hearts => TrumpHearts,
                TrumpSuit.Clubs => TrumpClubs,
                _ => throw new ArgumentException($"Invalid trump suit: {suit}")
            };
        }

        /// <summary>
        /// Gets a trump track bid action by enum value
        /// </summary>
        public static BidAction GetTrumpBid(TrumpBid bid)
        {
            return bid switch
            {
                TrumpBid.Trump => Trump,
                TrumpBid.Betl => Betl,
                TrumpBid.Sans => Sans,
                _ => throw new ArgumentException($"Invalid trump bid: {bid}")
            };
        }
    }
}
