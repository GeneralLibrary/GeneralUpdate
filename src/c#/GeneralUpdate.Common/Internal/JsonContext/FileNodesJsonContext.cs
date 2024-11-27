using System.Collections.Generic;
using System.Text.Json.Serialization;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Common.Internal.JsonContext;

[JsonSerializable(typeof(List<FileNode>))]
public partial class FileNodesJsonContext : JsonSerializerContext;