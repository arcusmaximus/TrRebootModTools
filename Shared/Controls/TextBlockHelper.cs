using Avalonia.Controls;
using Avalonia.Controls.Documents;
using System;
using System.Collections.Generic;

namespace TrRebootTools.Shared.Controls
{
    internal static class TextBlockHelper
    {
        public static void SetTextWithHighlights(TextBlock textBlock, string? text, string? highlightText)
        {
            List<Range>? highlightRanges = null;
            if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(highlightText))
            {
                int index = -highlightText.Length;
                while ((index = text.IndexOf(highlightText, index + highlightText.Length, StringComparison.InvariantCultureIgnoreCase)) >= 0)
                {
                    (highlightRanges ??= []).Add(new Range(index, index + highlightText.Length));
                }
            }

            if (highlightRanges == null)
            {
                textBlock.Text = text;
                textBlock.Inlines = null;
                return;
            }

            textBlock.Text = null;
            textBlock.Inlines = null;
            Index regularRangeStart = 0;
            foreach (Range highlightRange in highlightRanges)
            {
                AddRun(regularRangeStart..highlightRange.Start, false);
                AddRun(highlightRange, true);
                regularRangeStart = highlightRange.End;
            }
            AddRun(regularRangeStart.., false);
            return;

            void AddRun(Range range, bool highlight)
            {
                if (range.Start.Equals(range.End))
                    return;

                Run run = new(text![range]);
                if (highlight)
                    run.Classes.Add("highlight");

                (textBlock.Inlines ??= []).Add(run);
            }
        }
    }
}
