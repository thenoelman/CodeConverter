﻿using Xunit;

namespace CodeConverter.Tests.CSharp
{
    public class StandaloneMultiStatementTests : ConverterTestBase
    {
        [Fact]
        public void Reassignment()
        {
            TestConversionVisualBasicToCSharp(
@"Dim num as Integer = 4
num = 5",
@"int num = 4;
num = 5;",
expectSurroundingBlock: true);
        }

        [Fact]
        public void ObjectMemberInitializerSyntax()
        {
            TestConversionVisualBasicToCSharp(
@"Dim obj as New AttributeUsageAttribute With
{
    .AllowMultiple = True,
    .Inherited = False
}
obj = Nothing",
@"AttributeUsageAttribute obj = new AttributeUsageAttribute()
{
    AllowMultiple = true,
    Inherited = false
};
obj = null;",
                expectSurroundingBlock: true);
        }

        [Fact]
        public void AnonymousObjectCreationExpressionSyntax()
        {
            TestConversionVisualBasicToCSharp(
@"Dim obj = New With
{
    .Name = ""Hello"",
    .Value = ""World""
}
obj = Nothing",
@"var obj = new
{
    Name = ""Hello"",
    Value = ""World""
};
obj = null;",
                expectSurroundingBlock: true);
        }

        [Fact]
        public void SingleAssigment()
        {
            TestConversionVisualBasicToCSharp(
                @"Dim x = 3",
                @"var x = 3;",
                expectSurroundingBlock: true);
        }

        [Fact]
        public void SingleFieldDeclaration()
        {
            TestConversionVisualBasicToCSharp(
                @"Private x As Integer = 3",
                @"private int x = 3;");
        }

        [Fact]
        public void SingleEmptyClass()
        {
            TestConversionVisualBasicToCSharp(
@"Public Class Test
End Class",
@"public class Test
{
}");
        }

        [Fact]
        public void SingleAbstractMethod()
        {
            TestConversionVisualBasicToCSharp(
                @"Private MustOverride Sub abs()",
                @"private abstract void abs();");
        }

        [Fact]
        public void SingleEmptyNamespace()
        {
            TestConversionVisualBasicToCSharp(
@"Namespace nam
End Namespace",
@"namespace nam
{
}");
        }

        [Fact]
        public void SingleUnusedUsingAliasTidiedAway()
        {
            TestConversionVisualBasicToCSharp(@"Imports tr = System.IO.TextReader", "");
        }
    }
}
