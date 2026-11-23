using FamilyLife.Domain;
using RimWorld;
using System.Linq;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The moments a colony remembers (a death that leaves heirlooms, a theft from the family's supplies, the goods returned) arrive as letters that stay in the stack and jump to the people,
    /// not as a toast that is gone in seconds. Nothing here changes what happens; it only makes sure the player is told.
    /// </summary>
    internal static class StoryLetters
    {
        private static int announcedUpTo = -1;
        /// <summary>Disputes already told about this session, as "estate:thing". Seeded from the save on first sight so loading never replays them.</summary>
        private static readonly System.Collections.Generic.HashSet<string> disputesTold = new System.Collections.Generic.HashSet<string>();
        private static bool disputesSeeded;

        public static void Send(TaggedString title, TaggedString text, LetterDef def, Pawn who)
        {
            if (who != null && who.Spawned) Find.LetterStack.ReceiveLetter(title, text, def, who);
            else Find.LetterStack.ReceiveLetter(title, text, def);
        }

        /// <summary>Tells the player about new thefts and returns of goods among their own people. Started from the current save, so loading never replays old incidents.</summary>
        public static void Announce(FamilyComponent c, FamilyRegistry r)
        {
            if (announcedUpTo < 0) { announcedUpTo = r.NextIncidentId - 1; return; }
            foreach (var i in r.Incidents.Where(x => x.Id > announcedUpTo).OrderBy(x => x.Id).ToList())
            {
                announcedUpTo = System.Math.Max(announcedUpTo, i.Id);
                if (i.Kind != IncidentKind.Theft && i.Kind != IncidentKind.Restitution) continue;
                if (r.GetPerson(i.ActorId)?.InColony != true || r.GetPerson(i.VictimId)?.InColony != true) continue;
                string actor = FamilyUi.NameOf(r, i.ActorId), victim = FamilyUi.NameOf(r, i.VictimId);
                bool theft = i.Kind == IncidentKind.Theft;
                Send((theft ? "WFL_Theft_Title" : "WFL_Restitution_Title").Translate(actor, victim),
                    (theft ? "WFL_Theft_Text" : "WFL_Restitution_Text").Translate(actor, victim, i.Amount),
                    theft ? LetterDefOf.NegativeEvent : LetterDefOf.PositiveEvent, c.TryGetPawn(theft ? i.VictimId : i.ActorId));
            }
        }

        /// <summary>Tells the player when two people both claim a keepsake and the family has to settle it. Told once per keepsake.</summary>
        public static void AnnounceDisputes(FamilyComponent c, FamilyRegistry r)
        {
            foreach (var estate in r.Estates.Where(e => e.Open).ToList())
                foreach (var item in estate.Items.Where(i => i.Contested && !i.Terminal && i.Claims.Count >= 2).ToList())
                {
                    string key = estate.Id + ":" + item.ThingId;
                    if (!disputesTold.Add(key) || !disputesSeeded) continue;
                    var lead = item.Claims.OrderByDescending(x => x.Strength).Take(2).ToList();
                    if (r.GetPerson(lead[0].ClaimantId)?.InColony != true && r.GetPerson(lead[1].ClaimantId)?.InColony != true) continue;
                    Send("WFL_Dispute_Title".Translate(item.Label, FamilyUi.NameOf(r, estate.DeceasedId)),
                        "WFL_Dispute_Text".Translate(FamilyUi.NameOf(r, lead[0].ClaimantId), FamilyUi.NameOf(r, lead[1].ClaimantId), item.Label, FamilyUi.NameOf(r, estate.DeceasedId)),
                        LetterDefOf.NegativeEvent, c.TryGetPawn(lead[0].ClaimantId) ?? c.TryGetPawn(lead[1].ClaimantId));
                }
            disputesSeeded = true;
        }
    }
}
