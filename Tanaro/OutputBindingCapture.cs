using Microsoft.Azure.Functions.Worker;

namespace Tanaro;

// Reimplements the SDK's own (internal) output-binding extraction so multi-output POCO return
// values are observable without depending on IFunctionBindingsFeature, which Tanaro cannot
// implement from outside the SDK assembly.
internal static class OutputBindingCapture
{
    private const string ReturnBindingName = "$return";

    internal static void Capture(FunctionDefinition definition, object? result, DummyFunctionContext context)
    {
        if (result is null || definition.OutputBindings.Count == 0)
        {
            return;
        }

        if (definition.OutputBindings.ContainsKey(ReturnBindingName))
        {
            // Redundant with the directly-returned value - not captured.
            return;
        }

        var resultType = result.GetType();
        foreach (var name in definition.OutputBindings.Keys)
        {
            var property = resultType.GetProperty(name)
                ?? throw new InvalidOperationException($"Could not find expected property '{name}' on type '{resultType.FullName}'.");

            var value = property.GetValue(result);
            if (value is not null)
            {
                context.SetOutputBinding(name, value);
            }
        }
    }
}
