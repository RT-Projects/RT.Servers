using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Servers
{
    public struct Cookie
    {
        public string Name;
        public string Value;
        public string Path;
        public string Domain;
        public DateTime? Expires;
        public bool HttpOnly;
    }

    public struct HTTPCacheControl
    {
        public HTTPCacheControlState State;
        public int? IntParameter;
        public string StringParameter;
    }

    public struct HTTPContentDisposition
    {
        public HTTPContentDispositionMode Mode;
        public string Filename;
    }

    public struct HTTPContentRange
    {
        public int From;
        public int To;
        public int Total;
    }
}
