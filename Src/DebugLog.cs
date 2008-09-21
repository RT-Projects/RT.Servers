using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using RT.Util.ExtensionMethods;

namespace Servers
{
    /// <summary>
    /// Helper class to write a debugging log with timestamped messages to a file.
    /// </summary>
    public class DebugLog
    {
        /// <summary>
        /// Stores the file path and name for the log file.
        /// </summary>
        private string LogFile;

        /// <summary>
        /// Instantiates a new <see cref="DebugLog"/> instance.
        /// </summary>
        /// <param name="LogFileName">Path and name of a file to write logging messages to.</param>
        public DebugLog(string LogFileName) { LogFile = LogFileName; }

        /// <summary>
        /// Log a message to the log file. The message is automatically timestamped and then appended to the file.
        /// The file is immediately closed afterwards. This method is thread-safe.
        /// </summary>
        /// <param name="Message"></param>
        public void Log(string Message)
        {
            lock (this)
            {
                FileStream f = File.Open(LogFile, FileMode.Append, FileAccess.Write, FileShare.Write);
                byte[] Msg = ("[" + DateTime.Now.ToString("r") + "] " + Message + "\r\n").ToUTF8();
                f.Write(Msg, 0, Msg.Length);
                f.Close();
            }
        }
    }
}
