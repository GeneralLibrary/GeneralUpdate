using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Domain.PO
{
    public enum WillMessageStatus
    {
        /// <summary>
        /// Processing has not yet begun.
        /// </summary>
        NotStarted,
        /// <summary>
        /// Processing completed.
        /// </summary>
        Completed,
        /// <summary>
        /// Processing failure.
        /// </summary>
        Failed
    }

    public class BackupPO 
    {
        public string AppPath { get; set; }

        public string BackupPath { get; set; }

        public string Version { get; set; }

        public int AppType { get; set; }
    }

    public class WillMessagePO
    {
        public Stack<BackupPO> Message { get; private set; }
        public WillMessageStatus Status { get; private set; }
        public DateTime CreateTime { get; private set; }
        public DateTime ChangeTime { get; private set; }

        private WillMessagePO() { }

        public class Builder
        {
            private readonly WillMessagePO _messagePO = new WillMessagePO();

            public Builder SetMessage(Stack<BackupPO> message)
            {
                _messagePO.Message = message ?? throw new ArgumentNullException($"{nameof(message)} cannot be null");
                return this;
            }

            public Builder SetStatus(WillMessageStatus status)
            {
                _messagePO.Status = status;
                return this;
            }

            public Builder SetCreateTime(DateTime createTime)
            {
                _messagePO.CreateTime = createTime;
                return this;
            }

            public Builder SetChangeTime(DateTime changeTime)
            {
                _messagePO.ChangeTime = changeTime;
                return this;
            }

            public WillMessagePO Build()
            {
                return _messagePO;
            }
        }
    }

}
