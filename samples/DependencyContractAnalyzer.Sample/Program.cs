using System;
using DependencyContractAnalyzer;

// Keep the runnable sample warning-free so the repository build stays clean.
// The sample README carries intentionally invalid snippets separately.
[assembly: ContractHierarchy("immutable", "thread-safe")]

Console.WriteLine("DependencyContractAnalyzer sample project");

// A direct contract declaration satisfies dependency requirements without any alias expansion.
[ProvidesContract("thread-safe")]
public interface IClock
{
    int ReadHour();
}

public sealed class ThreadSafeClock : IClock
{
    public int ReadHour() => 12;
}

// This type demonstrates target and scope metadata on a dependency.
// Because the assembly declares "immutable -> thread-safe", this type also satisfies
// thread-safe requirements even though it only declares "immutable" directly.
[ContractTarget("repository")]
[ContractScope("repository")]
[ProvidesContract("immutable")]
public sealed class UserRepository
{
    // ValidStaticUsageExample uses this property to exercise static-member dependency discovery.
    public static UserRepository Current { get; } = new();
}

// Constructor parameters are the most common dependency source.
[RequiresDependencyContract(typeof(IClock), "thread-safe")]
public sealed class ValidConstructorExample
{
    public ValidConstructorExample(IClock clock)
    {
    }
}

// Property types are also treated as dependencies when property analysis is enabled.
[RequiresContractOnScope("repository", "thread-safe")]
public sealed class ValidPropertyExample
{
    public UserRepository Repository { get; set; } = new();
}

// Non-constructor method parameters participate in dependency analysis as well.
[RequiresDependencyContract(typeof(IClock), "thread-safe")]
public sealed class ValidMethodParameterExample
{
    public void Execute(IClock clock)
    {
    }
}

// Object creation is analyzed against the created type, not just declared fields or parameters.
[RequiresDependencyContract(typeof(UserRepository), "thread-safe")]
public sealed class ValidObjectCreationExample
{
    public UserRepository Create() => new();
}

// Static member access is another optional dependency source that the analyzer can inspect.
[RequiresContractOnTarget("repository", "thread-safe")]
public sealed class ValidStaticUsageExample
{
    public UserRepository Read() => UserRepository.Current;
}
