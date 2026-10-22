using System.Linq;
using NUnit.Framework;
using Order.Content;
using Order.Presentation;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class UnitSelectionTests
    {
        static PrototypeMatch Squad(){var match=new PrototypeMatch();match.AddTestSquad();return match;}
        static PrototypeUnit First(PrototypeMatch match,UnitKind kind)=>match.Units.Find(u=>u.Kind==kind);

        [Test] public void ClickReplacesShiftTogglesAndOrderPutsWorkersLast()
        {
            var match=Squad();var selection=new UnitSelection();
            var worker=First(match,UnitKind.Worker);var melee=First(match,UnitKind.Melee);var air=First(match,UnitKind.Air);
            selection.Click(worker,false,false,match.Units);Assert.That(selection.Units,Is.EqualTo(new[]{worker}));
            selection.Click(air,true,false,match.Units);selection.Click(melee,true,false,match.Units);
            Assert.That(selection.Units,Is.EqualTo(new[]{melee,air,worker}));
            selection.Click(air,true,false,match.Units);Assert.That(selection.Units,Is.EqualTo(new[]{melee,worker}));
            selection.Click(air,false,false,match.Units);Assert.That(selection.Units,Is.EqualTo(new[]{air}));
        }
        [Test] public void SameKindClickTakesOnlyVisibleUnitsOfThatKind()
        {
            var match=Squad();var selection=new UnitSelection();
            var ranged=match.Units.FindAll(u=>u.Kind==UnitKind.Ranged);
            selection.Click(ranged[0],false,true,match.Units.Where(u=>u!=ranged[2]));
            Assert.That(selection.Units,Is.EqualTo(new[]{ranged[0],ranged[1]}));
            selection.Click(First(match,UnitKind.Melee),true,true,match.Units);
            Assert.That(selection.Count,Is.EqualTo(4));Assert.That(selection.Units.All(u=>u.Kind!=UnitKind.Worker),Is.True);
        }
        [Test] public void BoxReplacesAddsWithShiftAndEmptyBoxClears()
        {
            var match=Squad();var selection=new UnitSelection();
            var melee=match.Units.FindAll(u=>u.Kind==UnitKind.Melee);var air=First(match,UnitKind.Air);
            selection.Box(melee,false);Assert.That(selection.Units,Is.EqualTo(melee));
            selection.Box(new[]{air,melee[0]},true);Assert.That(selection.Count,Is.EqualTo(3));
            selection.Box(new PrototypeUnit[0],true);Assert.That(selection.Count,Is.EqualTo(3));
            selection.Box(new PrototypeUnit[0],false);Assert.That(selection.Count,Is.Zero);
        }
        [Test] public void ControlGroupsAssignAddRecallAndForgetMissingUnits()
        {
            var match=Squad();var selection=new UnitSelection();
            var melee=match.Units.FindAll(u=>u.Kind==UnitKind.Melee);var air=First(match,UnitKind.Air);
            selection.Set(melee);selection.Assign(0);
            selection.Set(new[]{air});selection.AddToGroup(0);selection.Assign(9);
            Assert.That(selection.GroupSize(0,match.Units),Is.EqualTo(3));Assert.That(selection.GroupSize(9,match.Units),Is.EqualTo(1));
            selection.Clear();Assert.That(selection.Recall(0,match.Units),Is.True);Assert.That(selection.Units,Is.EqualTo(new[]{melee[0],melee[1],air}));
            Assert.That(selection.Recall(4,match.Units),Is.False);Assert.That(selection.Count,Is.EqualTo(3));
            match.Units.Remove(air);selection.Prune(match.Units);
            Assert.That(selection.Count,Is.EqualTo(2));Assert.That(selection.GroupSize(9,match.Units),Is.Zero);
            selection.Clear();selection.Assign(0);Assert.That(selection.GroupSize(0,match.Units),Is.Zero);
        }
    }
}
