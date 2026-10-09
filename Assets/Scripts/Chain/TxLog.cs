using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Chain
{
    public enum TxState { Pending, Confirmed, Failed }

    /// <summary>Which of the write points produced this. Mirrors IProfileStore's three, plus the account.</summary>
    public enum TxKind { RunStart, RunEnd, Unlock, Account, DesignMint }   // append only

    public class TxRecord
    {
        public TxKind Kind;
        public TxState State = TxState.Pending;

        /// <summary>One line, past tense, what was committed.</summary>
        public string Summary;

        /// <summary>The fields that actually went into the datum.</summary>
        public string Detail;

        /// <summary>Transaction hash once confirmed. "local" for the JSON store.</summary>
        public string Hash;

        public string Note;

        /// <summary>Seconds into the session, for ordering and display.</summary>
        public float PostedAt;
        public float SettledAt;

        public float Seconds => Mathf.Max(0f, SettledAt - PostedAt);
    }

    /// <summary>
    /// Every checkpoint write this SESSION, for the terminal in the hub.
    ///
    /// Session-scoped and never persisted, on purpose. This is a receipt roll, not a ledger: the
    /// authoritative record is the chain (or, today, the JSON on disk), and a saved copy of it
    /// here would be a second source of truth that could disagree with the first.
    ///
    /// Records are POSTED pending and CONFIRMED separately even though the local store settles
    /// immediately. That split is the whole point of building it now - a real CIP-68 store posts a
    /// transaction and hears back a block later, and a log that only ever knew about finished
    /// writes would need rebuilding rather than reconnecting.
    /// </summary>
    public static class TxLog
    {
        static readonly List<TxRecord> _all = new();

        public static IReadOnlyList<TxRecord> All => _all;

        /// <summary>Raised on post and on settle, so an open terminal can redraw.</summary>
        public static event Action Changed;

        public static int PendingCount
        {
            get
            {
                int n = 0;
                foreach (var t in _all) if (t.State == TxState.Pending) n++;
                return n;
            }
        }

        public static TxRecord Post(TxKind kind, string summary, string detail)
        {
            var tx = new TxRecord
            {
                Kind = kind,
                Summary = summary,
                Detail = detail,
                PostedAt = Time.realtimeSinceStartup,
            };
            _all.Add(tx);
            Changed?.Invoke();
            return tx;
        }

        public static void Confirm(TxRecord tx, string hash)
        {
            if (tx == null || tx.State != TxState.Pending) return;
            tx.State = TxState.Confirmed;
            tx.Hash = hash;
            tx.SettledAt = Time.realtimeSinceStartup;
            Changed?.Invoke();
        }

        public static void Fail(TxRecord tx, string reason)
        {
            if (tx == null || tx.State != TxState.Pending) return;
            tx.State = TxState.Failed;
            tx.Note = reason;
            tx.SettledAt = Time.realtimeSinceStartup;
            Changed?.Invoke();
        }

        public static void Clear()
        {
            _all.Clear();
            Changed?.Invoke();
        }

        public static string Label(TxKind kind) => kind switch
        {
            TxKind.RunStart => "RUN START",
            TxKind.RunEnd => "RUN END",
            TxKind.Unlock => "UNLOCK",
            TxKind.DesignMint => "DESIGN MINT",
            _ => "ACCOUNT",
        };
    }
}
