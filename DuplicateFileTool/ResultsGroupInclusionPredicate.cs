using System;
using System.Collections.Generic;
using System.ComponentModel;
using Path = System.IO.Path;

namespace DuplicateFileTool
{
    internal interface IResultsFilter : INotifyPropertyChanged
    {
        bool IsFilterFilePath { get; }
        bool IsFilterFileName { get; }
        bool IsFilterFileExtension { get; }
        bool IsFilterCaseSensitive { get; }
        bool IsIncludeFilter { get; }
        bool IsExcludeFilter { get; }
        string ResultsFilterKeywords { get; }
    }

    internal class ResultsGroupInclusionPredicate : IInclusionPredicate<DuplicateGroup>
    {
        private IResultsFilter Filter { get; }
        private List<string> Keywords { get; set; }

        public ResultsGroupInclusionPredicate(IResultsFilter filter)
        {
            Keywords = new List<string>();
            Filter = filter;
            Filter.PropertyChanged += PropertyChanged;
        }

        public bool IsIncluded(DuplicateGroup item)
        {
            if (Keywords.Count == 0)
                return true;

            var includeCount = 0;
            foreach (var duplicateFile in item.DuplicateFiles)
            {
                if (!IsIncluded(duplicateFile))
                    continue;
                if (++includeCount == 2)
                    return true;
            }

            return false;
        }

        public bool IsIncluded(DuplicateFile duplicateFile)
        {
            var fullFileName = duplicateFile.FileFullName;
            var path = Path.GetDirectoryName(fullFileName);
            var name = Path.GetFileNameWithoutExtension(fullFileName);
            var extension = Path.GetExtension(fullFileName);
            var stringComparison = Filter.IsFilterCaseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            var matchResult = Filter.IsIncludeFilter;

            foreach (var keyword in Keywords)
            {
                if (Filter.IsFilterFilePath && path != null && path.IndexOf(keyword, stringComparison) != -1)
                    return matchResult;
                if (Filter.IsFilterFileName && name != null && name.IndexOf(keyword, stringComparison) != -1)
                    return matchResult;
                if (Filter.IsFilterFileExtension && extension != null && extension.IndexOf(keyword, stringComparison) != -1)
                    return matchResult;
            }

            return !matchResult;
        }

        private void PropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(IResultsFilter.ResultsFilterKeywords))
                UpdateKeywords();
        }

        private void UpdateKeywords()
        {
            // Update the keywords list
            Keywords.Clear();
            var keywords = Filter.ResultsFilterKeywords;
            if (string.IsNullOrWhiteSpace(keywords))
                return;

            for (var index = 0; index < keywords.Length; ++index)
            {
                // Locate the keyword start
                var ch = keywords[index];
                if (ch == ' ')
                    continue;
                var keywordStart = index;

                // Locate the keyword end
                if (ch != '\"')
                {
                    for (; index < keywords.Length && keywords[index] != ' '; ++index)
                    {
                    }
                }
                else // Ends with quote
                {
                    keywordStart++;
                    var skipNextQuote = false;
                    for (index++; index < keywords.Length; ++index)
                    {
                        ch = keywords[index];
                        if (ch == '\\')
                            skipNextQuote = true;
                        else if (ch == '\"')
                        {
                            if (skipNextQuote)
                                skipNextQuote = false;
                            else
                                break;
                        }
                        else
                            skipNextQuote = false;
                    }
                }

                var keywordLength = index - keywordStart;
                if (keywordLength > 0)
                    Keywords.Add(keywords.Substring(keywordStart, keywordLength));
            }
        }
    }
}
