namespace ExtensionTest.TestSupport;

/// <summary>
///     Placeholder values for credential-shaped configuration fields (Token etc.). Values
///     are assembled at runtime from non-credential fragments so the test source carries
///     no credential-shaped material; these strings never reach a real endpoint.
/// </summary>
internal static class FixtureValues
{
    /// <summary>Deterministic placeholder for a bearer token field.</summary>
    public static string Token(string scope) => "fixture:" + scope + ":t1";
}
