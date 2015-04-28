﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Metra.Axxess
{
    public enum OperationStatus
    {
        Ready,
        Working,
        Finished
    };

    public enum OperationType
    {
        Download,
        Idle,
        Firmware,
        Remap
    };

    public class OpArgs
    {
        public IAxxessBoard Device { get; private set; }
        public string Path { get; private set; }

        public OpArgs(IAxxessBoard device, string path = null)
        {
            this.Device = device;
            this.Path = path;
        }
    }

    abstract class Operation : IOperation
    {
        public OperationStatus Status { get; protected set; }
        public OperationType Type { get; protected set; }
        public int OperationsCompleted { get; protected set; }
        public int TotalOperations { get; protected set; }
        public int Progress { get { return (OperationsCompleted * 100) / TotalOperations; } }

        public Thread WorkerThread { get; private set; }
        public ThreadStart WorkerMethod { get; private set; }

        public IAxxessBoard Device { get; private set; }

        public Operation(IAxxessBoard device, OperationType type)
        {
            this.Type = type;

            this.WorkerMethod = this.DoWork;
            this.WorkerThread = new Thread(this.WorkerMethod);

            this.Device = device;

            this.OperationsCompleted = 0;
            this.TotalOperations = 1;
            this.Status = OperationStatus.Ready;
        }

        public virtual void DoWork()
        {
            while(this.Status.Equals(OperationStatus.Working))
            {
                this.Work();
            }
        }

        public virtual void Work()
        {
            return;
        }

        public virtual void Start()
        {
            this.Status = OperationStatus.Working;
            this.WorkerThread.Start();
        }

        public virtual void Stop()
        {
            this.Status = OperationStatus.Finished;
            this.WorkerThread.Abort();
            this.Dispose();
        }

        public virtual void Dispose()
        {
            return;
        }
    
        #region Explicit IOperation Implementation
        OperationStatus IOperation.Status
        {
            get { return this.Status; }
        }

        OperationType IOperation.Type
        {
            get { return this.Type; }
        }

        int IOperation.Progress
        {
            get { return this.Progress; }
        }

        Thread IOperation.WorkerThread
        {
            get { return this.WorkerThread; }
        }

        void IOperation.Start()
        {
            this.Start();
        }

        void IOperation.Stop()
        {
            this.Stop();
        }
        #endregion
    }
}
