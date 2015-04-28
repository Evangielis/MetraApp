﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Metra.Axxess
{
    class OperationIdle : Operation
    {
        const int SLEEP_TIME = 100;

        public OperationIdle(IAxxessBoard device) : base(device, OperationType.Idle)
        {
            this.TotalOperations = 1;
            this.OperationsCompleted = 1;
        }

        public override void Work()
        {
            base.Work();

            try
            {
                this.Device.SendIntroPacket();
                Thread.Sleep(SLEEP_TIME);
            }
            catch (HIDDeviceException e)
            {
                this.Stop();
            }
        }
    }
}
