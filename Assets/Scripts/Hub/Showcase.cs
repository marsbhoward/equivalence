using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>One piece of showcase art.</summary>
    public struct ShowcaseItem
    {
        /// <summary>
        /// Stable identity - an asset id, or policy+name on Cardano. This is what a saved room
        /// layout stores, NOT the index: a wallet that gains or loses a token renumbers every
        /// index after it, and a layout keyed on those would silently rearrange the room.
        /// </summary>
        public string Key;

        public Sprite Art;
        public string Title;
        public string Collection;

        /// <summary>The piece's live page, for our own collections only - the web close-up's (NftFrame).</summary>
        public string HtmlUrl;
    }

    /// <summary>
    /// Where showcase art comes from.
    ///
    /// This is a SEAM, not a wallet client. Nothing in the game may depend on what is hanging in
    /// a frame: per the on-chain handoff the profile is custodial precisely so the client never
    /// has to do an ownership check, and Apple's guidelines say token ownership cannot gate
    /// features. A picture on a wall is decoration, and decoration is the one thing that stays
    /// safe on every platform - so a source that returns nothing must leave the game complete.
    ///
    /// A later wallet or backend implements this and assigns <see cref="Showcase.Source"/>; the
    /// room polls it and knows nothing else about where the images came from.
    /// </summary>
    public interface IShowcaseSource
    {
        /// <summary>How many pieces are held, details loaded or not.</summary>
        int Count { get; }

        /// <summary>The collections - a policy each, on Cardano - the pieces group into.</summary>
        IReadOnlyList<ShowcaseGroup> Groups { get; }

        /// <summary>A group's pieces loaded so far, in order, into <paramref name="into"/> (cleared first).
        /// WITHOUT pictures where they are fetched (Art may be null) - ask <see cref="TryGetByKey"/>
        /// for the ones on screen, or listing a big collection would fetch every picture in it.</summary>
        void Loaded(string group, List<ShowcaseItem> into);

        /// <summary>Whether a group has more pieces to load, and whether some are on their way.</summary>
        bool HasMore(string group);
        bool IsLoading(string group);

        /// <summary>Start loading a group's next page. A no-op while one is on its way or past the last.</summary>
        void LoadMore(string group);

        /// <summary>Look one up by its stable key, for restoring a saved layout.</summary>
        bool TryGetByKey(string key, out ShowcaseItem item);

        /// <summary>Pieces the room needs by key (its placed frames) - fetched if held and not loaded.</summary>
        void Want(IEnumerable<string> keys);
    }

    /// <summary>One collection of showcase art.</summary>
    public struct ShowcaseGroup
    {
        /// <summary>Its policy id (56 hex), or the dev source's own name.</summary>
        public string Id;
        /// <summary>Its collection name - empty when none is known, and the picker shows the id shortened.</summary>
        public string Name;
        public int Count;
    }

    /// <summary>
    /// The single global hook-up point. Static because there is exactly one wallet connection per
    /// player and the room that reads it is rebuilt on every return to the menu - handing the
    /// source to the room would mean re-handing it every time.
    /// </summary>
    public static class Showcase
    {
        static IShowcaseSource _source;

        /// <summary>Set this from wallet/backend code. Setting it repaints every live frame.</summary>
        public static IShowcaseSource Source
        {
            get => _source;
            set { _source = value; Refresh(); }
        }

        static readonly List<Frame> _frames = new();

        internal static void Register(Frame frame)
        {
            _frames.Add(frame);
            Apply(frame);
            // Built showing nothing for a piece the wallet holds but has not loaded: ask for it now
            // (Refresh asks too, but nothing repaints a room that was only rebuilt).
            if (_source != null && frame != null && !string.IsNullOrEmpty(frame.Key) && !frame.HasArt)
                _source.Want(new[] { frame.Key });
        }

        internal static void Unregister(Frame frame) => _frames.Remove(frame);

        /// <summary>
        /// Re-read the source into every frame. Safe to call when there is no source - covers a
        /// wallet connecting or disconnecting, AND a piece of art finishing its async fetch after
        /// the frame that wants it was already built (see WalletShowcaseSource.ArtArrived).
        /// </summary>
        public static void Refresh()
        {
            _frames.RemoveAll(f => f == null);
            foreach (var f in _frames) Apply(f);

            // A frame whose piece is held but not loaded asks for it; the answer comes back here.
            if (_source == null) return;
            var want = new List<string>();
            foreach (var f in _frames) if (!string.IsNullOrEmpty(f.Key) && !f.HasArt) want.Add(f.Key);
            if (want.Count > 0) _source.Want(want);
        }

        static void Apply(Frame frame)
        {
            if (frame == null) return;

            if (_source != null && !string.IsNullOrEmpty(frame.Key) &&
                _source.TryGetByKey(frame.Key, out var item))
            {
                frame.SetArt(item.Art, item.Title, item.Collection);
                return;
            }

            frame.Clear();
        }
    }
}
