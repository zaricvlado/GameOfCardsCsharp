using System.Collections.Generic;

namespace GameOfCardsCsharp.Preferance.GamePlay
{
    /// <summary>
    /// Result of role assignment phase
    /// </summary>
    public class RoleAssignmentResult
    {
        public int DeclarerId { get; }
        public ContractType Contract { get; }
        public TrumpSuit? TrumpSuit { get; }
        
        /// <summary>
        /// List of Defender player IDs.
        /// If Partner exists, there's only one Defender who called the Partner.
        /// If no Partner, there are two independent Defenders.
        /// </summary>
        public List<int> DefenderIds { get; }
        
        public List<int> SpectatorIds { get; }
        
        /// <summary>
        /// Partner player ID (if called by a Defender).
        /// Partner's tricks are attributed to the Defender who called them.
        /// </summary>
        public int? PartnerId { get; }

        public RoleAssignmentResult(
            int declarerId,
            ContractType contract,
            TrumpSuit? trumpSuit,
            List<int> defenderIds,
            List<int> spectatorIds,
            int? partnerId)
        {
            DeclarerId = declarerId;
            Contract = contract;
            TrumpSuit = trumpSuit;
            DefenderIds = defenderIds;
            SpectatorIds = spectatorIds;
            PartnerId = partnerId;
        }

        /// <summary>
        /// True if there's a Partner (Defender called someone)
        /// </summary>
        public bool HasPartner => PartnerId.HasValue;

        /// <summary>
        /// Gets the Defender who called the Partner (throws if no Partner)
        /// </summary>
        public int GetPartnerCallerId()
        {
            if (!HasPartner || DefenderIds.Count != 1)
                throw new InvalidOperationException("No Partner or invalid Defender count");
            
            return DefenderIds[0];
        }

        public override string ToString()
        {
            var contractInfo = TrumpSuit.HasValue 
                ? $"{Contract} ({TrumpSuit})" 
                : Contract.ToString();

            var roles = $"Declarer: Player {DeclarerId}";
            
            if (HasPartner)
            {
                roles += $"\nDefender: Player {DefenderIds[0]} (with Partner: Player {PartnerId})";
            }
            else
            {
                roles += $"\nDefenders: {string.Join(", ", DefenderIds.Select(id => $"Player {id}"))}";
            }

            if (SpectatorIds.Count > 0)
            {
                roles += $"\nSpectators: {string.Join(", ", SpectatorIds.Select(id => $"Player {id}"))}";
            }

            return $"Role Assignment:\n{contractInfo}\n{roles}";
        }
    }
}