using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// The bit bucket logger.
    /// </summary>
    class BitBucketLogger : ILog
    {
        /// <inheritdoc/>
        public void Trace(string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Trace(Exception exception, string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Debug(string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Debug(Exception exception, string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Info(string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Info(Exception exception, string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Warn(string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Warn(Exception exception, string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Error(string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Error(Exception exception, string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Fatal(string format, params object[] args)
        {
        }

        /// <inheritdoc/>
        public void Fatal(Exception exception, string format, params object[] args)
        {
        }
    }
}
