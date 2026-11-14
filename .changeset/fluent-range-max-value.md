---
"bielu-bettersearch": patch
---

Fix `WithMaxValue` in the fluent range configurators (`NumericRangeQueryConfigurator`, `LongRangeQueryConfigurator`, `StringRangeQueryConfigurator` and `DateRangeQueryConfigurator`) setting the lower bound. It now sets `MaxValue`, so `WithMinValue(1).WithMaxValue(10)` builds a range from 1 to 10 instead of one above 10 with no upper bound.
