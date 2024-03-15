using System;

namespace ConfigManager
{
    public class ProgressEventArgs : EventArgs
    {
        #region Properties

        public int Percentage { get; }

        #endregion

        #region Constructors

        public ProgressEventArgs(int percentage)
        {
            Percentage = percentage;
        }

        #endregion
    }
}
