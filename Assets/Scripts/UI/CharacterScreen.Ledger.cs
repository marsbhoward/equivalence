using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.UI
{
    /// <summary>
    /// The run sheet's benefits and burdens, pressable (the user's call, 2026-11-18): one pressed
    /// reads in the stats panel as a pressed slot does - its name in its kind's colour, the process it
    /// came out of (and the art it is inscribed on), what it does (the deal card's words), its stacks,
    /// and its capstone once complete. Pressing it again, an art or a slot goes back. The cards and
    /// the art cards' chips sit in the pad's focus with the rest (CharacterScreen.Run.cs).
    /// </summary>
    public partial class CharacterScreen
    {
        /// <summary>The ledger entry the stats panel is reading, or null.</summary>
        string _inspectedEntry;

        /// <summary>Each entry's card or art chip by entry id, and its colour unpressed.</summary>
        readonly Dictionary<string, RectTransform> _ledgerChips = new();
        readonly Dictionary<string, Color> _chipIdle = new();

        static readonly Color ChipPressed = new(0.20f, 0.17f, 0.09f);

        void RegisterLedgerFocus()
        {
            foreach (var chip in _ledgerChips.Values)
                if (chip != null) _focusRects.Add(chip);
        }

        /// <summary>A tap on a chip reads it (or lets it go). True when the tap was a chip's.</summary>
        bool TapLedger(Vector2 p)
        {
            foreach (var (id, chip) in _ledgerChips)
            {
                if (chip == null || !RectTransformUtility.RectangleContainsScreenPoint(chip, p, null)) continue;
                _inspectedEntry = _inspectedEntry == id ? null : id;
                _inspected = null;
                _inspectedArt = -1;
                _noteStat = null;
                Refresh();
                return true;
            }
            return false;
        }

        /// <summary>The pressed card, chip or art reads as pressed, as a pressed slot does.</summary>
        void PaintLedgerChips()
        {
            foreach (var (id, chip) in _ledgerChips)
                if (chip != null && chip.TryGetComponent<Image>(out var img))
                    img.color = id == _inspectedEntry ? ChipPressed : _chipIdle.TryGetValue(id, out var c) ? c : ArtIdle;
            PaintArtCards();
        }

        /// <summary>The stats panel reading a ledger entry. False when there is none to read.</summary>
        bool BuildEntryStats()
        {
            if (_inspectedEntry == null) return false;
            var mods = _mods?.Invoke();
            var e = ExchangeCatalog.Get(_inspectedEntry);
            int held = mods != null && e != null ? mods.StacksOf(e) : 0;
            if (e == null || held <= 0) { _inspectedEntry = null; return false; }

            _noteDefault = null;
            _noteShown = null;
            _noteTitle = _noteText = null;

            var stack = EntryStack();

            var tint = ExchangeInk.Tint(e);
            EntryLine(stack, e.Name, 24, tint, FontStyle.Bold);
            string side = e.Kind == ExchangeKind.Boon ? "benefit" : "burden";
            var process = ExchangeInk.Process(e);
            if (process != null) side += "  -  " + process;
            int socket = mods.SocketOf(e.Id);
            var art = socket >= 0 ? mods.SocketArt?.Invoke(socket) : null;
            if (art != null) side += "  -  on " + art.DisplayName;
            EntryLine(stack, side, 14, StatDim);
            EntryLine(stack, Glossary.Plain(e.Effect), 18, StatInk);

            if (e.Stackable)
                EntryLine(stack, $"stack {ExchangeEntry.Roman(held)} of {ExchangeEntry.Roman(e.MaxStacks)}", 15, StatDim);

            // The capstone once the last stack is in - a Rubedo, or a Nigredo.
            if (e.CapName != null && held >= e.MaxStacks)
            {
                var cap = e.Kind == ExchangeKind.Boon ? ExchangeScale.RubedoText : ExchangeScale.NigredoText;
                EntryLine(stack, ExchangeInk.Capstone(e), 16, cap, FontStyle.Bold);
                EntryLine(stack, Glossary.Plain(e.CapText), 16, cap);
            }
            return true;
        }

        /// <summary>A top-down column in the stats panel for a read made of lines.</summary>
        RectTransform EntryStack()
        {
            var stack = UiKit.Rect(_statsBody, "entry", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(SPad, 0), new Vector2(-SPad, -SPad));
            stack.pivot = new Vector2(0.5f, 1f);
            var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;
            stack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return stack;
        }

        static void EntryLine(RectTransform stack, string text, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var label = UiKit.Label(stack, text, size, color, TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.fontStyle = style;
            label.lineSpacing = 1.1f;
        }
    }
}
