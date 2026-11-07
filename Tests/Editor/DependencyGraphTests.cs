using System;
using System.Collections.Generic;
using FastIoC.API;
using FastIoC.Editor.Graph;
using FastIoC.Fixtures;
using NUnit.Framework;

namespace FastIoC.Tests.Editor
{
    /// <summary>Cycle and missing-binding validation that runs before code is generated.</summary>
    public class DependencyGraphTests
    {
        private static readonly Dictionary<Type, BindingInfo> NoBindings = new();

        private static bool HasCycles(params Type[] types)
        {
            var dag = DependencyDagValidator.BuildDag(new HashSet<Type>(types), NoBindings);
            return DependencyDagValidator.HasCycles(dag, out _);
        }

        private static List<string> MissingBindings(Dictionary<Type, BindingInfo> bindings, params Type[] types)
        {
            DependencyDagValidator.ValidateMissingBindings(new HashSet<Type>(types), bindings, new HashSet<Type>(), out List<string> errors);
            return errors;
        }

        [Test]
        public void ConstructorCycle_IsReported()
        {
            Assert.IsTrue(HasCycles(typeof(CycleA), typeof(CycleB)));
        }

        [Test]
        public void CycleThroughLazy_IsNotReported()
        {
            Assert.IsFalse(HasCycles(typeof(LazyCycleA), typeof(LazyCycleB)));
        }

        [Test]
        public void UnboundParameter_IsReported()
        {
            List<string> errors = MissingBindings(NoBindings, typeof(NeedsMissing));
            CollectionAssert.AreEqual(new[] { "NeedsMissing requires IMissing, which isn't bound in any installer." }, errors);
        }

        [Test]
        public void UnboundLazyParameter_NamesTheWrappedType()
        {
            List<string> errors = MissingBindings(NoBindings, typeof(NeedsLazyMissing));
            CollectionAssert.AreEqual(new[] { "NeedsLazyMissing requires Lazy<IMissing>, but IMissing isn't bound in any installer." }, errors);
        }

        [Test]
        public void ParameterWithADefaultValue_IsSkipped()
        {
            CollectionAssert.IsEmpty(MissingBindings(NoBindings, typeof(OptionalMissing)));
        }

        [Test]
        public void BoundParameters_IncludingLazyAndFunc_PassValidation()
        {
            Dictionary<Type, BindingInfo> bindings = new()
            {
                [typeof(IService)] = new BindingInfo(typeof(IService), typeof(Service), "Singleton"),
                [typeof(Other)] = new BindingInfo(typeof(Other), typeof(Other), "Transient"),
            };

            CollectionAssert.IsEmpty(MissingBindings(bindings, typeof(Consumer), typeof(Service), typeof(Other)));
        }
    }
}
