// IBinaryDiffer has been moved to GeneralUpdate.Core.Differential.
// This file provides a backward-compatible type alias.
// New code should reference GeneralUpdate.Core.Differential.IBinaryDiffer directly.

using CoreBinaryDiffer = GeneralUpdate.Core.Differential.IBinaryDiffer;

namespace GeneralUpdate.Differential.Abstractions
{
    /// <summary>
    /// Binary differential algorithm abstraction.
    /// </summary>
    /// <remarks>
    /// <b>Migration note:</b> This interface is an alias for
    /// <see cref="CoreBinaryDiffer"/>. Use
    /// <c>using GeneralUpdate.Core.Differential;</c> directly in new code.
    /// </remarks>
    public interface IBinaryDiffer : CoreBinaryDiffer
    {
    }
}
