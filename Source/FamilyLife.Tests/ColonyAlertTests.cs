using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class ColonyAlertTests
    {
        [Fact]
        public void The_colony_level_is_the_highest_published_by_an_enabled_module()
        {
            var b = new ColonyAlertBoard();
            Assert.Equal(ColonyAlertLevel.Normal, b.Level(null));
            b.Publish("wils.military", ColonyAlertLevel.Yellow); b.Publish("other", ColonyAlertLevel.Red);
            Assert.Equal(ColonyAlertLevel.Red, b.Level(null));
            Assert.Equal(ColonyAlertLevel.Yellow, b.Level(m => m != "other"));          // a stopped module's alert is ignored
            Assert.Equal("other", b.RaisedBy(null));
        }

        [Fact]
        public void Withdrawing_or_lowering_returns_the_colony_to_normal()
        {
            var b = new ColonyAlertBoard(); b.Publish("wils.military", ColonyAlertLevel.Red); b.Publish("wils.military", ColonyAlertLevel.Normal);
            Assert.Equal(ColonyAlertLevel.Normal, b.Level(null)); Assert.Null(b.RaisedBy(null));
            b.Publish("wils.military", ColonyAlertLevel.Red); b.Withdraw("wils.military"); Assert.Equal(ColonyAlertLevel.Normal, b.Level(null));
        }
    }
}
