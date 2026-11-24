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
                    Messages.Message("WFL_Tr_Dividend".Translate(pawn.LabelShort, paid.Value, TreasuryService.BusinessName(business)), pawn, MessageTypeDefOf.PositiveEvent, false);
                    Backed(c, pawn, business, "WFL_InvestmentPaid", "WFL_InvestorBacked");
                    CareerService.Report(c, pawn, TreasuryRoles.Career, TreasuryRoles.InvestorSpecialty, "invest.dividend", "invest.dividend|" + business + "|" + today + "|" + paid.Key, TreasuryRoles.InvestPoints, now);
                }

            if (!book.InvestorsMayInvest) return;
            foreach (int id in TreasuryService.Holders(r, TreasuryRoles.Investor))
            {
                var last = book.Latest(id); if (last != null && today - last.OpenedDay < InvestmentRules.CooldownDays) continue;
                int want = InvestmentRules.Stake(l.Available(TreasuryService.PersonAccount(r, id)), book.MaxStake); if (want < InvestmentRules.MinCapital) continue;
                int business = book.Pick(l, id); if (business == 0) continue;
                var stake = book.Open(l, id, business, want, today); if (stake == null) continue;
                var pawn = c.TryGetPawn(id); if (pawn == null) continue;
                Messages.Message("WFL_Tr_StakeOpened".Translate(pawn.LabelShort, stake.Capital, TreasuryService.BusinessName(business), (stake.ShareBp / 100f).ToString("0.#")), pawn, MessageTypeDefOf.PositiveEvent, false);
                Backed(c, pawn, business, null, "WFL_InvestorBacked");
                CareerService.Report(c, pawn, TreasuryRoles.Career, TreasuryRoles.InvestorSpecialty, "invest.open", "invest.open|" + stake.Id, TreasuryRoles.InvestPoints * 2, now);
            }
        }

        /// <summary>The investor feels good about a paying stake; whoever runs the business feels backed by someone who believes in it. Small and short, and only when the module that runs the business says who that is.</summary>
        private static void Backed(FamilyComponent c, Pawn investor, int business, string investorThought, string ownerThought)
        {
            if (investorThought != null) FrameworkServices.GainMemory(investor, investorThought, null);
            var owner = c.TryGetPawn(FamilyFramework.Commerce.OwnerPawnOf(business));
            if (owner != null && owner != investor) FrameworkServices.GainMemory(owner, ownerThought, investor);
        }
    }
}
