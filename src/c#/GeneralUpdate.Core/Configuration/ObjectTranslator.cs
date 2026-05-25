namespace GeneralUpdate.Core.Configuration;

public sealed class ObjectTranslator
{
    public static string GetPacketHash(object version)
    {
        if (!GeneralTracer.IsTracingEnabled()) return string.Empty;
        if (version is VersionInfo vi) return $"[PacketHash]:{vi.Hash} ";
        return string.Empty;
    }
}