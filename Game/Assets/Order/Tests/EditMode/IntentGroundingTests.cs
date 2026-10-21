using NUnit.Framework;
using Order.Voice;

namespace Order.Tests.EditMode
{
    public sealed class IntentGroundingTests
    {
        [TestCase("Громкость 11 процентов", 11)]
        [TestCase("звук двадцать один процент", 21)]
        public void BareVolumeKeepsExactNumber(string text, double expected)
        {
            Assert.That(IntentGrounding.TryDirect(text, out var decision), Is.True);
            Assert.That(decision.Action, Is.EqualTo("set_volume")); Assert.That(decision.Value, Is.EqualTo(expected));
        }
        [TestCase("Не выключай звук")]
        [TestCase("Уменьши музыку до 11 процентов")]
        public void UnsupportedOrNegatedRequestDoesNotBecomeVolume(string text)
        { IntentGrounding.TryDirect(text, out var decision); Assert.That(decision.Action, Is.EqualTo("clarify")); }
        [Test] public void ToAndByAreDifferentEvenIfModelMistakesThem()
        {
            var proposed = new IntentDecision { Action = "adjust_volume", Value = -50, Screen = "none" };
            var result = IntentGrounding.Ground(proposed, "убавь громкость до 11 процентов");
            Assert.That(result.Action, Is.EqualTo("set_volume")); Assert.That(result.Value, Is.EqualTo(11));
        }
        [Test] public void ModelCannotInventFaction()
        {
            var result = IntentGrounding.Ground(new IntentDecision { Action = "prepare_match", Faction = 2, Screen = "none" }, "Запусти матч против бота");
            Assert.That(result.Faction, Is.Null);
        }
    }
}
