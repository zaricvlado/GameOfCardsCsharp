using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using GameOfCardsCsharp;
using GameOfCardsCsharp.Preferance.Common;
using Microsoft.VSDiagnostics;

namespace GameOfCardsCsharp.Benchmarks
{
    [CPUUsageDiagnoser]
    public class SimulateDefense3PBenchmark
    {
        private PerfPerfectGameState _state = null !;
        [GlobalSetup]
        public void Setup()
        {
            // Representative 10-card 3-player layout, declarer = P0.
            var players = new List<string>
            {
                "P0",
                "P1",
                "P2"
            };
            _state = new PerfPerfectGameState(PreferanceGameMode.Sans, players, declarerIndex: 0);
            // Player 0 (declarer): A,K,Q clubs + A,J diamonds + A hearts + K,Q,J,10 spades
            _state.AddPlayerCards(0, new[] { new Card(Rank.Ace, Suit.Clubs), new Card(Rank.King, Suit.Clubs), new Card(Rank.Queen, Suit.Clubs), new Card(Rank.Ace, Suit.Diamonds), new Card(Rank.Jack, Suit.Diamonds), new Card(Rank.Ace, Suit.Hearts), new Card(Rank.King, Suit.Spades), new Card(Rank.Queen, Suit.Spades), new Card(Rank.Jack, Suit.Spades), new Card(Rank.Ten, Suit.Spades), });
            // Player 1: balanced mid-range
            _state.AddPlayerCards(1, new[] { new Card(Rank.Jack, Suit.Clubs), new Card(Rank.Ten, Suit.Clubs), new Card(Rank.King, Suit.Diamonds), new Card(Rank.Queen, Suit.Diamonds), new Card(Rank.Ten, Suit.Diamonds), new Card(Rank.King, Suit.Hearts), new Card(Rank.Queen, Suit.Hearts), new Card(Rank.Jack, Suit.Hearts), new Card(Rank.Nine, Suit.Spades), new Card(Rank.Eight, Suit.Spades), });
            // Player 2: low cards
            _state.AddPlayerCards(2, new[] { new Card(Rank.Nine, Suit.Clubs), new Card(Rank.Eight, Suit.Clubs), new Card(Rank.Seven, Suit.Clubs), new Card(Rank.Nine, Suit.Diamonds), new Card(Rank.Eight, Suit.Diamonds), new Card(Rank.Ten, Suit.Hearts), new Card(Rank.Nine, Suit.Hearts), new Card(Rank.Eight, Suit.Hearts), new Card(Rank.Seven, Suit.Spades), new Card(Rank.Seven, Suit.Hearts), });
        }

        /// <summary>
        /// Exercises AnalyzeSuitDiscard3P -> SimulateDefense3P x2 across all 4 suits,
        /// from the perspective of defender P1 attacked by declarer P0 (partner = P2).
        /// </summary>
        [Benchmark]
        public int AnalyzeAllSuits_DefenderP1()
        {
            int sum = 0;
            for (int s = 0; s < 4; s++)
            {
                var a = _state.AnalyzeSuitDiscard3P((Suit)s, attackerIndex: 0, defenderIndex: 1);
                sum += a.DiscardCost.DiscardPriority;
            }

            return sum;
        }

        /// <summary>
        /// Exercises the Case B branch of SimulateDefense3P (defender == declarer).
        /// </summary>
        [Benchmark]
        public int AnalyzeAllSuits_DefenderIsDeclarer()
        {
            int sum = 0;
            for (int s = 0; s < 4; s++)
            {
                var a = _state.AnalyzeSuitDiscard3P((Suit)s, attackerIndex: 1, defenderIndex: 0);
                sum += a.DiscardCost.DiscardPriority;
            }

            return sum;
        }
    }
}