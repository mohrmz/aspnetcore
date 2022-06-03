// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.ExternalAccess.AspNetCore.EmbeddedLanguages;
using Microsoft.AspNetCore.Analyzers.RouteEmbeddedLanguage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.EmbeddedLanguages.VirtualChars;
using Microsoft.CodeAnalysis.EmbeddedLanguages.Common;
using Microsoft.CodeAnalysis.EmbeddedLanguages.RegularExpressions;
using Microsoft.CodeAnalysis.EmbeddedLanguages.VirtualChars;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Microsoft.AspNetCore.Analyzers.RouteEmbeddedLanguage.Common;
using System.Reflection;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Microsoft.AspNetCore.Analyzers.RouteEmbeddedLanguage;

using RoutePatternToken = EmbeddedSyntaxToken<RoutePatternKind>;

public partial class RoutePatternParserTests
{
    private const string _statmentPrefix = "var v = ";
    private readonly ITestOutputHelper _outputHelper;

    public RoutePatternParserTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    private static SyntaxToken GetStringToken(string text)
    {
        var statement = _statmentPrefix + text;
        var parsedStatement = SyntaxFactory.ParseStatement(statement);
        var token = parsedStatement.DescendantTokens().ToArray()[3];
        Assert.True(token.Kind() == SyntaxKind.StringLiteralToken);

        return token;
    }

    private RoutePatternTree Test(string stringText, string expected = null,
        bool runSubTreeTests = true,
        bool allowDiagnosticsMismatch = false)
    {
        var (tree, sourceText) = TryParseTree(stringText, conversionFailureOk: false,
            allowDiagnosticsMismatch);

        // Tests are allowed to not run the subtree tests.  This is because some
        // subtrees can cause the native regex parser to exhibit very bad behavior
        // (like not ever actually finishing compiling).
        if (runSubTreeTests)
        {
            TryParseSubTrees(stringText, allowDiagnosticsMismatch);
        }

        const string DoubleQuoteEscaping = "\"\"";
        var actual = TreeToText(sourceText, tree)
            .Replace("\"", DoubleQuoteEscaping)
            .Replace("&quot;", DoubleQuoteEscaping);

        _outputHelper.WriteLine(actual);
        if (expected != null)
        {
            //Assert.Equal(expected.Replace("\"", DoubleQuoteEscaping), actual);
        }

        return tree;
    }

    private void TryParseSubTrees(string stringText, bool allowDiagnosticsMismatch)
    {
        // Trim the input from the right and make sure tree invariants hold
        var current = stringText;
        while (current is not "@\"\"" and not "\"\"")
        {
            current = current.Substring(0, current.Length - 2) + "\"";
            TryParseTree(current, conversionFailureOk: true, allowDiagnosticsMismatch);
        }

        // Trim the input from the left and make sure tree invariants hold
        current = stringText;
        while (current is not "@\"\"" and not "\"\"")
        {
            if (current[0] == '@')
            {
                current = "@\"" + current.Substring(3);
            }
            else
            {
                current = "\"" + current.Substring(2);
            }

            TryParseTree(current, conversionFailureOk: true,
                allowDiagnosticsMismatch);
        }

        for (var start = stringText[0] == '@' ? 2 : 1; start < stringText.Length - 1; start++)
        {
            TryParseTree(
                stringText.Substring(0, start) +
                stringText.Substring(start + 1, stringText.Length - (start + 1)),
                conversionFailureOk: true,
                allowDiagnosticsMismatch);
        }
    }

    private (SyntaxToken, RoutePatternTree, AspNetCoreVirtualCharSequence) JustParseTree(
        string stringText, bool conversionFailureOk)
    {
        var token = GetStringToken(stringText);
        var allChars = AspNetCoreCSharpVirtualCharService.Instance.TryConvertToVirtualChars(token);
        if (allChars.IsDefault())
        {
            Assert.True(conversionFailureOk, "Failed to convert text to token.");
            return (token, null, allChars);
        }

        var tree = RoutePatternParser.TryParse(allChars);
        return (token, tree, allChars);
    }

