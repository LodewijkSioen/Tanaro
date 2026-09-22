namespace Tanaro;

/// <summary>
/// Returned by the parameterless <see cref="FunctionHost.Run{TFunction}"/>; generated extension methods hang
/// off this to invoke a specific <c>[Function]</c> method on <typeparamref name="TFunction"/>.
/// </summary>
public readonly struct FunctionScenarios<TFunction>(FunctionHost host)
    where TFunction : class
{
    // Public (not internal) because generated hook methods live in the consumer's assembly.
    public FunctionHost Host { get; } = host;
}


