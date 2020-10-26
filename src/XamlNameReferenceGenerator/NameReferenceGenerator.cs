﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using XamlNameReferenceGenerator.Infrastructure;
using XamlNameReferenceGenerator.Parsers;

namespace XamlNameReferenceGenerator
{
    [Generator]
    public class NameReferenceGenerator : ISourceGenerator
    {
        private const string AttributeName = "XamlNameReferenceGenerator.GenerateTypedNameReferencesAttribute";
        private const string AttributeFile = "GenerateTypedNameReferencesAttribute";
        private const string AttributeCode = @"// <auto-generated />

using System;

namespace XamlNameReferenceGenerator
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class GenerateTypedNameReferencesAttribute : Attribute { }
}
";
        private static readonly PhysicalFileDebugger Debugger = new PhysicalFileDebugger();
        private static readonly SymbolDisplayFormat SymbolDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                             SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                             SymbolDisplayGenericsOptions.IncludeVariance);

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new NameReferenceSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource(AttributeFile, SourceText.From(AttributeCode, Encoding.UTF8));
            if (!(context.SyntaxReceiver is NameReferenceSyntaxReceiver receiver))
                return;

            var compilation = (CSharpCompilation) context.Compilation;
            var xamlParser = new XamlXNameReferenceXamlParser(compilation);
            var symbols = UnpackAnnotatedTypes(compilation, receiver);
            foreach (var typeSymbol in symbols)
            {
                var relevantXamlFile = context.AdditionalFiles
                    .First(text =>
                        text.Path.EndsWith($"{typeSymbol.Name}.xaml") ||
                        text.Path.EndsWith($"{typeSymbol.Name}.axaml"));

                var sourceCode = Debugger.Debug(
                    () => GenerateSourceCode(xamlParser, typeSymbol, relevantXamlFile));
                context.AddSource($"{typeSymbol.Name}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
            }
        }

        private static string GenerateSourceCode(
            INameReferenceXamlParser xamlParser,
            INamedTypeSymbol classSymbol,
            AdditionalText xamlFile)
        {
            var className = classSymbol.Name;
            var nameSpace = classSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat);
            var namedControls = xamlParser
                .GetNamedControls(xamlFile.GetText()!.ToString())
                .Select(info => "        " +
                                $"public {info.TypeName} {info.Name} => " +
                                $"this.FindControl<{info.TypeName}>(\"{info.Name}\");");
            return $@"// <auto-generated />

using Avalonia.Controls;

namespace {nameSpace}
{{
    public partial class {className}
    {{
{string.Join("\n", namedControls)}   
    }}
}}
";
        }

        private static IReadOnlyList<INamedTypeSymbol> UnpackAnnotatedTypes(
            CSharpCompilation existingCompilation,
            NameReferenceSyntaxReceiver nameReferenceSyntaxReceiver)
        {
            var options = (CSharpParseOptions)existingCompilation.SyntaxTrees[0].Options;
            var compilation = existingCompilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    SourceText.From(AttributeCode, Encoding.UTF8),
                    options));

            var symbols = new List<INamedTypeSymbol>();
            var attributeSymbol = compilation.GetTypeByMetadataName(AttributeName);
            foreach (var candidateClass in nameReferenceSyntaxReceiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(candidateClass.SyntaxTree);
                var typeSymbol = (INamedTypeSymbol) model.GetDeclaredSymbol(candidateClass);
                var relevantAttribute = typeSymbol!
                    .GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass!.Equals(attributeSymbol, SymbolEqualityComparer.Default));

                if (relevantAttribute != null)
                {
                    symbols.Add(typeSymbol);
                }
            }

            return symbols;
        }
    }
}