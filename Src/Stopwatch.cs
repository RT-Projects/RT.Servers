﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates a single entry in a <see cref="Stopwatch"/> log.
    /// </summary>
    public struct StopWatchElement
    {
        /// <summary>Number of milliseconds between the start of the stopwatch and this event.</summary>
        public double Milliseconds;
        /// <summary>Text describing the event.</summary>
        public string Event;
    }

    /// <summary>
    /// Abstract base class to encapsulate a stopwatch - an object that remembers events as they happen
    /// and when they happen and outputs a report with timing information at the end.
    /// </summary>
    public abstract class Stopwatch : IDisposable
    {
        /// <summary>
        /// Logs an event.
        /// </summary>
        /// <param name="msg">Message to log.</param>
        public abstract void Log(string msg);

        /// <summary>
        /// Outputs the stopwatch report with timing information to the specified file.
        /// </summary>
        /// <param name="filePath">File to save stopwatch output to. If the file already exists, it will be overwritten.</param>
        public abstract void SaveToFile(string filePath);

        /// <summary>No-op.</summary>
        public void Dispose() { }
    }

    /// <summary>
    /// Concrete implementation of <see cref="Stopwatch"/>. This class provides an object that remembers events
    /// as they happen and when they happen and outputs a report with timing information at the end.
    /// </summary>
    public class StopwatchReal : Stopwatch
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
        /// <param name="msg">Message to log.</param>
        public override void Log(string msg)
        {
            Elements.Add(new StopWatchElement
            {
                Event = msg,
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
            int maxLength = 0;
            foreach (var x in Elements)
                maxLength = Math.Max(maxLength, x.Event.Length + 5);
            sb.Append(new string('-', maxLength + 20) + "\r\n");
            for (int i = 0; i < Elements.Count; i++)
            {
                var x = Elements[i];
                sb.Append(x.Event.PadRight(maxLength, '.'));
                sb.Append(string.Format("{0,10:0.00}", (i > 0 ? x.Milliseconds - Elements[i - 1].Milliseconds : x.Milliseconds)));
                sb.Append(string.Format("{0,10:0.00}", x.Milliseconds));
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Outputs the stopwatch report with timing information to the specified file.
        /// </summary>
        /// <param name="filePath">File to save stopwatch output to. If the file already exists, it will be overwritten.</param>
        public override void SaveToFile(string filePath)
        {
            try
            {
                File.WriteAllBytes(filePath, this.ToString().ToUTF8());
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// Implementation of <see cref="Stopwatch"/> that doesn't do anything.
    /// </summary>
    public class StopwatchDummy : Stopwatch
    {
        /// <summary>
        /// Doesn't do anything.
        /// </summary>
        /// <param name="msg">Is ignored.</param>
        public override void Log(string msg) { }

        /// <summary>
        /// Returns an empty string.
        /// </summary>
        /// <returns>An empty string.</returns>
        public override string ToString() { return ""; }

        /// <summary>
        /// Doesn't do anything.
        /// </summary>
        /// <param name="filePath">Is ignored.</param>
        public override void SaveToFile(string filePath) { }
    }
}
