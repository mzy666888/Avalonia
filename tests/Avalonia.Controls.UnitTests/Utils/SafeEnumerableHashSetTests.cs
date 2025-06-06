﻿using System.Collections.Generic;
using Avalonia.UnitTests;
using Avalonia.Utilities;
using Xunit;

namespace Avalonia.Controls.UnitTests.Utils
{
    public class SafeEnumerableHashSetTests : ScopedTestBase
    {
        [Fact]
        public void Set_Is_Not_Copied_Outside_Enumeration()
        {
            var target = new SafeEnumerableHashSet<string>();
            var inner = target.Inner;

            target.Add("foo");
            target.Add("bar");
            target.Remove("foo");

            Assert.Same(inner, target.Inner);
        }

        [Fact]
        public void Set_Is_Copied_Outside_Enumeration()
        {
            var target = new SafeEnumerableHashSet<string>();
            var inner = target.Inner;

            target.Add("foo");

            foreach (var i in target)
            {
                Assert.Same(inner, target.Inner);
                target.Add("bar");
                Assert.NotSame(inner, target.Inner);
                Assert.Equal("foo", i);
            }

            inner = target.Inner;

            foreach (var i in target)
            {
                target.Add("baz");
                Assert.NotSame(inner, target.Inner);
            }

            Assert.Equal(new HashSet<string> { "foo", "bar", "baz", "baz" }, target);
        }

        [Fact]
        public void Set_Is_Not_Copied_After_Enumeration()
        {
            var target = new SafeEnumerableHashSet<string>();
            var inner = target.Inner;

            target.Add("foo");

            foreach (var i in target)
            {
                target.Add("bar");
                Assert.NotSame(inner, target.Inner);
                inner = target.Inner;
                Assert.Equal("foo", i);
            }

            target.Add("baz");
            Assert.Same(inner, target.Inner);
        }

        [Fact]
        public void Set_Is_Copied_Only_Once_During_Enumeration()
        {
            var target = new SafeEnumerableHashSet<string>();
            var inner = target.Inner;

            target.Add("foo");

            foreach (var i in target)
            {
                target.Add("bar");
                Assert.NotSame(inner, target.Inner);
                inner = target.Inner;
                target.Add("baz");
                Assert.Same(inner, target.Inner);
            }

            target.Add("baz");
        }

        [Fact]
        public void Set_Is_Copied_During_Nested_Enumerations()
        {
            var target = new SafeEnumerableHashSet<string>();
            var initialInner = target.Inner;
            var firstItems = new HashSet<string>();
            var secondItems = new HashSet<string>();
            HashSet<string> firstInner;
            HashSet<string> secondInner;

            target.Add("foo");

            foreach (var i in target)
            {
                target.Add("bar");

                firstInner = target.Inner;
                Assert.NotSame(initialInner, firstInner);

                foreach (var j in target)
                {
                    target.Add("baz");

                    secondInner = target.Inner;
                    Assert.NotSame(firstInner, secondInner);

                    secondItems.Add(j);
                }

                firstItems.Add(i);
            }

            Assert.Equal(new HashSet<string> { "foo" }, firstItems);
            Assert.Equal(new HashSet<string> { "foo", "bar" }, secondItems);
            Assert.Equal(new HashSet<string> { "foo", "bar", "baz", "baz" }, target);

            var finalInner = target.Inner;
            target.Add("final");
            Assert.Same(finalInner, target.Inner);
        }
    }
}
