using System.Collections.Generic;

namespace GeneralUpdate.Common.Shared.Object;

/// <summary>
/// Response DTO for plugin validation containing list of available plugin updates.
/// </summary>
public class PluginRespDTO : BaseResponseDTO<List<PluginInfo>>;
