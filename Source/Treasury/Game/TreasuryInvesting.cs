using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The Investor tier's work: a qualified, assigned Investor puts part of their own silver into the community's most profitable business, and every few days each business pays its investors a share
    /// of what it has spare. The silver is real ledger silver in both directions (the rules are in InvestmentBook); this only decides who invests and tells the player.
    /// </summary>
    internal static class TreasuryInvesting
    {
        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null) return;
            var r = c.Registry; var l = r.Ledger(); var book = r.Investments(); int today = TreasuryService.Today();

            foreach (int business in book.Live.Select(s => s.Business).Distinct().ToList())
                foreach (var paid in book.PayDividends(l, business, today))
                {
                    var pawn = c.TryGetPawn(paid.Key); if (pawn == null) continue;
                    Messages.Message("WFL_Tr_Dividend".Translate(pawn.LabelShort, paid.Value, "WFL_Tr_Business".Translate(business)), pawn, MessageTypeDefOf.PositiveEvent, false);
                    CareerService.Report(c, pawn, TreasuryRoles.Career, TreasuryRoles.InvestorSpecialty, "invest.dividend", "invest.dividend|" + business + "|" + today + "|" + paid.Key, TreasuryRoles.InvestPoints, now);
                }

            foreach (int id in TreasuryService.Holders(r, TreasuryRoles.Investor))
            {
                var last = book.Latest(id); if (last != null && today - last.OpenedDay < InvestmentRules.CooldownDays) continue;
                int want = InvestmentRules.Stake(l.Available(TreasuryService.PersonAccount(r, id))); if (want < InvestmentRules.MinCapital) continue;
                int business = book.Pick(l, id); if (business == 0) continue;
                var stake = book.Open(l, id, business, want, today); if (stake == null) continue;
                var pawn = c.TryGetPawn(id); if (pawn == null) continue;
                Messages.Message("WFL_Tr_StakeOpened".Translate(pawn.LabelShort, stake.Capital, "WFL_Tr_Business".Translate(business), (stake.ShareBp / 100f).ToString("0.#")), pawn, MessageTypeDefOf.PositiveEvent, false);
                CareerService.Report(c, pawn, TreasuryRoles.Career, TreasuryRoles.InvestorSpecialty, "invest.open", "invest.open|" + stake.Id, TreasuryRoles.InvestPoints * 2, now);
            }
        }
    }
}
