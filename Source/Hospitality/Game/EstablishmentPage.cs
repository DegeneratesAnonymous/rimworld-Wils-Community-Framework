using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    public static class EstablishmentPage
    {
        private static int selected;
        private static string rename;
        private static int tab;
        private static readonly string[] Tabs={"Overview","Premises","Customers","Finance","Character","Activity"};
        public static float Draw(Rect rect,FamilyComponent c)
        {
            var book=c.Registry.Establishments();float y=0f,x=0f;
            if(Widgets.ButtonText(new Rect(rect.x,rect.y,150f,28f),"+ New establishment"))Menu(Enum.GetValues(typeof(EstablishmentKind)).Cast<EstablishmentKind>().Select(kind=>new FloatMenuOption(kind.ToString(),()=>{var e=book.Create(Find.CurrentMap.uniqueID,kind);selected=e.Id;rename=e.Name;tab=0;})));
            x=158f;
            foreach(var e in book.Businesses)
            {
                float width=Mathf.Min(230f,Mathf.Max(130f,Text.CalcSize(e.Name).x+48f));if(x+width>rect.width){x=0f;y+=32f;}
                bool open=HospitalityService.Map(e)!=null&&e.OpenAt(GenLocalDate.HourOfDay(HospitalityService.Map(e)));string title=(e.Id==selected?"• ":"")+e.Name+"  "+(open?"Open":"Closed");
                if(Widgets.ButtonText(new Rect(rect.x+x,rect.y+y,width,28f),title)){selected=e.Id;rename=e.Name;tab=0;}x+=width+6f;
            }
            y+=36f;if(selected==0&&book.Businesses.Count>0){selected=book.Businesses[0].Id;rename=book.Businesses[0].Name;}
            var b=book.ById(selected);
            if(b==null)return CommunityPageLayout.Line(new Rect(rect.x,rect.y+y,rect.width,rect.height-y),0f,"Create a restaurant, tavern, inn or shop to begin.",true)+y;
            y=CommunityPageLayout.Section(rect,0f,y,rect.width,b.Kind+" summary",r=>Summary(r,c,b));
            y=CommunityPageLayout.Tabs(new Rect(rect.x,rect.y,rect.width,rect.height),y,Tabs,tab,i=>tab=i);
            switch(tab)
            {
                case 0:return Overview(rect,y,c,b);
                case 1:return Premises(rect,y,b);
                case 2:return Customers(rect,y,c,b,book);
                case 3:return Finance(rect,y,b,book);
                case 4:return Character(rect,y,b);
                default:return Activity(rect,y,c,b,book);
            }
        }
        private static float Summary(Rect r,FamilyComponent c,Establishment b)
        {
            float y=0f;rename=Widgets.TextField(new Rect(0f,y,Mathf.Min(360f,r.width*.55f),28f),rename??b.Name);b.Name=(rename??"").Length>60?rename.Substring(0,60):rename??b.Name;
            bool enabled=b.Enabled;y=CommunityPageLayout.Toggle(r,y+34f,"Open for business",ref enabled);b.Enabled=enabled;
            bool open=HospitalityService.Map(b)!=null&&b.OpenAt(GenLocalDate.HourOfDay(HospitalityService.Map(b)));int working=HospitalityService.Coverage(b).Count();
            y=CommunityPageLayout.Line(r,y,(open?"Open now":"Closed now")+"   •   "+working+" working   •   reputation "+b.Reputation+"   •   "+b.Visits+" completed visits");
            return CommunityPageLayout.Line(r,y,"Owner: "+OwnerName(b,c)+"   •   Staff: "+(b.Staff.Count==0?"none":string.Join(", ",b.Staff.Select(id=>c.TryGetPawn(id)?.LabelShort??("#"+id)))),b.Staff.Count==0);
        }
        private static float Overview(Rect rect,float y,FamilyComponent c,Establishment b)
        {
            float w=CommunityPageLayout.ColumnWidth(rect),left=CommunityPageLayout.Section(rect,0f,y,w,"People",r=>
            {
                float h=CommunityPageLayout.Line(r,0f,"Owner: "+OwnerName(b,c));
                h=CommunityPageLayout.Button(r,h,"Change owner",()=>OwnerMenu(b,c));
                string staff=b.Staff.Count==0?"none assigned":string.Join(", ",b.Staff.Select(id=>c.TryGetPawn(id)?.LabelShort??("#"+id)));
                h=CommunityPageLayout.Line(r,h,"Staff: "+staff,b.Staff.Count==0);
                h=CommunityPageLayout.Button(r,h,"Assign or remove staff",()=>StaffMenu(b,c));
                return CommunityPageLayout.Line(r,h,"Work hours and Cooking, Crafting, or Sell priorities decide what each employee does.",true);
            });
            bool columns=CommunityPageLayout.TwoColumns(rect);float right=CommunityPageLayout.Section(rect,columns?w+CommunityPageLayout.Gap:0f,columns?y:left,w,"Hours and prices",r=>
            {
                float h=CommunityPageLayout.Slider(r,0f,"Opens",ref b.OpenHour,0,23,"h");h=CommunityPageLayout.Slider(r,h,"Closes",ref b.CloseHour,0,23,"h");
                h=CommunityPageLayout.Slider(r,h,"Price",ref b.PricePercent,25,250,"%");
                if(b.Kind==EstablishmentKind.Inn){h=CommunityPageLayout.Slider(r,h,"Room per night",ref b.RoomPrice,1,50," silver");h=CommunityPageLayout.Slider(r,h,"Maximum stay",ref b.MaxStay,1,10," days");}
                h=CommunityPageLayout.Slider(r,h,"Household allowance",ref b.HouseholdBudget,0,100," silver/day");
                if(b.Kind==EstablishmentKind.Shop){h=CommunityPageLayout.Line(r,h,"Workshop focus: "+b.WorkshopFocus);h=CommunityPageLayout.Button(r,h,"Change workshop focus",()=>Menu(Enum.GetValues(typeof(WorkshopFocus)).Cast<WorkshopFocus>().Select(f=>new FloatMenuOption(f.ToString(),()=>b.WorkshopFocus=f))));}
                return CommunityPageLayout.Line(r,h,"Matching opening and closing hours means closed. Overnight schedules are supported.",true);
            });
            return columns?Math.Max(left,right):right;
        }
        private static float Premises(Rect rect,float y,Establishment b)
        {
            return CommunityPageLayout.Section(rect,0f,y,rect.width,"Premises setup",r=>
            {
                var actions=new List<KeyValuePair<string,Action>>{
                    Pair("Service area",()=>Activate(b,"Service")),Pair("Sales floor",()=>Activate(b,"Stock")),Pair("Private storage",()=>Activate(b,"Storage")),
                    Pair("Sales containers",()=>Activate(b,"StockContainer")),Pair("Storage containers",()=>Activate(b,"StorageContainer")),Pair("Service counter",()=>Activate(b,"Counter")),Pair("Kitchen benches",()=>Activate(b,"Bench"))};
                if(b.Kind==EstablishmentKind.Inn)actions.Add(Pair("Guest beds",()=>Activate(b,"Bed")));
                float h=CommunityPageLayout.ButtonGrid(r,0f,actions,r.width>700f?3:2);
                h=CommunityPageLayout.Line(r,h,"Service "+b.ServiceCells.Count+" cells   •   Sales "+b.StockCells.Count+" cells / "+b.StockContainers.Count+" containers   •   Storage "+b.StorageCells.Count+" cells / "+b.StorageContainers.Count+" containers");
                h=CommunityPageLayout.Line(r,h,"Counter "+(b.Counter==0?"not set":"set")+"   •   "+b.Benches.Count+" benches   •   "+b.Beds.Count+" guest beds",b.Counter==0);
                return CommunityPageLayout.Button(r,h,"Remove a designated area or object",()=>Menu(new[]{"Service","Stock","Storage","StockContainer","StorageContainer","Counter","Bench","Bed"}.Select(mode=>new FloatMenuOption(mode,()=>Activate(b,mode,true)))));
            });
        }
        private static float Customers(Rect rect,float y,FamilyComponent c,Establishment b,EstablishmentBook book)
        {
            float w=CommunityPageLayout.ColumnWidth(rect),left=CommunityPageLayout.Section(rect,0f,y,w,"Who may visit",r=>
            {
                float h=0f;h=CommunityPageLayout.Toggle(r,h,"Residents",ref b.Residents);h=CommunityPageLayout.Toggle(r,h,"Visitors",ref b.Visitors);
                h=CommunityPageLayout.Button(r,h,"Manage individual bans",()=>Menu(c.Registry.Persons.Where(p=>p.InColony||book.Guests.Any(g=>g.Pawn==p.Id)).Select(p=>new FloatMenuOption((b.BannedCustomers.Contains(p.Id)?"Allow ":"Ban ")+p.Name,()=>{if(!b.BannedCustomers.Remove(p.Id))b.BannedCustomers.Add(p.Id);})))) ;
                h=CommunityPageLayout.Button(r,h,"Manage excluded families",()=>Menu(c.Registry.Persons.Where(p=>p.PrimaryLineageId>0).GroupBy(p=>p.PrimaryLineageId).Select(g=>new FloatMenuOption((b.ExcludedFamilies.Contains(g.Key)?"Allow ":"Exclude ")+g.First().Name+" family",()=>{if(!b.ExcludedFamilies.Remove(g.Key))b.ExcludedFamilies.Add(g.Key);})))) ;
                return CommunityPageLayout.Line(r,h,b.BannedCustomers.Count+" individual bans   •   "+b.ExcludedFamilies.Count+" excluded families",true);
            });
            bool columns=CommunityPageLayout.TwoColumns(rect);float right=CommunityPageLayout.Section(rect,columns?w+CommunityPageLayout.Gap:0f,columns?y:left,w,"House rules",r=>
            {
                float h=0f;h=CommunityPageLayout.Toggle(r,h,"Serve alcohol",ref b.Alcohol);h=CommunityPageLayout.Toggle(r,h,"Cut off intoxicated customers",ref b.CutOffDrunks);h=CommunityPageLayout.Toggle(r,h,"Quiet house",ref b.Quiet);return h;
            });
            return columns?Math.Max(left,right):right;
        }
        private static float Finance(Rect rect,float y,Establishment b,EstablishmentBook book)
        {
            float w=CommunityPageLayout.ColumnWidth(rect),left=CommunityPageLayout.Section(rect,0f,y,w,"Pay policy",r=>
            {
                float h=CommunityPageLayout.Slider(r,0f,"Base wage",ref b.BasePayMilli,0,2000," milli-silver/hour");h=CommunityPageLayout.Slider(r,h,"Staff sales share",ref b.StaffShare,0,80,"%");
                for(int i=0;i<4;i++){int value=b.Weights[i];h=CommunityPageLayout.Slider(r,h,((ContributionKind)i)+" credit",ref value,0,10);b.Weights[i]=value;}
                return CommunityPageLayout.Button(r,h,"Use owner's suggested pay policy",()=>HospitalityService.OwnerPreset(b));
            });
            bool columns=CommunityPageLayout.TwoColumns(rect);float right=CommunityPageLayout.Section(rect,columns?w+CommunityPageLayout.Gap:0f,columns?y:left,w,"Business account",r=>
            {
                float h=CommunityPageLayout.Slider(r,0f,"Cash reserve",ref b.Reserve,0,500," silver");var finance=FamilyFramework.Commerce.Bank;
                if(finance==null)return CommunityPageLayout.Line(r,h,"Treasury unavailable. Paid orders and new paid shifts are paused; existing obligations are retained.",true);
                string account=finance.Business(b.Id);h=CommunityPageLayout.Line(r,h,"Spendable "+finance.Available(account)+"   •   Wage debt "+finance.Owed(account)+"   •   Pending pay "+book.Earnings.Where(e=>e.Business==b.Id&&!e.Posted).Sum(e=>e.Silver)+"   •   Staff pool "+b.BonusPool);
                h=CommunityPageLayout.ButtonGrid(r,h,new[]{Pair("Fund 25 silver",()=>HospitalityService.Fund(b,25)),Pair("Withdraw profit ("+book.Withdrawable(b,finance)+")",()=>HospitalityService.Withdraw(b))});
                foreach(var adapter in OptionalCommerceHooks.Status)h=CommunityPageLayout.Line(r,h,"Compatibility: "+adapter,true);return h;
            });
            return columns?Math.Max(left,right):right;
        }
        private static float Character(Rect rect,float y,Establishment b)
        {
            float w=CommunityPageLayout.ColumnWidth(rect),left=CommunityPageLayout.Section(rect,0f,y,w,"Business character",r=>
            {
                float h=CommunityPageLayout.Line(r,0f,b.Personality.Description);h=CommunityPageLayout.Line(r,h,"Warmth "+b.Personality.Culture.Warmth+"   •   Refinement "+b.Personality.Culture.Refinement+"   •   Thrift "+b.Personality.Culture.Frugality+"   •   Discipline "+b.Personality.Culture.Discipline+"   •   Revelry "+b.Personality.Culture.Revelry,true);
                h=CommunityPageLayout.Line(r,h,"Staff climate "+b.Personality.StaffClimate+"/100   •   Owner "+EstablishmentPersonalityService.OwnerMood(b));
                h=CommunityPageLayout.Toggle(r,h,"Autonomous owner conversations",ref b.Personality.OwnerAutonomy);h=CommunityPageLayout.Toggle(r,h,"Discretionary owner bonuses",ref b.Personality.AllowBonuses);h=CommunityPageLayout.Toggle(r,h,"Require equal treatment",ref b.Personality.EqualTreatment);return CommunityPageLayout.Line(r,h,b.Personality.LastReason,true);
            });
            bool columns=CommunityPageLayout.TwoColumns(rect);float right=CommunityPageLayout.Section(rect,columns?w+CommunityPageLayout.Gap:0f,columns?y:left,w,"Reputation",r=>
            {
                float h=CommunityPageLayout.Line(r,0f,"Overall "+b.Reputation+"/100   •   "+(b.Rowdiness>60?"Rowdy":b.Rowdiness<30?"Peaceful":"Sociable"));h=CommunityPageLayout.Line(r,h,"Food "+b.Food+"   •   Service "+b.Service+"   •   Comfort "+b.Comfort+"   •   Value "+b.Value+"   •   Safety "+b.Safety);
                h=CommunityPageLayout.Line(r,h,"Improve weak scores through supplies, seating, coverage and fair prices. House rules affect safety and atmosphere.",true);return h;
            });
            return columns?Math.Max(left,right):right;
        }
        private static float Activity(Rect rect,float y,FamilyComponent c,Establishment b,EstablishmentBook book)
        {
            float w=CommunityPageLayout.ColumnWidth(rect),left=CommunityPageLayout.Section(rect,0f,y,w,"Bookings and guests",r=>
            {
                float h=CommunityPageLayout.Button(r,0f,"Reserve a family celebration (50 silver)",()=>Menu(c.Registry.Households.Where(x=>x.ResidentIds.Any(id=>c.Registry.GetPerson(id)?.InColony==true)).Select(x=>new FloatMenuOption("Household #"+x.Id,()=>HospitalityStories.BookMeal(b,BookingKind.Celebration,x.Id,x.ResidentIds.Count,50)))));
                h=CommunityPageLayout.Button(r,h,"Commission a community meal (100 silver)",()=>HospitalityStories.BookMeal(b,BookingKind.CommunityMeal,0,c.Registry.Persons.Count(p=>p.InColony),100));if(b.Kind==EstablishmentKind.Inn)h=CommunityPageLayout.Button(r,h,"Offer emergency meals and shelter (100 silver)",()=>HospitalityStories.BookMeal(b,BookingKind.EmergencyShelter,0,c.Registry.Persons.Count(p=>p.InColony),100));
                foreach(var booking in c.Registry.HospitalityBookings().Bookings.Where(x=>x.Business==b.Id&&!x.Closed))h=CommunityPageLayout.Line(r,h,booking.Kind+": "+booking.Served+" served, "+(booking.Budget-booking.Committed)+" silver remaining");
                foreach(var guest in book.Guests.Where(g=>g.Business==b.Id))h=CommunityPageLayout.Line(r,h,(HospitalityService.Pawn(guest.Pawn)?.LabelShort??"Guest")+": until day "+guest.UntilDay+(guest.Leaving?"; leaving":""));
                return CommunityPageLayout.Line(r,h,"Unused booking funds are released when the booking expires.",true);
            });
            bool columns=CommunityPageLayout.TwoColumns(rect);float right=CommunityPageLayout.Section(rect,columns?w+CommunityPageLayout.Gap:0f,columns?y:left,w,"Recent activity",r=>
            {
                float h=CommunityPageLayout.Button(r,0f,"Refund a recent bill",()=>Menu(book.Orders.Where(o=>o.Business==b.Id&&o.Stage==ServiceStage.Settled&&!o.Refunded).Select(o=>new FloatMenuOption((HospitalityService.Pawn(o.Customer)?.LabelShort??"Customer #"+o.Customer)+": "+o.Price+" silver",()=>HospitalityStories.Refund(o)))));foreach(var line in book.History.AsEnumerable().Reverse().Take(10))h=CommunityPageLayout.Line(r,h,line,true);return h;
            });
            return columns?Math.Max(left,right):right;
        }
        private static void OwnerMenu(Establishment b,FamilyComponent c)
        {
            var options=new List<FloatMenuOption>{new FloatMenuOption("Community",()=>{b.Owner=EstablishmentOwner.Community;b.OwnerId=0;})};foreach(var p in c.Registry.Persons.Where(p=>p.InColony&&!p.Juvenile)){var who=p;options.Add(new FloatMenuOption(who.Name,()=>{b.Owner=EstablishmentOwner.Person;b.OwnerId=who.Id;}));}foreach(var h in c.Registry.Households.Where(h=>h.ResidentIds.Any(id=>c.Registry.GetPerson(id)?.InColony==true))){var house=h;options.Add(new FloatMenuOption("Household: "+string.Join(", ",house.ResidentIds.Select(id=>c.Registry.GetPerson(id)?.Name).Where(n=>n!=null)),()=>{b.Owner=EstablishmentOwner.Household;b.OwnerId=house.Id;}));}Menu(options);
        }
        private static void StaffMenu(Establishment b,FamilyComponent c)=>Menu(c.Registry.Persons.Where(p=>p.InColony&&!p.Juvenile).Select(p=>new FloatMenuOption((b.Staff.Contains(p.Id)?"Remove ":"Assign ")+p.Name,()=>{if(!b.Staff.Remove(p.Id)){b.Staff.Add(p.Id);if(b.Kind!=EstablishmentKind.Shop)c.Registry.Assignments().Assign(p.Id,CareerService.Professions.ById(CulinaryCareers.Cook),c.Registry.Careers(),FrameworkServices.Now);}})));
        private static KeyValuePair<string,Action> Pair(string label,Action action)=>new KeyValuePair<string,Action>(label,action);
        private static string OwnerName(Establishment b,FamilyComponent c)=>b.Owner==EstablishmentOwner.Community?"Community":b.Owner==EstablishmentOwner.Person?c.Registry.GetPerson(b.OwnerId)?.Name??"Former owner":"Household #"+b.OwnerId;
        private static void Menu(IEnumerable<FloatMenuOption> options)=>Find.WindowStack.Add(new FloatMenu(options.ToList()));
        private static void Activate(Establishment b,string mode,bool erase=false){Find.MainTabsRoot.SetCurrentTab(null);Find.DesignatorManager.Select(new EstablishmentDesignator(b.Id,mode,erase));}
    }
    public sealed class EstablishmentDesignator : Designator_Cells
    {
        private readonly int business;private readonly string mode;private readonly bool erase;
        private Zone_Stockpile paintedZone;
        private int DraggableDimensions=>mode=="Service"||mode=="Stock"||mode=="Storage"?2:0;
        public EstablishmentDesignator(int id,string mode,bool erase=false){business=id;this.mode=mode;this.erase=erase;defaultLabel=(erase?"Remove ":"Designate ")+mode;defaultDesc="Select premises for this establishment. Designated stock is explicitly allocated from communal supplies; private kitchen bills use only business stock/storage.";icon=ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile",false)??BaseContent.BadTex;useMouseIcon=true;}
        public override AcceptanceReport CanDesignateCell(IntVec3 cell)
        {
            var e=FamilyComponent.Current?.Registry.Establishments().ById(business);if(e==null||Map.uniqueID!=e.Map||!cell.InBounds(Map)||cell.Fogged(Map))return false;
            if(DraggableDimensions==2)
            {
                if(mode!="Service"&&!erase&&FamilyComponent.Current.Registry.Establishments().Businesses.Any(other=>other.Id!=e.Id&&other.Map==e.Map&&(other.StockCells.Contains(Map.cellIndices.CellToIndex(cell))||other.StorageCells.Contains(Map.cellIndices.CellToIndex(cell)))))return "Another establishment already owns this stock area.";
                return true;
            }
            return Pick(cell)!=null;
        }
        private Thing Pick(IntVec3 cell)=>cell.GetThingList(Map).FirstOrDefault(t=>mode=="Counter"?(t.def.surfaceType==SurfaceType.Eat||HospitalityInterop.IsRegister(t)):mode=="Bed"?t is Building_Bed:mode=="Bench"?t is IBillGiver:t is IStoreSettingsParent);
        public override void DesignateSingleCell(IntVec3 cell)
        {
            var e=FamilyComponent.Current?.Registry.Establishments().ById(business);if(e==null)return;
            int index=Map.cellIndices.CellToIndex(cell);
            if(DraggableDimensions==2){var cells=mode=="Service"?e.ServiceCells:mode=="Stock"?e.StockCells:e.StorageCells;if(erase){if(mode!="Service"&&cells.Contains(index))HospitalityNarrative.SuppliesRemoved(e,cell);cells.Remove(index);}else
                {
                    cells.Add(index);
                    if(mode!="Service"&&Map.zoneManager.ZoneAt(cell)==null&&cell.Standable(Map))
                    {
                        if(paintedZone==null){paintedZone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,Map.zoneManager);paintedZone.label=e.Name+" "+mode.ToLowerInvariant();Map.zoneManager.RegisterZone(paintedZone);}
                        paintedZone.AddCell(cell);
                    }
                }
                return;}
            var t=Pick(cell);if(t==null)return;
            var others=FamilyComponent.Current.Registry.Establishments().Businesses.Where(o=>o.Id!=e.Id&&o.Map==e.Map);
            if(!erase&&others.Any(o=>o.Counter==t.thingIDNumber||o.Beds.Contains(t.thingIDNumber)||o.Benches.Contains(t.thingIDNumber)||o.StockContainers.Contains(t.thingIDNumber)||o.StorageContainers.Contains(t.thingIDNumber))){Messages.Message("That object belongs to another establishment.",MessageTypeDefOf.RejectInput,false);return;}
            if(mode=="Counter"&&!erase&&!HospitalityInterop.CanUseCounter(t)){Messages.Message("Close the native Gastronomy restaurant and let its diners finish before assigning its register to this establishment. One system must own service.",MessageTypeDefOf.RejectInput,false);return;}
            if(mode=="Counter")e.Counter=erase?0:t.thingIDNumber;
            else if(mode=="Bed"||mode=="Bench"){var ids=mode=="Bed"?e.Beds:e.Benches;if(erase)ids.Remove(t.thingIDNumber);else if(!ids.Contains(t.thingIDNumber))ids.Add(t.thingIDNumber);}
            else{var ids=mode=="StockContainer"?e.StockContainers:e.StorageContainers;if(erase){if(ids.Contains(t.thingIDNumber))HospitalityNarrative.SuppliesRemoved(e,cell);ids.Remove(t.thingIDNumber);}else ids.Add(t.thingIDNumber);}
        }
        public override void SelectedUpdate(){base.SelectedUpdate();var e=FamilyComponent.Current?.Registry.Establishments().ById(business);if(e!=null&&e.Map==Map.uniqueID)GenDraw.DrawFieldEdges(e.ServiceCells.Select(Map.cellIndices.IndexToCell).ToList());}
    }
}
