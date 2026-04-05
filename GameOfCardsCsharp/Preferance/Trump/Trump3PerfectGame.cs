using System;
using System.Collections.Generic;
using System.Linq;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Trump
{
    /// <summary>
    /// Perfect information game for 3-player Trump.
    /// Currently uses heuristic-based strategy (Trump3HeuristicGame).
    /// Future: Will add minimax search with heuristic position evaluation.
    /// </summary>
    public class Trump3PerfectGame
    {
        private readonly PerfPerfectGameState _state;
        private readonly Trump3HeuristicGame _heuristicGame;
        private readonly int _declarerIndex;

        public PerfPerfectGameState State => _state;

        public Trump3PerfectGame(PerfPerfectGameState state, int declarerIndex)
        {
            if (state.Players.Count != 3)
            {
                throw new ArgumentException("Trump3PerfectGame only supports 3 players", nameof(state));
            }

            if (state.GameMode != PreferanceGameMode.Trump)
            {
                throw new ArgumentException("Trump3PerfectGame requires Trump game mode", nameof(state));
            }

            if (state.TrumpSuit == TrumpSuit.None)
            {
                throw new ArgumentException("Trump suit must be specified for Trump games", nameof(state));
            }

            _state = state;
            _declarerIndex = declarerIndex;
            _heuristicGame = new Trump3HeuristicGame(state, declarerIndex);
        }

        /// <summary>
        /// Calculates the best lead cards for the current player in a 3-player game.
        /// Returns a list of PerfectCardMove with ExpectedTricks already populated.
        /// </summary>
        public List<PerfectCardMove> BestLeadCard()
        {
            // Use heuristic game to select best lead
            var bestMove = _heuristicGame.BestLeadCard();
            
            // Estimate the score if we play this lead
            var estimatedScore = _heuristicGame.EstimateScore();
            
            // Attach expected tricks to the move
            var moveWithScore = bestMove.WithExpectedTricks(estimatedScore.IndividualTricks);
            
            // Return as list (for consistency with Trump2PerfectGame API)
            return new List<PerfectCardMove> { moveWithScore };
        }

        /// <summary>
        /// Calculates the best follow card for the current player responding to a lead card.
        /// Returns a PerfectCardMove with ExpectedTricks already populated.
        /// </summary>
        public PerfectCardMove BestFollowCard(PerfectCardMove leadMove, PerfectCardMove? firstFollowMove = null)
        {
            if (_state.CurrentPlayerIndex == leadMove.PlayerIndex)
            {
                throw new InvalidOperationException(
                    $"Cannot find follow move: current player ({_state.CurrentPlayerIndex}) is the same as lead player ({leadMove.PlayerIndex}).");
            }

            if (firstFollowMove != null)
            {
                if (_state.CurrentPlayerIndex == firstFollowMove.PlayerIndex)
                {
                    throw new InvalidOperationException(
                        $"Cannot find follow move: current player ({_state.CurrentPlayerIndex}) is the same as first follow player ({firstFollowMove.PlayerIndex}).");
                }

                if (leadMove.PlayerIndex == firstFollowMove.PlayerIndex)
                {
                    throw new InvalidOperationException(
                        $"Invalid state: lead player ({leadMove.PlayerIndex}) is the same as first follow player ({firstFollowMove.PlayerIndex}).");
                }
            }

            var bestMove = _heuristicGame.BestFollowCard(leadMove, firstFollowMove);
            var estimatedScore = EstimateScoreAfterFollow(leadMove, firstFollowMove, bestMove);
            
            return bestMove.WithExpectedTricks(estimatedScore.IndividualTricks);
        }

        /// <summary>
        /// Estimates the score after playing a follow move.
        /// Simulates the trick completion and uses heuristic evaluation for remaining play.
        /// </summary>
        private Score3 EstimateScoreAfterFollow(
            PerfectCardMove leadMove, 
            PerfectCardMove? firstFollowMove, 
            PerfectCardMove currentFollowMove)
        {
            _state.Moves[(int)leadMove.Card.Suit][leadMove.ListIndex].Available = false;
            
            if (firstFollowMove != null)
            {
                _state.Moves[(int)firstFollowMove.Card.Suit][firstFollowMove.ListIndex].Available = false;
            }
            
            _state.Moves[(int)currentFollowMove.Card.Suit][currentFollowMove.ListIndex].Available = false;

            int originalPlayer = _state.CurrentPlayerIndex;
            int winnerId;
            PerfectCardMove? thirdMove = null;
            
            if (firstFollowMove != null)
            {
                // We have all 3 cards - determine winner directly
                winnerId = DetermineWinner(leadMove, firstFollowMove, currentFollowMove);
            }
            else
            {
                // We need to simulate the third player's move
                int thirdPlayerIndex = GetThirdPlayer(leadMove.PlayerIndex, _state.CurrentPlayerIndex);
                int savedCurrentPlayer = _state.CurrentPlayerIndex;
                _state.CurrentPlayerIndex = thirdPlayerIndex;
                
                thirdMove = _heuristicGame.BestFollowCard(leadMove, currentFollowMove);
                _state.Moves[(int)thirdMove.Card.Suit][thirdMove.ListIndex].Available = false;
                
                winnerId = DetermineWinner(leadMove, currentFollowMove, thirdMove);
                _state.CurrentPlayerIndex = savedCurrentPlayer;
            }

            // Winner leads next trick
            _state.CurrentPlayerIndex = winnerId;
            
            // Calculate future score with all three cards marked as unavailable
            var futureScore = _heuristicGame.EstimateScore();
            var result = futureScore.IncrementPlayer(winnerId, _declarerIndex);

            // NOW restore all the cards back to their original state
            _state.Moves[(int)leadMove.Card.Suit][leadMove.ListIndex].Available = true;
            
            if (firstFollowMove != null)
            {
                _state.Moves[(int)firstFollowMove.Card.Suit][firstFollowMove.ListIndex].Available = true;
            }
            
            _state.Moves[(int)currentFollowMove.Card.Suit][currentFollowMove.ListIndex].Available = true;
            
            // Restore third move if it was simulated
            if (thirdMove != null)
            {
                _state.Moves[(int)thirdMove.Card.Suit][thirdMove.ListIndex].Available = true;
            }
            
            _state.CurrentPlayerIndex = originalPlayer;

            return result;
        }

        private int DetermineWinner(PerfectCardMove leadMove, PerfectCardMove follow1, PerfectCardMove follow2)
        {
            var trumpSuit = ConvertTrumpSuitToSuit(_state.TrumpSuit);
            var leadSuit = leadMove.Card.Suit;
            
            var trumpCards = new List<(PerfectCardMove move, int playerIndex)>();
            
            if (leadMove.Card.Suit == trumpSuit)
                trumpCards.Add((leadMove, leadMove.PlayerIndex));
            if (follow1.Card.Suit == trumpSuit)
                trumpCards.Add((follow1, follow1.PlayerIndex));
            if (follow2.Card.Suit == trumpSuit)
                trumpCards.Add((follow2, follow2.PlayerIndex));
            
            if (trumpCards.Any())
            {
                return trumpCards.OrderByDescending(t => t.move.Card.Rank).First().playerIndex;
            }
            
            var leadSuitCards = new List<(PerfectCardMove move, int playerIndex)>();
            
            if (leadMove.Card.Suit == leadSuit)
                leadSuitCards.Add((leadMove, leadMove.PlayerIndex));
            if (follow1.Card.Suit == leadSuit)
                leadSuitCards.Add((follow1, follow1.PlayerIndex));
            if (follow2.Card.Suit == leadSuit)
                leadSuitCards.Add((follow2, follow2.PlayerIndex));
            
            return leadSuitCards.OrderByDescending(t => t.move.Card.Rank).First().playerIndex;
        }

        private int GetThirdPlayer(int leadPlayerIndex, int currentPlayerIndex)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i != leadPlayerIndex && i != currentPlayerIndex)
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Could not determine third player");
        }

        private static Suit ConvertTrumpSuitToSuit(TrumpSuit trumpSuit)
        {
            return trumpSuit switch
            {
                TrumpSuit.Spades => Suit.Spades,
                TrumpSuit.Diamonds => Suit.Diamonds,
                TrumpSuit.Hearts => Suit.Hearts,
                TrumpSuit.Clubs => Suit.Clubs,
                _ => throw new ArgumentException($"Invalid trump suit: {trumpSuit}")
            };
        }

        public void PlayMove(PerfectCardMove move)
        {
            _state.Moves[(int)move.Card.Suit][move.ListIndex].Available = false;
            _state.AdvanceTurn();
        }
    }
}
