using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using RT.Util.ExtensionMethods;

namespace Servers
{
    /// <summary>
    /// Encapsulates a single entry in a <see cref="Stopwatch"/> log.
    /// </summary>
    public struct StopWatchElement
    {
        public double Milliseconds;
        public string Milestone;
    }

    /// <summary>
    /// This class provides an object that remembers events as they happen and
    /// when they happen and outputs a report with timing information at the end.
    /// For best performance it is useful to use this from a partial method (see example).
    /// </summary>
    /// <example>
    ///     The following code exemplifies the intended use of this class. Two partial methods
    ///     are declared which by default are empty. This way, if a stopwatch is not needed,
    ///     it has zero impact on runtime behaviour.
    ///
    ///     If the commented portion is commented back in, a Stopwatch object is instantiated
    ///     and the methods are implemented to use it.
    ///
    ///     <code>
    ///         partial void Stopwatch(string Msg);
    ///         partial void StopwatchOutput(string StopwatchFilename);
    ///
    ///         /*      <!-- simply change this to //* to enable the stopwatch -->
    ///         private static Stopwatch sw = null;
    ///         partial void Stopwatch(string Msg)
    ///         {
    ///             if (sw == null)
    ///                 sw = new Stopwatch();
    ///             sw.Log(Msg);
    ///         }
    ///         partial void StopwatchOutput(string StopwatchFilename)
    ///         {
    ///             if (sw != null)
    ///                 sw.SaveToFile(StopwatchFilename);
    ///         }
    ///         /**/
    ///     </code>
    /// </example>
    public class Stopwatch
    {
        /// <summary>
        /// Remembers when the stopwatch was started.
        /// </summary>
        public DateTime StartTime = DateTime.Now;

        /// <summary>
        /// Remembers the events as they happen.
        /// </summary>
        public List<StopWatchElement> Elements = new List<StopWatchElement>();

        /// <summary>
        /// Logs an event.
        /// </summary>
        /// <param name="Msg">Message to log.</param>
        public void Log(string Msg)
        {
            Elements.Add(new StopWatchElement
            {
                Milestone = Msg,
                Milliseconds = (DateTime.Now - StartTime).TotalMilliseconds
            });
        }

        /// <summary>
        /// Generates a report with timing information detailling when each logged event happened relative to the <see cref="StartTime"/>.
        /// </summary>
        /// <returns>A report with timing information detailling when each logged event happened relative to the <see cref="StartTime"/>.</returns>
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

        /// <summary>
        /// Outputs the stopwatch report with timing information to the specified file.
        /// </summary>
        /// <param name="Filepath">File to save stopwatch output to. If the file already exists, it will be overwritten.</param>
        public void SaveToFile(string Filepath)
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
}
