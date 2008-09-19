using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Servers
{
    public class DebugLog
    {
        private string LogFile;
        public DebugLog(string LogFileName) { LogFile = LogFileName; }
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
