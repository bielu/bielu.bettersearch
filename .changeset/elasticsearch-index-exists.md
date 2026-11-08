---
"bielu-bettersearch": patch
---

Elasticsearch `IndexExistsAsync` now returns `true` on a 200 and `false` on a 404, and a failed `Result` for any other answer, such as an unreachable server, a 401, 403 or 5xx, where it used to report the index as missing (#70). The failure carries the request's debug information and the original exception when there is one. `EnsureIndexExistsAsync` returns that failure instead of trying to create the index.
