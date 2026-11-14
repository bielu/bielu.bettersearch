using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class FuzzyQueryConfigurator : SubQueryConfigurator<FuzzyQueryConfigurator>
    {
        public FuzzyQueryConfigurator()
        {
            _query = new FuzzyQuery();
        }

        public FuzzyQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        public FuzzyQueryConfigurator WithValue(object value)
        {
            _query.Value = value;
            return this;
        }
        /// <summary>
        /// Sets <see cref="FuzzyQuery.Fuziness"/> to <paramref name="maxEdits"/> for both bounds. The providers allow the
        /// larger bound of edits.
        /// </summary>
        public FuzzyQueryConfigurator WithFuzziness(int maxEdits) => WithFuzziness(maxEdits, maxEdits);
        /// <summary>Sets both bounds of <see cref="FuzzyQuery.Fuziness"/>.</summary>
        public FuzzyQueryConfigurator WithFuzziness(int low, int high)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(low);
            ArgumentOutOfRangeException.ThrowIfNegative(high);
            _query.Fuziness = Tuple.Create(low, high);
            return this;
        }
        /// <summary>Sets <see cref="FuzzyQuery.PrefixLenght"/>, the number of leading characters that must match exactly.</summary>
        public FuzzyQueryConfigurator WithPrefixLength(int prefixLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(prefixLength);
            _query.PrefixLenght = prefixLength;
            return this;
        }
        /// <summary>Sets <see cref="FuzzyQuery.MaxExpansions"/>, the most terms the query expands to.</summary>
        public FuzzyQueryConfigurator WithMaxExpansions(int maxExpansions)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxExpansions);
            _query.MaxExpansions = maxExpansions;
            return this;
        }
        /// <summary>Sets <see cref="FuzzyQuery.Transposition"/>, whether swapping two adjacent characters counts as one edit.</summary>
        public FuzzyQueryConfigurator WithTranspositions(bool transpositions = true)
        {
            _query.Transposition = transpositions;
            return this;
        }

        private FuzzyQuery _query;
        public ISearchSubQuery Query => _query;
        protected override ISearchSubQuery SubQuery => _query;
    }
}
