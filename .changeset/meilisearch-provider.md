---
"bielu-bettersearch": minor
---

Add `Bielu.BetterSearch.Meilisearch`, a Meilisearch provider registered with `AddMeilisearch()`. It supports every index operation and waits for each Meilisearch task, so results report whether the write was applied; searching is not implemented yet and returns a failed `Result`.
