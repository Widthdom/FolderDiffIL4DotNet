namespace AssemblySemanticFixture;

public class OldBase { }

public class NewBase { }

public class AccessChanged { }

public class BaseTypeChanged : OldBase { }

public class KindChanged { }

public abstract class ModifiersChanged { }

public interface InterfaceA { }

public interface InterfaceB { }

public class InterfaceOrderStable : InterfaceA, InterfaceB { }

public class ScopedBaseTypeChanged : Shared.ScopedBase { }

public class ScopedInterfaceChanged : Shared.IScoped { }

public class ScopedMixedChange : Shared.IScoped, InterfaceA { }

public class EqualityContractPropertyAdded { }

public record StableRecord(int Value);

public class TokenTarget
{
    public static int Shared;
    public static int Spare;

    public static string FirstText() => "first";

    public static string SecondText() => "second";

    public static T Identity<T>(T value) => value;

    public static int Transform(int value) => value + 1;

    public int Combine(int value) => value + 2;
}

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

public static class NonTokenOperandConsumer
{
    public static int Constant() => 100;
}

public static class AssemblyReferenceVersionConsumer
{
    public static int Execute(int value) => VersionedDependency.VersionedTarget.Execute(value);
}

public static class SignatureTypeScopeConsumer
{
    public static int Execute(Shared.Widget value) => TargetLibrary.Target.Accept(value);
}
