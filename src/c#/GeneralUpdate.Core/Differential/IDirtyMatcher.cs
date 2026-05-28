using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// Defines the matching logic interface for the Dirty (patch application) stage,
/// used to find the differential patch file corresponding to an existing application file.
/// </summary>
/// <remarks>
/// <para>
/// The Dirty stage (patch application stage) is the second step of differential updates.
/// It corresponds to the Clean stage (differential generation stage) defined in
/// <see cref="ICleanMatcher"/>:
/// </para>
/// <list type="bullet">
///   <item><description><b>Clean stage</b>: Executed on the server/build machine. Analyzes differences
///   between the old and new versions and generates the patch package.</description></item>
///   <item><description><b>Dirty stage</b>: Executed on the client/at runtime. Receives the patch package
///   and applies it to existing files.</description></item>
/// </list>
/// <para>
/// DirtyMatcher is responsible for establishing a correspondence between <c>.patch</c> files
/// in the patch directory and the existing application files on the client.
/// After a successful match, the differential engine (e.g., <c>PatchService</c>) applies
/// the patch to the old file, producing the updated file.
/// </para>
/// <para>
/// Refer to <see cref="DefaultDirtyMatcher"/> for the default implementation, which uses
/// file name matching rules to locate patch files.
/// </para>
/// </remarks>
public interface IDirtyMatcher
{
    /// <summary>
    /// Searches the available patch file collection for a patch corresponding to the
    /// specified application file.
    /// </summary>
    /// <param name="oldFile">The existing application file that needs to be patched.</param>
    /// <param name="patchFiles">The collection of all available files in the patch directory.</param>
    /// <returns>
    /// The matched patch <see cref="FileInfo"/>; or <c>null</c> if no corresponding patch file
    /// is found (indicating the file does not need an update or should be replaced directly).
    /// </returns>
    /// <remarks>
    /// Implementations should establish the match based on file name, relative path,
    /// or other identifying information.
    /// When <c>null</c> is returned, the caller should directly copy the new version file
    /// to replace the old version file.
    /// </remarks>
    FileInfo? Match(FileInfo oldFile, IEnumerable<FileInfo> patchFiles);
}
