﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.CodeConverter.Shared;
using ICSharpCode.CodeConverter.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using SyntaxFactory = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using CSSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;
using SyntaxKind = Microsoft.CodeAnalysis.VisualBasic.SyntaxKind;
using VBSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace ICSharpCode.CodeConverter.CSharp
{
    public class VBToCSConversion : ILanguageConversion
    {
        private Compilation _sourceCompilation;
        private readonly List<SyntaxTree> _firstPassResults = new List<SyntaxTree>();
        private readonly List<SyntaxTree> _secondPassResults = new List<SyntaxTree>();
        private readonly Lazy<CSharpCompilation> _targetCompilation;

        public VBToCSConversion()
        {
            _targetCompilation = new Lazy<CSharpCompilation>(() => CreateCompilation(_firstPassResults));
        }

        private CSharpCompilation CreateCompilation(List<SyntaxTree> csTrees)
        {
            var references = _sourceCompilation.References.Select(ConvertReference);
            return CSharpCompilation.Create("Conversion", csTrees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private MetadataReference ConvertReference(MetadataReference nonLanguageSpecificRef)
        {
            if (!(nonLanguageSpecificRef is CompilationReference cr)) return nonLanguageSpecificRef;

            using (var stream = new MemoryStream())
            {
                cr.Compilation.Emit(stream);
                return MetadataReference.CreateFromStream(stream);
            }
        }

        public SyntaxTree SingleFirstPass(Compilation sourceCompilation, SyntaxTree tree)
        {
            _sourceCompilation = sourceCompilation;
            var converted = VisualBasicConverter.ConvertCompilationTree((VisualBasicCompilation)sourceCompilation, (VisualBasicSyntaxTree)tree);
            var convertedTree = SyntaxFactory.SyntaxTree(converted);
            _firstPassResults.Add(convertedTree);
            return convertedTree;
        }

        public SyntaxNode GetSurroundedNode(IEnumerable<SyntaxNode> descendantNodes,
            bool surroundedByMethod)
        {
            return surroundedByMethod
                ? descendantNodes.OfType<VBSyntax.MethodBlockBaseSyntax>().First<SyntaxNode>()
                : descendantNodes.OfType<VBSyntax.TypeBlockSyntax>().First<SyntaxNode>();
        }

        public bool MustBeContainedByMethod(SyntaxNode node)
        {
            return node is VBSyntax.IncompleteMemberSyntax ||
                   !(node is VBSyntax.DeclarationStatementSyntax) ||
                   CouldBeFieldOrLocalVariableDeclaration(node);
        }

        private static bool CouldBeFieldOrLocalVariableDeclaration(SyntaxNode node)
        {
            return node is VBSyntax.FieldDeclarationSyntax f && f.Modifiers.All(m => m.IsKind(SyntaxKind.DimKeyword));
        }

        public bool MustBeContainedByClass(SyntaxNode node)
        {
            return node is VBSyntax.MethodBlockBaseSyntax || node is VBSyntax.MethodBaseSyntax ||
                   node is VBSyntax.FieldDeclarationSyntax || node is VBSyntax.PropertyBlockSyntax ||
                   node is VBSyntax.EventBlockSyntax;
        }

        public string WithSurroundingMethod(string text)
        {
            return $@"Sub SurroundingSub()
{text}
End Sub";
        }

        public string WithSurroundingClass(string text)
        {
            return $@"Class SurroundingClass
{text}
End Class";
        }

        public List<SyntaxNode> FindSingleImportantChild(SyntaxNode annotatedNode)
        {
            var children = annotatedNode.ChildNodes().ToList();
            if (children.Count > 1) {
                switch (annotatedNode) {
                    case CSSyntax.MethodDeclarationSyntax _:
                        return annotatedNode.ChildNodes().OfType<CSSyntax.BlockSyntax>().ToList<SyntaxNode>();
                    case CSSyntax.BaseTypeSyntax _:
                        return annotatedNode.ChildNodes().OfType<CSSyntax.BlockSyntax>().ToList<SyntaxNode>();
                }
            }
            return children;
        }

        public SyntaxNode SingleSecondPass(KeyValuePair<string, SyntaxTree> cs)
        {
            var cSharpSyntaxNode = new CompilationErrorFixer(_targetCompilation.Value, (CSharpSyntaxTree)cs.Value).Fix();
            _secondPassResults.Add(cSharpSyntaxNode.SyntaxTree);
            return cSharpSyntaxNode;
        }

        public string GetWarningsOrNull()
        {
            var finalCompilation = CreateCompilation(_secondPassResults);
            var targetErrors = GetDiagnostics(finalCompilation);
            return targetErrors.Any() ? $"{targetErrors.Count} resulting compilation errors:{Environment.NewLine}{string.Join(Environment.NewLine, targetErrors)}" : null;
        }

        private static List<string> GetDiagnostics(Compilation compilation)
        {
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"{d.Id}: {d.GetMessage()}")
                .ToList();
            return diagnostics;
        }

        public SyntaxTree CreateTree(string text)
        {
            return Microsoft.CodeAnalysis.VisualBasic.SyntaxFactory.ParseSyntaxTree(SourceText.From(text));
        }

        public Compilation CreateCompilationFromTree(SyntaxTree tree, IEnumerable<MetadataReference> references)
        {
            var compilationOptions = new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithRootNamespace("TestProject")
                .WithGlobalImports(GlobalImport.Parse(
                    "System",
                    "System.Collections.Generic",
                    "System.Diagnostics",
                    "System.Globalization",
                    "System.IO",
                    "System.Linq",
                    "System.Reflection",
                    "System.Runtime.CompilerServices",
                    "System.Security",
                    "System.Text",
                    "System.Threading.Tasks",
                    "Microsoft.VisualBasic"));
            var compilation = VisualBasicCompilation.Create("Conversion", new[] {tree}, references)
                .WithOptions(compilationOptions);
            return compilation;
        }
    }
}