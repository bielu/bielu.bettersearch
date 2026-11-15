using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class FuzzyQueryConfigurator : SubQueryConfigurator<FuzzyQueryConfigurator>
    {
        public FuzzyQueryConfigurator()
        {
            _query = new FuzzySubQuery();
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
        /// <summary>Sets <see cref="FuzzySubQuery.MaxEdits"/>, the most edits a match may be from the value.</summary>
        public FuzzyQueryConfigurator WithMaxEdits(int maxEdits)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxEdits);
            _query.MaxEdits = maxEdits;
            return this;
        }
        /// <summary>Sets <see cref="FuzzySubQuery.PrefixLength"/>, the number of leading characters that must match exactly.</summary>
        public FuzzyQueryConfigurator WithPrefixLength(int prefixLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(prefixLength);
            _query.PrefixLength = prefixLength;
            return this;
        }
        /// <summary>Sets <see cref="FuzzySubQuery.MaxExpansions"/>, the most terms the query expands to.</summary>
        public FuzzyQueryConfigurator WithMaxExpansions(int maxExpansions)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxExpansions);
            _query.MaxExpansions = maxExpansions;
            return this;
        }
        /// <summary>Sets <see cref="FuzzySubQuery.Transposition"/>, whether swapping two adjacent characters counts as one edit.</summary>
        public FuzzyQueryConfigurator WithTranspositions(bool transpositions = true)
        {
            _query.Transposition = transpositions;
            return this;
        }

        private FuzzySubQuery _query;
        public ISearchSubQuery Query => _query;
        protected override ISearchSubQuery SubQuery => _query;
    }
}
