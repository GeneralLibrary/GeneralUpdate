namespace GeneralUpdate.Extension.DTOs
{
    /// <summary>
    /// Base HTTP response data transfer object
    /// </summary>
    public class HttpResponseDTO
    {
        /// <summary>
        /// HTTP status code
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Response message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Creates a success response
        /// </summary>
        /// <param name="message">Success message</param>
        /// <returns>Success response</returns>
        public static HttpResponseDTO Success(string message = "Success")
        {
            return new HttpResponseDTO
            {
                Code = 200,
                Message = message
            };
        }

        /// <summary>
        /// Creates an internal server error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <returns>Error response</returns>
        public static HttpResponseDTO InnerException(string message)
        {
            return new HttpResponseDTO
            {
                Code = 500,
                Message = message
            };
        }
    }

    /// <summary>
    /// Generic HTTP response data transfer object with body
    /// </summary>
    /// <typeparam name="T">Body type</typeparam>
    public sealed class HttpResponseDTO<T> : HttpResponseDTO
    {
        /// <summary>
        /// Response body
        /// </summary>
#nullable disable
        public T Body { get; set; }
#nullable restore

        private HttpResponseDTO()
        { }

        /// <summary>
        /// Creates a success response with data
        /// </summary>
        /// <param name="data">Response data</param>
        /// <param name="message">Success message</param>
        /// <returns>Success response</returns>
        public static HttpResponseDTO<T> Success(T data, string message = "Success")
        {
            return new HttpResponseDTO<T>
            {
                Code = 200,
                Body = data,
                Message = message
            };
        }

        /// <summary>
        /// Creates a failure response
        /// </summary>
        /// <param name="message">Failure message</param>
        /// <param name="data">Optional response data</param>
        /// <returns>Failure response</returns>
        public static HttpResponseDTO<T> Failure(string message, T data = default)
        {
            return new HttpResponseDTO<T>
            {
                Code = 400,
                Body = data,
                Message = message
            };
        }

        /// <summary>
        /// Creates an internal server error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="data">Optional response data</param>
        /// <returns>Error response</returns>
        public static HttpResponseDTO<T> InnerException(string message, T data = default)
        {
            return new HttpResponseDTO<T>
            {
                Code = 500,
                Body = data,
                Message = message
            };
        }
    }
}
