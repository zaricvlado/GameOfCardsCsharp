using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    public enum GamePhase
    {
        NotStarted,
        InProgress,
        RoundTransition,
        Finished
    }

    /// <summary>
    /// Observable game state
    /// </summary>
    public class TablicGameState
    {
        private GamePhase phase;
        private int currentRound;
        private int currentPlayerId;
        private readonly TablicPlayer player1;
        private readonly TablicPlayer player2;
        private readonly List<Card> talon = new();
        private int lastPickupPlayerId;

        private Action<string>? stateChangedCallback;

        public TablicGameState()
        {
            phase = GamePhase.NotStarted;
            currentRound = 0;
            currentPlayerId = 0;
            player1 = new TablicPlayer(0);
            player2 = new TablicPlayer(1);
            lastPickupPlayerId = -1;
        }

        // Phase
        public GamePhase GetPhase() => phase;

        public void SetPhase(GamePhase newPhase)
        {
            phase = newPhase;
            NotifyStateChanged("Phase");
        }

        // Round (1-4)
        public int GetCurrentRound() => currentRound;

        public void SetCurrentRound(int round)
        {
            currentRound = round;
            NotifyStateChanged("CurrentRound");
        }

        public void IncrementRound()
        {
            currentRound++;
            NotifyStateChanged("CurrentRound");
        }

        // Current player
        public int GetCurrentPlayerId() => currentPlayerId;

        public void SetCurrentPlayerId(int playerId)
        {
            currentPlayerId = playerId;
            NotifyStateChanged("CurrentPlayerId");
        }

        public void SwitchPlayer()
        {
            currentPlayerId = currentPlayerId == 0 ? 1 : 0;
            NotifyStateChanged("CurrentPlayerId");
        }

        // Players
        public TablicPlayer GetPlayer(int playerId)
        {
            return playerId == 0 ? player1 : player2;
        }

        public TablicPlayer GetCurrentPlayer()
        {
            return GetPlayer(currentPlayerId);
        }

        // Talon
        public IReadOnlyList<Card> GetTalon() => talon.AsReadOnly();

        public void SetTalon(IEnumerable<Card> newTalon)
        {
            talon.Clear();
            talon.AddRange(newTalon);
            NotifyStateChanged("Talon");
        }

        public void AddToTalon(Card card)
        {
            talon.Add(card);
            NotifyStateChanged("Talon");
        }

        public void RemoveFromTalon(IEnumerable<int> indices)
        {
            var sortedIndices = indices.OrderByDescending(x => x).ToList();

            foreach (int idx in sortedIndices)
            {
                if (idx >= 0 && idx < talon.Count)
                {
                    talon.RemoveAt(idx);
                }
            }

            NotifyStateChanged("Talon");
        }

        public void ClearTalon()
        {
            talon.Clear();
            NotifyStateChanged("Talon");
        }

        // Last pickup
        public int GetLastPickupPlayerId() => lastPickupPlayerId;

        public void SetLastPickupPlayerId(int playerId)
        {
            lastPickupPlayerId = playerId;
            NotifyStateChanged("LastPickupPlayerId");
        }

        // State snapshot
        public class StateSnapshot
        {
            public GamePhase Phase { get; set; }
            public int CurrentRound { get; set; }
            public int CurrentPlayerId { get; set; }
            public List<Card> Talon { get; set; } = new();
            public int Player1HandSize { get; set; }
            public int Player2HandSize { get; set; }
            public int Player1PileSize { get; set; }
            public int Player2PileSize { get; set; }
            public int LastPickupPlayerId { get; set; }
        }

        public StateSnapshot GetSnapshot()
        {
            return new StateSnapshot
            {
                Phase = phase,
                CurrentRound = currentRound,
                CurrentPlayerId = currentPlayerId,
                Talon = new List<Card>(talon),
                Player1HandSize = player1.GetHand().CardCount(),
                Player2HandSize = player2.GetHand().CardCount(),
                Player1PileSize = player1.GetPileSize(),
                Player2PileSize = player2.GetPileSize(),
                LastPickupPlayerId = lastPickupPlayerId
            };
        }

        // Reset
        public void Reset()
        {
            phase = GamePhase.NotStarted;
            currentRound = 0;
            currentPlayerId = 0;
            player1.Reset();
            player2.Reset();
            talon.Clear();
            lastPickupPlayerId = -1;
            NotifyStateChanged("Reset");
        }

        // Change notification
        public void SetStateChangedCallback(Action<string> callback)
        {
            stateChangedCallback = callback;
        }

        private void NotifyStateChanged(string propertyName)
        {
            stateChangedCallback?.Invoke(propertyName);
        }
    }
}