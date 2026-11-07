---
"bielu-bettersearch": patch
---

Fix Lifti dropping the matches of a double negation. A `MUSTNOT` of a nested bool with only `MUSTNOT` clauses now requires those clauses' matches: on its own it returns them (it returned nothing), and beside `SHOULD` clauses it returns the documents matching both (the nested matches were ignored), as in Lucene and Elasticsearch. A `SHOULD` of a nested bool with only `MUSTNOT` clauses no longer fails when the query can still be answered, for example beside a double negation or another `SHOULD` under a positive parent.
