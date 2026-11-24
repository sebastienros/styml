// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using System.Text.Yaml.Serialization.Tests;
using Xunit;

namespace System.Text.Yaml.Tests
{
    public class IncompleteTestClass
    {
        public int Key { get; set; }
    }

    public class YamlSerializerTests
    {
        [Fact]
        public void DeserializeBoolean_Null()
        {
            Assert.Throws<YamlException>(
                () => YamlSerializer.Deserialize<IList<bool>>("[null]"));
        }

        [Fact]
        public void DeserializeBoolean_DateTime()
        {
            Assert.Throws<YamlException>(
                () => YamlSerializer.Deserialize<IList<bool>>("['2000-12-20T10:55:55Z']"));
        }

        [Fact]
        public void DeserializeBoolean_BadString()
        {
            Assert.Throws<YamlException>(
                () => YamlSerializer.Deserialize<IList<bool>>("['pie']"));
        }

        [Fact]
        public void DeserializeBoolean_EmptyString()
        {
            Assert.Throws<YamlException>(
                () => YamlSerializer.Deserialize<IList<bool>>("['']"));
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void NewProperty()
        {
            Assert.Equal("""{"IsTransient":true}""", YamlSerializer.Serialize(new ChildClass { IsTransient = true }));

            ChildClass childClass = YamlSerializer.Deserialize<ChildClass>("""{"IsTransient":true}""");
            Assert.True(childClass.IsTransient);
        }

        [Fact]
        [Trait("category", "jsonoutput")]
        public void NewPropertyVirtual()
        {
            Assert.Equal("""{"IsTransient":true}""", YamlSerializer.Serialize(new ChildClassVirtual { IsTransient = true }));

            ChildClassVirtual childClass = YamlSerializer.Deserialize<ChildClassVirtual>("""{"IsTransient":true}""");
            Assert.True(childClass.IsTransient);
        }

        [Fact]
        public void DeserializeCommentTestObjectWithComments()
        {
            CommentTestObject o = YamlSerializer.Deserialize<CommentTestObject>("{/* Test */}", new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip });
            Assert.False(o.A);

            o = YamlSerializer.Deserialize<CommentTestObject>("""{"A": true/* Test */}""", new YamlSerializerOptions { ReadCommentHandling = YamlCommentHandling.Skip });
            Assert.True(o.A);
        }

        [Fact]
        public void PreserveReferencesCallbackTest()
        {
            PersonReference p1 = new PersonReference
            {
                Name = "John Smith"
            };
            PersonReference p2 = new PersonReference
            {
                Name = "Mary Sue",
            };

            p1.Spouse = p2;
            p2.Spouse = p1;
            Assert.Throws<YamlException> (() => YamlSerializer.Serialize(p1));
        }
    }

    public class CommentTestObject
    {
        public bool A { get; set; }
    }

    public class ChildClassVirtual : BaseClassVirtual
    {
        public new virtual bool IsTransient { get; set; }
    }

    public class BaseClassVirtual
    {
        internal virtual bool IsTransient { get; set; }
    }

    public class BaseClass
    {
        internal bool IsTransient { get; set; }
    }

    public class ChildClass : BaseClass
    {
        public new bool IsTransient { get; set; }
    }
}
