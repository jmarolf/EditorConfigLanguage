﻿using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EditorConfig
{
    internal sealed class OutliningTagger : ITagger<IOutliningRegionTag>
    {
        private readonly ITextBuffer _buffer;
        private ITextSnapshot _snapshot;
        private EditorConfigDocument _document;

        public OutliningTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _snapshot = buffer.CurrentSnapshot;

            _document = EditorConfigDocument.FromTextBuffer(buffer);
            _document.Parsed += BufferChanged;

            StartParsing();
        }

        public List<Region> Regions { get; private set; } = new List<Region>();

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_document.IsParsing || spans.Count == 0 || !Regions.Any())
                yield break;

            IEnumerable<Region> currentRegions = Regions;
            ITextSnapshot currentSnapshot = _snapshot;
            SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
            int startLineNumber = entire.Start.GetContainingLine().LineNumber;
            int endLineNumber = entire.End.GetContainingLine().LineNumber;

            foreach (var region in currentRegions)
            {
                if (region.StartLine <= endLineNumber && region.EndLine >= startLineNumber)
                {
                    var startLine = currentSnapshot.GetLineFromLineNumber(region.StartLine);
                    string text = startLine.GetText();
                    string hover = entire.Snapshot.GetText(region.StartOffset, region.EndOffset - region.StartOffset);

                    yield return new TagSpan<IOutliningRegionTag>(
                        new SnapshotSpan(currentSnapshot, region.StartOffset, region.EndOffset - region.StartOffset),
                        new OutliningRegionTag(false, false, text, hover));
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        void BufferChanged(object sender, EventArgs e)
        {
            StartParsing();
        }

        private void StartParsing()
        {
            ThreadHelper.Generic.BeginInvoke(() =>
            {
                Regions.Clear();
                ReParse();
                TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(_snapshot, 0, _snapshot.Length)));
            });
        }

        void ReParse()
        {
            ITextSnapshot newSnapshot = _buffer.CurrentSnapshot;
            List<Region> newRegions = new List<Region>();

            foreach (var section in _document.Sections)
            {
                var startLine = newSnapshot.GetLineFromPosition(section.Span.Start);
                var endLine = newSnapshot.GetLineFromPosition(section.Span.End);

                var region = new Region
                {
                    StartLine = startLine.LineNumber,
                    StartOffset = startLine.Start,
                    EndLine = endLine.LineNumber,
                    EndOffset = endLine.End
                };

                newRegions.Add(region);
            }

            _snapshot = newSnapshot;
            Regions = newRegions.Where(line => line.StartLine != line.EndLine).ToList();
        }
    }

    class Region
    {
        public int StartLine { get; set; }
        public int StartOffset { get; set; }
        public int EndLine { get; set; }
        public int EndOffset { get; set; }
    }
}
