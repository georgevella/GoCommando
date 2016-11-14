using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GoCommando.Extensions;
using NUnit.Framework;

namespace GoCommando.Tests
{
    [TestFixture]
    public class TypeExtensionsTests
    {
        [TestCase(typeof(IEnumerable<string>), true)]
        [TestCase(typeof(IEnumerable<int>), true)]
        [TestCase(typeof(ICollection<string>), true)]
        [TestCase(typeof(IList<Guid>), true)]
        [TestCase(typeof(List<int>), true)]
        [TestCase(typeof(Collection<string>), true)]
        [TestCase(typeof(Array), true)]
        [TestCase(typeof(ArrayList), true)]
        [TestCase(typeof(IEnumerable), true)]
        [TestCase(typeof(string), false)]
        public void EnumerableOrCollectionTypeDetection(Type type, bool expectedResult)
        {
            Assert.AreEqual(expectedResult, type.IsEnumerableOrCollection());
        }

        [TestCase(typeof(IEnumerable<string>), typeof(string))]
        [TestCase(typeof(IEnumerable<int>), typeof(int))]
        [TestCase(typeof(ICollection<string>), typeof(string))]
        [TestCase(typeof(IList<Guid>), typeof(Guid))]
        [TestCase(typeof(List<int>), typeof(int))]
        [TestCase(typeof(Collection<string>), typeof(string))]
        [TestCase(typeof(Array), typeof(object))]
        [TestCase(typeof(ArrayList), typeof(object))]
        [TestCase(typeof(IEnumerable), typeof(object))]
        [TestCase(typeof(TestCollection), typeof(string))]
        public void EnumerableOrCollectionItemTypeDetection(Type type, Type expectedItemType)
        {
            Assert.That(type.GetEnumerableOrCollectionItemType(), Is.EqualTo(expectedItemType));
        }

        class TestCollection : Collection<string>
        {

        }
    }
}