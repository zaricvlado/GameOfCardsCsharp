using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;

namespace GameOfCardsCsharp.Tests
{
    public class TablicGameStateTests
    {
        [Fact]
        public void TablicGameState_InitialPhase_NotStarted()
        {
            var state = new TablicGameState();
            Assert.Equal(GamePhase.NotStarted, state.GetPhase());
        }

        [Fact]
        public void TablicGameState_SetPhase_Updates()
        {
            var state = new TablicGameState();
            state.SetPhase(GamePhase.InProgress);
            Assert.Equal(GamePhase.InProgress, state.GetPhase());
        }

        [Fact]
        public void TablicGameState_SwitchPlayer_Alternates()
        {
            var state = new TablicGameState();
            Assert.Equal(0, state.GetCurrentPlayerId());

            state.SwitchPlayer();
            Assert.Equal(1, state.GetCurrentPlayerId());

            state.SwitchPlayer();
            Assert.Equal(0, state.GetCurrentPlayerId());
        }

        [Fact]
        public void TablicGameState_AddToTalon_IncreasesSize()
        {
            var state = new TablicGameState();
            var card = new Card(Rank.Ace, Suit.Spades);

            state.AddToTalon(card);
            Assert.Equal(1, state.GetTalon().Count);
        }

        [Fact]
        public void TablicGameState_RemoveFromTalon_DecreasesSize()
        {
            var state = new TablicGameState();
            state.AddToTalon(new Card(Rank.Ace, Suit.Spades));
            state.AddToTalon(new Card(Rank.King, Suit.Hearts));
            state.AddToTalon(new Card(Rank.Queen, Suit.Diamonds));

            var indices = new List<int> { 0, 2 };  // Remove first and third
            state.RemoveFromTalon(indices);

            Assert.Equal(1, state.GetTalon().Count);
        }

        [Fact]
        public void TablicGameState_GetSnapshot_ReturnsCorrectData()
        {
            var state = new TablicGameState();
            state.SetPhase(GamePhase.InProgress);
            state.SetCurrentRound(2);
            state.AddToTalon(new Card(Rank.Ace, Suit.Spades));

            var snapshot = state.GetSnapshot();

            Assert.Equal(GamePhase.InProgress, snapshot.Phase);
            Assert.Equal(2, snapshot.CurrentRound);
            Assert.Equal(1, snapshot.Talon.Count);
        }
    }
}