---
"bielu-bettersearch": patch
---

Query values are turned into text the same way on every machine (#33). The Elasticsearch and OpenSearch Prefix and PrefixPhrase translators, the OpenSearch Fuzzy translator, and the Lifti Term, Terms, Fuzzy, Prefix and PrefixPhrase translators used the culture-dependent `ToString()`, so `3.5` was sent as `"3,5"` on a machine running under a culture such as `pl-PL`. They now share `QueryValueText.From` in Abstractions, which writes numbers with the invariant culture, dates in ISO 8601 and booleans as `true` or `false`, so a date query value matches how Elasticsearch and OpenSearch store dates. Lifti also indexes field values that way, so indexing and querying follow the same rule. Lifti splits that text into words at punctuation, though, so a query for a decimal such as `3.5`, or a date, still matches nothing there, and a fuzzy one can match a nearby number; the feature matrix and the Lifti page say so.
