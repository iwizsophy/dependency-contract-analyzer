using System;
using DependencyContractAnalyzer;

[assembly: ContractAlias("immutable", "thread-safe")]

Console.WriteLine("DependencyContractAnalyzer sample project");

[ProvidesContract("thread-safe")]
public interface IClock
{
    int ReadHour();
}

public sealed class ThreadSafeClock : IClock
{
    public int ReadHour() => 12;
}

[ContractTarget("repository")]
[ContractScope("repository")]
[ProvidesContract("immutable")]
public sealed class UserRepository
{
    public static UserRepository Current { get; } = new();
}

[RequiresDependencyContract(typeof(IClock), "thread-safe")]
public sealed class ValidConstructorExample
{
    public ValidConstructorExample(IClock clock)
    {
    }
}

[RequiresContractOnScope("repository", "thread-safe")]
public sealed class ValidPropertyExample
{
    public UserRepository Repository { get; set; } = new();
}

[RequiresDependencyContract(typeof(IClock), "thread-safe")]
public sealed class ValidMethodParameterExample
{
    public void Execute(IClock clock)
    {
    }
}

[RequiresDependencyContract(typeof(UserRepository), "thread-safe")]
public sealed class ValidObjectCreationExample
{
    public UserRepository Create() => new();
}

[RequiresContractOnTarget("repository", "thread-safe")]
public sealed class ValidStaticUsageExample
{
    public UserRepository Read() => UserRepository.Current;
}
