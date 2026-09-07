namespace Tanaro;

/// <summary>
/// Marks a test fixture as exercising <typeparamref name="T"/>, so Tanaro's source generator emits
/// typed FunctionHost scenario methods for every method of <typeparamref name="T"/> carrying a
/// Microsoft.Azure.Functions.Worker.FunctionAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FunctionUnderTestAttribute<T> : Attribute
    where T : class;
