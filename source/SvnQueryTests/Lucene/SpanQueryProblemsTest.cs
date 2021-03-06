#region Apache License 2.0

// Copyright 2008 Christian Rodemeyer
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using Lucene.Net.Index;
using Lucene.Net.Search;
using NUnit.Framework;
using Lucene.Net.Search.Spans;
using SvnQuery.Lucene;

namespace SvnQuery.Tests.Lucene
{
    /// <summary>
    /// This test class documents some problems with the SpanQuery queries
    /// that prevented an easy implementation of multiple gap placeholders
    /// like a b c ** b c. The main problem is that the gap is only relative
    /// to a sequence so that the sequences can overlap which lead to either
    /// unexpected or completely wrong hits. What would help were a special
    /// LuceneQuery that would forbid overlaps and works with nested
    /// SpanQueries. Currently my Lucene understanding is not good enough
    /// to start such a task, so my workaround is to use nested gaps with
    /// overlap prevention through SpanNotQueries.
    /// </summary>
    [TestFixture]
    public class SpanQueryProblemsTest
    {
        // Content 3: aa bb cc dd ee ff ee dd cc bb aa aa bb cc dd
        // Content 4: aa bb cc dd cc
        // Content 5: cc dd ee ff 

#pragma warning disable 169
        readonly SpanTermQuery aa = new SpanTermQuery(new Term("content", "AA"));
        readonly SpanTermQuery bb = new SpanTermQuery(new Term("content", "BB"));
        readonly SpanTermQuery cc = new SpanTermQuery(new Term("content", "CC"));
        readonly SpanTermQuery dd = new SpanTermQuery(new Term("content", "DD"));
        readonly SpanTermQuery ee = new SpanTermQuery(new Term("content", "EE"));
        readonly SpanTermQuery ff = new SpanTermQuery(new Term("content", "FF"));
#pragma warning restore 169

        static SpanQuery MakeSpan(int gap, params SpanQuery[] clauses)
        {
            return new SpanNearQuery(clauses, gap, true);
        }

        static Query Content(string query)
        {
            return new Parser(TestIndex.Reader).ParseSimpleTerm(FieldName.Content, query);
        }

        [Test]
        public void OverlappingSpans_Part1()
        {

            // A SpanQuery clause can overlap its preceding clause if its
            // slope is greater than 0
            // In the following example ((cc dd) ** (dd cc)) matches content 3 and 4!
            var lm = MakeSpan(0, cc, dd);
            var rm = MakeSpan(0, dd, cc);
            var q1 = MakeSpan(16, lm, rm);
            TestIndex.AssertQuery(q1, 3, 4);
        }

        [Test]
        public void OverlappingSpans_Part2()
        {
            // If you rewrite the query as (cc (dd ** dd) cc) it doesn't work at all
            // because the inner match is to great
            var gap = MakeSpan(16, dd, dd);
            var q2 = MakeSpan(0, cc, gap, cc);
            TestIndex.AssertQuery(q2);
        }

        [Test]
        public void OverlappingSpans_Part3()
        {
            // If you rewrite it as (cc (dd ** (dd cc))) it matches only 4!
            Console.WriteLine("(cc (dd ** (dd cc)))");
            var q3 = MakeSpan(0, cc, MakeSpan(16, dd, MakeSpan(0, dd, cc)));
            TestIndex.AssertQuery(q3, 4);
        }

        [Test]
        public void OverlappingSpans_Part4()
        {
            var lm = MakeSpan(0, cc, dd);
            var rm = MakeSpan(0, dd, cc);

            // Rewriting with SpanNotQueries works!             
            Console.WriteLine("((cc dd) - (dd cc)) ** (dd cc)");
            var not = new SpanNotQuery(lm, rm);
            var q4 = MakeSpan(16, not, rm);
            TestIndex.AssertQuery(q4, 3);

            // This is now implemented int the parser
            TestIndex.AssertQuery(Content("cc dd ** dd cc"), 3);
        }

        [Test]
        public void OverlappingSpans_Part5()
        {
            // Interestingly, the following does not work with SpanNotQueries
            // dd ee * ee => ((dd ee) - ee) * ee never matches because the 
            // first span (dd ee) always overlaps with ee 
            var span = MakeSpan(0, dd, ee);
            TestIndex.AssertQuery(span, 3, 5);
            var q5 = new SpanNotQuery(span, ee);
            TestIndex.AssertQuery(q5);
        }

        [Test]
        public void OverlappingSpans_Part6()
        {       
            // But the reverse ways of excluding spans works
            // (dd ee * ee) => (dd ee) * (ee -(dd ee))
            var span = MakeSpan(0, dd, ee);
            var not = new SpanNotQuery(ee, span);
            var q6 = MakeSpan(1, span, not);
            TestIndex.AssertQuery(q6, 3);

            // The parser now implements this strategy
            var q = Content("dd ee * ee");
            TestIndex.AssertQuery(Content("dd ee * ee"), 3);
        }

        [Test]
        public void TheProblem_OverlapWithPartOfItself()
        {
            // Content 3: aa bb cc dd ee ff ee dd cc bb aa aa bb cc dd
            // Content 4: aa bb cc dd cc
            // Content 5: cc dd ee ff 

            Console.WriteLine("dd ee * ee");

            // dd ee * ee => (dd ee) * (ee)  does not work as expected
            var lm = MakeSpan(0, dd, ee);
            var rm = MakeSpan(0, ee);
            var q1 = MakeSpan(1, lm, rm);
            TestIndex.AssertQuery(q1, 3, 5);

            // dd ee * ee => dd (ee * ee) works, but only because nothing follows the last ee
            var q2 = MakeSpan(0, dd, MakeSpan(1, ee, ee));           
            TestIndex.AssertQuery(q2, 3);

            // (dd ee) * (ee - (dd ee))
            var not = new SpanNotQuery(ee, MakeSpan(0, dd, ee));
            var q3 = MakeSpan(1, lm, not);
            TestIndex.AssertQuery(q3, 3);
        }

    }
}