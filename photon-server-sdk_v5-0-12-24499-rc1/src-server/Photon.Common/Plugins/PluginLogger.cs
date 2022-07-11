using ExitGames.Logging;
using System;
using Photon.Plugins.Common;

namespace Photon.Common.Plugins
{
    public class PluginLogger : IPluginLogger
    {
        #region Constants and Fields

        private readonly ILogger logger;

        private readonly IPluginLogMessagesCounter messagesCounter;
        #endregion

        #region .ctor

        public PluginLogger(string name, IPluginLogMessagesCounter counter = null)
        {
            this.logger = LogManager.GetLogger("Plugin." + name);
            this.messagesCounter = counter ?? NullPluginLogMessageCounter.Instance;
        }

        #endregion

        #region Properties

        public bool IsDebugEnabled
        {
            get { return this.logger.IsDebugEnabled; }
        }

        public bool IsErrorEnabled
        {
            get { return this.logger.IsErrorEnabled; }
        }

        public bool IsFatalEnabled
        {
            get { return this.logger.IsFatalEnabled; }
        }

        public bool IsInfoEnabled
        {
            get { return this.logger.IsInfoEnabled; }
        }

        public bool IsWarnEnabled
        {
            get { return this.logger.IsWarnEnabled; }
        }

        public string Name { get { return this.logger.Name; } }


        #endregion

        #region Publics

        public void Debug(object message)
        {
            this.logger.Debug(message);
        }

        public void Debug(object message, Exception exception)
        {
            this.logger.Debug(message, exception);
        }

        public void DebugFormat(string format, params object[] args)
        {
            this.logger.DebugFormat(format, args);
        }

        public void DebugFormat(IFormatProvider provider, string format, params object[] args)
        {
            this.logger.DebugFormat(provider, format, args);
        }

        public void Error(object message)
        {
            this.logger.Error(message);
            this.messagesCounter.IncrementErrorsCount();
        }

        public void Error(object message, Exception exception)
        {
            this.logger.Error(message, exception);
            this.messagesCounter.IncrementErrorsCount();
        }

        public void ErrorFormat(string format, params object[] args)
        {
            this.logger.ErrorFormat(format, args);
            this.messagesCounter.IncrementErrorsCount();
        }

        public void ErrorFormat(IFormatProvider provider, string format, params object[] args)
        {
            this.logger.ErrorFormat(provider, format, args);
            this.messagesCounter.IncrementErrorsCount();
        }

        public void Fatal(object message)
        {
            this.logger.Fatal(message);
            this.messagesCounter.IncrementFatalsCount();
        }

        public void Fatal(object message, Exception exception)
        {
            this.logger.Fatal(message, exception);
            this.messagesCounter.IncrementFatalsCount();
        }

        public void FatalFormat(string format, params object[] args)
        {
            this.logger.FatalFormat(format, args);
            this.messagesCounter.IncrementFatalsCount();
        }

        public void FatalFormat(IFormatProvider provider, string format, params object[] args)
        {
            this.logger.FatalFormat(provider, format, args);
            this.messagesCounter.IncrementFatalsCount();
        }

        public void Info(object message)
        {
            this.logger.Info(message);
        }

        public void Info(object message, Exception exception)
        {
            this.logger.Info(message, exception);
        }

        public void InfoFormat(string format, params object[] args)
        {
            this.logger.InfoFormat(format, args);
        }

        public void InfoFormat(IFormatProvider provider, string format, params object[] args)
        {
            this.logger.InfoFormat(provider, format, args);
        }

        public void Warn(object message)
        {
            this.logger.Warn(message);
            this.messagesCounter.IncrementWarnsCount();
        }

        public void Warn(object message, Exception exception)
        {
            this.logger.Warn(message, exception);
            this.messagesCounter.IncrementWarnsCount();
        }

        public void WarnFormat(string format, params object[] args)
        {
            this.logger.WarnFormat(format, args);
            this.messagesCounter.IncrementWarnsCount();
        }

        public void WarnFormat(IFormatProvider provider, string format, params object[] args)
        {
            this.logger.WarnFormat(provider, format, args);
            this.messagesCounter.IncrementWarnsCount();
        }

        #endregion
    }
}
