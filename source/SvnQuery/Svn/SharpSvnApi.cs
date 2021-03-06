#region Apache License 2.0

// Copyright 2008-2010 Christian Rodemeyer
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using SharpSvn;

namespace SvnQuery.Svn
{
    public class SharpSvnApi : ISvnApi
    {
        readonly Uri _uri;
        readonly string _user;
        readonly string _password;
        readonly Dictionary<int, string> _messages = new Dictionary<int, string>();
        readonly List<SvnClient> _clientPool = new List<SvnClient>();

        public SharpSvnApi(string repositoryUri) : this(repositoryUri, "", "")
        {}

        public SharpSvnApi(string repositoryUri, string user, string password)
        {
            _uri = new Uri(repositoryUri);
            _user = user;
            _password = password;
        }

        SvnClient AllocSvnClient()
        {
            SvnClient client = null;
            lock (_clientPool)
            {
                int last = _clientPool.Count - 1;
                if (last >= 0)
                {
                    client = _clientPool[last];
                    _clientPool.RemoveAt(last);
                }
            }
            return client ?? MakeNewSvnClient();
        }

        SvnClient MakeNewSvnClient()
        {
            SvnClient client = new SvnClient();

            try
            {
                // load configuration right away so exceptions during loading can be catched
                client.LoadConfigurationDefault();
            }
            catch (SvnFormatException)
            {
                // workaround for exception "Can't determine the user's config path" for special users like NetworkService
                // -> try again loading from some specific path
                client.LoadConfiguration(Path.Combine(Path.GetTempPath(), "SharpSvnApi"));
            }

            client.Authentication.UserNameHandlers += (s, e) => e.UserName = _user;
            client.Authentication.UserNamePasswordHandlers += (s, e) =>
            {
                e.UserName = _user;
                e.Password = _password;
            };
            client.Authentication.SslServerTrustHandlers += (s, e) =>
            {
                e.AcceptedFailures = SharpSvn.Security.SvnCertificateTrustFailures.MaskAllFailures;
            };

            return client;
        }

        void FreeSvnClient(SvnClient client)
        {
            lock (_clientPool) _clientPool.Add(client);
        }

        SvnInfoEventArgs Info
        {
            get
            {
                if (_info == null)
                {
                    SvnClient client = AllocSvnClient();
                    try
                    {
                        SvnTarget target = new SvnUriTarget(_uri);
                        client.GetInfo(target, out _info);
                    }
                    finally
                    {
                        FreeSvnClient(client);
                    }
                }
                return _info;
            }
        }

        SvnInfoEventArgs _info;

        public int GetYoungestRevision()
        {
            return (int) Info.LastChangeRevision;
        }

        public Guid GetRepositoryId()
        {
            return Info.RepositoryId;
        }

        public string GetLogMessage(int revision)
        {
            string message;
            lock (_messages) _messages.TryGetValue(revision, out message);
            if (message == null)
            {
                SvnClient client = AllocSvnClient();
                try
                {
                    if (!client.GetRevisionProperty(_uri, new SvnRevision(revision), "svn:log", out message))
                        message = "";
                }
                finally
                {
                    FreeSvnClient(client);
                }
                lock (_messages) _messages[revision] = message;
            }
            return message;
        }

        public List<RevisionData> GetRevisionData(int firstRevision, int lastRevision)
        {
            List<RevisionData> revisions = new List<RevisionData>();
            SvnClient client = AllocSvnClient();
            try
            {
                SvnLogArgs args = new SvnLogArgs(new SvnRevisionRange(firstRevision, lastRevision));
                args.StrictNodeHistory = false;
                args.RetrieveChangedPaths = true;
                args.ThrowOnError = true;
                args.ThrowOnCancel = true;
                Collection<SvnLogEventArgs> logEvents;
                client.GetLog(_uri, args, out logEvents);
                foreach (SvnLogEventArgs e in logEvents)
                {
                    RevisionData data = new RevisionData();
                    data.Revision = (int) e.Revision;
                    data.Author = e.Author ?? "";
                    data.Message = e.LogMessage ?? "";
                    data.Timestamp = e.Time;
                    AddChanges(data, e.ChangedPaths);
                    revisions.Add(data);

                    lock (_messages) _messages[data.Revision] = data.Message;
                }
            }
            finally
            {
                FreeSvnClient(client);
            }
            return revisions;
        }

