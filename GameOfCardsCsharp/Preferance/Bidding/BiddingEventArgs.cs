using System;
using System.Collections.Generic;

namespace GameOfCardsCsharp.Preferance.Bidding
{
    /// <summary>
    /// Event arguments for bidding started event
    /// </summary>
    public class BiddingStartedEventArgs : EventArgs
    {
        public List<int> PlayerIds { get; }
        public int StartingPlayerId { get; }

        public BiddingStartedEventArgs(List<int> playerIds, int startingPlayerId)
        {
            PlayerIds = playerIds;
            StartingPlayerId = startingPlayerId;
        }
    }

    /// <summary>
    /// Event arguments for when it's a player's turn to bid
    /// </summary>
    public class PlayerTurnEventArgs : EventArgs
    {
        public int PlayerId { get; }
        public List<BidAction> AvailableActions { get; }

        public PlayerTurnEventArgs(int playerId, List<BidAction> availableActions)
        {
            PlayerId = playerId;
            AvailableActions = availableActions;
        }
    }

    /// <summary>
    /// Event arguments for when a bid is placed
    /// </summary>
    public class BidPlacedEventArgs : EventArgs
    {
        public int PlayerId { get; }
        public BidAction Action { get; }
        public bool IsPass { get; }

        public BidPlacedEventArgs(int playerId, BidAction action)
        {
            PlayerId = playerId;
            Action = action;
            IsPass = action.IsPass;
        }
    }

    /// <summary>
    /// Event arguments for bidding completed event
    /// </summary>
    public class BiddingCompletedEventArgs : EventArgs
    {
        public BiddingResult Result { get; }

        public BiddingCompletedEventArgs(BiddingResult result)
        {
            Result = result;
        }
    }
}