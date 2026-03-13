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

public interface IUnreliableClock
{
    int ReadHour();
}

public sealed class UnreliableClock : IUnreliableClock
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

[ContractTarget("repository")]
public sealed class SlowRepository
{
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

// Expected diagnostic: DCA001
[RequiresDependencyContract(typeof(IUnreliableClock), "thread-safe")]
public sealed class InvalidMethodParameterExample
{
    public void Execute(IUnreliableClock clock)
    {
    }
}

// Expected diagnostic: DCA001
[RequiresContractOnTarget("repository", "thread-safe")]
public sealed class InvalidTargetExample
{
    public SlowRepository Create() => new();
}
