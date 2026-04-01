using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.GamePlay
{
    /// <summary>
    /// Base state machine for game play logic.
    /// Handles trick-taking, legal move validation, and scoring.
    /// </summary>
    internal abstract class GamePlayStateMachine
    {
        protected readonly RoleAssignmentResult _roles;
        protected readonly Dictionary<int, PreferancePlayer> _players;
        protected readonly Dictionary<int, int> _actualTrickCounts; // Actual tricks won by each player
        protected readonly Dictionary<int, int> _attributedTrickCounts; // Tricks attributed for scoring (Partner → Defender)
        protected readonly List<TrickState> _tricks;

        public int CurrentTrick { get; protected set; }
        public int CurrentLeaderId { get; protected set; }

        protected GamePlayStateMachine(
            RoleAssignmentResult roles,
            Dictionary<int, PreferancePlayer> players)
        {
            _roles = roles;
            _players = players;
            _actualTrickCounts = new Dictionary<int, int>();
            _attributedTrickCounts = new Dictionary<int, int>();
            _tricks = new List<TrickState>();

            // Initialize trick counts only (hands are managed by players directly)
            foreach (var playerId in players.Keys)
            {
                _actualTrickCounts[playerId] = 0;
                _attributedTrickCounts[playerId] = 0;
            }

            CurrentTrick = 0;
            CurrentLeaderId = roles.DeclarerId; // Declarer leads first trick
        }

        /// <summary>
        /// Starts a new trick
        /// </summary>
        public virtual void StartNewTrick()
        {
            CurrentTrick++;
            _tricks.Add(new TrickState(CurrentTrick, CurrentLeaderId));
        }

        /// <summary>
        /// Gets players who participate in the current trick (excludes spectators)
        /// </summary>
        public List<int> GetPlayersInTrick()
        {
            var activePlayers = new List<int> { _roles.DeclarerId };
            activePlayers.AddRange(_roles.DefenderIds);
            
            if (_roles.PartnerId.HasValue)
            {
                activePlayers.Add(_roles.PartnerId.Value);
            }

            // Order players starting from current leader
            return OrderPlayersFromLeader(activePlayers);
        }

        /// <summary>
        /// Orders players clockwise from the current leader
        /// </summary>
        protected List<int> OrderPlayersFromLeader(List<int> playerIds)
        {
            var ordered = new List<int>();
            var sortedIds = playerIds.OrderBy(id => id).ToList();
            var leaderIndex = sortedIds.IndexOf(CurrentLeaderId);

            for (int i = 0; i < sortedIds.Count; i++)
            {
                var index = (leaderIndex + i) % sortedIds.Count;
                ordered.Add(sortedIds[index]);
            }

            return ordered;
        }

        /// <summary>
        /// Plays a card for a player
        /// </summary>
        public virtual void PlayCard(int playerId, Card card)
        {
            var hand = _players[playerId].GetHand();
            
            if (!hand.GetCards().Contains(card))
                throw new InvalidOperationException($"Player {playerId} doesn't have card {card}");

            // Remove from player's hand (single source of truth)
            hand.RemoveCard(card);
            
            _tricks[CurrentTrick - 1].PlayCard(playerId, card);
        }

        /// <summary>
        /// Gets legal moves for a player
        /// </summary>
        public abstract List<Card> GetLegalMoves(int playerId);

        /// <summary>
        /// Completes the current trick and determines winner
        /// </summary>
        public virtual int CompleteTrick()
        {
            if (CurrentTrick == 0 || _tricks.Count < CurrentTrick)
                throw new InvalidOperationException("No active trick to complete");

            var trick = _tricks[CurrentTrick - 1];
            var winnerId = DetermineWinner(trick);
            
            // Award trick cards to appropriate pile(s) based on contract rules
            AwardTrickCards(trick, winnerId);
            
            // Record actual trick winner (for scoring)
            _actualTrickCounts[winnerId]++;
            trick.SetWinner(winnerId);
            
            // Attribute trick for scoring purposes
            AttributeTrickForScoring(winnerId);
            
            // Winner leads next trick
            CurrentLeaderId = winnerId;

            return winnerId;
        }

        /// <summary>
        /// Awards trick cards to player pile(s).
        /// Override in derived classes for special rules (e.g., Betl).
        /// Default: cards go to trick winner (Sans, Trump)
        /// </summary>
        protected virtual void AwardTrickCards(TrickState trick, int winnerId)
        {
            // Default behavior: Award to trick winner
            foreach (var card in trick.CardsPlayed.Values)
            {
                _players[winnerId].AddToPile(card);
            }
        }

        /// <summary>
        /// Attributes the trick for scoring based on role rules:
        /// - If winner is Partner, attribute to the Defender who called them
        /// - Otherwise, attribute to the actual winner
        /// </summary>
        private void AttributeTrickForScoring(int actualWinnerId)
        {
            // Check if the winner is a Partner
            if (_roles.PartnerId.HasValue && actualWinnerId == _roles.PartnerId.Value)
            {
                // Partner's tricks go to the Defender who called them
                var defenderId = _roles.DefenderIds.First();
                _attributedTrickCounts[defenderId]++;
            }
            else
            {
                // Normal attribution - winner keeps their trick
                _attributedTrickCounts[actualWinnerId]++;
            }
        }

        /// <summary>
        /// Determines the winner of a trick
        /// </summary>
        protected abstract int DetermineWinner(TrickState trick);

        /// <summary>
        /// Checks if the game is complete
        /// </summary>
        public virtual bool IsGameComplete()
        {
            // Game complete when all ACTIVE players (excluding spectators) have empty hands
            var activePlayers = GetPlayersInTrick(); // Returns Declarer + Defenders + Partner (no Spectators)
            
            return activePlayers.All(playerId => _players[playerId].GetHand().CardCount() == 0);
        }

        /// <summary>
        /// Gets the lead card of the current trick (null if no cards played yet)
        /// </summary>
        public Card? GetLeadCard()
        {
            if (CurrentTrick == 0 || _tricks[CurrentTrick - 1].CardsPlayed.Count == 0)
                return null;

            return _tricks[CurrentTrick - 1].CardsPlayed.Values.First();
        }

        /// <summary>
        /// Gets all cards played in a specific trick
        /// </summary>
        public Dictionary<int, Card> GetTrickCards(int trickNumber)
        {
            if (trickNumber < 1 || trickNumber > _tricks.Count)
                return new Dictionary<int, Card>();

            return new Dictionary<int, Card>(_tricks[trickNumber - 1].CardsPlayed);
        }

        /// <summary>
        /// Gets the final game result with proper trick attribution
        /// </summary>
        public virtual GamePlayResult GetResult()
        {
            // Use attributed tricks for scoring
            var declarerTricks = _attributedTrickCounts[_roles.DeclarerId];
            
            // Sum all defender tricks (including Partner's attributed tricks)
            var defenderTricks = _roles.DefenderIds.Sum(id => _attributedTrickCounts[id]);

            bool declarerWon = EvaluateDeclarerSuccess(declarerTricks);

            return new GamePlayResult(
                _roles.Contract,
                declarerWon,
                declarerTricks,
                defenderTricks,
                new Dictionary<int, int>(_actualTrickCounts),
                new Dictionary<int, int>(_attributedTrickCounts));
        }

        /// <summary>
        /// Gets the current trick counts for all players
        /// </summary>
        public Dictionary<int, int> GetTrickCounts()
        {
            return new Dictionary<int, int>(_actualTrickCounts);
        }

        /// <summary>
        /// Gets the attributed trick counts (with Partner attribution)
        /// </summary>
        public Dictionary<int, int> GetAttributedTrickCounts()
        {
            return new Dictionary<int, int>(_attributedTrickCounts);
        }

        /// <summary>
        /// Evaluates if declarer succeeded based on contract type
        /// </summary>
        protected abstract bool EvaluateDeclarerSuccess(int declarerTricks);
    }

    /// <summary>
    /// Represents the state of a single trick
    /// </summary>
    internal class TrickState
    {
        public int TrickNumber { get; }
        public int LeaderId { get; }
        public Dictionary<int, Card> CardsPlayed { get; }
        public int? WinnerId { get; private set; }

        public TrickState(int trickNumber, int leaderId)
        {
            TrickNumber = trickNumber;
            LeaderId = leaderId;
            CardsPlayed = new Dictionary<int, Card>();
            WinnerId = null;
        }

        public void PlayCard(int playerId, Card card)
        {
            CardsPlayed[playerId] = card;
        }

        public void SetWinner(int winnerId)
        {
            WinnerId = winnerId;
        }
    }
}