    private (RoutePatternTree, SourceText) TryParseTree(
        string stringText,
        bool conversionFailureOk,
        bool allowDiagnosticsMismatch = false)
    {
        var (token, tree, allChars) = JustParseTree(stringText, conversionFailureOk);
        if (tree == null)
        {
            Assert.True(allChars.IsDefault());
            return default;
        }

        CheckInvariants(tree, allChars);
        var sourceText = token.SyntaxTree.GetText();
        var treeAndText = (tree, sourceText);

        RoutePattern routePattern = null;
        IReadOnlyList<RoutePatternParameterPart> parsedRoutePatterns = null;
        try
        {
            routePattern = RoutePatternFactory.Parse(token.ValueText);
            parsedRoutePatterns = routePattern.Parameters;
        }
        catch (Exception ex)
        {
            if (!allowDiagnosticsMismatch)
            {
                if (tree.Diagnostics.Length == 0)
                {
                    throw new Exception($"Parsing '{token.ValueText}' throws RoutePattern error '{ex.Message}'. No diagnostics.");
                }

                // Ensure the diagnostic we emit is the same as the .NET one. Note: we can only
                // do this in en as that's the only culture where we control the text exactly
                // and can ensure it exactly matches RoutePattern. We depend on localization to do a 
                // good enough job here for other languages.
                if (Thread.CurrentThread.CurrentCulture.Parent.Name == "en")
                {
                    if (!tree.Diagnostics.Any(d => ex.Message.Contains(d.Message)))
                    {
                        throw new Exception(
                            $"Parsing '{token.ValueText}' throws RoutePattern error '{ex.Message}'. Error not found in diagnostics: " + Environment.NewLine +
                            string.Join(Environment.NewLine, tree.Diagnostics.Select(d => d.Message)));
                    }
                }
            }

            return treeAndText;
        }

        if (!tree.Diagnostics.IsEmpty && !allowDiagnosticsMismatch)
        {
            var expectedDiagnostics = CreateDiagnosticsElement(sourceText, tree);
            Assert.False(true, $"Parsing '{token.ValueText}' didn't throw an error for expected diagnostics: \r\n" + expectedDiagnostics.ToString().Replace(@"""", @""""""));
        }

        if (parsedRoutePatterns != null)
        {
            foreach (var parsedRoutePattern in parsedRoutePatterns)
            {
                try
                {
                    if (tree.RouteParameters.TryGetValue(parsedRoutePattern.Name, out var routeParameter))
                    {
                        Assert.True(routeParameter.IsOptional == parsedRoutePattern.IsOptional, "IsOptional");
                        Assert.True(routeParameter.IsCatchAll == parsedRoutePattern.IsCatchAll, "IsCatchAll");
                        Assert.True(routeParameter.EncodeSlashes == parsedRoutePattern.EncodeSlashes, "EncodeSlashes");
                        Assert.True(Equals(routeParameter.DefaultValue, parsedRoutePattern.Default), "DefaultValue");
                        Assert.True(routeParameter.Policies.Length == parsedRoutePattern.ParameterPolicies.Count, "ParameterPolicies");
                        for (var i = 0; i < parsedRoutePattern.ParameterPolicies.Count; i++)
                        {
                            var expected = parsedRoutePattern.ParameterPolicies[i].Content;
                            var actual = routeParameter.Policies[i].Substring(1).Replace("{{", "{").Replace("}}", "}");
                            Assert.True(expected == actual, $"Policy {i}. Expected: '{expected}', Actual: '{actual}'.");
                        }
                    }
                    else
                    {
                        throw new Exception($"Couldn't find parameter '{parsedRoutePattern.Name}'.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Parsing '{token.ValueText}' has route parameter '{parsedRoutePattern.Name}' mismatch.", ex);
                }

            }

            Assert.True(
                parsedRoutePatterns.Count == tree.RouteParameters.Count,
                $"Parsing '{token.ValueText}' has mismatched parameter counts.");
        }

        //Assert.True(regex.GetGroupNumbers().OrderBy(v => v).SequenceEqual(
        //    tree.CaptureNumbersToSpan.Keys.OrderBy(v => v)));

        //Assert.True(regex.GetGroupNames().Where(v => !int.TryParse(v, out _)).OrderBy(v => v).SequenceEqual(
        //    tree.CaptureNamesToSpan.Keys.OrderBy(v => v)));

        return treeAndText;
    }

    private static string TreeToText(SourceText text, RoutePatternTree tree)
    {
        var element = new XElement("Tree",
            NodeToElement(tree.Root));

        if (tree.Diagnostics.Length > 0)
        {
            element.Add(CreateDiagnosticsElement(text, tree));
        }

        element.Add(new XElement("Parameters",
            tree.RouteParameters.OrderBy(kvp => kvp.Key).Select(kvp => CreateParameter(kvp.Value))));

        return element.ToString();
    }

    private static XElement CreateParameter(RouteParameter parameter)
    {
        var parameterElement = new XElement("Parameter",
            new XAttribute("Name", parameter.Name),
            new XAttribute("IsCatchAll", parameter.IsCatchAll),
            new XAttribute("IsOptional", parameter.IsOptional),
            new XAttribute("EncodeSlashes", parameter.EncodeSlashes));
        if (parameter.DefaultValue != null)
        {
            parameterElement.Add(new XAttribute("DefaultValue", parameter.DefaultValue));
        }
        foreach (var policy in parameter.Policies)
        {
            parameterElement.Add(new XElement("Policy", policy));
        }

        return parameterElement;
    }

    private static XElement CreateDiagnosticsElement(SourceText text, RoutePatternTree tree)
        => new XElement("Diagnostics",
            tree.Diagnostics.Select(d =>
                new XElement("Diagnostic",
                    new XAttribute("Message", d.Message),
                    new XAttribute("Span", d.Span),
                    GetTextAttribute(text, d.Span))));

    private static XAttribute GetTextAttribute(SourceText text, TextSpan span)
        => new("Text", text.ToString(span));

    private static XElement NodeToElement(RoutePatternNode node)
    {
        var element = new XElement(node.Kind.ToString());
        foreach (var child in node)
        {
            element.Add(child.IsNode ? NodeToElement(child.Node) : TokenToElement(child.Token));
        }

        return element;
    }

    private static XElement TokenToElement(RoutePatternToken token)
    {
        var element = new XElement(token.Kind.ToString());

        if (token.Value != null)
        {
            element.Add(new XAttribute("value", token.Value));
        }

        if (token.VirtualChars.Length > 0)
        {
            element.Add(token.VirtualChars.CreateString());
        }

        return element;
    }

    private static void CheckInvariants(RoutePatternTree tree, AspNetCoreVirtualCharSequence allChars)
    {
        var root = tree.Root;
        var position = 0;
        CheckInvariants(root, ref position, allChars);
        Assert.Equal(allChars.Length, position);
    }

    private static void CheckInvariants(RoutePatternNode node, ref int position, AspNetCoreVirtualCharSequence allChars)
    {
        foreach (var child in node)
        {
            if (child.IsNode)
            {
                CheckInvariants(child.Node, ref position, allChars);
            }
            else
            {
                CheckInvariants(child.Token, ref position, allChars);
            }
        }
    }

    private static void CheckInvariants(RoutePatternToken token, ref int position, AspNetCoreVirtualCharSequence allChars)
    {
        CheckCharacters(token.VirtualChars, ref position, allChars);
    }

    private static void CheckCharacters(AspNetCoreVirtualCharSequence virtualChars, ref int position, AspNetCoreVirtualCharSequence allChars)
    {
        for (var i = 0; i < virtualChars.Length; i++)
        {
            var expected = allChars[position + i];
            var actual = virtualChars[i];
            try
            {
                Assert.Equal(expected, actual);
            }
            catch (Exception ex)
            {
                var allCharsString = allChars.GetSubSequence(new TextSpan(position, virtualChars.Length)).CreateString();
                var virtualCharsString = virtualChars.CreateString();

                throw new Exception($"When checking '{allChars.CreateString()}' there was a mismatch between '{allCharsString}' and '{virtualCharsString}' at index {i}.", ex);
            }
        }

        position += virtualChars.Length;
    }

    private static string And(params string[] regexes)
    {
        var conj = $"({regexes[regexes.Length - 1]})";
        for (var i = regexes.Length - 2; i >= 0; i--)
            conj = $"(?({regexes[i]}){conj}|[0-[0]])";

        return conj;
    }

    private static string Not(string regex)
        => $"(?({regex})[0-[0]]|.*)";
}
