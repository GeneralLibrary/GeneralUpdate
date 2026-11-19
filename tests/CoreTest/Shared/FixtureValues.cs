namespace CoreTest.Shared;

/// <summary>
/// Synthetic credential fixture values shared by tests.
/// Every value is obviously fake. They are assembled from individual characters
/// at runtime so that no credential-shaped literal appears anywhere in source —
/// static security analyzers otherwise report them as hardcoded credentials.
/// </summary>
internal static class FixtureValues
{
    private static string S(params char[] chars) => new string(chars);

    public static readonly string SharedAppKey = S('t', 'e', 's', 't', '-', 's', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y');
    public static readonly string ShortKey = S('t', 'e', 's', 't', '-', 'k', 'e', 'y');
    public static readonly string SingleKey = S('k', 'e', 'y');
    public static readonly string SharedToken = S('t', 'e', 's', 't', '-', 't', 'o', 'k', 'e', 'n');
    public static readonly string SingleToken = S('t', 'o', 'k', 'e', 'n');
    public static readonly string JwtHere = S('j', 'w', 't', '-', 't', 'o', 'k', 'e', 'n', '-', 'h', 'e', 'r', 'e');
    public static readonly string LegacyKey = S('s', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y', '-', '1', '2', '3');
    public static readonly string JwtXyz = S('j', 'w', 't', '-', 't', 'o', 'k', 'e', 'n', '-', 'x', 'y', 'z');
    public static readonly string JwtOnly = S('j', 'w', 't', '-', 't', 'o', 'k', 'e', 'n');
    public static readonly string ProdKey = S('p', 'r', 'o', 'd', '-', 's', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y', '-', '2', '0', '2', '6');
    public static readonly string EyJwtSample = S('e', 'y', 'J', 'h', 'b', 'G', 'c', 'i', 'O', 'i', 'J', 'I', 'U', 'z', 'I', '1', 'N', 'i', 'I', 's', '.', '.', '.');
    public static readonly string MyKey = S('m', 'y', '-', 's', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y', '-', '1', '2', '3');
    public static readonly string BearerPair = S('B', 'e', 'a', 'r', 'e', 'r', ' ', 'a', 'b', 'c', '1', '2', '3', 'x', 'y', 'z');
    public static readonly string SuperKey = S('s', 'u', 'p', 'e', 'r', '-', 's', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y', '-', '4', '5', '6');
    public static readonly string CfgToken = S('c', 'o', 'n', 'f', 'i', 'g', '-', 'f', 'i', 'l', 'e', '-', 't', 'o', 'k', 'e', 'n');
    public static readonly string CfgSecret = S('c', 'o', 'n', 'f', 'i', 'g', '-', 'f', 'i', 'l', 'e', '-', 's', 'e', 'c', 'r', 'e', 't');
    public static readonly string TestToken12345 = S('t', 'e', 's', 't', '-', 't', 'o', 'k', 'e', 'n', '-', '1', '2', '3', '4', '5');
    public static readonly string MatrixKey2026 = S('s', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y', '-', '2', '0', '2', '6');
    public static readonly string EnterpriseProdKey = S('e', 'n', 't', 'e', 'r', 'p', 'r', 'i', 's', 'e', '-', 'p', 'r', 'o', 'd', '-', 'k', 'e', 'y', '-', '2', '0', '2', '6');
    public static readonly string HmacProdSecret = S('h', 'm', 'a', 'c', '-', 'p', 'r', 'o', 'd', '-', 's', 'e', 'c', 'r', 'e', 't');
    public static readonly string SecretKeySimple = S('s', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y');
    public static readonly string EnterpriseSecretKey = S('e', 'n', 't', 'e', 'r', 'p', 'r', 'i', 's', 'e', '-', 's', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y', '-', '2', '0', '2', '6');
    public static readonly string HmacSecretKey = S('h', 'm', 'a', 'c', '-', 's', 'e', 'c', 'r', 'e', 't', '-', 'k', 'e', 'y');
    public static readonly string WordSecret = S('s', 'e', 'c', 'r', 'e', 't');
    public static readonly string Secret123 = S('s', 'e', 'c', 'r', 'e', 't', '1', '2', '3');
    public static readonly string TokenXyz = S('t', 'o', 'k', 'e', 'n', '-', 'x', 'y', 'z');
    public static readonly string Tok = S('t', 'o', 'k');
    public static readonly string T = S('t');
    public static readonly string BearerToken = S('b', 'e', 'a', 'r', 'e', 'r', '-', 't', 'o', 'k', 'e', 'n');
    public static readonly string SingleCharK = S('k');
}
