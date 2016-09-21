//
// RichTextViewBackend.cs
//
// Author:
//       Jérémie Laval <jeremie.laval@xamarin.com>
//
// Copyright (c) 2012 Xamarin, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;

using Xwt;
using Xwt.Backends;


namespace Xwt.GtkBackend
{
	public class RichTextViewBackend : WidgetBackend, IRichTextViewBackend
	{
		Gtk.TextTagTable table;
		LinkLabel [] links;

		int ParagraphSpacing {
			get {
				var font = Font as Pango.FontDescription;
				var size = font.SizeIsAbsolute ? font.Size : font.Size / Pango.Scale.PangoScale;
				return (int)size / 4; // default to 1/4 of the font size
			}
		}

		bool NavigateToUrlEnabled {
			get; set;
		}

		public RichTextViewBackend ()
		{
			Widget = new Gtk.TextView ();
			Widget.Show ();
			Widget.Editable = false;
			Widget.WrapMode = Gtk.WrapMode.Word;
			InitTagTable ();
		}

		void InitTagTable ()
		{
			table = new Gtk.TextTagTable ();
			table.Add (new Gtk.TextTag ("bold") {
				Weight = Pango.Weight.Bold
			});
			table.Add (new Gtk.TextTag ("italic") {
				Style = Pango.Style.Italic
			});
			table.Add (new Gtk.TextTag ("tt") {
				Family = "Monospace"
			});
			table.Add (new Gtk.TextTag ("li") {
				LeftMargin = 14
			});
			table.Add (new Gtk.TextTag ("h1") {
				Weight = Pango.Weight.Bold,
				Scale = Pango.Scale.XXLarge
			});
			table.Add (new Gtk.TextTag ("h2") {
				Weight = Pango.Weight.Bold,
				Scale = Pango.Scale.XLarge
			});
			table.Add (new Gtk.TextTag ("h3") {
				Weight = Pango.Weight.Bold,
				Scale = Pango.Scale.Large
			});
			table.Add (new Gtk.TextTag ("h4") {
				Scale = Pango.Scale.Large
			});
			table.Add (new Gtk.TextTag ("pre") {
				Family = "Monospace",
				Indent = 14,
				WrapMode = Gtk.WrapMode.None
			});
			table.Add (new Gtk.TextTag ("p") {
				Size = 1,
				PixelsAboveLines = Math.Max (LineSpacing, ParagraphSpacing),
				PixelsBelowLines = Math.Max (LineSpacing, ParagraphSpacing),
			});
		}

		protected new Gtk.TextView Widget {
			get {
				return (Gtk.TextView)base.Widget;
			}
			set {
				base.Widget = value;
			}
		}

		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is RichTextViewEvent) {
				switch ((RichTextViewEvent) eventId) {
				case RichTextViewEvent.NavigateToUrl:
					NavigateToUrlEnabled = true;
					break;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is RichTextViewEvent) {
				switch ((RichTextViewEvent) eventId) {
				case RichTextViewEvent.NavigateToUrl:
					NavigateToUrlEnabled = false;
					break;
				}
			}
		}

		public IRichTextBuffer CreateBuffer ()
		{
			return new RichTextBuffer (table);
		}

		public void SetBuffer (IRichTextBuffer buffer)
		{
			var buf = buffer as RichTextBuffer;
			if (buf == null)
				throw new ArgumentException ("Passed buffer is of incorrect type", "buffer");

			if (links != null) {
				foreach (var link in links)
					link.NavigateToUrl -= HandleNavigateToUrl;
			}

			Widget.Buffer = buf;
			links = new LinkLabel [buf.Links.Count];
			for (var i = 0; i < links.Length; i++) {
				var link = buf.Links [i];
				var label = new LinkLabel (link.Text)
				{
					Uri = link.Href
				};
				label.NavigateToUrl += HandleNavigateToUrl;
				Widget.AddChildAtAnchor ((Gtk.Widget) ApplicationContext.Toolkit.GetNativeWidget (label), link.Anchor);
				links [i] = label;
			}
		}

		public IRichTextBuffer CurrentBuffer {
			get {
				return Widget.Buffer as RichTextBuffer;
			}
		}

		public bool ReadOnly { 
			get { 
				return !Widget.Editable;
			}
			set {
				Widget.Editable = !value;
			}
		}

		public bool Selectable {
			get {
				return Widget.Sensitive;
			}
			set {
				Widget.Sensitive = value;
			}
		}

		public int LineSpacing {
			get {
				return Widget.PixelsInsideWrap;
			}
			set {
				Widget.PixelsInsideWrap = value;
				Widget.PixelsBelowLines = value;
				var tag = table.Lookup ("p");
				tag.PixelsBelowLines = tag.PixelsAboveLines = Math.Max (value, ParagraphSpacing);
			}
		}

		public override object Font {
			get {
				return base.Font;
			}
			set {
				base.Font = value;
				var tag = table.Lookup ("p");
				tag.PixelsBelowLines = tag.PixelsAboveLines = Math.Max (LineSpacing, ParagraphSpacing);
			}
		}

		protected override void OnSetBackgroundColor(Drawing.Color color)
		{
			base.OnSetBackgroundColor(color);
			Widget.SetBackgroundColor(Gtk.StateType.Normal, color);
			Widget.SetBackgroundColor(Gtk.StateType.Insensitive, color);
			#if !XWT_GTK3
			Widget.ModifyBase(Gtk.StateType.Normal, color.ToGtkValue());
			Widget.ModifyBase(Gtk.StateType.Insensitive, color.ToGtkValue());
			#endif
		}

