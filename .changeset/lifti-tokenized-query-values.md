---
"bielu-bettersearch": patch
---

Lifti now splits term, terms, fuzzy, prefix and phrase prefix values into words with the tokenizer the index uses, and matches the words next to each other, in order (#66). A whole decimal (`3.5`), date, `DateTimeOffset` or exponent (`1E-07`) now matches the value it was indexed from, where it used to match nothing; a fuzzy `3.5` matches `3.5` instead of `35`; accented terms (`żółw`) match; a phrase prefix of several words (`jane d`) matches `Jane Doe`; and a term of several words (`john smith`) matches them as a phrase. A value with no words (`null`, `""`, `-`) now returns a failed `Result` instead of a successful empty one. The values of a multi-valued field are now indexed with a private-use separator word between them, so the words of one value are never next to the words of another; Lifti indexes live in memory, so every index picks this up when it is next filled.
