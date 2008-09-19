using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Servers
{
    public struct StopWatchElement
    {
        public double Milliseconds;
        public string Milestone;
    }

    public abstract class Stopwatch
    {
        public abstract void w(string Msg);
        public abstract void SaveToFile(string Filepath);
    }

    public class StopwatchReal : Stopwatch
    {
        public DateTime StartTime = DateTime.Now;
        public List<StopWatchElement> Elements = new List<StopWatchElement>();
        public override void w(string Msg)
        {
            Elements.Add(new StopWatchElement()
            {
                Milestone = Msg,
                Milliseconds = (DateTime.Now - StartTime).TotalMilliseconds
            });
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            int MaxLength = 0;
            foreach (var x in Elements)
                MaxLength = Math.Max(MaxLength, x.Milestone.Length + 5);
            sb.Append(new string('-', MaxLength + 20) + "\r\n");
            for (int i = 0; i < Elements.Count; i++)
            {
                var x = Elements[i];
                sb.Append(x.Milestone.PadRight(MaxLength, '.'));
                sb.Append(string.Format("{0,10:0.00}", (i > 0 ? x.Milliseconds - Elements[i - 1].Milliseconds : x.Milliseconds)));
                sb.Append(string.Format("{0,10:0.00}", x.Milliseconds));
                sb.Append("\r\n");
            }
            return sb.ToString();
        }
        public override void SaveToFile(string Filepath)
        {
            try
            {
                FileStream f = File.Open(Filepath, FileMode.Create, FileAccess.Write, FileShare.Write);
                byte[] b = this.ToString().ToUTF8();
                f.Write(b, 0, b.Length);
                f.Close();
            }
            catch (IOException)
            {
            }
        }
    }

    public class StopwatchDummy : Stopwatch
    {
        public override void w(string Msg) { }
        public override string ToString() { return ""; }
        public override void SaveToFile(string Filepath) { }
    }
}
