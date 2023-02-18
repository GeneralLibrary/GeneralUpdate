using System;
using System.Text.RegularExpressions;

namespace GeneralUpdate.Core.Domain.Entity
{
    public class Entity
    {
        /// <summary>
        /// 委派标识
        /// </summary>
        protected string Identity { get; set; }

        public string ID
        {
            get { return this.Identity; }
            protected set { this.Identity = value; }
        }

        protected bool IsURL(string url)
        {
            string check = @"((http|ftp|https)://)(([a-zA-Z0-9\._-]+\.[a-zA-Z]{2,6})|([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}))(:[0-9]{1,4})*(/[a-zA-Z0-9\&%_\./-~-]*)?";
            var regex = new Regex(check);
            return regex.IsMatch(url);
        }

        protected bool IsVersion(string version)
        {
            return Version.TryParse(version, out var ver);
        }
    }
}