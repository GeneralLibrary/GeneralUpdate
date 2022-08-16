using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
