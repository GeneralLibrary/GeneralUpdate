namespace GeneralUpdate.Core.Configuration;

public sealed class ObjectTranslator
{
    public static string GetPacketHash(object version) => 
        !GeneralTracer.IsTracingEnabled() ? string.Empty : $"[PacketHash]:{(version as VersionInfo).Hash} ";
}