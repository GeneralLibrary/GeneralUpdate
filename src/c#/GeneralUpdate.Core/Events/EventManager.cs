using System.ComponentModel;

namespace GeneralUpdate.Core.Events
{
    /// <summary>
    /// Manage all events in the component.
    /// </summary>
    public class EventManager : IEventManager, IDisposable
    {
        //TODO:https://gitee.com/Juster-zhu/GeneralUpdate/commit/59f8a4ba072c73d924a6bce744629d40934ac44d

        // Use interop to call the method necessary
        // to clean up the unmanaged resource.
        [System.Runtime.InteropServices.DllImport("Kernel32")]
        private static extern Boolean CloseHandle(IntPtr handle);

        private static readonly object _lockObj = new object();
        private static EventManager _instance;
        private Dictionary<Type, Delegate> _dicDelegates = new Dictionary<Type, Delegate>();

        // Track whether Dispose has been called.
        private bool disposed = false;

        // Pointer to an external unmanaged resource.
        private IntPtr handle;

        // Other managed resource this class uses.
        private Component component = null;

        private EventManager() => component = new Component();

        // Use C# finalizer syntax for finalization code.
        // This finalizer will run only if the Dispose method
        // does not get called.
        // It gives your base class the opportunity to finalize.
        // Do not provide finalizer in types derived from this class.
        ~EventManager()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(disposing: false) is optimal in terms of
            // readability and maintainability.
            Dispose(disposing: false);
        }

        public static EventManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        if (_instance == null)
                            _instance = new EventManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Add listener
        /// </summary>
        /// <typeparam name="TDelegate">Specify the delegate type.</typeparam>
        /// <param name="newDelegate">Delegate to be added.</param>
        /// <exception cref="ArgumentNullException">parameter null exception.</exception>
        public void AddListener<TDelegate>(TDelegate newDelegate) where TDelegate : Delegate
        {
            if (newDelegate == null) throw new ArgumentNullException(nameof(newDelegate));
            if (_dicDelegates.ContainsKey(typeof(TDelegate))) return;
            handle = new IntPtr(1);
            _dicDelegates.Add(typeof(TDelegate), newDelegate);
        }

        /// <summary>
        /// Remove listener
        /// </summary>
        /// <typeparam name="TDelegate">Specify the delegate type.</typeparam>
        /// <param name="oldDelegate">Remove old delegates.</param>
        /// <exception cref="ArgumentNullException">parameter null exception.</exception>
        public void RemoveListener<TDelegate>(TDelegate oldDelegate) where TDelegate : Delegate
        {
            if (oldDelegate == null) throw new ArgumentNullException(nameof(oldDelegate));
            var delegateType = oldDelegate.GetType();
            if (!delegateType.IsInstanceOfType(typeof(TDelegate))) return;
            Delegate tempDelegate = null;
            if (_dicDelegates.TryGetValue(delegateType, out tempDelegate))
            {
                if (tempDelegate == null)
                {
                    _dicDelegates.Remove(delegateType);
                }
                else
                {
                    _dicDelegates[delegateType] = tempDelegate;
                }
            }
        }

        /// <summary>
        /// Triggers a delegate of the same type.
        /// </summary>
        /// <typeparam name="TDelegate"></typeparam>
        /// <param name="sender">trigger source object.</param>
        /// <param name="eventArgs">event args.</param>
        /// <exception cref="ArgumentNullException">parameter null exception.</exception>
        public void Dispatch<TDelegate>(object sender, EventArgs eventArgs) where TDelegate : Delegate
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));
            if (!_dicDelegates.ContainsKey(typeof(TDelegate))) return;
            _dicDelegates[typeof(TDelegate)].DynamicInvoke(sender, eventArgs);
        }

        /// <summary>
        /// Clear all listeners.
        /// </summary>
        public void Clear() => _dicDelegates.Clear();

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(disposing: true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the
        // runtime from inside the finalizer and you should not reference
        // other objects. Only unmanaged resources can be disposed.
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    component.Dispose();
                }

                // Call the appropriate methods to clean up
                // unmanaged resources here.
                // If disposing is false,
                // only the following code is executed.
                CloseHandle(handle);
                handle = IntPtr.Zero;

                // Note disposing has been done.
                disposed = true;
            }
        }
    }
}