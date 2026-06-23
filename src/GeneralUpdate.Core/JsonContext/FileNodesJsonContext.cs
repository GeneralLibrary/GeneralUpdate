using System.Collections.Generic;
using System.Text.Json.Serialization;
using GeneralUpdate.Core.FileSystem;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(List<FileNode>))]
public partial class FileNodesJsonContext : JsonSerializerContext;