namespace TargetLibrary;

public static class Target
{
    public static int Accept(Shared.Widget value) => value is null ? 0 : 1;
}
