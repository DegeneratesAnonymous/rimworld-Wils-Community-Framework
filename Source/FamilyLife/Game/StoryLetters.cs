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
    }
}
