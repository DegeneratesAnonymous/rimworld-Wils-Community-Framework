using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    public sealed class AgricultureEvent
    {
        public string Id,Role,Fact,Thought;public int Mood,Opinion;
        public bool Matches(string role,string fact,bool other)=>Role==role&&Fact==fact&&(Opinion==0||other);
    }
    public static class AgricultureEvents
    {
        public static readonly string[] Roles={"Forager","Tracker","Handler","Farmer","Hunter","Rancher","Horticulturist","Herbalist","Arborist","Stalker","Gamekeeper","BigGameHunter","Shepherd","Breeder","Beastmaster"};
        private static readonly string[] Positive={"GoodHarvest","CleanKill","HealthyStock","Resourceful","ProudWork"};
        private static readonly string[] Negative={"SpoiledCrop","LostTrail","InjuredAnimal","Overhunted","FailedSeason"};
        private static readonly string[] Social={"SharedKnowledge","HelpedWork","PraisedSkill","TerritoryDispute","AnimalDispute"};
        public static readonly AgricultureEvent[] All=Build().ToArray();
        private static IEnumerable<AgricultureEvent> Build()
        {
            foreach(var role in Roles)
            {
                for(int i=0;i<5;i++)yield return new AgricultureEvent{Id=role+"Positive"+i,Role=role,Fact=Positive[i],Thought="WFL_A_Positive"+(i+1),Mood=2+i%2};
                for(int i=0;i<5;i++)yield return new AgricultureEvent{Id=role+"Negative"+i,Role=role,Fact=Negative[i],Thought="WFL_A_Negative"+(i+1),Mood=-2-i%2};
                for(int i=0;i<5;i++)yield return new AgricultureEvent{Id=role+"Social"+i,Role=role,Fact=Social[i],Thought="WFL_A_Social"+(i+1),Opinion=i<3?3:-3};
            }
        }
        public static IEnumerable<AgricultureEvent> Eligible(string role,string fact,bool other)=>All.Where(e=>e.Matches(role,fact,other));
    }
}
