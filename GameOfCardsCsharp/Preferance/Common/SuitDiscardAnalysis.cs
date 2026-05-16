using System.Collections.Generic;

namespace GameOfCardsCsharp.Preferance.Common
{
    /// <summary>
    /// Numeric outcome of a single attacker-leads / defender-responds simulation
    /// in one suit, measured from the defender's perspective. "Sure" refers only
    /// to the assumption that the attacker keeps leading this suit — it is not
    /// a global guarantee for the whole game.
    /// </summary>
    /// <param name="SureTricks">
    /// Tricks the defender wins during the forced exchange, assuming the
    /// attacker keeps leading this suit.
    /// </param>
    /// <param name="LengthTricks">
    /// Cards still remaining in the defender's hand in this suit after the
    /// attacker has run out of leads in it. They are potential length tricks.
    /// </param>
    public readonly record struct SuitDefenseOutcome(int SureTricks, int LengthTricks)
    {
        public static SuitDefenseOutcome operator -(SuitDefenseOutcome a, SuitDefenseOutcome b)
            => new(a.SureTricks - b.SureTricks, a.LengthTricks - b.LengthTricks);

        /// <summary>
        /// Lower is a better discard. Orders priority buckets so that
        /// (0,0) &lt; (0,1) &lt; (1,0) &lt; (1,1) ...
        /// </summary>
        public int DiscardPriority => SureTricks * 100 + LengthTricks;
    }

    /// <summary>
    /// Per-suit analysis used by discard policies (2-player and 3-player).
    ///
    /// Models a single suit being attacked: the <i>attacker</i> leads the suit
    /// repeatedly (highest first) and the <i>defender</i> responds with the
    /// smallest card that beats the lead, otherwise the smallest card. The
    /// simulation runs twice for the defender — once including the discard
    /// <see cref="Candidate"/> (<see cref="WithCandidate"/>) and once excluding
    /// it (<see cref="WithoutCandidate"/>) — so a policy can score the cost of
    /// giving the candidate up.
    ///
    /// Counts in other seats (declarer / partner / second defender) and the
    /// strongest move in the suit are included so a policy doesn't need to
    /// re-query <see cref="PerfPerfectGameState"/>.
    /// </summary>
    public readonly struct SuitDiscardAnalysis
    {
        /// <summary>The suit being analyzed.</summary>
        public Suit Suit { get; }

        /// <summary>
        /// The defender's lowest available card in the suit — i.e. the discard
        /// candidate. <c>null</c> when the defender has no cards in this suit.
        /// </summary>
        public PerfectCardMove? Candidate { get; }

        /// <summary>Defender outcome when keeping the candidate.</summary>
        public SuitDefenseOutcome WithCandidate { get; }

        /// <summary>Defender outcome after discarding the candidate.</summary>
        public SuitDefenseOutcome WithoutCandidate { get; }

        /// <summary>
        /// Highest available card in the suit (and its owner). <c>null</c>
        /// when no cards are available in this suit.
        /// </summary>
        public PerfectCardMove? StrongestMove { get; }

        /// <summary>
        /// Available card counts in the suit, indexed by player.
        /// Length matches <see cref="PerfPerfectGameState.Players"/>.Count (2 or 3).
        /// </summary>
        public IReadOnlyList<int> CardsPerPlayer { get; }

        /// <summary>Number of cards the defender currently has in this suit.</summary>
        public int DefenderLength
            => Candidate is null ? 0 : CardsPerPlayer[Candidate.PlayerIndex];

        /// <summary>
        /// Diff = <see cref="WithCandidate"/> − <see cref="WithoutCandidate"/>.
        /// Lower <see cref="SuitDefenseOutcome.DiscardPriority"/> means a cheaper
        /// (better) discard.
        /// </summary>
        public SuitDefenseOutcome DiscardCost => WithCandidate - WithoutCandidate;

        public SuitDiscardAnalysis(
            Suit suit,
            PerfectCardMove? candidate,
            SuitDefenseOutcome withCandidate,
            SuitDefenseOutcome withoutCandidate,
            PerfectCardMove? strongestMove,
            IReadOnlyList<int> cardsPerPlayer)
        {
            Suit = suit;
            Candidate = candidate;
            WithCandidate = withCandidate;
            WithoutCandidate = withoutCandidate;
            StrongestMove = strongestMove;
            CardsPerPlayer = cardsPerPlayer;
        }
    }
}