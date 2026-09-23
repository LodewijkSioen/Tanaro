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
    private const string CancellationTokenMetadataName = "global::System.Threading.CancellationToken";
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

                if (parameterType == CancellationTokenMetadataName)
                {
                    // Mirrors the real SDK's CancellationTokenConverter, which fills this parameter from FunctionContext.CancellationToken.
                    argumentParts.Add("ctx.CancellationToken");
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
            else if (GetUnwrappedReturnType(method.ReturnType) is INamedTypeSymbol returnTypeSymbol)
            {
                // Multi-output POCO: each output-bound property (e.g. [EventHubOutput]) becomes its own named binding.
                foreach (var property in returnTypeSymbol.GetMembers().OfType<IPropertySymbol>())
                {
                    var propertyBindingAttribute = property.GetAttributes().FirstOrDefault(a => TryGetBindingInfo(a.AttributeClass, out _, out _));
                    if (propertyBindingAttribute is not null &&
                        TryGetBindingInfo(propertyBindingAttribute.AttributeClass, out var propertyBindingType, out var propertyIsOutput) &&
                        propertyIsOutput)
                    {
                        outputBindingInitializers.Add($"new global::System.Collections.Generic.KeyValuePair<string, global::Microsoft.Azure.Functions.Worker.BindingMetadata>(\"{property.Name}\", new global::Tanaro.DummyBindingMetadata(\"{property.Name}\", \"{propertyBindingType}\", global::Microsoft.Azure.Functions.Worker.BindingDirection.Out))");
                    }
                }
            }

            builder.Add(new FunctionMethodModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString(),
                method.Name,
                SanitizeIdentifier(rawName),
                rawName,
                shape,
                returnTypeArgument,
                string.Join(", ", signatureParts),
                string.Join(", ", argumentParts),
                BuildParametersInitializer(parameterInitializers),
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

    private static string BuildParametersInitializer(List<string> parameterInitializers) =>
        parameterInitializers.Count == 0
            ? "global::System.Collections.Immutable.ImmutableArray<global::Microsoft.Azure.Functions.Worker.FunctionParameter>.Empty"
            : $"global::System.Collections.Immutable.ImmutableArray.Create({string.Join(", ", parameterInitializers)})";

    private static string BuildBindingsInitializer(List<string> keyValueInitializers) =>
        keyValueInitializers.Count == 0
            ? "global::System.Collections.Immutable.ImmutableDictionary<string, global::Microsoft.Azure.Functions.Worker.BindingMetadata>.Empty"
            : $"global::System.Collections.Immutable.ImmutableDictionary.CreateRange(new global::System.Collections.Generic.KeyValuePair<string, global::Microsoft.Azure.Functions.Worker.BindingMetadata>[] {{ {string.Join(", ", keyValueInitializers)} }})";

    // Unwraps Task<T>/Task/void the same way GetReturnShape does, so return-type properties can be scanned for output bindings.
    private static ITypeSymbol? GetUnwrappedReturnType(ITypeSymbol returnType)
    {
        if (returnType.SpecialType == SpecialType.System_Void)
        {
            return null;
        }

        if (returnType is INamedTypeSymbol { Name: "Task" } named &&
            named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
        {
            return named.IsGenericType ? named.TypeArguments[0] : null;
        }

        return returnType;
    }

    // Includes the '?' modifier so a nullable-returning function's TResult carries that nullability all the way
    // through Scenario<TFunction, TResult>/ScenarioResult<TResult>, instead of collapsing to the same TResult as a
    // non-nullable-returning function.
    private static readonly SymbolDisplayFormat ReturnTypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

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
                ? (ReturnShape.TaskOfValue, named.TypeArguments[0].ToDisplayString(ReturnTypeFormat))
                : (ReturnShape.Task, null);
        }

        return (ReturnShape.Value, returnType.ToDisplayString(ReturnTypeFormat));
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

        // Prefixed with the declaring type so two different function classes exposing the same
        // [Function("Name")] (legal across separate classes) don't emit colliding type names.
        var declaringTypeIdentifier = SanitizeIdentifier(method.DeclaringTypeFullyQualifiedName.Replace("global::", string.Empty));
        var scenarioTypeName = $"{declaringTypeIdentifier}_{method.FunctionName}Scenario";
        var extensionsTypeName = $"{declaringTypeIdentifier}_{method.FunctionName}Extensions";
        var hostReturnType = method.ReturnShape is ReturnShape.Value or ReturnShape.TaskOfValue
            ? $"global::System.Threading.Tasks.Task<global::Tanaro.ScenarioResult<{method.ReturnTypeFullyQualifiedName}>>"
            : "global::System.Threading.Tasks.Task<global::Tanaro.ScenarioResult>";
        var runScenarioTypeArguments = $"{resultTypeArguments}, {scenarioTypeName}";
        var entryPoint = $"{method.DeclaringTypeFullyQualifiedName.Replace("global::", string.Empty)}.{method.MethodName}";
        var functionDefinition = $"new global::Tanaro.DummyFunctionDefinition(name: \"{method.RawFunctionName}\", id: \"{method.RawFunctionName}\", entryPoint: \"{entryPoint}\", pathToAssembly: typeof({method.DeclaringTypeFullyQualifiedName}).Assembly.Location, inputBindings: {method.InputBindingsInitializer}, outputBindings: {method.OutputBindingsInitializer}, parameters: {method.ParametersInitializer})";
        var namespaceDeclaration = method.DeclaringTypeNamespace.Length == 0
            ? string.Empty
            : $"namespace {method.DeclaringTypeNamespace};\n\n";

        return $$"""
        // <auto-generated/>
        #nullable enable

        {{namespaceDeclaration}}public sealed class {{scenarioTypeName}}(global::Tanaro.DummyFunctionContext functionContext) : {{scenarioBase}}(functionContext)
        {
            public {{scenarioTypeName}} Execute({{method.ParameterList}})
            {
                Record((f, ctx) => {{invocation}});
                return this;
            }
        }

        public static class {{extensionsTypeName}}
        {
            public static {{hostReturnType}} {{method.FunctionName}}(this global::Tanaro.FunctionScenarios<{{method.DeclaringTypeFullyQualifiedName}}> s, global::System.Action<{{scenarioTypeName}}> configure)
                => s.Host.Runner.RunScenario<{{runScenarioTypeArguments}}>(configure, ctx => new {{scenarioTypeName}}(ctx), {{functionDefinition}});
        }
        """;
    }
}

