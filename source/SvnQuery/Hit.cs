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
using System.Linq;
using Lucene.Net.Documents;
using SvnQuery.Lucene;

namespace SvnQuery
{
    public class Hit
    {
        readonly Document _doc;
        readonly string   _fragment;

        internal Hit(Document doc, string fragment)
        {
            _doc = doc;
            _fragment = fragment;
        }

        public string Id
        {
            get { return _doc.Get(FieldName.Id); }
        }

        public string Path
        {
            get
            {
                if (_path == null)
                {                    
                    _path = Id.Split('@')[0];
                }
                return _path;
            }
        }

        string _path;

        public string Folder
        {
            get { return Path.Substring(0, _path.LastIndexOf('/')); }
        }

        public string File
        {
            get { return Path.Substring(_path.LastIndexOf('/') + 1); }
        }

        public int Revision
        {
            get { return int.Parse(_doc.Get(FieldName.RevisionFirst)); }
        }

        public string RevisionFirst
        {
            get { return NiceRevision(_doc.Get(FieldName.RevisionFirst)); }
        }

        public string RevisionLast
        {
            get { return NiceRevision(_doc.Get(FieldName.RevisionLast)); }
        }

        static string NiceRevision(string rev)
        {
            return rev == SvnQuery.Revision.HeadString ? "head" : rev.TrimStart('0');
        }

        /// <summary>
        /// The approx size in bytes as integer. Note that this comes from a
        /// packed size where the size is grouped in classes
        /// </summary>
        public int SizeInBytes
        {
            get { return PackedSizeConverter.FromSortableString(_doc.Get(FieldName.Size)); }
        }

        /// <summary>
        /// The formatted size as 17 bytes or 42 kB. 
        /// </summary>
        public string Size
        {
            get { return PackedSizeConverter.ToString(SizeInBytes); }
        }

        public string Author
        {
            get { return _doc.Get(FieldName.Author); }
        }

        public DateTime LastModification
        {
            get { return DateTime.Parse(_doc.Get(FieldName.Timestamp)); }
        }

        public string HighlightedFragment
        {
            get { return _fragment; }
        }

        public override string ToString()
        {
            return Path;
        }
    }
}