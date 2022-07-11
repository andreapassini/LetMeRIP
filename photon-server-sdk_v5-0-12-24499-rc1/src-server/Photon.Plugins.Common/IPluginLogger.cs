using System;

namespace Photon.Plugins.Common
{
    /// <summary>
    ///   Interface for a logger.
    /// </summary>
    public interface IPluginLogger
    {
        #region Properties

        /// <summary>
        ///   Gets a value indicating whether debug logging level is enabled.
        /// </summary>
        bool IsDebugEnabled { get; }

        /// <summary>
        ///   Gets a value indicating whether error logging level is enabled.
        /// </summary>
        bool IsErrorEnabled { get; }

        /// <summary>
        ///   Gets a value indicating whether fatal logging level is enabled.
        /// </summary>
        bool IsFatalEnabled { get; }

        /// <summary>
        ///   Gets a value indicating whether info logging level is enabled.
        /// </summary>
        bool IsInfoEnabled { get; }

        /// <summary>
        ///   Gets a value indicating whether warn logging level is enabled.
        /// </summary>
        bool IsWarnEnabled { get; }

        /// <summary>
        ///   Gets the name.
        /// </summary>
        string Name { get; }

        #endregion

        #region Public Methods

        /// <summary>
        ///   Logs a debug message.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        void Debug(object message);

        /// <summary>
        ///   Logs a debug message with an exception.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        /// <param name = "exception">
        ///   The exception.
        /// </param>
        void Debug(object message, Exception exception);

        /// <summary>
        ///   Logs a formatted debug message.
        /// </summary>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void DebugFormat(string format, params object[] args);

        /// <summary>
        ///   Logs a formatted debug message.
        /// </summary>
        /// <param name = "provider">
        ///   The provider.
        /// </param>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void DebugFormat(IFormatProvider provider, string format, params object[] args);

        /// <summary>
        ///   Logs an error message.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        void Error(object message);

        /// <summary>
        ///   Logs an error message with an exception.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        /// <param name = "exception">
        ///   The exception.
        /// </param>
        void Error(object message, Exception exception);

        /// <summary>
        ///   Logs a formatted error message.
        /// </summary>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void ErrorFormat(string format, params object[] args);

        /// <summary>
        ///   Logs a formatted error message.
        /// </summary>
        /// <param name = "provider">
        ///   The provider.
        /// </param>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void ErrorFormat(IFormatProvider provider, string format, params object[] args);

        /// <summary>
        ///   Logs a fatal message.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        void Fatal(object message);

        /// <summary>
        ///   Logs a fatal message with an exception.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        /// <param name = "exception">
        ///   The exception.
        /// </param>
        void Fatal(object message, Exception exception);

        /// <summary>
        ///   Logs a formatted fatal message.
        /// </summary>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void FatalFormat(string format, params object[] args);

        /// <summary>
        ///   Logs a formatted fatal message.
        /// </summary>
        /// <param name = "provider">
        ///   The provider.
        /// </param>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void FatalFormat(IFormatProvider provider, string format, params object[] args);

        /// <summary>
        ///   Logs an info message.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        void Info(object message);

        /// <summary>
        ///   Logs a info message with an exception.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        /// <param name = "exception">
        ///   The exception.
        /// </param>
        void Info(object message, Exception exception);

        /// <summary>
        ///   Logs a formatted info message.
        /// </summary>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void InfoFormat(string format, params object[] args);

        /// <summary>
        ///   Logs a formatted info message.
        /// </summary>
        /// <param name = "provider">
        ///   The provider.
        /// </param>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void InfoFormat(IFormatProvider provider, string format, params object[] args);

        /// <summary>
        ///   Logs a warning.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        void Warn(object message);

        /// <summary>
        ///   Logs a warning with an exception.
        /// </summary>
        /// <param name = "message">
        ///   The message.
        /// </param>
        /// <param name = "exception">
        ///   The exception.
        /// </param>
        void Warn(object message, Exception exception);

        /// <summary>
        ///   Logs a formatted warning.
        /// </summary>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void WarnFormat(string format, params object[] args);

        /// <summary>
        ///   Logs a formatted warning.
        /// </summary>
        /// <param name = "provider">
        ///   The provider.
        /// </param>
        /// <param name = "format">
        ///   The formatted string.
        /// </param>
        /// <param name = "args">
        ///   The args.
        /// </param>
        void WarnFormat(IFormatProvider provider, string format, params object[] args);

        #endregion
    }
}
