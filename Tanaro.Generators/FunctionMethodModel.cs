namespace Tanaro.Generators;

internal enum ReturnShape
{
    Value,
    Task,
    TaskOfValue,
    Void,
}

// Plain-string/enum model: keeps the type incrementally cacheable (avoids ImmutableArray/symbol reference-equality pitfalls).
internal sealed record FunctionMethodModel(
    string DeclaringTypeFullyQualifiedName,
    string DeclaringTypeName,
    string MethodName,
    string FunctionName,
    ReturnShape ReturnShape,
    string? ReturnTypeFullyQualifiedName,
    string ParameterList,
    string ArgumentList);
