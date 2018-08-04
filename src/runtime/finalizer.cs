using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Python.Runtime
{
    public class Finalizer
    {
        public class CollectArgs : EventArgs
        {
            public int ObjectCount { get; set; }
        }

        public class ErrorArgs : EventArgs
        {
            public Exception Error { get; set; }
        }

        public static readonly Finalizer Instance = new Finalizer();

        public event EventHandler<CollectArgs> CollectOnce;
        public event EventHandler<ErrorArgs> ErrorHandler;

        private ConcurrentQueue<IDisposable> _objQueue = new ConcurrentQueue<IDisposable>();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PendingCall(IntPtr arg);
        private readonly PendingCall _collectAction;

        private bool _pending = false;
        private readonly object _collectingLock = new object();
        public int Threshold { get; set; }
        public bool Enable { get; set; }

        private Finalizer()
        {
            Enable = true;
            Threshold = 200;
            _collectAction = OnPendingCollect;
        }

        public void CallPendingFinalizers()
        {
            if (Thread.CurrentThread.ManagedThreadId != Runtime.MainManagedThreadId)
            {
                throw new Exception("PendingCall should execute in main Python thread");
            }
            Runtime.Py_MakePendingCalls();
        }

        public void Collect()
        {
            using (var gilState = new Py.GILState())
            {
                DisposeAll();
            }
        }

        public List<WeakReference> GetCollectedObjects()
        {
            return _objQueue.Select(T => new WeakReference(T)).ToList();
        }

        internal void AddFinalizedObject(IDisposable obj)
        {
            if (!Enable)
            {
                return;
            }
            if (Runtime.Py_IsInitialized() == 0)
            {
                // XXX: Memory will leak if a PyObject finalized after Python shutdown,
                // for avoiding that case, user should call GC.Collect manual before shutdown.
                return;
            }
            _objQueue.Enqueue(obj);
            GC.ReRegisterForFinalize(obj);
            if (_objQueue.Count >= Threshold)
            {
                AddPendingCollect();
            }
        }

        internal static void Shutdown()
        {
            if (Runtime.Py_IsInitialized() == 0)
            {
                Instance._objQueue = new ConcurrentQueue<IDisposable>();
                return;
            }
            Instance.DisposeAll();
            Instance.CallPendingFinalizers();
        }

        private void AddPendingCollect()
        {
            if (_pending)
            {
                return;
            }
            lock (_collectingLock)
            {
                if (_pending)
                {
                    return;
                }
                _pending = true;
                IntPtr func = Marshal.GetFunctionPointerForDelegate(_collectAction);
                if (Runtime.Py_AddPendingCall(func, IntPtr.Zero) != 0)
                {
                    // Full queue, append next time
                    _pending = false;
                }
            }
        }

        private int OnPendingCollect(IntPtr arg)
        {
            DisposeAll();
            _pending = false;
            return 0;
        }

        private void DisposeAll()
        {
            CollectOnce?.Invoke(this, new CollectArgs()
            {
                ObjectCount = _objQueue.Count
            });
            IDisposable obj;
            while (_objQueue.TryDequeue(out obj))
            {
                try
                {
                    obj.Dispose();
                    Runtime.CheckExceptionOccurred();
                }
                catch (Exception e)
                {
                    // We should not bother the main thread
                    ErrorHandler?.Invoke(this, new ErrorArgs()
                    {
                        Error = e
                    });
                }
            }
        }
    }
}