using System;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public sealed class NarrativeActorHandle
    {
        public NarrativeActorHandle(
            string roleId,
            string displayName,
            Texture portrait,
            CardInstance card)
            : this(roleId, displayName, portrait, card, string.Empty)
        {
        }

        public NarrativeActorHandle(
            string roleId,
            string displayName,
            Texture portrait,
            CardInstance card,
            string cardDefinitionId)
        {
            RoleId = roleId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Portrait = portrait;
            Card = card;
            CardDefinitionId = cardDefinitionId ?? string.Empty;
        }

        public string RoleId { get; }
        public string DisplayName { get; }
        public Texture Portrait { get; }
        public CardInstance Card { get; private set; }
        public string CardDefinitionId { get; }

        public void AttachCard(CardInstance card)
        {
            Card = card;
        }
    }

    public sealed class NarrativeActorResolver
    {
        public NarrativeActorHandle Resolve(NarrativeActorBinding binding)
        {
            if (binding == null)
                return null;

            CardInstance card = binding.ResolveMode switch
            {
                NarrativeActorResolveMode.PresentationOnly => null,
                NarrativeActorResolveMode.SpawnTemporary => null,
                NarrativeActorResolveMode.PartyLeader =>
                    GameDirector.Instance?.FindActiveProtagonistCard(),
                NarrativeActorResolveMode.PersistentId =>
                    CardManager.Instance?.AllCards.FirstOrDefault(candidate =>
                        string.Equals(candidate.PersistentId,
                            binding.PersistentId, StringComparison.Ordinal)),
                NarrativeActorResolveMode.CardDefinitionId =>
                    CardManager.Instance?.AllCards.FirstOrDefault(candidate =>
                        string.Equals(candidate.BaseDefinition?.Id,
                            binding.CardDefinitionId,
                            StringComparison.Ordinal)),
                _ => null
            };

            return new NarrativeActorHandle(
                binding.RoleId,
                string.IsNullOrWhiteSpace(binding.DisplayName)
                    ? card?.BaseDefinition?.DisplayName
                    : binding.DisplayName,
                binding.Portrait != null
                    ? binding.Portrait
                    : card?.BaseDefinition?.ArtTexture,
                card,
                binding.CardDefinitionId);
        }
    }
}
