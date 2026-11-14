---
"bielu-bettersearch": patch
---

OpenSearch `RemoveDocumentAsync` now builds its success check on the provider's shared `IsSuccess` helper instead of repeating it. Behavior is unchanged. The solution is formatted with `dotnet format`, and pull requests now fail CI when `dotnet format --verify-no-changes` reports changes.
