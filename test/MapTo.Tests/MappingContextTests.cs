﻿using MapTo.Sources;
using MapTo.Tests.Extensions;
using MapTo.Tests.Infrastructure;
using Xunit;
using static MapTo.Tests.Common;

namespace MapTo.Tests
{
    public class MappingContextTests
    {
        [Fact(Skip ="Need to update to new AttributeSyntax")]
        public void VerifyMappingContextSource()
        {
            // Arrange
            const string source = "";
            var expected = @"
// <auto-generated />

using System;
using System.Collections.Generic;

namespace MapTo
{
    public sealed class MappingContext
    {
        private readonly Dictionary<object, object> _cache;

        public MappingContext()
        {
            _cache = new Dictionary<object, object>(1);
        }

        public TMapped MapFromWithContext<TOriginal, TMapped>(TOriginal original, Func<TOriginal, MappingContext, TMapped> CreateDelegate)
        {
            if (original == null)
            {
                return default(TMapped);
            }

            if (!TryGetValue<TOriginal, TMapped>(original, out var mapped))
            {
                mapped = CreateDelegate(original, this);
            }

            return mapped;
        }

        public void Register<TOriginal, TMapped>(TOriginal original, TMapped mapped)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (mapped == null) throw new ArgumentNullException(nameof(mapped));

            if (!_cache.ContainsKey(original))
            {
                _cache.Add(original, mapped);
            }
        }

        private bool TryGetValue<TOriginal, TMapped>(TOriginal original, out TMapped mapped)
        {
            if (original != null && _cache.TryGetValue(original, out var value))
            {
                mapped = (TMapped)value;
                return true;
            }

            mapped = default(TMapped);
            return false;
        }
    }
}
".Trim();

            // Act
            var (compilation, diagnostics) = CSharpGenerator.GetOutputCompilation(source, analyzerConfigOptions: DefaultAnalyzerOptions);

            // Assert
            diagnostics.ShouldBeSuccessful();
            compilation.SyntaxTrees.ShouldContainSource(MappingContextSource.ClassName, expected);
        }
    }
}