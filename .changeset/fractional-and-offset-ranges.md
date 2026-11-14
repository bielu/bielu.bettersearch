---
"bielu-bettersearch": minor
---

Ranges over fractional numbers and `DateTimeOffset` dates. `NumericRange` now holds `double?` bounds, so a price from 9.99 to 49.5 or a rating of at least 4.5 can be queried, on Elasticsearch, OpenSearch and Meilisearch; use `LongRange` for whole numbers that need precision above 2^53. A bound is sent unchanged, not rounded, so at least 4.5 on whole numbers matches 5 and not 4. A `NaN` or infinite bound returns a failed `Result` on every provider (`NumericRange has a bound that is not a finite number; use a finite MinValue and MaxValue.`). The bounds of `DateRangeQuery` are a new `DateRangeValue`, which a `DateTime` and a `DateTimeOffset` both convert to implicitly: a `DateTimeOffset` keeps its instant, and a `DateTime` with `Kind.Unspecified` is read as UTC on every provider, as query values are. The fluent `NumericRangeQueryConfigurator.WithMinValue` and `WithMaxValue` take a `double`, and `DateRangeQueryConfigurator.WithMinValue` and `WithMaxValue` a `DateRangeValue`. Lifti still fails every range.

Breaking:

- `NumericRange.MinValue` and `MaxValue` are `double?`, not `int?`. Code that writes them keeps compiling, as an `int` converts implicitly; code that reads them into an `int` does not:

  ```csharp
  int? min = range.MinValue;          // no longer compiles
  double? min = range.MinValue;       // instead
  ```

- `DateRangeQuery.MinValue` and `MaxValue` are `DateRangeValue?`, not `DateTime?`. Assigning a `DateTime` or a `DateTimeOffset` compiles; reading one back needs `UtcDateTime` or `Instant`:

  ```csharp
  DateTime? min = range.MinValue;            // no longer compiles
  DateTime? min = range.MinValue?.UtcDateTime; // instead
  ```
