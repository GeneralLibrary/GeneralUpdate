using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

namespace GeneralUpdate.Extension.PackageGeneration
{
    /// <summary>
    /// Defines the contract for generating extension packages.
    /// Supports creating extension packages from source directories with flexible structure.
    /// </summary>
    public interface IExtensionPackageGenerator
    {
        /// <summary>
        /// Generates an extension package (ZIP) from the specified source directory.
        /// </summary>
        /// <param name="sourceDirectory">The source directory containing the extension files.</param>
        /// <param name="descriptor">The extension descriptor metadata.</param>
        /// <param name="outputPath">The output path for the generated package file.</param>
        /// <returns>A task representing the asynchronous operation. Returns the path to the generated package.</returns>
        Task<string> GeneratePackageAsync(string sourceDirectory, Metadata.ExtensionDescriptor descriptor, string outputPath);

        /// <summary>
        /// Validates that the source directory contains required extension files.
        /// </summary>
        /// <param name="sourceDirectory">The source directory to validate.</param>
        /// <returns>True if valid; otherwise, false.</returns>
        bool ValidateExtensionStructure(string sourceDirectory);
    }
}
