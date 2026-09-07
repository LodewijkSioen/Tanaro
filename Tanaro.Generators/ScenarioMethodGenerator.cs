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
            string? contextSignaturePart = null;
            foreach (var parameter in method.Parameters)
            {
                var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (parameterType == FunctionContextMetadataName)
                {
                    contextSignaturePart = $"{FunctionContextMetadataName}? context = null";
                    argumentParts.Add("context ?? ctx");
                    continue;
                }

                signatureParts.Add($"{parameterType} {parameter.Name}");
                argumentParts.Add(parameter.Name);
            }

            if (contextSignaturePart is not null)
            {
                signatureParts.Add(contextSignaturePart);
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
        // Block-bodied lambdas are used deliberately: a "return expr;" body only binds to the Func<...> Scenario
        // overloads, and a bare statement body only binds to the Action<...> overload - avoiding the ambiguity a
        // single-expression lambda would have between the Action<T> and Func<T, Task> Scenario overloads.
        var (returnType, scenarioTypeArguments, lambdaBody) = method.ReturnShape switch
        {
            ReturnShape.Value => (
                $"global::System.Threading.Tasks.Task<{method.ReturnTypeFullyQualifiedName}>",
                $"{method.DeclaringTypeFullyQualifiedName}, {method.ReturnTypeFullyQualifiedName}",
                $"return f.{method.MethodName}({method.ArgumentList});"),
            ReturnShape.TaskOfValue => (
                $"global::System.Threading.Tasks.Task<{method.ReturnTypeFullyQualifiedName}>",
                $"{method.DeclaringTypeFullyQualifiedName}, {method.ReturnTypeFullyQualifiedName}",
                $"return f.{method.MethodName}({method.ArgumentList});"),
            ReturnShape.Task => (
                "global::System.Threading.Tasks.Task",
                method.DeclaringTypeFullyQualifiedName,
                $"return f.{method.MethodName}({method.ArgumentList});"),
            ReturnShape.Void => (
                "global::System.Threading.Tasks.Task",
                method.DeclaringTypeFullyQualifiedName,
                $"f.{method.MethodName}({method.ArgumentList});"),
            _ => throw new global::System.NotSupportedException($"Unsupported return shape: {method.ReturnShape}"),
        };

        return $$"""
        // <auto-generated/>
        #nullable enable

        namespace Tanaro.Generated;

        public static class {{method.FunctionName}}Extensions
        {
            public static {{returnType}} {{method.FunctionName}}(this global::Tanaro.FunctionHost host, {{method.ParameterList}})
                => host.Scenario<{{scenarioTypeArguments}}>((f, ctx) =>
                {
                    {{lambdaBody}}
                });
        }
        """;
    }
}

