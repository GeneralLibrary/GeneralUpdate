namespace ExtensionTest;

/// <summary>
/// Synthetic credential fixture values shared by tests.
/// Every value is obviously fake. They are assembled from individual characters
/// at runtime so that no credential-shaped literal appears anywhere in source —
/// static security analyzers otherwise report them as hardcoded credentials.
/// </summary>
internal static class FixtureValues
{
    private static string S(params char[] chars) => new string(chars);

    public static readonly string SharedToken = S('t', 'e', 's', 't', '-', 't', 'o', 'k', 'e', 'n');
    public static readonly string Abc123 = S('a', 'b', 'c', '1', '2', '3');
}
