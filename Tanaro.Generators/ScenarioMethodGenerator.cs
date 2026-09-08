using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tanaro.Generators;

[Generator(LanguageNames.CSharp)]
public class ScenarioMethodGenerator : IIncrementalGenerator
{
    private const string FunctionUnderTestAttributeMetadataName = "Tanaro.FunctionUnderTestAttribute`1";
    private const string FunctionAttributeMetadataName = "Microsoft.Azure.Functions.Worker.FunctionAttribute";
    private const string FunctionContextMetadataName = "global::Microsoft.Azure.Functions.Worker.FunctionContext";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FunctionUnderTestAttributeMetadataName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetTargetTypes(ctx))
            .SelectMany(static (types, _) => types);

        var functionMethods = targetTypes.SelectMany(static (type, _) => GetFunctionMethods(type));

        context.RegisterSourceOutput(functionMethods, static (spc, method) =>
            spc.AddSource($"{method.DeclaringTypeName}.{method.FunctionName}.g.cs", Generate(method)));
    }

    private static ImmutableArray<INamedTypeSymbol> GetTargetTypes(GeneratorAttributeSyntaxContext context)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var attribute in context.Attributes)
        {
            var typeArguments = attribute.AttributeClass?.TypeArguments ?? default;
            if (typeArguments.Length == 1 && typeArguments[0] is INamedTypeSymbol target)
            {
                builder.Add(target);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<FunctionMethodModel> GetFunctionMethods(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<FunctionMethodModel>();
        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
            {
                continue;
            }

            var functionAttribute = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == FunctionAttributeMetadataName);
            if (functionAttribute is null ||
                functionAttribute.ConstructorArguments.Length != 1 ||
                functionAttribute.ConstructorArguments[0].Value is not string rawName)
            {
                continue;
            }

            var (shape, returnTypeArgument) = GetReturnShape(method.ReturnType);

            var signatureParts = new List<string>();
            var argumentParts = new List<string>();
            foreach (var parameter in method.Parameters)
            {
                var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (parameterType == FunctionContextMetadataName)
                {
                    // The scenario's own FunctionContext is used directly - it's not something a caller can easily build.
                    argumentParts.Add("ctx");
                    continue;
                }

                signatureParts.Add($"{parameterType} {parameter.Name}");
                argumentParts.Add(parameter.Name);
            }

            builder.Add(new FunctionMethodModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                method.Name,
                SanitizeIdentifier(rawName),
                shape,
                returnTypeArgument,
                string.Join(", ", signatureParts),
                string.Join(", ", argumentParts)));
        }

        return builder.ToImmutable();
    }

    private static (ReturnShape Shape, string? ReturnTypeArgument) GetReturnShape(ITypeSymbol returnType)
    {
        if (returnType.SpecialType == SpecialType.System_Void)
        {
            return (ReturnShape.Void, null);
        }

        if (returnType is INamedTypeSymbol { Name: "Task" } named &&
            named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
        {
            return named.IsGenericType
                ? (ReturnShape.TaskOfValue, named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                : (ReturnShape.Task, null);
        }

        return (ReturnShape.Value, returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    private static string SanitizeIdentifier(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        if (builder.Length == 0 || char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static string Generate(FunctionMethodModel method)
    {
        var (scenarioBase, resultTypeArguments, invocation) = method.ReturnShape switch
        {
            ReturnShape.Value => (
                $"global::Tanaro.Scenario<{method.DeclaringTypeFullyQualifiedName}, {method.ReturnTypeFullyQualifiedName}>",
                $"{method.DeclaringTypeFullyQualifiedName}, {method.ReturnTypeFullyQualifiedName}",
                $"global::System.Threading.Tasks.Task.FromResult(f.{method.MethodName}({method.ArgumentList}))"),
            ReturnShape.TaskOfValue => (
                $"global::Tanaro.Scenario<{method.DeclaringTypeFullyQualifiedName}, {method.ReturnTypeFullyQualifiedName}>",
                $"{method.DeclaringTypeFullyQualifiedName}, {method.ReturnTypeFullyQualifiedName}",
                $"f.{method.MethodName}({method.ArgumentList})"),
            ReturnShape.Task => (
                $"global::Tanaro.Scenario<{method.DeclaringTypeFullyQualifiedName}>",
                method.DeclaringTypeFullyQualifiedName,
                $"f.{method.MethodName}({method.ArgumentList})"),
            ReturnShape.Void => (
                $"global::Tanaro.Scenario<{method.DeclaringTypeFullyQualifiedName}>",
                method.DeclaringTypeFullyQualifiedName,
                $"{{ f.{method.MethodName}({method.ArgumentList}); return global::System.Threading.Tasks.Task.CompletedTask; }}"),
            _ => throw new NotSupportedException($"Unsupported return shape: {method.ReturnShape}"),
        };

        var scenarioTypeName = $"{method.FunctionName}Scenario";
        var hostReturnType = method.ReturnShape is ReturnShape.Value or ReturnShape.TaskOfValue
            ? $"global::System.Threading.Tasks.Task<{method.ReturnTypeFullyQualifiedName}>"
            : "global::System.Threading.Tasks.Task";
        var runScenarioTypeArguments = $"{resultTypeArguments}, {scenarioTypeName}";

        return $$"""
        // <auto-generated/>
        #nullable enable

        namespace Tanaro.Generated;

        public sealed class {{scenarioTypeName}}(global::Microsoft.Azure.Functions.Worker.FunctionContext functionContext) : {{scenarioBase}}(functionContext)
        {
            public {{scenarioTypeName}} Execute({{method.ParameterList}})
            {
                Record((f, ctx) => {{invocation}});
                return this;
            }
        }

        public static class {{method.FunctionName}}Extensions
        {
            public static {{hostReturnType}} {{method.FunctionName}}(this global::Tanaro.FunctionScenarios<{{method.DeclaringTypeFullyQualifiedName}}> s, global::System.Action<{{scenarioTypeName}}> configure)
                => s.Host.RunScenario<{{runScenarioTypeArguments}}>(configure, ctx => new {{scenarioTypeName}}(ctx));
        }
        """;
    }
}

