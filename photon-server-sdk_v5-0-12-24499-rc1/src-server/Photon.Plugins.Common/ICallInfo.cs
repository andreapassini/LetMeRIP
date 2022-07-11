using System.Collections.Generic;

namespace Photon.Plugins.Common
{
    public interface ICallInfo
    {
        #region Properties
        /// <summary>
        /// The operation request triggering the callback call.
        /// </summary>
        IOperationRequest OperationRequest { get; }

        /// <summary>
        /// Helper property to check if call is not processed nor deferred.
        /// </summary>
        bool IsNew { get; }
        /// <summary>
        /// Helper property to check if call is deferred.
        /// </summary>
        bool IsDeferred { get; }
        /// <summary>
        /// Helper property to check if call was processed successfully (Continue() was called).
        /// </summary>
        bool IsSucceeded { get; }
        /// <summary>
        /// Helper property to check if Fail() was called.
        /// </summary>
        bool IsFailed { get; }
        /// <summary>
        /// Helper property to check if Cancel() was called.
        /// </summary>
        bool IsCanceled { get; }
        /// <summary>
        /// Helper property to check if the call was processed using any of the three methods: Continue, Cancel or Fail.
        /// </summary>
        bool IsProcessed { get; }
        /// <summary>
        /// Helper property to check if call is Paused
        /// </summary>
        bool IsPaused { get; }
        #endregion

        #region Public Methods

        void Continue();

        void Fail(string msg = null, Dictionary<byte, object> errorData = null);

        bool StrictModeCheck(out string errorMsg);

        #endregion
    }
}
