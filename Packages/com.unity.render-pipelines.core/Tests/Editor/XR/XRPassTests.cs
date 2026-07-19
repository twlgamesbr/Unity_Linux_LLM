using NUnit.Framework;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Experimental.Tests.XR
{
    [TestFixture]
    class XRPassTests
    {
        [Test]
        public void EmptyPass_IsFirstAndLastPass()
        {
            Assert.IsTrue(XRSystem.emptyPass.isFirstCameraPass);
            Assert.IsTrue(XRSystem.emptyPass.isLastCameraPass);
        }
    }
}
