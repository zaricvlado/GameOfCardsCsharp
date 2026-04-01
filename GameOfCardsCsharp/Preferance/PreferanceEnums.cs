using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Preferance
{
    /// <summary>
    /// Types of contracts that can be bid in Preferance
    /// </summary>
    public enum ContractType
    {
        None,
        Trump,          // Trump contract (with specific suit)
        Betl,           // Betl contract
        Sans            // Sans contract
    }

    /// <summary>
    /// Trump suits with their bidding values
    /// </summary>
    public enum TrumpSuit
    {
        None = 0,       // No trump (for Sans/Betl)
        Spades = 2,
        Diamonds = 3,
        Hearts = 4,
        Clubs = 5
    }

    /// <summary>
    /// Player's role during contract execution
    /// </summary>
    public enum PlayerRole
    {
        None = 0,
        Declarer,       // Player who won the bidding and plays the contract
        Defender,       // Player defending against the declarer
        Partner,        // Player called by declarer to assist (helps but not affected)
        Spectator       // Player not involved (passed early or game doesn't involve them)
    }

    /// <summary>
    /// Player's current state in the game flow
    /// </summary>
    public enum PlayerState
    {
        None = 0,       // Not yet started or inactive
        Bidding,        // Participating in the bidding phase
        Playing         // Playing cards in the contract phase
    }

    public enum GameState
    {
        NotStarted,
        Bidding,
        Playing,
        Dealing,
        Finished
    }

    /// <summary>
    /// Regular bidding values (2-7) - always gets talon cards
    /// Hierarchy: 2 &lt; 3 &lt; 4 &lt; 5 &lt; 6 &lt; 7
    /// </summary>
    public enum RegularBid
    {
        None = 0,
        Two = 2,        // Spades trump + talon
        Three = 3,      // Diamonds trump + talon
        Four = 4,       // Hearts trump + talon
        Five = 5,       // Clubs trump + talon
        Six = 6,        // Betl + talon
        Seven = 7       // Sans + talon
    }

    /// <summary>
    /// Trump/Game bidding options (no talon) - higher priority than Regular bids
    /// Can only be bid if player hasn't bid before (first bid only)
    /// Once someone bids Trump track, all Regular bidders are eliminated
    /// Hierarchy: Trump &lt; Betl &lt; Sans
    /// </summary>
    public enum TrumpBid
    {
        None = 0,
        Trump = 1,      // Trump (no talon, suit declared later if contested)
        Betl = 2,       // Betl (no talon, lose all tricks)
        Sans = 3        // Sans (no talon, win all tricks)
    }

    /// <summary>
    /// Bidding track - separates Regular (with talon) from Trump (no talon)
    /// Players are locked into one track after their first non-pass bid
    /// </summary>
    public enum BiddingTrack
    {
        None = 0,              // Haven't bid yet (can choose either track)
        Regular = 1,           // Committed to Regular bidding (2-7)
        Trump = 2,             // Committed to Trump bidding (Trump/Betl/Sans)
        TrumpDeclaration = 3   // Declaring trump suit during conflict resolution
    }

    /// <summary>
    /// Represents a player's bid action
    /// </summary>
    public readonly struct PlayerBid
    {
        public int PlayerId { get; }
        public bool IsPass { get; }
        public RegularBid RegularBidValue { get; }
        public TrumpBid TrumpBidValue { get; }
        public TrumpSuit? DeclaredTrump { get; }  // Only set when Trump bids compete

        private PlayerBid(int playerId, bool isPass, RegularBid regularBid, TrumpBid trumpBid, TrumpSuit? declaredTrump)
        {
            PlayerId = playerId;
            IsPass = isPass;
            RegularBidValue = regularBid;
            TrumpBidValue = trumpBid;
            DeclaredTrump = declaredTrump;
        }

        /// <summary>
        /// Creates a Pass bid
        /// </summary>
        public static PlayerBid Pass(int playerId) 
            => new(playerId, true, RegularBid.None, TrumpBid.None, null);

        /// <summary>
        /// Creates a Regular bid (2-7, gets talon)
        /// </summary>
        public static PlayerBid Regular(int playerId, RegularBid bid) 
            => new(playerId, false, bid, TrumpBid.None, null);

        /// <summary>
        /// Creates a Trump bid (no talon, suit declared later if needed)
        /// </summary>
        public static PlayerBid Trump(int playerId, TrumpSuit? declaredSuit = null) 
            => new(playerId, false, RegularBid.None, TrumpBid.Trump, declaredSuit);

        /// <summary>
        /// Creates a Trump Betl bid (no talon, lose all tricks)
        /// </summary>
        public static PlayerBid TrumpBetl(int playerId) 
            => new(playerId, false, RegularBid.None, TrumpBid.Betl, null);

        /// <summary>
        /// Creates a Trump Sans bid (no talon, win all tricks)
        /// </summary>
        public static PlayerBid TrumpSans(int playerId) 
            => new(playerId, false, RegularBid.None, TrumpBid.Sans, null);

        public bool IsRegularBid() => RegularBidValue != RegularBid.None;
        public bool IsTrumpBid() => TrumpBidValue != TrumpBid.None;
        public bool GetsTalon() => IsRegularBid();
        
        public BiddingTrack GetTrack()
        {
            if (IsPass) return BiddingTrack.None;
            if (IsRegularBid()) return BiddingTrack.Regular;
            if (IsTrumpBid()) return DeclaredTrump.HasValue ? BiddingTrack.TrumpDeclaration : BiddingTrack.Trump;
            return BiddingTrack.None;
        }

        /// <summary>
        /// Gets the final contract type from this bid
        /// </summary>
        public ContractType GetContractType()
        {
            if (IsRegularBid())
            {
                return RegularBidValue switch
                {
                    RegularBid.Two => ContractType.Trump,
                    RegularBid.Three => ContractType.Trump,
                    RegularBid.Four => ContractType.Trump,
                    RegularBid.Five => ContractType.Trump,
                    RegularBid.Six => ContractType.Betl,
                    RegularBid.Seven => ContractType.Sans,
                    _ => ContractType.None
                };
            }

            if (IsTrumpBid())
            {
                return TrumpBidValue switch
                {
                    TrumpBid.Trump => ContractType.Trump,
                    TrumpBid.Betl => ContractType.Betl,
                    TrumpBid.Sans => ContractType.Sans,
                    _ => ContractType.None
                };
            }

            return ContractType.None;
        }

        /// <summary>
        /// Gets the trump suit from this bid (if applicable)
        /// </summary>
        public TrumpSuit GetTrumpSuit()
        {
            if (IsRegularBid())
            {
                return RegularBidValue switch
                {
                    RegularBid.Two => TrumpSuit.Spades,
                    RegularBid.Three => TrumpSuit.Diamonds,
                    RegularBid.Four => TrumpSuit.Hearts,
                    RegularBid.Five => TrumpSuit.Clubs,
                    _ => TrumpSuit.None
                };
            }

            if (IsTrumpBid() && TrumpBidValue == TrumpBid.Trump)
            {
                return DeclaredTrump ?? TrumpSuit.None;
            }

            return TrumpSuit.None;
        }

        public override string ToString()
        {
            if (IsPass) return $"Player {PlayerId}: Pass";
            if (IsRegularBid()) return $"Player {PlayerId}: Regular {RegularBidValue}";
            if (IsTrumpBid())
            {
                var trumpInfo = DeclaredTrump.HasValue ? $" ({DeclaredTrump})" : "";
                return $"Player {PlayerId}: Trump {TrumpBidValue}{trumpInfo}";
            }
            return $"Player {PlayerId}: Unknown";
        }
    }

    /// <summary>
    /// Represents the active contract in a Preferance game
    /// </summary>
    public readonly struct Contract
    {
        public ContractType Type { get; }
        public TrumpSuit Trump { get; }
        public int DeclarerPlayerId { get; }
        public bool HasTalon { get; }

        public Contract(ContractType type, TrumpSuit trump, int declarerPlayerId, bool hasTalon)
        {
            Type = type;
            Trump = trump;
            DeclarerPlayerId = declarerPlayerId;
            HasTalon = hasTalon;
        }

        /// <summary>
        /// Creates a contract from a winning bid
        /// </summary>
        public static Contract FromBid(PlayerBid bid)
        {
            return new Contract(
                bid.GetContractType(),
                bid.GetTrumpSuit(),
                bid.PlayerId,
                bid.GetsTalon()
            );
        }

        public bool IsSans() => Type == ContractType.Sans;
        public bool IsBetl() => Type == ContractType.Betl;
        public bool IsTrump() => Type == ContractType.Trump;
        public bool HasTrumpSuit() => Trump != TrumpSuit.None;

        public override string ToString()
        {
            var talonInfo = HasTalon ? " (with talon)" : " (no talon)";
            return Type switch
            {
                ContractType.Trump => $"Trump {Trump}{talonInfo} - Player {DeclarerPlayerId}",
                ContractType.Sans => $"Sans{talonInfo} - Player {DeclarerPlayerId}",
                ContractType.Betl => $"Betl{talonInfo} - Player {DeclarerPlayerId}",
                _ => "No Contract"
            };
        }
    }
}
