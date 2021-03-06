#region Apache License 2.0

// Copyright 2008-2009 Christian Rodemeyer
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

using System.Collections;
using System.Diagnostics;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace SvnQuery.Lucene
{
    public class RevisionFilter : Filter
    {
        public const string RevFormat = "d8";
        const int Head = Revision.Head;
        const int All = Revision.All;

        readonly int _revLast;
        readonly int _revFirst;

        public RevisionFilter() : this(Head, Head)
        {}

        public RevisionFilter(int first, int last)
        {
            _revFirst = first;
            _revLast = last;
        }

        public override BitArray Bits(IndexReader reader)
        {
            // reader.GetVersion could be used to cache
            // Debug.WriteLine(reader.GetVersion()); // could be used to cache
            // if (cached reader == reader && _revFirst == 

            if (_revFirst == All || _revLast == All) // optimization
                return new BitArray(reader.MaxDoc(), true);

            BitArray last_bits = new BitArray(reader.MaxDoc(), false);

            TermEnum t = reader.Terms(new Term(FieldName.RevisionLast, _revFirst.ToString(RevFormat)));
            TermDocs d = reader.TermDocs();
            //if (t.SkipTo((new Term(FieldName.RevisionLast, revision.ToString(RevFormat))))) // extremely slow
            if (t.Term() != null)
            {
                while (t.Term().Field() == FieldName.RevisionLast)
                {
                    d.Seek(t);
                    while(d.Next()) last_bits[d.Doc()] = true;                     
                    if (!t.Next()) break;
                }
            }

            // optimization, skip if we just using the head revision
            if (_revLast == Head)
                return last_bits;

            BitArray first_bits = new BitArray(reader.MaxDoc(), true);
            t = reader.Terms(new Term("rev_first", (_revLast + 1).ToString(RevFormat)));
            //if (t.SkipTo((new Term("rev_first", (revision + 1).ToString(RevFormat))))) // extremely slow
            if (t.Term() != null)
            {
                while (t.Term().Field() == "rev_first")
                {
                    d.Seek(t);
                    while (d.Next()) first_bits[d.Doc()] = false; 
                    if (!t.Next()) break;
                }
            }
            return last_bits.And(first_bits);
        }

        internal static void Print(string name, BitArray bits)
        {
            Debug.Write(name + ": ");
            for (int i = 0; i < bits.Length; ++i)
            {
                Debug.Write(bits[i] ? "1" : "0");
            }
            Debug.WriteLine("");
        }
    }
}