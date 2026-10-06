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
        /// <summary>How many pieces are available to display.</summary>
        int Count { get; }

        bool TryGet(int index, out ShowcaseItem item);

        /// <summary>Look one up by its stable key, for restoring a saved layout.</summary>
        bool TryGetByKey(string key, out ShowcaseItem item);
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
