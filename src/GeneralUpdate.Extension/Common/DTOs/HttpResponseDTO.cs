using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Common.DTOs;

/// <summary>
/// HTTP response wrapper
/// </summary>
/// <typeparam name="T">Type of data in response</typeparam>
public class HttpResponseDTO<T>
{
    /// <summary>
    /// Response message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Response data
    /// </summary>
    public T? Body { get; set; }

    /// <summary>
    /// Error code if applicable
    /// </summary>
    public string? Code { get; set; }
}
