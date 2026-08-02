using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public sealed class CombatItemReservationService
    {
        private readonly BackpackData backpack;

        public CombatItemReservationService(BackpackData backpack)
        {
            this.backpack = backpack;
        }

        public bool TryReserve(
            string entryId,
            string sessionId,
            string commandId)
        {
            if (backpack == null || string.IsNullOrWhiteSpace(sessionId) ||
                string.IsNullOrWhiteSpace(commandId))
                return false;

            BackpackEntryData entry = backpack.Find(entryId);
            if (entry?.Card == null || entry.IsReserved)
                return false;

            entry.ReservationOwnerId = sessionId;
            entry.ReservationCommandId = commandId;
            return true;
        }

        public bool ConsumeCommand(string commandId, out CardData card)
        {
            card = null;
            BackpackEntryData entry = backpack?.Entries?.FirstOrDefault(
                value => value != null && value.ReservationCommandId == commandId);
            return entry != null && backpack.TryConsumeReserved(
                entry.InstanceId,
                commandId,
                out card);
        }

        public bool IsReservedBy(
            string entryId,
            string sessionId,
            string commandId)
        {
            BackpackEntryData entry = backpack?.Find(entryId);
            return entry?.IsReserved == true &&
                entry.ReservationOwnerId == sessionId &&
                entry.ReservationCommandId == commandId;
        }

        public void ReleaseCommand(string commandId)
        {
            if (backpack?.Entries == null || string.IsNullOrWhiteSpace(commandId))
                return;
            foreach (BackpackEntryData entry in backpack.Entries.Where(
                         value => value?.ReservationCommandId == commandId))
                entry.ClearReservation();
        }

        public void ReleaseSession(string sessionId)
        {
            if (backpack?.Entries == null || string.IsNullOrWhiteSpace(sessionId))
                return;
            foreach (BackpackEntryData entry in backpack.Entries.Where(
                         value => value?.ReservationOwnerId == sessionId))
                entry.ClearReservation();
        }

        public void ReleaseOrphans(IReadOnlyCollection<string> activeSessionIds)
        {
            if (backpack?.Entries == null)
                return;
            var active = activeSessionIds != null
                ? new HashSet<string>(activeSessionIds)
                : new HashSet<string>();
            foreach (BackpackEntryData entry in backpack.Entries)
            {
                if (entry != null && entry.IsReserved &&
                    !active.Contains(entry.ReservationOwnerId))
                    entry.ClearReservation();
            }
        }

        public void ReleaseOrphans(IEnumerable<CombatTask> activeTasks)
        {
            if (backpack?.Entries == null)
                return;
            var valid = new HashSet<string>(
                (activeTasks ?? Enumerable.Empty<CombatTask>())
                .Where(task => task?.IsOngoing == true)
                .SelectMany(task => task.QueuedCommands
                    .Where(command => command?.Type == CombatCommandType.UseItem)
                    .Select(command => string.Join("|",
                        task.SessionId,
                        command.CommandId,
                        command.BackpackEntryId))));
            foreach (BackpackEntryData entry in backpack.Entries)
            {
                if (entry?.IsReserved != true)
                    continue;
                string key = string.Join("|",
                    entry.ReservationOwnerId,
                    entry.ReservationCommandId,
                    entry.InstanceId);
                if (!valid.Contains(key))
                    entry.ClearReservation();
            }
        }
    }
}
