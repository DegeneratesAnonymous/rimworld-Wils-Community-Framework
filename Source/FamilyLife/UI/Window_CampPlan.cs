using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The planning card for a family's camping wish: why they want to go, where, for how long and who they would bring, and the three things the player can do (agree, put it off, or - once agreed - open the game's own
    /// caravan screen). Nothing here sends anyone anywhere; agreeing only says yes, and the caravan is still formed by the player.
    /// </summary>
    public class Window_CampPlan : Window
    {
        private readonly int tripId;
        private Vector2 scroll;
        private float listHeight;

        public Window_CampPlan(int tripId)
        {
            this.tripId = tripId;
            doCloseX = true; forcePause = false; absorbInputAroundWindow = false; draggable = true; closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(560f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            var c = FamilyComponent.Current; var r = c?.Registry; var trip = r?.CampTripFor(tripId);
            if (trip == null) { Close(); return; }
            string house = r.LineageDisplayName(r.GetLineage(trip.FamilyId));
            float y = 0f; Text.Font = GameFont.Medium; Widgets.Label(new Rect(0f, y, inRect.width, 32f), "WFL_Camp_PlanTitle".Translate(house)); Text.Font = GameFont.Small; y += 36f;

            string why = "WFL_Camp_PlanWhy".Translate(FamilyUi.NameOf(r, trip.OrganizerId), ("WFL_CampMotive_" + trip.Motive).Translate()).ToString();
            float h = Text.CalcHeight(why, inRect.width); Widgets.Label(new Rect(0f, y, inRect.width, h), why); y += h + 6f;

            var home = Find.AnyPlayerHomeMap; int homeTile = home == null ? -1 : WorldTiles.HomeTile(home);
            string far = homeTile >= 0 ? "WFL_Camp_PlanFar".Translate(Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(homeTile, trip.Tile))).ToString() : "";
            string facts = "WFL_Camp_PlanFacts".Translate(trip.StayTicks / 2500, ("WFL_CampState_" + trip.State).Translate(), far).ToString();
            GUI.color = Color.gray; h = Text.CalcHeight(facts, inRect.width); Widgets.Label(new Rect(0f, y, inRect.width, h), facts); GUI.color = Color.white; y += h + 8f;

            Widgets.Label(new Rect(0f, y, inRect.width, 24f), "WFL_Camp_PlanBring".Translate()); y += 26f;
            var area = new Rect(0f, y, inRect.width, inRect.height - y - 110f); var view = new Rect(0f, 0f, area.width - 16f, Mathf.Max(listHeight, area.height));
            Widgets.BeginScrollView(area, ref scroll, view); float ly = 0f;
            foreach (int id in trip.Suggested)
            {
                var pawn = c.TryGetPawn(id); var row = new Rect(0f, ly, view.width, 26f);
                if (pawn != null) CommunityWidgets.NameButton(new Rect(4f, ly, 200f, 26f), pawn); else Widgets.Label(new Rect(4f, ly, 200f, 26f), FamilyUi.NameOf(r, id));
                bool free = pawn != null && pawn.Spawned && !pawn.Downed && !pawn.Drafted && !pawn.InMentalState;
                GUI.color = free ? new Color(.55f, .85f, .55f) : new Color(.85f, .6f, .4f); Widgets.Label(new Rect(210f, ly, view.width - 214f, 26f), (free ? "WFL_Camp_PlanReady" : "WFL_Camp_PlanBusy").Translate()); GUI.color = Color.white;
                ly += 26f;
            }
            listHeight = ly; Widgets.EndScrollView();

            string guide = (trip.State == CampState.Proposed ? "WFL_Camp_PlanGuideAsk" : trip.State == CampState.Authorized ? "WFL_Camp_PlanGuideGo" : "WFL_Camp_PlanGuideDone").Translate().ToString();
            h = Text.CalcHeight(guide, inRect.width); GUI.color = Color.gray; Widgets.Label(new Rect(0f, inRect.height - 104f, inRect.width, h), guide); GUI.color = Color.white;

            float by = inRect.height - 36f, x = 0f;
            if (trip.State == CampState.Proposed)
            {
                if (Widgets.ButtonText(new Rect(x, by, 160f, 32f), "WFL_Camp_Plan".Translate())) CampingDriver.Authorize(c, trip); x += 166f;
                if (Widgets.ButtonText(new Rect(x, by, 120f, 32f), "WFL_Camp_Decline".Translate())) { r.DeclineCamp(trip.Id, Activities.Now); Close(); } x += 126f;
            }
            else if (trip.State == CampState.Authorized) { if (Widgets.ButtonText(new Rect(x, by, 170f, 32f), "WFL_Camp_FormCaravan".Translate())) CampingDriver.OpenCaravanForm(); x += 176f; }
            if (Widgets.ButtonText(new Rect(x, by, 130f, 32f), "WFL_Camp_ShowPlace".Translate())) WorldTiles.Jump(trip.Tile);
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, by, 110f, 32f), "WFL_Plan_Close".Translate())) Close();
        }
    }
}
