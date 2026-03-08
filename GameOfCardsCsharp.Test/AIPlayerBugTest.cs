using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;

namespace GameOfCardsCsharp.Tests
{
    /// <summary>
    /// Test to reproduce the bug where AI player didn't pick up A, K, 6 with 7
    /// From logs: CARD PLAYED: Player=1, Card=7 ♠, Pickup=true, PickedCards=[7 ♣], RemainingTalon=[K ♥, A ♥, 6 ♠]
    /// Expected: Should pick up [A♥, 6♠] because A(1) + 6(6) = 7
    /// </summary>
    public class AIPlayerBugTest
    {
        [Fact]
        public void Test_SevenShouldPickupAceAndSix()
        {
            // Arrange
            var engine = new TablicGameEngine();
            
            // Set up AI player with rules
            var rules = new TablicRules();
            var strategy = new GreedyStrategy(rules);
            var aiPlayer = new AIPlayer(1, strategy, "TestAI");
            
            // Set up a human player for player 0
            var humanPlayer = new HumanPlayer(0, "TestHuman");
            
            engine.SetPlayer(0, humanPlayer);
            engine.SetPlayer(1, aiPlayer);
            
            // Start the game to initialize state
            engine.StartNewGame();
            
            var state = engine.GetState();
            
            // Clear hands and talon to set up our exact scenario
            state.GetPlayer(0).GetHand().Clear();
            state.GetPlayer(1).GetHand().Clear();
            state.ClearTalon();
            
            // Set up the exact scenario from the logs:
            // Talon: [K♥, A♥, 6♠, 7♣]
            state.AddToTalon(new Card(Rank.King, Suit.Hearts));   // K♥
            state.AddToTalon(new Card(Rank.Ace, Suit.Hearts));    // A♥
            state.AddToTalon(new Card(Rank.Six, Suit.Spades));    // 6♠
            state.AddToTalon(new Card(Rank.Seven, Suit.Clubs));   // 7♣
            
            // Give the AI player a 7♠ in hand
            state.GetPlayer(1).GetHand().AddCard(new Card(Rank.Seven, Suit.Spades));
            
            // Set AI as current player
            state.SetCurrentPlayerId(1);
            
            // Act
            var possibleMoves = engine.GetPossibleMoves();
            
            // Debug output
            Console.WriteLine("=== DEBUG: Possible Moves ===");
            for (int i = 0; i < possibleMoves.Count; i++)
            {
                var move = possibleMoves[i];
                Console.WriteLine($"Hand Index {i}: Card={move.Card.Rank} {move.Card.Suit}, CanPickup={move.CanPickup}");
                Console.WriteLine($"  Possible Combinations ({move.PossibleCombinations.Count}):");
                foreach (var combo in move.PossibleCombinations)
                {
                    var cards = combo.Select(idx => state.GetTalon()[idx]).ToList();
                    var cardStrings = cards.Select(c => $"{c.Rank}{GetSuitSymbol(c.Suit)}");
                    var sum = cards.Sum(c => GetCardValue(c));
                    Console.WriteLine($"    [{string.Join(", ", cardStrings)}] - Sum: {sum}");
                }
            }
            
            Console.WriteLine("\n=== Talon Cards ===");
            var talon = state.GetTalon();
            for (int i = 0; i < talon.Count; i++)
            {
                Console.WriteLine($"Index {i}: {talon[i].Rank}{GetSuitSymbol(talon[i].Suit)} (value: {GetCardValue(talon[i])})");
            }
            
            // Assert
            Assert.NotEmpty(possibleMoves);
            var sevenMove = possibleMoves[0]; // Should be 7♠
            
            Assert.True(sevenMove.CanPickup, "7♠ should be able to pickup cards from talon");
            Assert.NotEmpty(sevenMove.PossibleCombinations);
            
            // Check if we have a combination that includes [A, 6]
            bool hasAceSixCombo = sevenMove.PossibleCombinations.Any(combo =>
            {
                if (combo.Count != 2) return false;
                var cards = combo.Select(idx => talon[idx]).ToList();
                return cards.Any(c => c.Rank == Rank.Ace && c.Suit == Suit.Hearts) &&
                       cards.Any(c => c.Rank == Rank.Six && c.Suit == Suit.Spades);
            });
            
            // Check if we have the [7♣] exact match
            bool hasSevenClubsCombo = sevenMove.PossibleCombinations.Any(combo =>
            {
                if (combo.Count != 1) return false;
                var card = talon[combo[0]];
                return card.Rank == Rank.Seven && card.Suit == Suit.Clubs;
            });
            
            Console.WriteLine($"\n=== ASSERTIONS ===");
            Console.WriteLine($"Has [7♣] combo: {hasSevenClubsCombo}");
            Console.WriteLine($"Has [A♥, 6♠] combo: {hasAceSixCombo}");
            
            // The bug: We expect both combinations to be present
            Assert.True(hasSevenClubsCombo, "Should have [7♣] exact match combination");
            Assert.True(hasAceSixCombo, "Should have [A♥, 6♠] combination (A=1 + 6=6 = 7)");
        }
        
        private string GetSuitSymbol(Suit suit)
        {
            return suit switch
            {
                Suit.Hearts => "♥",
                Suit.Diamonds => "♦",
                Suit.Clubs => "♣",
                Suit.Spades => "♠",
                _ => "?"
            };
        }
        
        private int GetCardValue(Card card)
        {
            // Using standard Tablic rules
            return card.Rank switch
            {
                Rank.Ace => 1, // Default value for Aces (can be 11 in some contexts)
                Rank.King => 1,
                Rank.Queen => 1,
                Rank.Jack => 1,
                _ => (int)card.Rank
            };
        }
    }
}