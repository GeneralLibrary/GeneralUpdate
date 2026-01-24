# AOT Compatibility Guide

## Native AOT Support

GeneralUpdate.Extension is fully compatible with .NET Native AOT compilation. The library has been designed with AOT constraints in mind:

### ✅ AOT-Safe Patterns Used

1. **No Reflection**
   - No `Type.GetType()`, `Assembly.Load()`, or `Activator.CreateInstance()`
   - All types are statically referenced
   - No dynamic method invocation

2. **Statically Resolvable Types**
   - All generics are closed at compile time
   - No runtime generic type construction
   - All interfaces have concrete implementations

3. **JSON Serialization**
   - Uses `System.Text.Json` with concrete types
   - All serialized types are known at compile time
   - Compatible with source generators

4. **No Dynamic Code**
   - No `Emit` or dynamic code generation
   - No expression trees or dynamic LINQ
   - All code paths are statically analyzable

### Enabling AOT in Your Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

### Verified AOT Scenarios

The following scenarios have been verified to work with Native AOT:

- ✅ Extension catalog loading and management
- ✅ Version compatibility checking
- ✅ Update queue operations
- ✅ Extension download and installation
- ✅ Package generation
- ✅ Event handling and callbacks
- ✅ Dependency injection registration

### Dependencies AOT Status

| Package | AOT Compatible | Notes |
|---------|---------------|-------|
| System.Text.Json | ✅ Yes | Use with source generators for best performance |
| Microsoft.Extensions.DependencyInjection.Abstractions | ✅ Yes | Only abstractions, no runtime dependencies |
| GeneralUpdate.Common | ⚠️ Check | Depends on implementation |
| GeneralUpdate.Differential | ⚠️ Check | Depends on implementation |

### Using Source Generators with JSON

For optimal AOT performance, use JSON source generators:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(ExtensionDescriptor))]
[JsonSerializable(typeof(InstalledExtension))]
[JsonSerializable(typeof(AvailableExtension))]
[JsonSerializable(typeof(List<AvailableExtension>))]
internal partial class ExtensionJsonContext : JsonSerializerContext
{
}

// Usage
var options = new JsonSerializerOptions
{
    TypeInfoResolver = ExtensionJsonContext.Default
};

var json = JsonSerializer.Serialize(descriptor, options);
var obj = JsonSerializer.Deserialize<ExtensionDescriptor>(json, options);
```

### Troubleshooting AOT Issues

If you encounter AOT warnings or errors:

1. **Check for reflection usage**
   ```bash
   grep -rE "typeof|GetType|Activator" YourCode.cs
   ```

2. **Verify all types are concrete**
   - Avoid open generics
   - Use closed generic types
   - Ensure all interface implementations are registered

3. **Review JSON serialization**
   - Use concrete types, not dynamic
   - Consider source generators
   - Avoid polymorphic serialization

### Performance Benefits

With Native AOT:
- ✅ Faster startup time (no JIT compilation)
- ✅ Lower memory usage (no JIT overhead)
- ✅ Smaller deployment size
- ✅ Better predictability (no JIT variations)

### Limitations

When using Native AOT, be aware of:
- Cannot use reflection-based scenarios
- Dynamic assembly loading not supported
- Some third-party libraries may not be compatible
- Plugin systems requiring runtime type discovery need alternative approaches

## Testing AOT Compatibility

To test your application with AOT:

```bash
# Publish with AOT
dotnet publish -c Release -r win-x64 /p:PublishAot=true

# Check for AOT warnings
dotnet publish -c Release -r win-x64 /p:PublishAot=true > aot-warnings.txt
grep -i "warning.*AOT" aot-warnings.txt
```

## Support

For AOT-related issues:
1. Check this guide first
2. Review .NET AOT documentation: https://learn.microsoft.com/dotnet/core/deploying/native-aot/
3. Open an issue with details on the AOT warning/error
