using System.Collections.Generic;

namespace Photon.Plugins.Common
{
    /// <summary>
    /// Base interface of all operation requests containing common parameters as sent by client.
    /// </summary>
    public interface IOperationRequest
    {
        #region Properties
        /// <summary>
        /// Unique reserved code per operation.
        /// </summary>
        byte OperationCode { get; }

        /// <summary>
        /// Operation request parameters combined as sent by client.
        /// </summary>
        Dictionary<byte, object> Parameters { get; }

        /// <summary>
        /// Request webflags optionnaly set by client to control webhooks behaviour.
        /// </summary>
        byte WebFlags { get; set; }

        #endregion
    }
}