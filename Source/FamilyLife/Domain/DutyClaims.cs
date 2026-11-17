using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// Who is committed to which module's duty right now. A person holds at most one claim: the module that took it. Another module sees the claim and leaves them alone until it ends or runs out, so one
    /// pawn holding roles in several modules is never sent to two duties at once. Pure and never saved (a duty is minutes long; after a load nobody is claimed).
    /// </summary>
    public sealed class DutyClaimBook
    {
        private sealed class Claim { public string Module; public int Until; }
        private readonly Dictionary<int, Claim> claims = new Dictionary<int, Claim>();

        private bool Live(int person, int now, out Claim claim)
        {
            if (claims.TryGetValue(person, out claim)) { if (now < claim.Until) return true; claims.Remove(person); }
            claim = null; return false;
        }

        /// <summary>Takes the person for a module for <paramref name="ticks"/>. Refused when another module holds them; the same module renews its own claim.</summary>
        public bool TryClaim(int person, string module, int now, int ticks)
        {
            if (person == 0 || string.IsNullOrEmpty(module) || ticks <= 0) return false;
            if (Live(person, now, out var c) && c.Module != module) return false;
            claims[person] = new Claim { Module = module, Until = now + ticks };
            return true;
        }

        /// <summary>True when a module other than <paramref name="module"/> has the person.</summary>
        public bool HeldByOther(int person, string module, int now) => Live(person, now, out var c) && c.Module != module;

        public string HolderOf(int person, int now) => Live(person, now, out var c) ? c.Module : null;

        /// <summary>Ends the module's own claim (a duty finished early). Another module's claim is never ended by this.</summary>
        public void Release(int person, string module) { if (claims.TryGetValue(person, out var c) && c.Module == module) claims.Remove(person); }

        public void Clear() => claims.Clear();
        public int Count => claims.Count;
    }
}
