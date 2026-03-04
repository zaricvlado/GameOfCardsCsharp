using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;

namespace GameOfCardsCsharp.Tests
{
    public class TablicGameEngineTests
    {
        [Fact]
        public void TablicGameEngine_StartNewGame_ShouldInitializeGame()
        {
            var engine = new TablicGameEngine();

            var result = engine.StartNewGame();

            Assert.True(result.Success);
            Assert.False(engine.IsGameFinished());
            Assert.Equal(0, engine.GetCurrentPlayerId());
            Assert.True(engine.IsWaitingForMove());
        }

        [Fact]
        public void TablicGameEngine_StartNewGame_WhenAlreadyInProgress_ShouldFail()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var result = engine.StartNewGame();

            Assert.False(result.Success);
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_WithWrongPlayerId_ShouldFail()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var move = new PlayerMove
            {
                PlayerId = 1, // Wrong player (should be 0)
                HandIndex = 0,
                AttemptPickup = false
            };

            var result = engine.SubmitMove(move);

            Assert.False(result.Success);
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_WithInvalidHandIndex_ShouldFail()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var move = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = 99, // Invalid index
                AttemptPickup = false
            };

            var result = engine.SubmitMove(move);

            Assert.False(result.Success);
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_WithNegativeHandIndex_ShouldFail()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var move = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = -1,
                AttemptPickup = false
            };

            var result = engine.SubmitMove(move);

            Assert.False(result.Success);
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_WithInvalidTalonIndex_ShouldFail()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var move = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = 0,
                AttemptPickup = true,
                TalonIndices = new List<int> { 99 } // Invalid talon index
            };

            var result = engine.SubmitMove(move);

            Assert.False(result.Success);
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_WithDuplicateTalonIndices_ShouldFail()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var move = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = 0,
                AttemptPickup = true,
                TalonIndices = new List<int> { 0, 0 } // Duplicate
            };

            var result = engine.SubmitMove(move);

            Assert.False(result.Success);
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_WithEmptyTalonIndicesWhenAttemptingPickup_ShouldFail()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var move = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = 0,
                AttemptPickup = true,
                TalonIndices = new List<int>() // Empty
            };

            var result = engine.SubmitMove(move);

            Assert.False(result.Success);
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_ValidMove_ShouldSucceed()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var move = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = 0,
                AttemptPickup = false
            };

            var result = engine.SubmitMove(move);

            Assert.True(result.Success);
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_ShouldSwitchPlayer()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            Assert.Equal(0, engine.GetCurrentPlayerId());

            var move = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = 0,
                AttemptPickup = false
            };
            engine.SubmitMove(move);

            Assert.Equal(1, engine.GetCurrentPlayerId());
        }

        [Fact]
        public void TablicGameEngine_SubmitMove_WhenNotWaitingForMove_ShouldFail()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var move1 = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = 0,
                AttemptPickup = false
            };
            engine.SubmitMove(move1);

            // Try to submit another move for same player
            var move2 = new PlayerMove
            {
                PlayerId = 0,
                HandIndex = 1,
                AttemptPickup = false
            };

            var result = engine.SubmitMove(move2);

            Assert.False(result.Success);
        }

        [Fact]
        public void TablicGameEngine_NeedsNewRound_AfterRoundComplete_ShouldReturnTrue()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            // Play all 6 cards for both players (12 moves total)
            for (int i = 0; i < 12; i++)
            {
                var move = new PlayerMove
                {
                    PlayerId = engine.GetCurrentPlayerId(),
                    HandIndex = 0,
                    AttemptPickup = false
                };
                engine.SubmitMove(move);
            }

            Assert.True(engine.IsRoundComplete());
            Assert.True(engine.NeedsNewRound());
        }

        [Fact]
        public void TablicGameEngine_AIPlayer_RequestMove_ShouldReturnValidMove()
        {
            var engine = new TablicGameEngine();
            var rules = new TablicRules();
            var aiPlayer = new AIPlayer(0, new RandomStrategy(rules), "Test AI");

            engine.SetPlayer(0, aiPlayer);
            engine.StartNewGame();

            var move = aiPlayer.RequestMove(engine);

            Assert.NotNull(move);
            Assert.Equal(0, move.PlayerId);
            Assert.True(move.HandIndex >= 0 && move.HandIndex < 6);
        }

        [Fact]
        public void TablicGameEngine_HumanPlayer_RequestMove_ShouldReturnNull()
        {
            var engine = new TablicGameEngine();
            var humanPlayer = new HumanPlayer(0, "Test Human");

            engine.SetPlayer(0, humanPlayer);
            engine.StartNewGame();

            var move = humanPlayer.RequestMove(engine);

            Assert.Null(move);
        }

        [Fact]
        public void TablicGameEngine_GetPossibleMoves_ShouldReturnMovesForCurrentPlayer()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            var moves = engine.GetPossibleMoves();

            Assert.Equal(6, moves.Count); // 6 cards in hand
        }

        [Fact]
        public void TablicGameEngine_IsGameFinished_AfterFourRounds_ShouldReturnTrue()
        {
            var engine = new TablicGameEngine();
            engine.StartNewGame();

            // Play all 4 rounds (6 cards × 2 players × 4 rounds = 48 moves)
            for (int round = 0; round < 4; round++)
            {
                for (int card = 0; card < 12; card++)
                {
                    var move = new PlayerMove
                    {
                        PlayerId = engine.GetCurrentPlayerId(),
                        HandIndex = 0,
                        AttemptPickup = false
                    };
                    engine.SubmitMove(move);
                }

                if (round < 3)
                {
                    engine.StartNewRound();
                }
            }

            Assert.True(engine.IsGameFinished());
        }

        [Fact]
        public void TablicGameEngine_GetPlayer_ValidId_ShouldReturnPlayer()
        {
            var engine = new TablicGameEngine();

            var player0 = engine.GetPlayer(0);
            var player1 = engine.GetPlayer(1);

            Assert.NotNull(player0);
            Assert.NotNull(player1);
            Assert.Equal(0, player0.GetPlayerId());
            Assert.Equal(1, player1.GetPlayerId());
        }

        [Fact]
        public void TablicGameEngine_GetPlayer_InvalidId_ShouldReturnNull()
        {
            var engine = new TablicGameEngine();

            var player = engine.GetPlayer(99);

            Assert.Null(player);
        }

        [Fact]
        public void TablicGameEngine_SetPlayer_ShouldReplacePlayer()
        {
            var engine = new TablicGameEngine();
            var rules = new TablicRules();

            var aiPlayer = new AIPlayer(0, new GreedyStrategy(rules), "Custom AI");

            engine.SetPlayer(0, aiPlayer);

            var player = engine.GetPlayer(0);
            Assert.NotNull(player);
            Assert.Equal(PlayerType.AI, player.GetPlayerType());
        }
    }
}