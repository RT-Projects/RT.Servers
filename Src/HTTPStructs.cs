using System;

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
        public long From;
        public long To;
        public long Total;
    }

    public struct HTTPRange
    {
        public long? From;
        public long? To;
    }
}
