using System.Collections.Generic;
using FastIoC.Editor.Generation;
using NUnit.Framework;

namespace FastIoC.Tests.Editor
{
    // Deleting a script used to remove all generated code, even when nothing generated referenced it. In batch mode
    // nothing regenerated it, so builds shipped without injection.
    public class GeneratedCodeReferencesTests
    {
        private static readonly string[] Generated =
        {
            "case 3: Inject_Shop_ShopView((global::Shop.ShopView)target, context); return;",
            "{ typeof(global::Shop.ShopModel), 4 },"
        };

        [Test]
        public void ADeletedScriptTheGeneratedCodeUsesIsReported()
        {
            List<string> inUse = GeneratedCodeReferences.FindDeletedScriptsInUse(new[] { "Assets/Shop/ShopView.cs" }, Generated);

            CollectionAssert.AreEqual(new[] { "Assets/Shop/ShopView.cs" }, inUse);
        }

        [Test]
        public void AnUnrelatedDeletedScriptIsIgnored()
        {
            List<string> inUse = GeneratedCodeReferences.FindDeletedScriptsInUse(
                new[] { "Packages/com.example.screens/Runtime/ScreenParameters.cs", "Assets/Shop/ShopViewOld.cs" }, Generated);

            Assert.IsEmpty(inUse, "another package's script, and a name that only contains a used one");
        }

        [Test]
        public void GeneratedFilesAndNonScriptsAreIgnored()
        {
            List<string> inUse = GeneratedCodeReferences.FindDeletedScriptsInUse(
                new[] { "Assets/FastIoC/Generated/GeneratedMonoInjectors.cs", "Assets/Shop/ShopView.prefab" }, Generated);

            Assert.IsEmpty(inUse);
        }

        [Test]
        public void NoGeneratedCodeMeansNothingIsInUse()
        {
            Assert.IsEmpty(GeneratedCodeReferences.FindDeletedScriptsInUse(new[] { "Assets/Shop/ShopView.cs" }, new List<string>()));
        }
    }
}
