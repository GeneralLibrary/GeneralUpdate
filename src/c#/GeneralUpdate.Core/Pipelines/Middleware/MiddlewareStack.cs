using GeneralUpdate.Core.Pipelines.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    public sealed class MiddlewareNode
    {
        /// <summary>
        /// Go to the next middleware node.
        /// </summary>
        public Func<BaseContext, MiddlewareStack, Task> Next { get; set; }

        public MiddlewareNode(Func<BaseContext, MiddlewareStack, Task> next) => Next = next;
    }

    /// <summary>
    /// Middleware stack space.
    /// </summary>
    public sealed class MiddlewareStack
    {
        private int maxSize;
        private MiddlewareNode[] stackArray;
        private int top = -1;

        public MiddlewareStack(int maxSize)
        {
            this.maxSize = maxSize;
            stackArray = new MiddlewareNode[maxSize];
        }

        public MiddlewareStack(IList<MiddlewareNode> nodes)
        {
            maxSize = nodes.Count;
            top = maxSize - 1;
            stackArray = nodes.Reverse().ToArray();
        }

        public bool IsFull() => top == maxSize - 1;

        public bool IsEmpty() => top == -1;

        /// <summary>
        /// Add middleware.
        /// </summary>
        /// <param name="value"></param>
        public void Push(MiddlewareNode value)
        {
            if (IsFull()) return;
            top++;
            stackArray[top] = value;
        }

        public MiddlewareNode Pop()
        {
            if (IsEmpty()) return null;
            return stackArray[top--];
        }
    }
}