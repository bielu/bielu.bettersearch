---
"bielu-bettersearch": minor
---

Add `Bielu.BetterSearch.Meilisearch`, a Meilisearch provider registered with `AddMeilisearch()`. It supports every index operation and waits for each Meilisearch task, so a successful result means the write was applied (after a `TaskTimeout` the outcome is unknown); searching is not implemented yet and returns a failed `Result`.