        static void AddChanges(RevisionData data, IEnumerable<SvnChangeItem> changes)
        {
            if (changes == null) return;
            foreach (var item in changes)
            {
                data.Changes.Add(new PathChange
                                 {
                                     Change = ConvertActionToChange(item.Action),
                                     Revision = data.Revision,
                                     Path = item.Path,
                                     IsCopy = item.CopyFromPath != null
                                 });
            }
        }

        static Change ConvertActionToChange(SvnChangeAction action)
        {
            switch (action)
            {
                case SvnChangeAction.Add:
                    return Change.Add;
                case SvnChangeAction.Modify:
                    return Change.Modify;
                case SvnChangeAction.Delete:
                    return Change.Delete;
                case SvnChangeAction.Replace:
                    return Change.Replace;
                default:
                    throw new Exception("Invalid SvnChangeAction: " + (int) action);
            }
        }

        public void ForEachChild(string path, int revision, Change change, Action<PathChange> action)
        {
            SvnClient client = AllocSvnClient();
            SvnTarget target = MakeTarget(path, change == Change.Delete ? revision - 1 : revision);
            try
            {
                SvnListArgs args = new SvnListArgs {Depth = SvnDepth.Infinity};
                client.List(target, args, delegate(object s, SvnListEventArgs e)
                {
                    if (string.IsNullOrEmpty(e.Path)) return;

                    var pc = new PathChange();
                    {
                        pc.Change = change;
                        pc.Revision = revision;
                        // to be compatible with the log output (which has no trailing '/' for directories)
                        // we need to remove trailing '/' 
                        pc.Path = (e.BasePath.EndsWith("/") ? e.BasePath : e.BasePath + "/") + e.Path.TrimEnd('/');
                    }
                    action(pc);
                });
            }
            finally
            {
                FreeSvnClient(client);
            }
        }

        public PathInfo GetPathInfo(string path, int revision)
        {
            SvnClient client = AllocSvnClient();
            try
            {
                SvnInfoEventArgs info;
                client.GetInfo(MakeTarget(path, revision), out info);

                PathInfo result = new PathInfo();
                result.Size = (int) info.RepositorySize;
                result.Author = info.LastChangeAuthor ?? "";
                result.Timestamp = info.LastChangeTime;
                //result.Revision = (int) info.LastChangeRevision; // wrong if data is directory                
                result.IsDirectory = info.NodeKind == SvnNodeKind.Directory;
                return result;
            }
            catch (SvnException x)
            {
                if (x.SvnErrorCode == SvnErrorCode.SVN_ERR_RA_ILLEGAL_URL)
                {
                    return null;
                }
                if (path.IndexOfAny(InvalidPathChars) >= 0) // this condition exists only because of a bug in the svn client for local repositoreis
                {
                    Console.WriteLine("WARNING: path with invalid charactes could not be indexed: " + path + "@" + revision);
                    return null;
                }
                throw;
            }
            finally
            {
                FreeSvnClient(client);
            }
        }

        static readonly char[] InvalidPathChars = new[] {':', '$', '\\'};

        public IDictionary<string, string> GetPathProperties(string path, int revision)
        {
            SvnClient client = AllocSvnClient();
            try
            {
                Collection<SvnPropertyListEventArgs> pc;
                client.GetPropertyList(MakeTarget(path, revision), out pc);
                Dictionary<string, string> properties = new Dictionary<string, string>();
                foreach (var proplist in pc)
                {
                    foreach (var property in proplist.Properties)
                    {
                        properties.Add(property.Key, property.StringValue.ToLowerInvariant());
                    }
                }
                return properties;
            }
            finally
            {
                FreeSvnClient(client);
            }
        }

        public string GetPathContent(string path, int revision, int size)
        {
            SvnClient client = AllocSvnClient();
            try
            {
                using (MemoryStream stream = new MemoryStream(size + 512))
                {
                    client.Write(MakeTarget(path, revision), stream);
                    stream.Position = 0;
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd(); // default utf-8 encoding, does not work with codepages                    
                    }
                }
            }
            catch (SvnException x)
            {
                if (x.SvnErrorCode != SvnErrorCode.SVN_ERR_IO_INCONSISTENT_EOL) throw;

                Console.WriteLine("WARNIG: Could not read " + path);
                return "";
            }
            finally
            {
                FreeSvnClient(client);
            }
        }

        SvnTarget MakeTarget(string path, int revision)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string part in path.Split('/')) // Build an escaped Uri in the way svn likes it
            {
                sb.Append(Uri.EscapeDataString(part));
                sb.Append('/');
            }
            sb.Length -= 1;
            return new SvnUriTarget(new Uri(_uri + sb.ToString()), revision);
        }
    }
}