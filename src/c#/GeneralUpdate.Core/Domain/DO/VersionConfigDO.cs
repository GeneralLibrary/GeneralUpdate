namespace GeneralUpdate.Core.Domain.DO
{
    public class VersionConfigDO
    {
        /// <summary>
        /// Product branch ID (Used to distinguish multiple branches under the same product).
        /// </summary>
        public string Guid { get; set; }

        /// <summary>
        /// Update package download location.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// MD5 verification code
        /// </summary>
        public string MD5 { get; set; }

        /// <summary>
        /// Update the package name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Update the package file format.
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// The version number that will be updated.
        /// </summary>
        public string Version { get; set; }
    }
}
