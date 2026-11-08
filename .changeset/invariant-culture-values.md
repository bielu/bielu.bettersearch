---
"bielu-bettersearch": patch
---

Query values are turned into text with the invariant culture (#33). The Elasticsearch and OpenSearch Prefix and PrefixPhrase translators, and the Lifti Term, Terms, Fuzzy, Prefix and PrefixPhrase translators, used the culture-dependent `ToString()`, so `3.5` was sent as `"3,5"` on a machine running under a culture such as `pl-PL`. They now send `"3.5"` on every machine. Lifti also indexes field values with the invariant culture, so indexed text and queries agree; re-index Lifti documents holding numbers or dates that were indexed under another culture.
