using System;
using System.Collections.Generic;

namespace GameOfCardsCsharp.Preferance.GamePlay
{
    public class GamePlayStartedEventArgs : EventArgs
    {
        public RoleAssignmentResult Roles { get; }

        public GamePlayStartedEventArgs(RoleAssignmentResult roles)
        {
            Roles = roles;
        }
    }

    public class TrickStartedEventArgs : EventArgs
    {
        public int TrickNumber { get; }
        public int LeaderId { get; }

        public TrickStartedEventArgs(int trickNumber, int leaderId)
        {
            TrickNumber = trickNumber;
            LeaderId = leaderId;
        }
    }

    public class PlayerTurnToPlayEventArgs : EventArgs
    {
        public int PlayerId { get; }
        public List<Card> LegalMoves { get; }
        public Card? LeadCard { get; }

        public PlayerTurnToPlayEventArgs(int playerId, List<Card> legalMoves, Card? leadCard)
        {
            PlayerId = playerId;
            LegalMoves = legalMoves;
            LeadCard = leadCard;
        }
    }

    public class CardPlayedEventArgs : EventArgs
    {
        public int PlayerId { get; }
        public Card Card { get; }
        public int TrickNumber { get; }
        public int CardPositionInTrick { get; }

        public CardPlayedEventArgs(int playerId, Card card, int trickNumber, int cardPositionInTrick)
        {
            PlayerId = playerId;
            Card = card;
            TrickNumber = trickNumber;
            CardPositionInTrick = cardPositionInTrick;
        }
    }

    public class TrickCompletedEventArgs : EventArgs
    {
        public int TrickNumber { get; }
        public int WinnerId { get; }
        public Dictionary<int, Card> CardsPlayed { get; }

        public TrickCompletedEventArgs(int trickNumber, int winnerId, Dictionary<int, Card> cardsPlayed)
        {
            TrickNumber = trickNumber;
            WinnerId = winnerId;
            CardsPlayed = cardsPlayed;
        }
    }

    public class GamePlayCompletedEventArgs : EventArgs
    {
        public GamePlayResult Result { get; }

        public GamePlayCompletedEventArgs(GamePlayResult result)
        {
            Result = result;
        }
    }
}
