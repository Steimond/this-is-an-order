using NUnit.Framework;
using Order.Voice.Local;

namespace Order.Tests.EditMode
{
    public sealed class IntentDecodeTests
    {
        [Test] public void StrictDecisionDecodes()
        {
            var result = LocalIntentInterpreter.Decode("{\"action\":\"set_volume\",\"value\":11,\"screen\":\"none\",\"faction\":null,\"open_audio\":false}");
            Assert.That(result.Value, Is.EqualTo(11));
        }
        [TestCase("{}")]
        [TestCase("{\"action\":\"set_volume\",\"value\":\"11\",\"screen\":\"none\",\"faction\":null,\"open_audio\":false}")]
        [TestCase("{\"action\":\"set_volume\",\"value\":11,\"screen\":\"none\",\"faction\":null,\"open_audio\":false,\"code\":\"something\"}")]
        [TestCase("{\"action\":\"set_volume\",\"action\":\"navigate\",\"value\":11,\"screen\":\"none\",\"faction\":null,\"open_audio\":false}")]
        public void MalformedDecisionsAreRejected(string json) => Assert.That(() => LocalIntentInterpreter.Decode(json), Throws.Exception);
    }
}
