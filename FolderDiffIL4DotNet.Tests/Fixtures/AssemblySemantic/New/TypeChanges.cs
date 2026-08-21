namespace AssemblySemanticFixture;

public class OldBase { }

public class NewBase { }

internal class AccessChanged { }

public class BaseTypeChanged : NewBase { }

public struct KindChanged { }

public sealed class ModifiersChanged { }

public interface InterfaceA { }

public interface InterfaceB { }

public class InterfaceOrderStable : InterfaceB, InterfaceA { }

public class ScopedBaseTypeChanged : Shared.ScopedBase { }

public class ScopedInterfaceChanged : Shared.IScoped { }

public class ScopedMixedChange : Shared.IScoped, InterfaceB { }

public class EqualityContractPropertyAdded
{
    public int EqualityContract => 0;
}

public record StableRecord(int Value);

public sealed class TokenOperandConsumer
{
    public int Execute(object candidate, int value)
    {
        var target = (TokenTarget)candidate;
        TokenTarget.Shared = value;
        return TokenTarget.Transform(TokenTarget.Shared)
            + target.Combine(value)
            + TokenTarget.Identity(value)
            + typeof(TokenTarget).Name.Length;
    }

    public string Describe() => "stable";
}

public class TokenTarget
{
    public static int Spare;
    public static int Shared;

    public int Combine(int value) => value + 2;

    public static int Transform(int value) => value + 1;

    public static T Identity<T>(T value) => value;

    public static string SecondText() => "second";

    public static string FirstText() => "first";
}

public static class NonTokenOperandConsumer
{
    public static int Constant() => 101;
}

public static class AssemblyReferenceVersionConsumer
{
    public static int Execute(int value) => VersionedDependency.VersionedTarget.Execute(value);
}

public static class SignatureTypeScopeConsumer
{
    public static int Execute(Shared.Widget value) => TargetLibrary.Target.Accept(value);
}
