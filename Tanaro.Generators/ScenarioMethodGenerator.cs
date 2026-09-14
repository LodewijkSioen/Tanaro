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
    private const string BindingAttributeMetadataName = "Microsoft.Azure.Functions.Worker.Extensions.Abstractions.BindingAttribute";
    private const string OutputBindingAttributeMetadataName = "Microsoft.Azure.Functions.Worker.Extensions.Abstractions.OutputBindingAttribute";

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
            var parameterInitializers = new List<string>();
            var inputBindingInitializers = new List<string>();
            var outputBindingInitializers = new List<string>();
            foreach (var parameter in method.Parameters)
            {
                var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // FunctionDefinition.Parameters reflects the raw method signature, so every parameter is included here.
                parameterInitializers.Add($"new global::Microsoft.Azure.Functions.Worker.FunctionParameter(\"{parameter.Name}\", typeof({parameterType}))");

                var bindingAttribute = parameter.GetAttributes().FirstOrDefault(a => TryGetBindingInfo(a.AttributeClass, out _, out _));
                if (bindingAttribute is not null && TryGetBindingInfo(bindingAttribute.AttributeClass, out var bindingType, out var isOutput))
                {
                    var initializer = $"new global::System.Collections.Generic.KeyValuePair<string, global::Microsoft.Azure.Functions.Worker.BindingMetadata>(\"{parameter.Name}\", new global::Tanaro.DummyBindingMetadata(\"{parameter.Name}\", \"{bindingType}\", global::Microsoft.Azure.Functions.Worker.BindingDirection.{(isOutput ? "Out" : "In")}))";
                    (isOutput ? outputBindingInitializers : inputBindingInitializers).Add(initializer);
                }

                if (parameterType == FunctionContextMetadataName)
                {
                    // The scenario's own FunctionContext is used directly - it's not something a caller can easily build.
                    argumentParts.Add("ctx");
                    continue;
                }

                signatureParts.Add($"{parameterType} {parameter.Name}");
                argumentParts.Add(parameter.Name);
            }

            // Output bindings on the return value (e.g. [EventHubOutput]) are attributes placed directly on the method.
            var returnBindingAttribute = method.GetAttributes().FirstOrDefault(a => TryGetBindingInfo(a.AttributeClass, out _, out _));
            if (returnBindingAttribute is not null && TryGetBindingInfo(returnBindingAttribute.AttributeClass, out var returnBindingType, out _))
            {
                outputBindingInitializers.Add($"new global::System.Collections.Generic.KeyValuePair<string, global::Microsoft.Azure.Functions.Worker.BindingMetadata>(\"$return\", new global::Tanaro.DummyBindingMetadata(\"$return\", \"{returnBindingType}\", global::Microsoft.Azure.Functions.Worker.BindingDirection.Out))");
            }

            builder.Add(new FunctionMethodModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                method.Name,
                SanitizeIdentifier(rawName),
                rawName,
                shape,
                returnTypeArgument,
                string.Join(", ", signatureParts),
                string.Join(", ", argumentParts),
                $"global::System.Collections.Immutable.ImmutableArray.Create({string.Join(", ", parameterInitializers)})",
                BuildBindingsInitializer(inputBindingInitializers),
                BuildBindingsInitializer(outputBindingInitializers)));
        }

        return builder.ToImmutable();
    }

    // Real binding attributes derive from TriggerBindingAttribute/InputBindingAttribute (direction In) or
    // OutputBindingAttribute (direction Out) - both ultimately derive from BindingAttribute.
    private static bool TryGetBindingInfo(INamedTypeSymbol? attributeClass, out string type, out bool isOutput)
    {
        type = string.Empty;
        isOutput = false;

        var current = attributeClass;
        while (current is not null)
        {
            var name = current.ToDisplayString();
            if (name == OutputBindingAttributeMetadataName)
            {
                isOutput = true;
                type = GetBindingTypeName(attributeClass!.Name);
                return true;
            }

            if (name == BindingAttributeMetadataName)
            {
                type = GetBindingTypeName(attributeClass!.Name);
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static string GetBindingTypeName(string attributeClassName)
    {
        var name = attributeClassName;
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - "Attribute".Length);
        }

        if (name.EndsWith("Input", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - "Input".Length);
        }
        else if (name.EndsWith("Output", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - "Output".Length);
        }

        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string BuildBindingsInitializer(List<string> keyValueInitializers) =>
        keyValueInitializers.Count == 0
            ? "global::System.Collections.Immutable.ImmutableDictionary<string, global::Microsoft.Azure.Functions.Worker.BindingMetadata>.Empty"
            : $"global::System.Collections.Immutable.ImmutableDictionary.CreateRange(new global::System.Collections.Generic.KeyValuePair<string, global::Microsoft.Azure.Functions.Worker.BindingMetadata>[] {{ {string.Join(", ", keyValueInitializers)} }})";

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
        var entryPoint = $"{method.DeclaringTypeFullyQualifiedName.Replace("global::", string.Empty)}.{method.MethodName}";
        var functionDefinition = $"new global::Tanaro.DummyFunctionDefinition(name: \"{method.RawFunctionName}\", id: \"{method.RawFunctionName}\", entryPoint: \"{entryPoint}\", pathToAssembly: typeof({method.DeclaringTypeFullyQualifiedName}).Assembly.Location, inputBindings: {method.InputBindingsInitializer}, outputBindings: {method.OutputBindingsInitializer}, parameters: {method.ParametersInitializer})";

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
                => s.Host.RunScenario<{{runScenarioTypeArguments}}>(configure, ctx => new {{scenarioTypeName}}(ctx), {{functionDefinition}});
        }
        """;
    }
}

