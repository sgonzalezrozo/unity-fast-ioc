using FastIoC.Editor.Generation;
using NUnit.Framework;
using UnityEngine;

namespace FastIoC.Tests.Editor
{
    /// <summary>Where the InternalsVisibleTo files go: inside Generated, compiled into each assembly by an .asmref.</summary>
    public class InternalsAccessPlanTests
    {
        [System.Serializable]
        private class AsmrefData
        {
            public string reference;
        }

        [Test]
        public void TheFilesLiveUnderGenerated()
            => Assert.AreEqual("Assets/FastIoC/Generated/InternalsVisibleTo", InternalsAccessPlan.RootFolder);

        [Test]
        public void TheAsmref_PointsAtTheAsmdefByGuid()
        {
            string json = InternalsAccessPlan.AsmrefContent("0123456789abcdef0123456789abcdef");

            AsmrefData data = JsonUtility.FromJson<AsmrefData>(json);
            Assert.AreEqual("GUID:0123456789abcdef0123456789abcdef", data.reference);
        }
    }
}
