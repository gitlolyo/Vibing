using System;
using PokerSocial.Data;

namespace PokerSocial.Systems
{
    /// <summary>
    /// Handles public "reads" - players calling out bluffs mid-round.
    /// Wrong reads cost chips; right reads build the ReadAccuracy stat
    /// that becomes your progression/status system.
    /// </summary>
    public class ReadSystem
    {
        public event Action<DeclaredRead> OnReadDeclared;
        public event Action<DeclaredRead, bool> OnReadResolved;

        private const int WRONG_READ_PENALTY = 25; // chip cost, tune per stakes

        public DeclaredRead DeclareRead(PlayerData reader, PlayerData target, bool claimingBluff)
        {
            if (reader.chipStack < WRONG_READ_PENALTY)
                throw new InvalidOperationException("Not enough chips to risk a declared read");

            var read = new DeclaredRead
            {
                readerId = reader.playerId,
                targetId = target.playerId,
                claimedBluff = claimingBluff
            };

            reader.readsDeclared++;
            OnReadDeclared?.Invoke(read);
            return read;
        }

        /// <summary>
        /// Call this at showdown (or fold reveal) once ground truth is known.
        /// </summary>
        public void ResolveRead(DeclaredRead read, PlayerData reader, bool targetWasActuallyBluffing)
        {
            read.wasCorrect = read.claimedBluff == targetWasActuallyBluffing;

            if (read.wasCorrect)
            {
                reader.readsCorrect++;
            }
            else
            {
                reader.chipStack -= WRONG_READ_PENALTY;
            }

            OnReadResolved?.Invoke(read, read.wasCorrect);
        }
    }
}
