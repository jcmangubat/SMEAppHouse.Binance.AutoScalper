using SMEAppHouse.Core.TopshelfAdapter;
using System;
using System.Collections.Generic;
using System.Text;

namespace SMEAppHouse.Binance.AutoScalper3.Worker
{
    public class UnixTimerService : TopshelfSocketBase<UnixTimerService>
    {
        public event TickCallbackEventHandler OnTickCallback;

        #region constructors

        public UnixTimerService()
            : base(TimeSpan.FromSeconds(1), null, true, true)
        {
        }

        #endregion

        #region TopshelfSocketBase members

        protected override void ServiceInitializeCallback()
        {
            //throw new NotImplementedException();
        }

        protected override void ServiceTerminateCallback()
        {
            //throw new NotImplementedException();
        }

        protected override void ServiceActionCallback()
        {
            OnTickCallback?.Invoke(this, new TickCallbackEventArgs(DateTime.Now));
        }

        #endregion

        #region event handlers

        public class TickCallbackEventArgs : EventArgs
        {
            public DateTime DateTime { get; private set; }

            public TickCallbackEventArgs()
                : this(DateTime.Now)
            {
            }

            public TickCallbackEventArgs(DateTime dateTime)
            {
                DateTime = dateTime;
            }
        }

        public delegate void TickCallbackEventHandler(object sender, TickCallbackEventArgs e);

        #endregion
    }
}