		void HandleNavigateToUrl (object sender, NavigateToUrlEventArgs e)
		{
			if (NavigateToUrlEnabled) {
				((IRichTextViewEventSink) EventSink).OnNavigateToUrl (e.Uri);
				e.SetHandled ();
			}
		}

		struct Link {
			public string Text;
			public Uri Href;
			public Gtk.TextChildAnchor Anchor;
		}

		class RichTextBuffer : Gtk.TextBuffer, IRichTextBuffer
		{
			const string NewLine = "\n";

			bool needsParagraphBreak;
			bool needsListBreak;

			void BreakParagraph ()
			{
				if (needsParagraphBreak) {
					var iterEnd = EndIter;
					Insert (ref iterEnd, NewLine);

					var m = CreateMark (null, iterEnd, true);
					Insert (ref iterEnd, NewLine);
					var iterStart = GetIterAtMark (m);
					ApplyTag ("p", iterStart, iterEnd);
					DeleteMark (m);

					needsParagraphBreak = false;
					return;
				}
			}

			void BreakList ()
			{
				if (needsListBreak) {
					var end = EndIter;
					Insert (ref end, NewLine);
					needsListBreak = false;
					return;
				}
			}

			public List<Link> Links {
				get; private set;
			}

			struct StartState {
				public Gtk.TextMark Mark;
				public int Data;
				public StartState (Gtk.TextMark mark, int data)
				{
					Mark = mark;
					Data = data;
				}
			}
			Stack<StartState> openHeaders;
			Stack<Link> openLinks;

			public RichTextBuffer (Gtk.TextTagTable table) : base (table)
			{
				Links = new List<Link> ();
				openHeaders = new Stack<StartState> ();
				openLinks = new Stack<Link> ();
			}

			public string PlainText {
				get {
					return Text;
				}
			}

			public void EmitText (string text, RichTextInlineStyle style)
			{
				//FIXME: it would be nice to have real styled text in a link, but for now short circuit it
				if (openLinks.Count != 0) {
					var link = openLinks.Pop ();
					link.Text += text;
					openLinks.Push (link);
					return;
				}

				var iterEnd = EndIter;
				var m = CreateMark (null, iterEnd, true);
				Insert (ref iterEnd, text);
				var iterStart = GetIterAtMark (m);

				if ((style & RichTextInlineStyle.Bold) != 0)
					ApplyTag ("bold", iterStart, iterEnd);

				if ((style & RichTextInlineStyle.Italic) != 0)
					ApplyTag ("italic", iterStart, iterEnd);

				if ((style & RichTextInlineStyle.Monospace) != 0)
					ApplyTag ("tt", iterStart, iterEnd);

				DeleteMark (m);
			}

			public void EmitStartHeader (int level)
			{
				BreakParagraph ();
				var iter = EndIter;
				openHeaders.Push (new StartState (CreateMark (null, iter, true), level));
			}

			public void EmitEndHeader ()
			{
				var start = openHeaders.Pop ();
				var iterStart = GetIterAtMark (start.Mark);
				var iterEnd = EndIter;
				ApplyTag ("h" + start.Data, iterStart, iterEnd);
				DeleteMark (start.Mark);
				needsParagraphBreak = true;
			}

			public void EmitStartParagraph (int indentLevel)
			{
				BreakParagraph ();
				//FIXME: support indentLevel
			}

			public void EmitEndParagraph ()
			{
				needsParagraphBreak = true;
			}

			public void EmitOpenList ()
			{
				BreakParagraph ();
			}

			public void EmitOpenBullet ()
			{
				BreakList ();
				var iter = EndIter;
				InsertWithTagsByName (ref iter, "• ", "li");
			}

			public void EmitCloseBullet ()
			{
				needsListBreak = true;
			}

			public void EmitCloseList ()
			{
				needsListBreak = false;
				needsParagraphBreak = true;
			}

			public void EmitStartLink (string href, string title)
			{
				//FIXME: it would be nice to support title
				var iter = EndIter;
				var anchor = CreateChildAnchor (ref iter);

				Uri uri;
				if (!Uri.TryCreate (href, UriKind.RelativeOrAbsolute, out uri))
					uri = null;

				openLinks.Push (new Link ()
				{
					Text = string.Empty,
					Href = uri,
					Anchor = anchor
				});
			}

			public void EmitEndLink ()
			{
				Links.Add (openLinks.Pop ());
			}

			public void EmitCodeBlock (string code)
			{
				BreakParagraph ();
				var iter = EndIter;
				InsertWithTagsByName (ref iter, code.Trim (), "pre");
				needsParagraphBreak = true;
			}

			public void EmitHorizontalRuler ()
			{
				//FIXME
			}

			public void EmitText (FormattedText markup)
			{
				using (var list = new FastPangoAttrList ()) {
					var indexer = new TextIndexer (markup.Text);
					list.AddAttributes (indexer, markup.Attributes);
	
					var attrList = GLib.Opaque.GetOpaque (list.Handle, typeof(Pango.AttrList), false) as Pango.AttrList;
					var iter = EndIter;
					var mark = CreateMark (null, iter, false);
					var attrIter = attrList.Iterator;
	
					do {
						int start, end;
	
						attrIter.Range (out start, out end);
	
						if (end == int.MaxValue) // last chunk
							end = markup.Text.Length - 1;
						if (end <= start)
							break;
	
						Gtk.TextTag tag;
						if (attrIter.GetTagForAttributes (null, out tag)) {
							TagTable.Add (tag);
							InsertWithTags (ref iter, markup.Text.Substring (start, end - start), tag);
						} else
							Insert (ref iter, markup.Text.Substring (start, end - start));
	
						iter = GetIterAtMark (mark);
					}
					while (attrIter.Next ());
				}
			}
		}
	}
}

