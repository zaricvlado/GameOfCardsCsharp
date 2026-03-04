using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    public class GameStartedEventArgs
    {
        public List<Card> InitialTalon { get; set; } = new();
        public int Player1InitialCards { get; set; }
        public int Player2InitialCards { get; set; }
    }

    public class RoundStartedEventArgs
    {
        public int RoundNumber { get; set; }
        public int CurrentPlayerId { get; set; }
        public int CardsDealtToPlayer1 { get; set; }
        public int CardsDealtToPlayer2 { get; set; }
    }

    public class CardPlayedEventArgs
    {
        public int PlayerId { get; set; }
        public int HandIndex { get; set; }  // Add this property
        public Card PlayedCard { get; set; } = null!;
        public bool WasPickup { get; set; }
        public List<Card> PickedCards { get; set; } = new();
        public List<Card> CurrentTalon { get; set; } = new();
    }

    public class PlayerSwitchedEventArgs
    {
        public int PreviousPlayerId { get; set; }
        public int CurrentPlayerId { get; set; }
    }

    public class RoundCompletedEventArgs
    {
        public int RoundNumber { get; set; }
        public int Player1PileSize { get; set; }
        public int Player2PileSize { get; set; }
    }

    public class GameFinishedEventArgs
    {
        public PlayerScore Player1Score { get; set; } = null!;
        public PlayerScore Player2Score { get; set; } = null!;
        public int WinnerId { get; set; }
    }

    public class GameErrorEventArgs
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
    }
}