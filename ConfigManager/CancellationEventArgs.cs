using System;

namespace ConfigManager
{
    public class CancellationEventArgs : EventArgs
    {
        #region Properties

        public bool Cancelled { get; }

        #endregion

        #region Constructors

        public CancellationEventArgs(bool cancelled)
        {
            Cancelled = cancelled;
        }

        #endregion
    }
}
