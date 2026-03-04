using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// Move validation result
    /// </summary>
    public class MoveValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static MoveValidationResult Valid() => new() { IsValid = true };

        public static MoveValidationResult Invalid(string message) =>
            new() { IsValid = false, ErrorMessage = message };
    }

    /// <summary>
    /// Match result
    /// </summary>
    public class MatchResult
    {
        public bool HasMatch { get; set; }
        public List<int> TalonIndices { get; set; } = new();
    }

    /// <summary>
    /// Validates moves in Tablic
    /// </summary>
    public class TablicMoveValidator
    {
        private readonly GameRules rules;
        private const int MAX_TALON_CARDS_TO_CONSIDER = 7;
        private readonly Random random = new();

        public TablicMoveValidator(GameRules rules)
        {
            this.rules = rules;
        }

        public MoveValidationResult ValidateCardPlay(Card card, IEnumerable<Card> hand)
        {
            foreach (var handCard in hand)
            {
                if (handCard.Rank == card.Rank && handCard.Suit == card.Suit)
                {
                    return MoveValidationResult.Valid();
                }
            }

            return MoveValidationResult.Invalid("Card not in hand");
        }

        public MoveValidationResult ValidatePickup(Card playedCard, IEnumerable<Card> talonCards)
        {
            var talonList = talonCards.ToList();

            if (talonList.Count == 0)
            {
                return MoveValidationResult.Invalid("No talon cards to pick up");
            }

            // Use 11 as target value for Aces, otherwise use normal card value
            int targetValue = playedCard.Rank == Rank.Ace ? 11 : rules.GetCardValue(playedCard);

            if (!CanPartitionCardsIntoGroups(talonList, targetValue))
            {
                return MoveValidationResult.Invalid("Selected talon cards cannot be grouped to match played card value");
            }

            return MoveValidationResult.Valid();
        }

        public List<MatchResult> FindMatches(Card playedCard, IReadOnlyList<Card> talon)
        {
            var results = new List<MatchResult>();

            if (talon.Count == 0)
            {
                return results;
            }

            int targetValue = rules.GetCardValue(playedCard);

            // STEP 1: Filter talon cards to consider
            var filteredIndices = FilterTalonForMatching(playedCard, talon);

            // STEP 2: Calculate matches on filtered subset
            CalculateMatchesForFilteredTalon(talon, filteredIndices, targetValue, results);

            return results;
        }

        /// <summary>
        /// STEP 1: Filter talon cards to consider for matching
        /// Returns indices of cards to consider (max 7 by default)
        /// </summary>
        private List<int> FilterTalonForMatching(Card playedCard, IReadOnlyList<Card> talon)
        {
            int targetValue = rules.GetCardValue(playedCard);
            var candidates = new List<(int index, int value, Card card)>();

            // Collect all cards with their values
            for (int i = 0; i < talon.Count; i++)
            {
                int cardValue = rules.GetCardValue(talon[i]);
                
                // Primary filter: Only consider cards with value <= target value
                if (cardValue <= targetValue)
                {
                    candidates.Add((i, cardValue, talon[i]));
                }
            }

            // If we have <= MAX_TALON_CARDS_TO_CONSIDER, return all
            if (candidates.Count <= MAX_TALON_CARDS_TO_CONSIDER)
            {
                return candidates.Select(c => c.index).ToList();
            }

            // Advanced filtering: Prioritize more valuable cards
            // Sort by: 1) Trick cards first, 2) Higher value, 3) Original position
            var prioritized = candidates
                .OrderByDescending(c => rules.GetTrickValue(c.card))
                .ThenByDescending(c => c.value)
                .ThenBy(c => c.index)
                .ToList();

            // Take top MAX_TALON_CARDS_TO_CONSIDER cards
            var selected = prioritized
                .Take(MAX_TALON_CARDS_TO_CONSIDER)
                .Select(c => c.index)
                .OrderBy(idx => idx) // Restore original order
                .ToList();

            return selected;
        }

        /// <summary>
        /// Alternative filtering strategy (can be swapped in)
        /// Filter based on min/max range heuristic
        /// </summary>
        private List<int> FilterTalonByRange(Card playedCard, IReadOnlyList<Card> talon)
        {
            int targetValue = rules.GetCardValue(playedCard);
            var candidates = new List<(int index, int value)>();

            for (int i = 0; i < talon.Count; i++)
            {
                int cardValue = rules.GetCardValue(talon[i]);
                candidates.Add((i, cardValue));
            }

            if (candidates.Count == 0)
                return new List<int>();

            // Find min card value
            int minValue = candidates.Min(c => c.value);

            // Filter: Only include cards where (card + min) <= targetValue
            // This ensures we don't include cards that are too large
            var filtered = candidates
                .Where(c => c.value + minValue <= targetValue || c.value == targetValue)
                .Select(c => c.index)
                .ToList();

            // If still too many, randomly sample
            if (filtered.Count > MAX_TALON_CARDS_TO_CONSIDER)
            {
                filtered = filtered
                    .OrderBy(_ => random.Next())
                    .Take(MAX_TALON_CARDS_TO_CONSIDER)
                    .OrderBy(idx => idx)
                    .ToList();
            }

            return filtered;
        }

        /// <summary>
        /// STEP 2: Calculate all valid matches for the filtered talon subset
        /// Handles Ace value variations (1 or 11)
        /// </summary>
        private void CalculateMatchesForFilteredTalon(IReadOnlyList<Card> fullTalon, 
            List<int> filteredIndices, int targetValue, List<MatchResult> results)
        {
            if (filteredIndices.Count == 0)
                return;

            // Count Aces in filtered subset
            int aceCount = filteredIndices.Count(idx => fullTalon[idx].Rank == Rank.Ace);

            if (aceCount == 0)
            {
                // No Aces - straightforward matching
                var values = filteredIndices.Select(idx => rules.GetCardValue(fullTalon[idx])).ToList();
                FindMatchingSubsets(filteredIndices, values, targetValue, results);
            }
            else
            {
                // Try all combinations: 0 to aceCount aces as 11, rest as 1
                var seenPartitions = new HashSet<string>();

                for (int acesAsEleven = 0; acesAsEleven <= aceCount; acesAsEleven++)
                {
                    var values = new List<int>();
                    int acesSeen = 0;

                    foreach (var idx in filteredIndices)
                    {
                        if (fullTalon[idx].Rank == Rank.Ace)
                        {
                            values.Add(acesSeen < acesAsEleven ? 11 : 1);
                            acesSeen++;
                        }
                        else
                        {
                            values.Add(rules.GetCardValue(fullTalon[idx]));
                        }
                    }

                    var tempResults = new List<MatchResult>();
                    FindMatchingSubsets(filteredIndices, values, targetValue, tempResults);

                    // Add only unique partitions (by indices)
                    foreach (var result in tempResults)
                    {
                        var key = string.Join(",", result.TalonIndices.OrderBy(x => x));
                        if (seenPartitions.Add(key))
                        {
                            results.Add(result);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find all valid subsets from filtered indices that can form groups summing to targetValue
        /// Uses bitmask iteration for small sets (efficient for N <= 7)
        /// </summary>
        private void FindMatchingSubsets(List<int> originalIndices, List<int> values, 
            int targetValue, List<MatchResult> results)
        {
            int n = values.Count;

            // For small subsets (N <= 10), brute force with bitmask is efficient
            // For N=7: 2^7 = 128 subsets - very fast
            for (int mask = 1; mask < (1 << n); mask++)
            {
                var subsetIndices = new List<int>();
                var subsetValues = new List<int>();
                int sum = 0;

                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        subsetIndices.Add(originalIndices[i]);
                        subsetValues.Add(values[i]);
                        sum += values[i];
                    }
                }

                // Pruning: Only check if sum is multiple of target
                if (sum % targetValue != 0)
                    continue;

                // Check if this subset can form valid groups
                if (CanPartitionIntoGroups(subsetValues, targetValue))
                {
                    results.Add(new MatchResult
                    {
                        HasMatch = true,
                        TalonIndices = subsetIndices
                    });
                }
            }
        }

        private bool CanPartitionCardsIntoGroups(List<Card> talonCards, int targetValue)
        {
            // Count Aces in talon
            int aceCount = talonCards.Count(c => c.Rank == Rank.Ace);

            if (aceCount == 0)
            {
                // No Aces - straightforward conversion
                var values = talonCards.Select(c => rules.GetCardValue(c)).ToList();
                return CanPartitionIntoGroups(values, targetValue);
            }

            // Try all combinations: 0 to aceCount aces as 11, rest as 1
            for (int acesAsEleven = 0; acesAsEleven <= aceCount; acesAsEleven++)
            {
                var values = new List<int>();
                int acesSeen = 0;

                foreach (var card in talonCards)
                {
                    if (card.Rank == Rank.Ace)
                    {
                        values.Add(acesSeen < acesAsEleven ? 11 : 1);
                        acesSeen++;
                    }
                    else
                    {
                        values.Add(rules.GetCardValue(card));
                    }
                }

                if (CanPartitionIntoGroups(values, targetValue))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// REUSABLE: Check if values can be partitioned into groups that each sum to targetValue
        /// </summary>
        private bool CanPartitionIntoGroups(List<int> values, int targetValue)
        {
            int totalSum = values.Sum();

            // If total sum is not a multiple of target value, it's impossible to partition
            if (totalSum % targetValue != 0)
            {
                return false;
            }

            int numGroups = totalSum / targetValue;
            var used = new bool[values.Count];

            return CanFormGroups(values, used, targetValue, numGroups, 0, 0);
        }

        private bool CanFormGroups(List<int> values, bool[] used, int targetSum, 
            int groupsRemaining, int currentSum, int startIndex)
        {
            // All groups formed successfully
            if (groupsRemaining == 0)
            {
                return true;
            }

            // Current group complete, start forming next group
            if (currentSum == targetSum)
            {
                return CanFormGroups(values, used, targetSum, groupsRemaining - 1, 0, 0);
            }

            // Try adding cards to current group
            for (int i = startIndex; i < values.Count; i++)
            {
                if (used[i] || currentSum + values[i] > targetSum)
                {
                    continue;
                }

                // Include this card in current group
                used[i] = true;
                if (CanFormGroups(values, used, targetSum, groupsRemaining, currentSum + values[i], i + 1))
                {
                    return true;
                }
                // Backtrack
                used[i] = false;
            }

            return false;
        }
    }
}